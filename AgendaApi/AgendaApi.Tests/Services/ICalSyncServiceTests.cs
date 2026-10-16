using System.Net;
using System.Text;
using AgendaApi.Data;
using AgendaApi.Models;
using AgendaApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AgendaApi.Tests.Services;

public sealed class ICalSyncServiceTests : IDisposable
{
    private const string FeedUrl = "https://calendar.example.test/feed.ics";

    private readonly TestDatabase _database = new();
    private readonly AgendaDbContext _context;
    private readonly User _user;
    private readonly int _subscriptionId;
    private FakeHttpHandler _feed = FakeHttpHandler.Returning(HttpStatusCode.OK, Calendar(), "text/calendar");

    public ICalSyncServiceTests()
    {
        _user = _database.AddUser("Ada");
        _subscriptionId = AddSubscription(FeedUrl);
        _context = _database.CreateContext();
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }

    // ── URL and response checks ─────────────────────────────────────────────

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://calendar.example.test/feed.ics")]
    [InlineData("webcal-without-scheme.example.test/feed.ics")]
    [InlineData("/relative/feed.ics")]
    public async Task NonHttpUrls_AreRejectedWithoutAnyRequest(string url)
    {
        var (success, error) = await Sync(AddSubscription(url));

        Assert.False(success);
        Assert.Equal("Invalid iCal URL. Only http and https URLs are supported.", error);
        Assert.Empty(_feed.RequestedUris);
    }

    [Fact]
    public async Task HttpError_IsReportedGenerically()
    {
        _feed = FakeHttpHandler.Returning(HttpStatusCode.NotFound, "secret internal detail");

        var (success, error) = await Sync();

        Assert.False(success);
        Assert.Equal("Could not fetch the iCal feed.", error);
    }

    [Fact]
    public async Task FeedDeclaringMoreThan5MB_IsRejectedBeforeReading()
    {
        _feed = new FakeHttpHandler(_ =>
        {
            var content = new StringContent(Calendar(), Encoding.UTF8, "text/calendar");
            content.Headers.ContentLength = 5 * 1024 * 1024 + 1;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        var (success, error) = await Sync();

        Assert.False(success);
        Assert.Equal("The iCal feed is too large to process.", error);
    }

    [Fact]
    public async Task FeedStreamingPast5MB_WithoutContentLength_IsCutOff()
    {
        _feed = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new NonSeekableStream(new byte[5 * 1024 * 1024 + 1]))
        });

        var (success, error) = await Sync();

        Assert.False(success);
        Assert.Equal("The iCal feed is too large to process.", error);
        Assert.Empty(Events());
    }

    [Fact]
    public async Task WrongContentType_IsStillParsed()
    {
        _feed = FakeHttpHandler.Returning(HttpStatusCode.OK, Calendar(Vevent("uid-1", "Standup")), "application/octet-stream");

        var (success, _) = await Sync();

        Assert.True(success);
        Assert.Single(Events());
    }

    [Fact]
    public async Task UnparseableFeed_FailsWithoutCreatingEvents()
    {
        _feed = FakeHttpHandler.Returning(HttpStatusCode.OK, "<html>definitely not a calendar</html>", "text/html");

        var (success, _) = await Sync();

        Assert.False(success);
        Assert.Empty(Events());
    }

    // ── importing events ────────────────────────────────────────────────────

    [Fact]
    public async Task NewEvents_AreCreatedAsReadOnlySubscriptionEvents()
    {
        _feed = FakeHttpHandler.Returning(HttpStatusCode.OK, Calendar(
            Vevent("uid-1", "Standup", "DTSTART:20260310T090000Z", "DTEND:20260310T091500Z", "DESCRIPTION:Daily sync")),
            "text/calendar");

        var (success, error) = await Sync();

        Assert.True(success, error);
        var evt = Assert.Single(Events());
        Assert.Equal("Standup", evt.Title);
        Assert.Equal("Daily sync", evt.Description);
        Assert.Equal(new DateTime(2026, 3, 10, 9, 0, 0), evt.StartDateTime);
        Assert.Equal(new DateTime(2026, 3, 10, 9, 15, 0), evt.EndDateTime);
        Assert.Equal("uid-1", evt.ExternalEventId);
        Assert.Equal(_subscriptionId, evt.CalendarSubscriptionId);
        Assert.Equal(_user.Id, evt.UserId);
        Assert.Equal("#FF69B4", evt.Color);
        Assert.True(evt.IsFromSubscription);
        Assert.True(evt.IsReadOnly);
    }

    [Fact]
    public async Task TimesWithATimeZone_AreStoredAsUtc()
    {
        // 09:00 in Amsterdam in March (before DST) is 08:00 UTC
        _feed = FakeHttpHandler.Returning(HttpStatusCode.OK, Calendar(
            Vevent("uid-1", "Standup", "DTSTART;TZID=Europe/Amsterdam:20260310T090000", "DTEND;TZID=Europe/Amsterdam:20260310T100000")),
            "text/calendar");

        await Sync();

        Assert.Equal(new DateTime(2026, 3, 10, 8, 0, 0), Events().Single().StartDateTime);
    }

    [Fact]
    public async Task AllDayEvents_KeepTheirDate()
    {
        _feed = FakeHttpHandler.Returning(HttpStatusCode.OK, Calendar(
            Vevent("uid-1", "Holiday", "DTSTART;VALUE=DATE:20260427", "DTEND;VALUE=DATE:20260428")),
            "text/calendar");

        await Sync();

        var evt = Events().Single();
        Assert.Equal(new DateTime(2026, 4, 27), evt.StartDateTime);
        Assert.Equal(new DateTime(2026, 4, 28), evt.EndDateTime);
    }

    [Fact]
    public async Task RecurrenceRules_AreCopied()
    {
        _feed = FakeHttpHandler.Returning(HttpStatusCode.OK, Calendar(
            Vevent("uid-1", "Gym", "DTSTART:20260302T180000Z", "DTEND:20260302T190000Z", "RRULE:FREQ=WEEKLY;BYDAY=MO,TH")),
            "text/calendar");

        await Sync();

        var evt = Events().Single();
        Assert.True(evt.IsRecurring);
        Assert.Contains("FREQ=WEEKLY", evt.RecurrenceRule);
        Assert.Contains("BYDAY=MO,TH", evt.RecurrenceRule);
    }

    [Fact]
    public async Task EventsWithoutUid_GetAGeneratedUid_AndStaySingleAcrossResyncs()
    {
        // Ical.Net assigns a random UID when a VEVENT has none, so the service's "skip events
        // without UID" branch never runs: such an event is replaced by a fresh row on every sync.
        _feed = FakeHttpHandler.Returning(HttpStatusCode.OK, Calendar(Vevent(null, "No uid"), Vevent("uid-2", "Has uid")), "text/calendar");
        await Sync();
        var firstUid = Events().Single(e => e.Title == "No uid").ExternalEventId;

        _context.ChangeTracker.Clear();
        await Sync();

        Assert.Equal(new[] { "Has uid", "No uid" }, Events().Select(e => e.Title));
        Assert.False(string.IsNullOrEmpty(firstUid));
        Assert.NotEqual(firstUid, Events().Single(e => e.Title == "No uid").ExternalEventId);
    }

    [Fact]
    public async Task Success_RecordsTheSyncTimeAndClearsAnOldError()
    {
        using (var context = _database.CreateContext())
        {
            context.CalendarSubscriptions.Single(s => s.Id == _subscriptionId).LastSyncError = "old error";
            context.SaveChanges();
        }

        await Sync();

        var subscription = Subscription(_subscriptionId);
        Assert.Null(subscription.LastSyncError);
        Assert.NotNull(subscription.LastSyncedAt);
    }

    // ── resyncing ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Resync_UpdatesChangedEvents_AndRemovesOnesThatLeftTheFeed()
    {
        _feed = FakeHttpHandler.Returning(HttpStatusCode.OK, Calendar(Vevent("uid-1", "Standup"), Vevent("uid-2", "Retro")), "text/calendar");
        await Sync();

        _feed = FakeHttpHandler.Returning(HttpStatusCode.OK, Calendar(Vevent("uid-1", "Standup (moved)")), "text/calendar");
        _context.ChangeTracker.Clear();
        await Sync();

        Assert.Equal(new[] { "Standup (moved)" }, Events().Select(e => e.Title));
    }

    [Fact]
    public async Task Resync_DoesNotTouchEventsOfOtherSubscriptionsOrManualEvents()
    {
        var otherSubscription = AddSubscription("https://other.example.test/feed.ics");
        _database.AddEvent(_user, "Other feed", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), e =>
        {
            e.CalendarSubscriptionId = otherSubscription;
            e.ExternalEventId = "other-uid";
        });
        _database.AddEvent(_user, "Typed in by hand", new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc));
        _feed = FakeHttpHandler.Returning(HttpStatusCode.OK, Calendar(), "text/calendar");

        await Sync();

        Assert.Equal(new[] { "Other feed", "Typed in by hand" }, AllEvents().Select(e => e.Title).Order());
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task<(bool Success, string? Error)> Sync(int? subscriptionId = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(_feed));
        var service = new ICalSyncService(_context, factory, NullLogger<ICalSyncService>.Instance);

        var subscription = await _context.CalendarSubscriptions.SingleAsync(s => s.Id == (subscriptionId ?? _subscriptionId));
        return await service.SyncSubscriptionAsync(subscription);
    }

    private int AddSubscription(string url)
    {
        using var context = _database.CreateContext();
        var subscription = new CalendarSubscription
        {
            Name = "Team",
            ICalUrl = url,
            Color = "#FF69B4",
            UserId = _user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.CalendarSubscriptions.Add(subscription);
        context.SaveChanges();
        return subscription.Id;
    }

    private CalendarSubscription Subscription(int id)
    {
        using var context = _database.CreateContext();
        return context.CalendarSubscriptions.AsNoTracking().Single(s => s.Id == id);
    }

    private List<Event> Events()
    {
        using var context = _database.CreateContext();
        return context.Events.AsNoTracking().Where(e => e.CalendarSubscriptionId == _subscriptionId).OrderBy(e => e.Title).ToList();
    }

    private List<Event> AllEvents()
    {
        using var context = _database.CreateContext();
        return context.Events.AsNoTracking().ToList();
    }

    private static string Calendar(params string[] events) =>
        "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//tests//EN\r\n" + string.Concat(events) + "END:VCALENDAR\r\n";

    private static string Vevent(string? uid, string summary, params string[] lines)
    {
        var body = new StringBuilder("BEGIN:VEVENT\r\n");
        if (uid != null) body.Append("UID:").Append(uid).Append("\r\n");
        body.Append("SUMMARY:").Append(summary).Append("\r\n");
        if (!lines.Any(l => l.StartsWith("DTSTART")))
            body.Append("DTSTART:20260310T090000Z\r\nDTEND:20260310T100000Z\r\n");
        foreach (var line in lines) body.Append(line).Append("\r\n");
        body.Append("DTSTAMP:20260301T000000Z\r\nEND:VEVENT\r\n");
        return body.ToString();
    }

    /// A body whose length is unknown up front, like a chunked response.
    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableStream(byte[] data) => _inner = new MemoryStream(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
