using System.Reflection;
using AgendaApi.DTOs;
using AgendaApi.Mapping;
using AgendaApi.Models;
using AgendaApi.Services;

namespace AgendaApi.Tests.Mapping;

public class MapperTests
{
    private static readonly DateTime Start = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    // ── EventMapper ─────────────────────────────────────────────────────────

    [Fact]
    public void EventToDto_CopiesEveryPropertyTheDtoShares()
    {
        // Reflection, so a property added to both Event and EventDto but forgotten in the mapper fails here
        AssertSharedPropertiesEqual(FullEvent(), EventMapper.ToDto(FullEvent()));
    }

    [Fact]
    public void EventToWithOwnerDto_CopiesTheEventAndAddsOwnerInfo()
    {
        var evt = FullEvent();
        evt.User = new User { GoogleId = "g", Email = "owner@example.test", Name = "Owner" };

        var dto = EventMapper.ToWithOwnerDto(evt, isOwnEvent: false, SharePermission.Read, "Work feed");

        AssertSharedPropertiesEqual(evt, dto, except: nameof(Event.User));
        Assert.Equal("Owner", dto.OwnerName);
        Assert.Equal("owner@example.test", dto.OwnerEmail);
        Assert.False(dto.IsOwnEvent);
        Assert.Equal(SharePermission.Read, dto.Permission);
        Assert.Equal("Work feed", dto.SubscriptionName);
    }

    [Fact]
    public void EventToWithOwnerDto_WithoutLoadedUser_LeavesOwnerFieldsEmpty()
    {
        var dto = EventMapper.ToWithOwnerDto(FullEvent(), isOwnEvent: true, SharePermission.ReadWrite, null);

        Assert.Null(dto.OwnerName);
        Assert.Null(dto.OwnerEmail);
    }

    [Fact]
    public void CreateDtoToEntity_DefaultsMissingFlagsToFalse()
    {
        var entity = EventMapper.ToEntity(new CreateEventDto { Title = "Lunch", StartDateTime = Start });

        Assert.Equal("Lunch", entity.Title);
        Assert.Equal(Start, entity.StartDateTime);
        Assert.False(entity.IsAllDay);
        Assert.False(entity.IsRecurring);
    }

    [Fact]
    public void CreateDtoToEntity_CopiesRecurrenceFields()
    {
        var dto = new CreateEventDto
        {
            Title = "Gym",
            StartDateTime = Start,
            EndDateTime = Start.AddHours(1),
            IsAllDay = true,
            IsRecurring = true,
            RecurrencePattern = "weekly",
            RecurrenceInterval = 2,
            RecurrenceEndDate = Start.AddMonths(3),
            RecurrenceRule = "FREQ=WEEKLY;INTERVAL=2",
            Color = "#00FF00"
        };

        var entity = EventMapper.ToEntity(dto);

        Assert.True(entity.IsAllDay);
        Assert.True(entity.IsRecurring);
        Assert.Equal("weekly", entity.RecurrencePattern);
        Assert.Equal(2, entity.RecurrenceInterval);
        Assert.Equal(Start.AddMonths(3), entity.RecurrenceEndDate);
        Assert.Equal("FREQ=WEEKLY;INTERVAL=2", entity.RecurrenceRule);
        Assert.Equal("#00FF00", entity.Color);
    }

    [Fact]
    public void ApplyUpdate_KeepsExceptionDatesAndRecurrenceIdWhenNotSent()
    {
        var target = FullEvent();

        EventMapper.ApplyUpdate(target, new UpdateEventDto { Title = "Renamed", StartDateTime = Start });

        Assert.Equal("Renamed", target.Title);
        Assert.Equal("2026-03-12T09:00:00.0000000Z", target.ExceptionDates);
        Assert.Equal(Start.AddDays(-7), target.RecurrenceId);
    }

    [Fact]
    public void ApplyUpdate_OverwritesExceptionDatesWhenSent()
    {
        var target = FullEvent();

        EventMapper.ApplyUpdate(target, new UpdateEventDto { Title = "x", StartDateTime = Start, ExceptionDates = "" });

        Assert.Equal("", target.ExceptionDates);
    }

    [Fact]
    public void OccurrenceToDto_CopiesEveryProperty()
    {
        var occurrence = new EventOccurrence
        {
            EventId = 5,
            OccurrenceStart = Start,
            OccurrenceEnd = Start.AddHours(1),
            Title = "Standup",
            Description = "daily",
            Color = "#123456",
            IsRecurring = true,
            OwnerName = "Owner",
            OwnerEmail = "owner@example.test",
            IsOwnEvent = false,
            Permission = SharePermission.Read
        };

        AssertSharedPropertiesEqual(occurrence, EventMapper.ToDto(occurrence));
    }

    // ── CalendarSubscriptionMapper ──────────────────────────────────────────

    [Fact]
    public void SubscriptionToDto_CopiesEveryProperty()
    {
        var subscription = new CalendarSubscription
        {
            Id = 3,
            Name = "Holidays",
            ICalUrl = "https://example.test/h.ics",
            Color = "#FF69B4",
            SyncIntervalMinutes = 15,
            IsActive = false,
            LastSyncedAt = Start,
            LastSyncError = "boom",
            CreatedAt = Start.AddDays(-1),
            UpdatedAt = Start,
            UserId = 9
        };

        AssertSharedPropertiesEqual(subscription, CalendarSubscriptionMapper.ToDto(subscription), except: nameof(CalendarSubscription.User));
    }

    [Fact]
    public void SubscriptionToEntity_DefaultsToHourlyAndActive()
    {
        var entity = CalendarSubscriptionMapper.ToEntity(
            new CreateCalendarSubscriptionDto { Name = "Holidays", ICalUrl = "https://example.test/h.ics" }, userId: 9);

        Assert.Equal(60, entity.SyncIntervalMinutes);
        Assert.True(entity.IsActive);
        Assert.Equal(9, entity.UserId);
    }

    [Fact]
    public void SubscriptionApplyUpdate_KeepsTheIntervalWhenNotSent()
    {
        var target = new CalendarSubscription { Name = "Old", ICalUrl = "https://old.test", SyncIntervalMinutes = 15 };

        CalendarSubscriptionMapper.ApplyUpdate(target, new UpdateCalendarSubscriptionDto { Name = "New", ICalUrl = "https://new.test" });

        Assert.Equal("New", target.Name);
        Assert.Equal("https://new.test", target.ICalUrl);
        Assert.Equal(15, target.SyncIntervalMinutes);
    }

    // ── ColorMappingService ─────────────────────────────────────────────────

    [Theory]
    [InlineData("4", "#FF0000")]
    [InlineData("5", "#FFFF00")]
    [InlineData("9", "#0000FF")]
    [InlineData("11", "#FF0000")]
    [InlineData("12", "#000000")]
    [InlineData("", "#000000")]
    [InlineData(null, "#000000")]
    public void GoogleColorIds_MapToAppColors_DefaultingToBlack(string? colorId, string expected)
    {
        Assert.Equal(expected, ColorMappingService.MapGoogleColorToAppColor(colorId));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static Event FullEvent() => new()
    {
        Id = 12,
        Title = "Standup",
        Description = "daily sync",
        StartDateTime = Start,
        EndDateTime = Start.AddMinutes(15),
        CreatedAt = Start.AddDays(-30),
        UpdatedAt = Start.AddDays(-1),
        IsRecurring = true,
        RecurrencePattern = "daily",
        RecurrenceInterval = 1,
        RecurrenceEndDate = Start.AddMonths(6),
        ParentEventId = 4,
        RecurrenceRule = "FREQ=DAILY",
        ExceptionDates = "2026-03-12T09:00:00.0000000Z",
        RecurrenceId = Start.AddDays(-7),
        IsAllDay = true,
        Color = "#FF8800",
        GoogleCalendarId = "primary",
        GoogleEventId = "g-123",
        IsImportedFromGoogle = true,
        IsLocallyModified = true,
        CalendarSubscriptionId = 2,
        ExternalEventId = "uid-1",
        IsFromSubscription = true,
        IsReadOnly = true,
        UserId = 7
    };

    private static void AssertSharedPropertiesEqual(object source, object target, params string[] except)
    {
        var sourceProps = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name);
        var shared = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => sourceProps.ContainsKey(p.Name) && !except.Contains(p.Name))
            .ToList();

        Assert.NotEmpty(shared);
        foreach (var property in shared)
        {
            Assert.True(Equals(sourceProps[property.Name].GetValue(source), property.GetValue(target)),
                $"{property.Name} was not copied");
        }
    }
}
