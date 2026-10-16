using AgendaApi.Data;
using AgendaApi.DTOs;
using AgendaApi.Models;
using AgendaApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AgendaApi.Tests.Services;

public sealed class EventServiceTests : IDisposable
{
    private static readonly DateTime March1 = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime April1 = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly TestDatabase _database = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly List<AgendaDbContext> _contexts = new();
    private readonly User _owner;
    private readonly User _reader;
    private readonly User _writer;
    private readonly User _stranger;

    public EventServiceTests()
    {
        _owner = _database.AddUser("Owner");
        _reader = _database.AddUser("Reader");
        _writer = _database.AddUser("Writer");
        _stranger = _database.AddUser("Stranger");
        _database.AddShare(_owner, _reader, SharePermission.Read);
        _database.AddShare(_owner, _writer, SharePermission.ReadWrite);
    }

    public void Dispose()
    {
        _contexts.ForEach(c => c.Dispose());
        _cache.Dispose();
        _database.Dispose();
    }

    // ── edit and delete permissions ─────────────────────────────────────────

    [Fact]
    public async Task Update_ByOwner_Succeeds()
    {
        var evt = _database.AddEvent(_owner, "Dentist", March1.AddDays(9));

        var result = await NewService().UpdateEventAsync(_owner.Id, evt.Id, Update("Dentist (moved)", March1.AddDays(10)));

        Assert.Equal(EventServiceStatus.Success, result.Status);
        Assert.Equal("Dentist (moved)", (await Fresh(evt.Id)).Title);
    }

    [Fact]
    public async Task Update_BySharedReadWriteUser_Succeeds()
    {
        var evt = _database.AddEvent(_owner, "Dentist", March1.AddDays(9));

        var result = await NewService().UpdateEventAsync(_writer.Id, evt.Id, Update("Changed by writer", March1.AddDays(9)));

        Assert.Equal(EventServiceStatus.Success, result.Status);
    }

    [Fact]
    public async Task Update_BySharedReadOnlyUser_IsForbiddenAndChangesNothing()
    {
        var evt = _database.AddEvent(_owner, "Dentist", March1.AddDays(9));

        var result = await NewService().UpdateEventAsync(_reader.Id, evt.Id, Update("Changed by reader", March1.AddDays(9)));

        Assert.Equal(EventServiceStatus.Forbidden, result.Status);
        Assert.Equal("Dentist", (await Fresh(evt.Id)).Title);
    }

    [Fact]
    public async Task Update_ByStranger_IsNotFound_SoTheEventsExistenceDoesNotLeak()
    {
        var evt = _database.AddEvent(_owner, "Dentist", March1.AddDays(9));

        var result = await NewService().UpdateEventAsync(_stranger.Id, evt.Id, Update("Hijacked", March1.AddDays(9)));

        Assert.Equal(EventServiceStatus.NotFound, result.Status);
        Assert.Equal("Dentist", (await Fresh(evt.Id)).Title);
    }

    [Fact]
    public async Task Update_UnknownEvent_IsNotFound()
    {
        var result = await NewService().UpdateEventAsync(_owner.Id, 9999, Update("Nothing", March1));

        Assert.Equal(EventServiceStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Update_OfAGoogleImport_MarksItLocallyModified()
    {
        var evt = _database.AddEvent(_owner, "From Google", March1.AddDays(2), e => e.IsImportedFromGoogle = true);

        await NewService().UpdateEventAsync(_owner.Id, evt.Id, Update("Edited here", March1.AddDays(2)));

        Assert.True((await Fresh(evt.Id)).IsLocallyModified);
    }

    [Fact]
    public async Task Update_KeepsExceptionDatesWhenTheFormDoesNotSendThem()
    {
        var evt = _database.AddEvent(_owner, "Gym", March1, e =>
        {
            e.RecurrenceRule = "FREQ=DAILY";
            e.ExceptionDates = "2026-03-03T00:00:00.0000000Z";
        });

        await NewService().UpdateEventAsync(_owner.Id, evt.Id, Update("Gym", March1, rrule: "FREQ=DAILY"));

        Assert.Equal("2026-03-03T00:00:00.0000000Z", (await Fresh(evt.Id)).ExceptionDates);
    }

    [Theory]
    [InlineData("owner", EventServiceStatus.Success)]
    [InlineData("writer", EventServiceStatus.Success)]
    [InlineData("reader", EventServiceStatus.Forbidden)]
    [InlineData("stranger", EventServiceStatus.NotFound)]
    public async Task Delete_FollowsTheSharePermission(string who, EventServiceStatus expected)
    {
        var evt = _database.AddEvent(_owner, "Dentist", March1.AddDays(9));
        var user = who switch { "owner" => _owner, "writer" => _writer, "reader" => _reader, _ => _stranger };

        var result = await NewService().DeleteEventAsync(user.Id, evt.Id);

        Assert.Equal(expected, result.Status);
        Assert.Equal(expected == EventServiceStatus.Success, await FreshOrNull(evt.Id) == null);
    }

    [Fact]
    public async Task GetEventWithOwner_ForStranger_ReturnsNull()
    {
        var evt = _database.AddEvent(_owner, "Private", March1.AddDays(3));

        Assert.Null(await NewService().GetEventWithOwnerAsync(_stranger.Id, evt.Id));
    }

    [Fact]
    public async Task GetEventWithOwner_ForReader_ReturnsItWithReadPermission()
    {
        var evt = _database.AddEvent(_owner, "Shared", March1.AddDays(3));

        var dto = await NewService().GetEventWithOwnerAsync(_reader.Id, evt.Id);

        Assert.NotNull(dto);
        Assert.False(dto.IsOwnEvent);
        Assert.Equal(SharePermission.Read, dto.Permission);
    }

    // ── occurrences ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Occurrences_IncludeOwnAndSharedEvents_ButNotStrangersEvents()
    {
        _database.AddEvent(_reader, "Reader's own", March1.AddDays(5));
        _database.AddEvent(_owner, "Owner's", March1.AddDays(4));
        _database.AddEvent(_stranger, "Stranger's", March1.AddDays(3));

        var occurrences = await NewService().GetOccurrencesAsync(_reader.Id, March1, April1);

        Assert.Equal(new[] { "Owner's", "Reader's own" }, occurrences.Select(o => o.Title));
        var own = occurrences.Single(o => o.Title == "Reader's own");
        var shared = occurrences.Single(o => o.Title == "Owner's");
        Assert.True(own.IsOwnEvent);
        Assert.Equal(SharePermission.ReadWrite, own.Permission);
        Assert.False(shared.IsOwnEvent);
        Assert.Equal(SharePermission.Read, shared.Permission);
        Assert.Equal("Owner", shared.OwnerName);
    }

    [Fact]
    public async Task Occurrences_ExpandRecurringEvents_SortedByStart()
    {
        _database.AddEvent(_owner, "Weekly", March1.AddHours(9), e => e.RecurrenceRule = "FREQ=WEEKLY");
        _database.AddEvent(_owner, "Once", March1.AddDays(2));

        var occurrences = await NewService().GetOccurrencesAsync(_owner.Id, March1, April1);

        Assert.Equal(new[] { "Weekly", "Once", "Weekly", "Weekly", "Weekly", "Weekly" }, occurrences.Select(o => o.Title));
        Assert.Equal(occurrences.Select(o => o.OccurrenceStart).Order(), occurrences.Select(o => o.OccurrenceStart));
    }

    [Fact]
    public async Task Occurrences_SkipRecurringSeriesThatEndedBeforeTheRange()
    {
        _database.AddEvent(_owner, "Ended", March1.AddMonths(-3), e =>
        {
            e.IsRecurring = true;
            e.RecurrencePattern = "daily";
            e.RecurrenceEndDate = March1.AddDays(-1);
        });

        Assert.Empty(await NewService().GetOccurrencesAsync(_owner.Id, March1, April1));
    }

    [Fact]
    public async Task Occurrences_AreCached_AndCreatingAnEventInvalidatesTheCache()
    {
        _database.AddEvent(_owner, "First", March1.AddDays(1));
        var service = NewService();
        Assert.Single(await service.GetOccurrencesAsync(_owner.Id, March1, April1));

        _database.AddEvent(_owner, "Added behind the cache's back", March1.AddDays(2));
        Assert.Single(await service.GetOccurrencesAsync(_owner.Id, March1, April1));

        await service.CreateEventAsync(_owner.Id, new CreateEventDto { Title = "Created", StartDateTime = March1.AddDays(3) });
        Assert.Equal(3, (await service.GetOccurrencesAsync(_owner.Id, March1, April1)).Count);
    }

    // ── single occurrences of a series ──────────────────────────────────────

    [Fact]
    public async Task AddExceptionDate_HidesThatOccurrence_AndIsNotAddedTwice()
    {
        var evt = _database.AddEvent(_owner, "Daily", March1.AddHours(8), e => e.RecurrenceRule = "FREQ=DAILY");
        var service = NewService();
        var march3 = March1.AddDays(2).AddHours(8);

        await service.AddExceptionDateAsync(_owner.Id, evt.Id, march3);
        await service.AddExceptionDateAsync(_owner.Id, evt.Id, march3.AddHours(3)); // same day

        Assert.Single((await Fresh(evt.Id)).ExceptionDates!.Split(','));
        var occurrences = await service.GetOccurrencesAsync(_owner.Id, March1, March1.AddDays(5));
        Assert.DoesNotContain(occurrences, o => o.OccurrenceStart == march3);
        Assert.Equal(4, occurrences.Count);
    }

    [Fact]
    public async Task EditOccurrence_CreatesALinkedEventAndHidesTheOriginal()
    {
        var series = _database.AddEvent(_owner, "Daily", March1.AddHours(8), e => e.RecurrenceRule = "FREQ=DAILY");
        var original = March1.AddDays(1).AddHours(8);

        var result = await NewService().EditOccurrenceAsync(_owner.Id, series.Id, new EditOccurrenceDto
        {
            OriginalOccurrenceDate = original,
            Title = "Moved to the afternoon",
            NewStartDateTime = original.AddHours(6),
            NewEndDateTime = original.AddHours(7)
        });

        Assert.Equal(EventServiceStatus.Success, result.Status);
        var moved = await Fresh(result.Value!.Id);
        Assert.Equal(series.Id, moved.ParentEventId);
        Assert.Equal(original, moved.RecurrenceId);
        Assert.False(moved.IsRecurring);
        Assert.Contains("2026-03-02", (await Fresh(series.Id)).ExceptionDates);
    }

    [Fact]
    public async Task EditOccurrence_OfANonRecurringEvent_IsABadRequest()
    {
        var evt = _database.AddEvent(_owner, "Once", March1.AddDays(1));

        var result = await NewService().EditOccurrenceAsync(_owner.Id, evt.Id, new EditOccurrenceDto
        {
            OriginalOccurrenceDate = evt.StartDateTime,
            Title = "x",
            NewStartDateTime = evt.StartDateTime,
            NewEndDateTime = evt.StartDateTime.AddHours(1)
        });

        Assert.Equal(EventServiceStatus.BadRequest, result.Status);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private EventService NewService()
    {
        var context = _database.CreateContext();
        _contexts.Add(context);
        return new EventService(context, new RecurrenceService(), new CalendarShareService(context), _cache);
    }

    private static UpdateEventDto Update(string title, DateTime start, string? rrule = null) => new()
    {
        Title = title,
        StartDateTime = start,
        EndDateTime = start.AddHours(1),
        RecurrenceRule = rrule
    };

    private async Task<Event> Fresh(int id) => (await FreshOrNull(id))!;

    private async Task<Event?> FreshOrNull(int id)
    {
        using var context = _database.CreateContext();
        return await context.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id);
    }
}
