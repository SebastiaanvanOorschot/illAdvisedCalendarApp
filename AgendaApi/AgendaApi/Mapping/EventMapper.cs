using AgendaApi.DTOs;
using AgendaApi.Models;
using AgendaApi.Services;

namespace AgendaApi.Mapping;

public static class EventMapper
{
    public static EventDto ToDto(Event e)
    {
        return new EventDto
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            StartDateTime = e.StartDateTime,
            EndDateTime = e.EndDateTime,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            IsAllDay = e.IsAllDay,
            Color = e.Color,
            IsRecurring = e.IsRecurring,
            RecurrencePattern = e.RecurrencePattern,
            RecurrenceInterval = e.RecurrenceInterval,
            RecurrenceEndDate = e.RecurrenceEndDate,
            RecurrenceRule = e.RecurrenceRule,
            ExceptionDates = e.ExceptionDates,
            RecurrenceId = e.RecurrenceId,
            ParentEventId = e.ParentEventId,
            IsImportedFromGoogle = e.IsImportedFromGoogle,
            IsLocallyModified = e.IsLocallyModified,
            IsFromSubscription = e.IsFromSubscription,
            IsReadOnly = e.IsReadOnly,
            CalendarSubscriptionId = e.CalendarSubscriptionId,
            UserId = e.UserId
        };
    }

    public static EventWithOwnerDto ToWithOwnerDto(Event e, bool isOwnEvent, SharePermission? permission, string? subscriptionName)
    {
        return new EventWithOwnerDto
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            StartDateTime = e.StartDateTime,
            EndDateTime = e.EndDateTime,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            IsAllDay = e.IsAllDay,
            Color = e.Color,
            IsRecurring = e.IsRecurring,
            RecurrencePattern = e.RecurrencePattern,
            RecurrenceInterval = e.RecurrenceInterval,
            RecurrenceEndDate = e.RecurrenceEndDate,
            RecurrenceRule = e.RecurrenceRule,
            ExceptionDates = e.ExceptionDates,
            RecurrenceId = e.RecurrenceId,
            ParentEventId = e.ParentEventId,
            IsImportedFromGoogle = e.IsImportedFromGoogle,
            IsLocallyModified = e.IsLocallyModified,
            IsFromSubscription = e.IsFromSubscription,
            IsReadOnly = e.IsReadOnly,
            CalendarSubscriptionId = e.CalendarSubscriptionId,
            UserId = e.UserId,
            OwnerName = e.User?.Name,
            OwnerEmail = e.User?.Email,
            IsOwnEvent = isOwnEvent,
            Permission = permission,
            SubscriptionName = subscriptionName
        };
    }

    public static Event ToEntity(CreateEventDto dto)
    {
        return new Event
        {
            Title = dto.Title,
            Description = dto.Description,
            StartDateTime = dto.StartDateTime,
            EndDateTime = dto.EndDateTime,
            IsAllDay = dto.IsAllDay,
            Color = dto.Color,
            IsRecurring = dto.IsRecurring,
            RecurrencePattern = dto.RecurrencePattern,
            RecurrenceInterval = dto.RecurrenceInterval,
            RecurrenceEndDate = dto.RecurrenceEndDate,
            RecurrenceRule = dto.RecurrenceRule,
            ExceptionDates = dto.ExceptionDates,
            RecurrenceId = dto.RecurrenceId,
            ParentEventId = dto.ParentEventId
        };
    }

    public static void ApplyUpdate(Event target, UpdateEventDto dto)
    {
        target.Title = dto.Title;
        target.Description = dto.Description;
        target.StartDateTime = dto.StartDateTime;
        target.EndDateTime = dto.EndDateTime;
        target.IsAllDay = dto.IsAllDay;
        target.Color = dto.Color;
        target.IsRecurring = dto.IsRecurring;
        target.RecurrencePattern = dto.RecurrencePattern;
        target.RecurrenceInterval = dto.RecurrenceInterval;
        target.RecurrenceEndDate = dto.RecurrenceEndDate;
        target.RecurrenceRule = dto.RecurrenceRule;
        target.ExceptionDates = dto.ExceptionDates;
        target.RecurrenceId = dto.RecurrenceId;
        target.ParentEventId = dto.ParentEventId;
    }

    public static EventOccurrenceDto ToDto(EventOccurrence o)
    {
        return new EventOccurrenceDto
        {
            EventId = o.EventId,
            OccurrenceStart = o.OccurrenceStart,
            OccurrenceEnd = o.OccurrenceEnd,
            Title = o.Title,
            Description = o.Description,
            Color = o.Color,
            IsRecurring = o.IsRecurring,
            OwnerName = o.OwnerName,
            OwnerEmail = o.OwnerEmail,
            IsOwnEvent = o.IsOwnEvent,
            Permission = o.Permission
        };
    }
}
