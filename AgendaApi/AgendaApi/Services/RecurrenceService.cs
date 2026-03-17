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
        var occurrences = new List<EventOccurrence>();

        try
        {
            var calEvent = new CalendarEvent
            {
                DtStart = new CalDateTime(evt.StartDateTime),
                DtEnd = new CalDateTime(evt.EndDateTime),
                RecurrenceRules = new List<RecurrencePattern>
                {
                    new RecurrencePattern(evt.RecurrenceRule)
                }
            };

            var eventDuration = evt.EndDateTime - evt.StartDateTime;

            // TakeWhileBefore is Ical.Net's built-in range terminator — it signals the
            // evaluation engine to stop generating occurrences at rangeEnd, which is
            // critical for infinite recurrences (no UNTIL/COUNT) to avoid huge enumerations
            var calendarOccurrences = calEvent
                .GetOccurrences(new CalDateTime(rangeStart))
                .TakeWhileBefore(new CalDateTime(rangeEnd))
                .ToList();

            // Parse exception dates and filter them out manually
            var exceptionDates = ParseExceptionDates(evt.ExceptionDates);
            if (exceptionDates.Any())
            {
                // Remove occurrences that match exception dates (compare by date only)
                calendarOccurrences = calendarOccurrences
                    .Where(o => !exceptionDates.Any(ex =>
                        o.Period.StartTime.Value.Date == ex.Date))
                    .ToList();
            }

            foreach (var occurrence in calendarOccurrences)
            {
                var startTime = occurrence.Period.StartTime.Value;
                var endTime = startTime + eventDuration;

                occurrences.Add(new EventOccurrence
                {
                    EventId = evt.Id,
                    OccurrenceStart = startTime,
                    OccurrenceEnd = endTime,
                    Title = evt.Title,
                    Description = evt.Description,
                    Color = evt.Color,
                    IsRecurring = true
                });
            }
        }
        catch (Exception)
        {
            // If RRULE parsing fails, fall back to simple recurrence or single event
            return GetSimpleRecurrenceOccurrences(evt, rangeStart, rangeEnd);
        }

        return occurrences;
    }

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
