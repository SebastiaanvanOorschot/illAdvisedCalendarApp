using AgendaApi.Models;
using AgendaApi.Services;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace AgendaApi.Tests.Services;

public class RecurrenceServiceTests
{
    private readonly RecurrenceService _service = new();

    // ── non-recurring events ────────────────────────────────────────────────

    [Fact]
    public void NonRecurring_InsideRange_ReturnsOneOccurrenceWithEventFields()
    {
        var evt = Event(Utc(2026, 3, 10, 9), end: Utc(2026, 3, 10, 10));
        evt.Description = "standup";
        evt.Color = "#FF0000";

        var occurrence = Assert.Single(_service.GetOccurrences(evt, Utc(2026, 3, 1), Utc(2026, 4, 1)));

        Assert.Equal(evt.Id, occurrence.EventId);
        Assert.Equal(Utc(2026, 3, 10, 9), occurrence.OccurrenceStart);
        Assert.Equal(Utc(2026, 3, 10, 10), occurrence.OccurrenceEnd);
        Assert.Equal("Test event", occurrence.Title);
        Assert.Equal("standup", occurrence.Description);
        Assert.Equal("#FF0000", occurrence.Color);
        Assert.False(occurrence.IsRecurring);
    }

    [Fact]
    public void NonRecurring_RangeIsStartInclusiveAndEndExclusive()
    {
        var atStart = Event(Utc(2026, 3, 1));
        var atEnd = Event(Utc(2026, 4, 1));

        Assert.Single(_service.GetOccurrences(atStart, Utc(2026, 3, 1), Utc(2026, 4, 1)));
        Assert.Empty(_service.GetOccurrences(atEnd, Utc(2026, 3, 1), Utc(2026, 4, 1)));
    }

    [Fact]
    public void NonRecurring_OutsideRange_ReturnsNothing()
    {
        var evt = Event(Utc(2026, 2, 27, 9));

        Assert.Empty(_service.GetOccurrences(evt, Utc(2026, 3, 1), Utc(2026, 4, 1)));
    }

    // ── fast path ───────────────────────────────────────────────────────────

    [Fact]
    public void Daily_ReturnsEveryDayInRange_WithDurationPreserved()
    {
        var evt = Event(Utc(2026, 3, 1, 9), end: Utc(2026, 3, 1, 10, 30), rrule: "FREQ=DAILY");

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 3, 1), Utc(2026, 3, 8));

        Assert.Equal(Days(Utc(2026, 3, 1, 9), 7), Starts(occurrences));
        Assert.All(occurrences, o => Assert.Equal(TimeSpan.FromMinutes(90), o.OccurrenceEnd - o.OccurrenceStart));
        Assert.All(occurrences, o => Assert.True(o.IsRecurring));
    }

    [Fact]
    public void Daily_WithInterval_SkipsDays()
    {
        var evt = Event(Utc(2026, 3, 1, 9), rrule: "FREQ=DAILY;INTERVAL=3");

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 3, 1), Utc(2026, 3, 11));

        Assert.Equal(new[] { Utc(2026, 3, 1, 9), Utc(2026, 3, 4, 9), Utc(2026, 3, 7, 9), Utc(2026, 3, 10, 9) }, Starts(occurrences));
    }

    [Fact]
    public void Daily_LongBeforeRange_StaysInPhaseAfterSkippingAhead()
    {
        // 2026-03-03 is 2253 days (a multiple of 3) after 2020-01-01
        var evt = Event(Utc(2020, 1, 1, 9), rrule: "FREQ=DAILY;INTERVAL=3");

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 3, 1), Utc(2026, 3, 9));

        Assert.Equal(new[] { Utc(2026, 3, 3, 9), Utc(2026, 3, 6, 9) }, Starts(occurrences));
    }

    [Fact]
    public void ExceptionDates_AreLeftOut()
    {
        var evt = Event(Utc(2026, 3, 1, 9), rrule: "FREQ=DAILY");
        evt.ExceptionDates = "2026-03-02T09:00:00.0000000Z,2026-03-04T09:00:00.0000000Z";

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 3, 1), Utc(2026, 3, 6));

        Assert.Equal(new[] { Utc(2026, 3, 1, 9), Utc(2026, 3, 3, 9), Utc(2026, 3, 5, 9) }, Starts(occurrences));
    }

    [Fact]
    public void Weekly_WithoutByDay_RepeatsOnTheStartWeekday()
    {
        var evt = Event(Utc(2026, 3, 4, 18), rrule: "FREQ=WEEKLY"); // a Wednesday

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 3, 1), Utc(2026, 4, 1));

        Assert.Equal(new[] { Utc(2026, 3, 4, 18), Utc(2026, 3, 11, 18), Utc(2026, 3, 18, 18), Utc(2026, 3, 25, 18) }, Starts(occurrences));
    }

    [Fact]
    public void Weekly_WithByDay_RepeatsOnEachListedDay()
    {
        var evt = Event(Utc(2026, 3, 2, 7), rrule: "FREQ=WEEKLY;BYDAY=MO,WE,FR"); // a Monday

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 3, 1), Utc(2026, 3, 9));

        Assert.Equal(new[] { Utc(2026, 3, 2, 7), Utc(2026, 3, 4, 7), Utc(2026, 3, 6, 7) }, Starts(occurrences));
    }

    [Fact]
    public void Weekly_WithByDay_NeverReturnsDaysBeforeTheSeriesStart()
    {
        var evt = Event(Utc(2026, 3, 4, 7), rrule: "FREQ=WEEKLY;BYDAY=MO,WE"); // starts on a Wednesday

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 3, 1), Utc(2026, 3, 10));

        Assert.Equal(new[] { Utc(2026, 3, 4, 7), Utc(2026, 3, 9, 7) }, Starts(occurrences));
    }

    [Fact]
    public void Weekly_EveryOtherWeek_WithByDay_StaysInTheSameWeeks()
    {
        // Starts on Wednesday 4 March. Weeks run Monday to Sunday, so the series' weeks are
        // 2-8 March, 16-22 March, 30 March-5 April: never the Monday of 9 or 23 March.
        var evt = Event(Utc(2026, 3, 4, 7), rrule: "FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE");

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 3, 1), Utc(2026, 4, 6));

        Assert.Equal(new[] { Utc(2026, 3, 4, 7), Utc(2026, 3, 16, 7), Utc(2026, 3, 18, 7), Utc(2026, 3, 30, 7), Utc(2026, 4, 1, 7) }, Starts(occurrences));
    }

    [Fact]
    public void Monthly_RepeatsOnTheSameDayOfMonth()
    {
        var evt = Event(Utc(2026, 1, 15, 12), rrule: "FREQ=MONTHLY");

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 1, 1), Utc(2026, 5, 1));

        Assert.Equal(new[] { Utc(2026, 1, 15, 12), Utc(2026, 2, 15, 12), Utc(2026, 3, 15, 12), Utc(2026, 4, 15, 12) }, Starts(occurrences));
    }

    [Fact]
    public void Monthly_WithInterval_LongBeforeRange_StaysInPhase()
    {
        // every 2 months from Jan 2020: odd months only
        var evt = Event(Utc(2020, 1, 10, 12), rrule: "FREQ=MONTHLY;INTERVAL=2");

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 2, 1), Utc(2026, 8, 1));

        Assert.Equal(new[] { Utc(2026, 3, 10, 12), Utc(2026, 5, 10, 12), Utc(2026, 7, 10, 12) }, Starts(occurrences));
    }

    [Fact]
    public void Monthly_OnThe31st_SkipsMonthsThatAreTooShort()
    {
        // RFC 5545: an invalid date (Feb 31, Apr 31) is skipped, not moved to the last day
        var evt = Event(Utc(2026, 1, 31, 9), rrule: "FREQ=MONTHLY");

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 1, 1), Utc(2026, 9, 1));

        Assert.Equal(new[] { Utc(2026, 1, 31, 9), Utc(2026, 3, 31, 9), Utc(2026, 5, 31, 9), Utc(2026, 7, 31, 9), Utc(2026, 8, 31, 9) }, Starts(occurrences));
    }

    [Fact]
    public void Yearly_OnFebruary29_OnlyOccursInLeapYears()
    {
        var evt = Event(Utc(2024, 2, 29, 12), rrule: "FREQ=YEARLY");

        var occurrences = _service.GetOccurrences(evt, Utc(2024, 1, 1), Utc(2033, 1, 1));

        Assert.Equal(new[] { Utc(2024, 2, 29, 12), Utc(2028, 2, 29, 12), Utc(2032, 2, 29, 12) }, Starts(occurrences));
    }

    [Fact]
    public void Yearly_RepeatsOnTheSameDate()
    {
        var evt = Event(Utc(2019, 6, 1, 0), rrule: "FREQ=YEARLY");

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 1, 1), Utc(2028, 12, 31));

        Assert.Equal(new[] { Utc(2026, 6, 1), Utc(2027, 6, 1), Utc(2028, 6, 1) }, Starts(occurrences));
    }

    [Fact]
    public void DailyForever_IsCappedAt10000Iterations()
    {
        var evt = Event(Utc(2026, 1, 1, 9), rrule: "FREQ=DAILY");

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 1, 1), Utc(2126, 1, 1));

        Assert.Equal(10000, occurrences.Count);
    }

    // ── fast path versus Ical.Net ───────────────────────────────────────────

    public static TheoryData<string, DateTime> RulesTheFastPathHandles => new()
    {
        { "FREQ=DAILY", Utc(2026, 1, 5, 9) },
        { "FREQ=DAILY;INTERVAL=4", Utc(2025, 11, 3, 6, 15) },
        { "FREQ=WEEKLY", Utc(2025, 12, 31, 20) },
        { "FREQ=WEEKLY;INTERVAL=3", Utc(2025, 10, 7, 8) },
        { "FREQ=WEEKLY;BYDAY=TU,TH", Utc(2026, 1, 6, 17) },
        { "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR", Utc(2025, 9, 1, 8, 30) },
        { "FREQ=WEEKLY;BYDAY=SA,SU;WKST=MO", Utc(2026, 1, 3, 10) },
        { "FREQ=MONTHLY", Utc(2025, 8, 12, 14) },
        { "FREQ=MONTHLY;INTERVAL=3", Utc(2024, 2, 20, 14) },
        { "FREQ=MONTHLY;BYMONTHDAY=15", Utc(2025, 7, 15, 9) },
        { "FREQ=YEARLY", Utc(2010, 3, 1, 0) },
        { "FREQ=YEARLY;BYMONTH=12", Utc(2015, 12, 24, 18) },
        { "FREQ=YEARLY;INTERVAL=2", Utc(2011, 5, 5, 5) },
        { "FREQ=MONTHLY", Utc(2025, 1, 31, 9) },
        { "FREQ=MONTHLY;INTERVAL=2", Utc(2025, 8, 30, 9) },
        { "FREQ=MONTHLY;BYMONTHDAY=29", Utc(2024, 1, 29, 9) },
        { "FREQ=YEARLY", Utc(2020, 2, 29, 9) },
        { "FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE", Utc(2026, 1, 7, 7) },
        { "FREQ=WEEKLY;INTERVAL=3;BYDAY=SU,TU", Utc(2025, 11, 13, 7) },
        { "FREQ=WEEKLY;INTERVAL=2;BYDAY=TU,TH,SA", Utc(2025, 6, 5, 21) },
        { "FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,SU;WKST=SU", Utc(2026, 1, 7, 7) },
        { "FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,SU;WKST=MO", Utc(2026, 1, 7, 7) },
    };

    public static TheoryData<string, DateTime> RulesForIcalNet => new()
    {
        { "FREQ=MONTHLY;BYHOUR=9,17", Utc(2025, 1, 31, 9) },
        { "FREQ=YEARLY;BYHOUR=8,20", Utc(2020, 2, 29, 8) },
        { "FREQ=MONTHLY;BYDAY=2TU", Utc(2025, 6, 10, 19) },
        { "FREQ=WEEKLY;BYDAY=MO;BYHOUR=7,19", Utc(2025, 3, 3, 7) },
    };

    [Theory]
    [MemberData(nameof(RulesForIcalNet))]
    public void SlowPath_GivesTheSameOccurrencesAsIcalNetFromTheRealStart(string rrule, DateTime start)
    {
        // The slow path shifts DTSTART forward before calling Ical.Net; that shift must not change the result
        var evt = Event(start, end: start.AddHours(1), rrule: rrule);
        var rangeStart = Utc(2026, 2, 1);
        var rangeEnd = Utc(2026, 9, 1);

        var occurrences = _service.GetOccurrences(evt, rangeStart, rangeEnd);

        Assert.Equal(1, _service.SlowCount);
        Assert.Equal(IcalNetStarts(rrule, start, rangeStart, rangeEnd), Starts(occurrences));
    }

    [Theory]
    [MemberData(nameof(RulesTheFastPathHandles))]
    public void FastPath_GivesTheSameOccurrencesAsIcalNet(string rrule, DateTime start)
    {
        var evt = Event(start, end: start.AddHours(1), rrule: rrule);
        var rangeStart = Utc(2026, 2, 1);
        var rangeEnd = Utc(2026, 9, 1);

        var occurrences = _service.GetOccurrences(evt, rangeStart, rangeEnd);

        Assert.Equal(1, _service.FastCount);
        Assert.Equal(IcalNetStarts(rrule, start, rangeStart, rangeEnd), Starts(occurrences));
    }

    [Fact]
    public void SlowPath_SkipAheadIntoAShortMonth_KeepsTheDayOfMonth()
    {
        // Any skip-ahead before handing the rule to Ical.Net must not land DTSTART on April 30
        const string rrule = "FREQ=MONTHLY;BYHOUR=9,17";
        var start = Utc(2025, 1, 31, 9);

        var occurrences = _service.GetOccurrences(Event(start, end: start.AddHours(1), rrule: rrule), Utc(2026, 5, 1), Utc(2026, 9, 1));

        Assert.Equal(IcalNetStarts(rrule, start, Utc(2026, 5, 1), Utc(2026, 9, 1)), Starts(occurrences));
        Assert.All(occurrences, o => Assert.Equal(31, o.OccurrenceStart.Day));
    }

    // ── routing between fast path and Ical.Net ──────────────────────────────

    [Theory]
    [InlineData("FREQ=DAILY;COUNT=5")]
    [InlineData("FREQ=MONTHLY;BYDAY=1MO")]
    [InlineData("FREQ=MONTHLY;BYDAY=-1FR")]
    [InlineData("FREQ=MONTHLY;BYDAY=MO,TU,WE,TH,FR;BYSETPOS=-1")]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=-1")]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=1,15")]
    [InlineData("FREQ=YEARLY;BYMONTH=1,7")]
    [InlineData("FREQ=DAILY;BYHOUR=9,17")]
    public void ComplexRules_GoToIcalNet(string rrule)
    {
        var evt = Event(Utc(2026, 1, 1, 9), rrule: rrule);

        _service.GetOccurrences(evt, Utc(2026, 1, 1), Utc(2026, 3, 1));

        Assert.Equal(0, _service.FastCount);
        Assert.Equal(1, _service.SlowCount);
    }

    [Theory]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=20")] // event is on the 10th
    [InlineData("FREQ=YEARLY;BYMONTH=6")]      // event is in January
    [InlineData("FREQ=DAILY;BYDAY=MO")]        // BYDAY only handled for WEEKLY
    public void ByPartsThatDoNotMatchTheStart_GoToIcalNet(string rrule)
    {
        var evt = Event(Utc(2026, 1, 10, 9), rrule: rrule);

        _service.GetOccurrences(evt, Utc(2026, 1, 1), Utc(2026, 3, 1));

        Assert.Equal(1, _service.SlowCount);
    }

    [Fact]
    public void Count_IsHonouredViaIcalNet()
    {
        var evt = Event(Utc(2026, 3, 1, 9), rrule: "FREQ=DAILY;COUNT=3");

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 3, 1), Utc(2026, 4, 1));

        Assert.Equal(new[] { Utc(2026, 3, 1, 9), Utc(2026, 3, 2, 9), Utc(2026, 3, 3, 9) }, Starts(occurrences));
    }

    [Fact]
    public void LastFridayOfTheMonth_ViaIcalNet()
    {
        var evt = Event(Utc(2026, 1, 30, 16), rrule: "FREQ=MONTHLY;BYDAY=-1FR");

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 1, 1), Utc(2026, 4, 1));

        Assert.Equal(new[] { Utc(2026, 1, 30, 16), Utc(2026, 2, 27, 16), Utc(2026, 3, 27, 16) }, Starts(occurrences));
    }

    [Fact]
    public void ResetCounters_SetsBothCountersToZero()
    {
        _service.GetOccurrences(Event(Utc(2026, 1, 1), rrule: "FREQ=DAILY"), Utc(2026, 1, 1), Utc(2026, 1, 2));
        _service.GetOccurrences(Event(Utc(2026, 1, 1), rrule: "FREQ=DAILY;COUNT=2"), Utc(2026, 1, 1), Utc(2026, 1, 2));

        _service.ResetCounters();

        Assert.Equal(0, _service.FastCount);
        Assert.Equal(0, _service.SlowCount);
    }

    [Fact]
    public void UnparseableRule_ReturnsNothingInsteadOfThrowing()
    {
        var evt = Event(Utc(2026, 1, 1, 9), rrule: "this is not an rrule");

        Assert.Empty(_service.GetOccurrences(evt, Utc(2026, 1, 1), Utc(2026, 2, 1)));
    }

    // ── legacy recurrence (IsRecurring + RecurrencePattern, no RRULE) ──────

    [Fact]
    public void Legacy_WeeklyWithInterval_StopsAtRecurrenceEndDate()
    {
        var evt = Event(Utc(2026, 3, 2, 9), end: Utc(2026, 3, 2, 10));
        evt.IsRecurring = true;
        evt.RecurrencePattern = "weekly";
        evt.RecurrenceInterval = 2;
        evt.RecurrenceEndDate = Utc(2026, 4, 13, 9);

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 3, 1), Utc(2026, 6, 1));

        Assert.Equal(new[] { Utc(2026, 3, 2, 9), Utc(2026, 3, 16, 9), Utc(2026, 3, 30, 9), Utc(2026, 4, 13, 9) }, Starts(occurrences));
        Assert.All(occurrences, o => Assert.Equal(TimeSpan.FromHours(1), o.OccurrenceEnd - o.OccurrenceStart));
    }

    [Fact]
    public void Legacy_DailyLongBeforeRange_SkipsAheadAndHonoursExceptions()
    {
        var evt = Event(Utc(2024, 1, 1, 9));
        evt.IsRecurring = true;
        evt.RecurrencePattern = "daily";
        evt.ExceptionDates = "2026-03-02T09:00:00.0000000Z";

        var occurrences = _service.GetOccurrences(evt, Utc(2026, 3, 1), Utc(2026, 3, 4));

        Assert.Equal(new[] { Utc(2026, 3, 1, 9), Utc(2026, 3, 3, 9) }, Starts(occurrences));
        Assert.All(occurrences, o => Assert.Null(o.OccurrenceEnd));
    }

    // ── helpers on the interface ────────────────────────────────────────────

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", null, null)]
    [InlineData("daily", null, "FREQ=DAILY")]
    [InlineData("weekly", 1, "FREQ=WEEKLY")]
    [InlineData("monthly", 3, "FREQ=MONTHLY;INTERVAL=3")]
    public void ConvertSimplePatternToRRule_BuildsFreqAndInterval(string? pattern, int? interval, string? expected)
    {
        Assert.Equal(expected, _service.ConvertSimplePatternToRRule(pattern, interval, null));
    }

    [Fact]
    public void ConvertSimplePatternToRRule_AddsUntilInUtc()
    {
        var rrule = _service.ConvertSimplePatternToRRule("weekly", 2, Utc(2026, 12, 31, 23, 59));

        Assert.Equal("FREQ=WEEKLY;INTERVAL=2;UNTIL=20261231T235900Z", rrule);
    }

    [Theory]
    [InlineData("FREQ=WEEKLY;BYDAY=MO", true)]
    [InlineData("FREQ=MONTHLY;BYDAY=-1FR", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsValidRRule(string? rrule, bool expected)
    {
        Assert.Equal(expected, _service.IsValidRRule(rrule));
    }

    [Theory]
    [InlineData("FREQ=DAILY", "Daily")]
    [InlineData("FREQ=WEEKLY", "Weekly")]
    [InlineData("FREQ=MONTHLY;COUNT=5", "Monthly, 5 times")]
    public void GetRRuleDescription(string rrule, string expected)
    {
        Assert.Equal(expected, _service.GetRRuleDescription(rrule));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static Event Event(DateTime start, DateTime? end = null, string? rrule = null) => new()
    {
        Id = 42,
        Title = "Test event",
        StartDateTime = start,
        EndDateTime = end,
        RecurrenceRule = rrule
    };

    // Sorted: GetOccurrences doesn't promise an order (EventService sorts all occurrences
    // afterwards), so these tests compare the set of start times.
    private static List<DateTime> Starts(IEnumerable<EventOccurrence> occurrences) =>
        occurrences.Select(o => o.OccurrenceStart).Order().ToList();

    private static List<DateTime> Days(DateTime first, int count) =>
        Enumerable.Range(0, count).Select(i => first.AddDays(i)).ToList();

    /// The reference: Ical.Net expanding the rule from the real start, with no skip-ahead tricks.
    private static List<DateTime> IcalNetStarts(string rrule, DateTime start, DateTime rangeStart, DateTime rangeEnd)
    {
        var calEvent = new CalendarEvent
        {
            DtStart = new CalDateTime(start),
            DtEnd = new CalDateTime(start.AddHours(1)),
            RecurrenceRules = new List<RecurrencePattern> { new(rrule) }
        };

        return calEvent
            .GetOccurrences(new CalDateTime(rangeStart))
            .TakeWhileBefore(new CalDateTime(rangeEnd))
            .Select(o => DateTime.SpecifyKind(o.Period.StartTime.Value, DateTimeKind.Utc))
            .ToList();
    }
}
