using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using AgendaApi.Models;
using System.Linq;

namespace AgendaApi.Services;

/// <summary>
/// Service for handling RRULE (RFC 5545) recurrence patterns
/// </summary>
public interface IRecurrenceService
{
    /// <summary>
    /// Calculate event occurrences within a date range
    /// </summary>
    List<EventOccurrence> GetOccurrences(Event evt, DateTime rangeStart, DateTime rangeEnd);

    /// <summary>
    /// Convert simple recurrence pattern to RRULE string
    /// </summary>
    string? ConvertSimplePatternToRRule(string? pattern, int? interval, DateTime? endDate);

    /// <summary>
    /// Validate an RRULE string
    /// </summary>
    bool IsValidRRule(string? rrule);

    /// <summary>
    /// Get human-readable description of RRULE
    /// </summary>
    string GetRRuleDescription(string rrule);
}

public class RecurrenceService : IRecurrenceService
{
    public List<EventOccurrence> GetOccurrences(Event evt, DateTime rangeStart, DateTime rangeEnd)
    {
        var occurrences = new List<EventOccurrence>();

        // Handle non-recurring events
        if (string.IsNullOrEmpty(evt.RecurrenceRule) && !evt.IsRecurring)
        {
            if (evt.StartDateTime >= rangeStart && evt.StartDateTime < rangeEnd)
            {
                occurrences.Add(new EventOccurrence
                {
                    EventId = evt.Id,
                    OccurrenceStart = evt.StartDateTime,
                    OccurrenceEnd = evt.EndDateTime,
                    Title = evt.Title,
                    Description = evt.Description,
                    Color = evt.Color,
                    IsRecurring = false
                });
            }
            return occurrences;
        }

        // Handle RRULE-based recurrence
        if (!string.IsNullOrEmpty(evt.RecurrenceRule))
        {
            return GetRRuleOccurrences(evt, rangeStart, rangeEnd);
        }

        // Handle legacy simple recurrence
        if (evt.IsRecurring)
        {
            return GetSimpleRecurrenceOccurrences(evt, rangeStart, rangeEnd);
        }

        return occurrences;
    }

    private List<EventOccurrence> GetRRuleOccurrences(Event evt, DateTime rangeStart, DateTime rangeEnd)
    {
        try
        {
            var pattern      = new RecurrencePattern(evt.RecurrenceRule);
            var duration     = evt.EndDateTime - evt.StartDateTime;
            var exceptions   = new HashSet<DateTime>(ParseExceptionDates(evt.ExceptionDates).Select(d => d.Date));
            var interval     = pattern.Interval > 0 ? pattern.Interval : 1;

            // Effective recurrence end — honour UNTIL if it is earlier than the requested range end
            var recEnd = rangeEnd;
            if (pattern.Until != null && pattern.Until.Value < recEnd)
                recEnd = pattern.Until.Value;

            // Fall back to Ical.Net for patterns our fast path does not cover:
            //   COUNT-based, BYSETPOS, BYWEEKNO, BYMONTH, BYMONTHDAY, BYHOUR, BYMINUTE,
            //   BYSECOND, or BYDAY with ordinals (e.g. "1MO" = first Monday of month).
            bool hasComplexRule =
                pattern.Count > 0 ||
                pattern.BySetPosition.Any() ||
                pattern.ByWeekNo.Any() ||
                pattern.ByMonth.Any() ||
                pattern.ByMonthDay.Any() ||
                pattern.ByHour.Any() ||
                pattern.ByMinute.Any() ||
                pattern.BySecond.Any() ||
                (pattern.ByDay.Any() && pattern.Frequency != FrequencyType.Weekly) ||
                pattern.ByDay.Any(d => d.Offset != 0);

            if (hasComplexRule)
                return GetIcalNetOccurrences(evt, rangeStart, recEnd, pattern, duration, exceptions);

            var result = new List<EventOccurrence>();

            switch (pattern.Frequency)
            {
                case FrequencyType.Daily:
                    ExpandDaily(evt, rangeStart, recEnd, interval, duration, exceptions, result);
                    break;
                case FrequencyType.Weekly:
                    ExpandWeekly(evt, rangeStart, recEnd, interval, pattern.ByDay, duration, exceptions, result);
                    break;
                case FrequencyType.Monthly:
                    ExpandMonthly(evt, rangeStart, recEnd, interval, duration, exceptions, result);
                    break;
                case FrequencyType.Yearly:
                    ExpandYearly(evt, rangeStart, recEnd, interval, duration, exceptions, result);
                    break;
                default:
                    return GetIcalNetOccurrences(evt, rangeStart, recEnd, pattern, duration, exceptions);
            }

            return result;
        }
        catch
        {
            return GetSimpleRecurrenceOccurrences(evt, rangeStart, rangeEnd);
        }
    }

    // ── Fast-path expanders (pure C# — no Ical.Net overhead) ─────────────────

    private void ExpandDaily(Event evt, DateTime rangeStart, DateTime rangeEnd,
        int interval, TimeSpan duration, HashSet<DateTime> exceptions, List<EventOccurrence> result)
    {
        var cur = evt.StartDateTime;
        if (cur < rangeStart)
        {
            var skip = Math.Max(0, (int)Math.Floor((rangeStart - cur).TotalDays / interval) - 1);
            cur = cur.AddDays(skip * interval);
        }
        for (var n = 0; cur < rangeEnd && n < 10000; n++, cur = cur.AddDays(interval))
            if (cur >= rangeStart && !exceptions.Contains(cur.Date))
                result.Add(MakeOccurrence(evt, cur, duration));
    }

    private void ExpandWeekly(Event evt, DateTime rangeStart, DateTime rangeEnd,
        int interval, IList<WeekDay> byDay, TimeSpan duration, HashSet<DateTime> exceptions,
        List<EventOccurrence> result)
    {
        var dtStart = evt.StartDateTime;

        if (byDay.Any())
        {
            // BYDAY weekly — e.g. FREQ=WEEKLY;BYDAY=MO,WE
            var targetDays = byDay.Select(wd => wd.DayOfWeek).OrderBy(d => d).ToList();

            // Anchor to the start of the week that contains dtStart
            var anchor = dtStart;
            if (anchor < rangeStart)
            {
                var weeksToSkip = Math.Max(0, (int)Math.Floor((rangeStart - anchor).TotalDays / (7.0 * interval)) - 1);
                anchor = anchor.AddDays(weeksToSkip * 7 * interval);
            }

            for (var n = 0; anchor < rangeEnd && n < 10000; n++, anchor = anchor.AddDays(7 * interval))
            {
                foreach (var dow in targetDays)
                {
                    var daysFromAnchor = ((int)dow - (int)anchor.DayOfWeek + 7) % 7;
                    var occ = anchor.Date.AddDays(daysFromAnchor).Add(dtStart.TimeOfDay);
                    if (occ >= dtStart && occ >= rangeStart && occ < rangeEnd && !exceptions.Contains(occ.Date))
                        result.Add(MakeOccurrence(evt, occ, duration));
                }
            }
        }
        else
        {
            // Simple weekly — same day of week as DtStart
            var cur = dtStart;
            if (cur < rangeStart)
            {
                var skip = Math.Max(0, (int)Math.Floor((rangeStart - cur).TotalDays / (7.0 * interval)) - 1);
                cur = cur.AddDays(skip * 7 * interval);
            }
            for (var n = 0; cur < rangeEnd && n < 10000; n++, cur = cur.AddDays(7 * interval))
                if (cur >= rangeStart && !exceptions.Contains(cur.Date))
                    result.Add(MakeOccurrence(evt, cur, duration));
        }
    }

    private void ExpandMonthly(Event evt, DateTime rangeStart, DateTime rangeEnd,
        int interval, TimeSpan duration, HashSet<DateTime> exceptions, List<EventOccurrence> result)
    {
        var cur = evt.StartDateTime;
        if (cur < rangeStart)
        {
            var totalMonths = (rangeStart.Year - cur.Year) * 12 + rangeStart.Month - cur.Month;
            var skip = Math.Max(0, totalMonths / interval - 1) * interval;
            cur = cur.AddMonths(skip);
        }
        for (var n = 0; cur < rangeEnd && n < 10000; n++, cur = cur.AddMonths(interval))
            if (cur >= rangeStart && !exceptions.Contains(cur.Date))
                result.Add(MakeOccurrence(evt, cur, duration));
    }

    private void ExpandYearly(Event evt, DateTime rangeStart, DateTime rangeEnd,
        int interval, TimeSpan duration, HashSet<DateTime> exceptions, List<EventOccurrence> result)
    {
        var cur = evt.StartDateTime;
        if (cur < rangeStart)
        {
            var skip = Math.Max(0, (rangeStart.Year - cur.Year) / interval - 1) * interval;
            cur = cur.AddYears(skip);
        }
        for (var n = 0; cur < rangeEnd && n < 10000; n++, cur = cur.AddYears(interval))
            if (cur >= rangeStart && !exceptions.Contains(cur.Date))
                result.Add(MakeOccurrence(evt, cur, duration));
    }

    // Ical.Net fallback for complex patterns — reuses already-parsed RecurrencePattern
    private List<EventOccurrence> GetIcalNetOccurrences(Event evt, DateTime rangeStart, DateTime rangeEnd,
        RecurrencePattern pattern, TimeSpan duration, HashSet<DateTime> exceptions)
    {
        var effectiveStart = evt.StartDateTime;
        if (effectiveStart < rangeStart && pattern.Count <= 0)
        {
            var interval = pattern.Interval > 0 ? pattern.Interval : 1;
            switch (pattern.Frequency)
            {
                case FrequencyType.Daily:
                    effectiveStart = effectiveStart.AddDays(Math.Max(0, (int)(rangeStart - effectiveStart).TotalDays / interval - 1) * interval);
                    break;
                case FrequencyType.Weekly:
                    effectiveStart = effectiveStart.AddDays(Math.Max(0, (int)(rangeStart - effectiveStart).TotalDays / (7 * interval) - 1) * 7 * interval);
                    break;
                case FrequencyType.Monthly:
                    var months = (rangeStart.Year - effectiveStart.Year) * 12 + rangeStart.Month - effectiveStart.Month;
                    effectiveStart = effectiveStart.AddMonths(Math.Max(0, months / interval - 1) * interval);
                    break;
                case FrequencyType.Yearly:
                    effectiveStart = effectiveStart.AddYears(Math.Max(0, (rangeStart.Year - effectiveStart.Year) / interval - 1) * interval);
                    break;
            }
        }

        var calEvent = new CalendarEvent
        {
            DtStart = new CalDateTime(effectiveStart),
            DtEnd   = new CalDateTime(effectiveStart + duration),
            RecurrenceRules = new List<RecurrencePattern> { pattern }
        };

        return calEvent
            .GetOccurrences(new CalDateTime(rangeStart))
            .TakeWhileBefore(new CalDateTime(rangeEnd))
            .Where(o => !exceptions.Contains(o.Period.StartTime.Value.Date))
            .Select(o => MakeOccurrence(evt, o.Period.StartTime.Value, duration))
            .ToList();
    }

    private EventOccurrence MakeOccurrence(Event evt, DateTime start, TimeSpan duration) =>
        new EventOccurrence
        {
            EventId        = evt.Id,
            OccurrenceStart = start,
            OccurrenceEnd   = start + duration,
            Title          = evt.Title,
            Description    = evt.Description,
            Color          = evt.Color,
            IsRecurring    = true
        };

    /// <summary>
    /// Parse comma-separated exception dates from string
    /// </summary>
    private List<DateTime> ParseExceptionDates(string? exceptionDates)
    {
        var dates = new List<DateTime>();

        if (string.IsNullOrWhiteSpace(exceptionDates))
            return dates;

        var parts = exceptionDates.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (DateTime.TryParse(part.Trim(), out DateTime date))
            {
                dates.Add(date);
            }
        }

        return dates;
    }

    private List<EventOccurrence> GetSimpleRecurrenceOccurrences(Event evt, DateTime rangeStart, DateTime rangeEnd)
    {
        var occurrences = new List<EventOccurrence>();

        if (!evt.IsRecurring || string.IsNullOrEmpty(evt.RecurrencePattern))
            return occurrences;

        var current = evt.StartDateTime;
        var endDate = evt.RecurrenceEndDate ?? rangeEnd.AddYears(1); // Limit to 1 year if no end date
        var interval = evt.RecurrenceInterval ?? 1;
        var eventDuration = evt.EndDateTime - evt.StartDateTime;

        // Parse exception dates
        var exceptionDates = ParseExceptionDates(evt.ExceptionDates);

        // Skip ahead to near rangeStart to avoid iterating through years of past occurrences
        if (current < rangeStart)
        {
            var approxDaysPerStep = evt.RecurrencePattern?.ToLower() switch
            {
                "daily"   => (double)interval,
                "weekly"  => 7.0 * interval,
                "monthly" => 30.44 * interval,
                "yearly"  => 365.25 * interval,
                _         => 1.0
            };
            var stepsToSkip = (int)Math.Max(0, Math.Floor((rangeStart - current).TotalDays / approxDaysPerStep) - 2);
            for (var i = 0; i < stepsToSkip; i++)
            {
                current = evt.RecurrencePattern?.ToLower() switch
                {
                    "daily"   => current.AddDays(interval),
                    "weekly"  => current.AddDays(7 * interval),
                    "monthly" => current.AddMonths(interval),
                    "yearly"  => current.AddYears(interval),
                    _         => current.AddDays(1)
                };
            }
        }

        // Prevent infinite loops
        var maxOccurrences = 1000;
        var count = 0;

        while (current <= endDate && current < rangeEnd && count < maxOccurrences)
        {
            if (current >= rangeStart)
            {
                // Check if this occurrence is NOT in the exception dates
                bool isException = exceptionDates.Any(ex => ex.Date == current.Date);

                if (!isException)
                {
                    occurrences.Add(new EventOccurrence
                    {
                        EventId = evt.Id,
                        OccurrenceStart = current,
                        OccurrenceEnd = current + eventDuration,
                        Title = evt.Title,
                        Description = evt.Description,
                        Color = evt.Color,
                        IsRecurring = true
                    });
                }
            }

            // Calculate next occurrence based on pattern
            current = evt.RecurrencePattern.ToLower() switch
            {
                "daily" => current.AddDays(interval),
                "weekly" => current.AddDays(7 * interval),
                "monthly" => current.AddMonths(interval),
                "yearly" => current.AddYears(interval),
                _ => current.AddDays(1) // Default to daily
            };

            count++;
        }

        return occurrences;
    }

    public string? ConvertSimplePatternToRRule(string? pattern, int? interval, DateTime? endDate)
    {
        if (string.IsNullOrEmpty(pattern))
            return null;

        var freq = pattern.ToUpper(); // DAILY, WEEKLY, MONTHLY, YEARLY
        var rrule = $"FREQ={freq}";

        if (interval.HasValue && interval > 1)
            rrule += $";INTERVAL={interval}";

        if (endDate.HasValue)
        {
            // Format as UTC for RRULE
            var utcEnd = endDate.Value.ToUniversalTime();
            rrule += $";UNTIL={utcEnd:yyyyMMddTHHmmssZ}";
        }

        return rrule;
    }

    public bool IsValidRRule(string? rrule)
    {
        if (string.IsNullOrEmpty(rrule))
            return false;

        try
        {
            var pattern = new RecurrencePattern(rrule);
            return true; // If parsing succeeds, it's valid
        }
        catch
        {
            return false;
        }
    }

    public string GetRRuleDescription(string rrule)
    {
        try
        {
            var pattern = new RecurrencePattern(rrule);

            // Basic description generation
            var description = pattern.Frequency switch
            {
                FrequencyType.Daily => "Daily",
                FrequencyType.Weekly => "Weekly",
                FrequencyType.Monthly => "Monthly",
                FrequencyType.Yearly => "Yearly",
                FrequencyType.Hourly => "Hourly",
                _ => "Custom recurrence"
            };

            if (pattern.Interval > 1)
                description = $"Every {pattern.Interval} {description.ToLower()}";

            if (pattern.Count > 0)
                description += $", {pattern.Count} times";
            else if (pattern.Until != null)
                description += $" until {pattern.Until.Value:MMM dd, yyyy}";

            return description;
        }
        catch
        {
            return "Custom recurrence pattern";
        }
    }
}

/// <summary>
/// Represents a single occurrence of a potentially recurring event
/// </summary>
public class EventOccurrence
{
    public int EventId { get; set; }
    public DateTime OccurrenceStart { get; set; }
    public DateTime OccurrenceEnd { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public bool IsRecurring { get; set; }

    // Owner information for shared calendars
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public bool IsOwnEvent { get; set; }
    public SharePermission? Permission { get; set; }
}
