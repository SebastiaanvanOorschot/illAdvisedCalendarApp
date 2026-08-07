using AgendaApi.DTOs;
using AgendaApi.Models;

namespace AgendaApi.Mapping;

public static class CalendarSubscriptionMapper
{
    public static CalendarSubscriptionDto ToDto(CalendarSubscription s)
    {
        return new CalendarSubscriptionDto
        {
            Id = s.Id,
            Name = s.Name,
            ICalUrl = s.ICalUrl,
            Color = s.Color,
            SyncIntervalMinutes = s.SyncIntervalMinutes,
            IsActive = s.IsActive,
            LastSyncedAt = s.LastSyncedAt,
            LastSyncError = s.LastSyncError,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            UserId = s.UserId
        };
    }

    public static CalendarSubscription ToEntity(CreateCalendarSubscriptionDto dto, int userId)
    {
        return new CalendarSubscription
        {
            Name = dto.Name,
            ICalUrl = dto.ICalUrl,
            Color = dto.Color,
            SyncIntervalMinutes = dto.SyncIntervalMinutes ?? 60,
            IsActive = true,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static void ApplyUpdate(CalendarSubscription target, UpdateCalendarSubscriptionDto dto)
    {
        target.Name = dto.Name;
        target.ICalUrl = dto.ICalUrl;
        target.Color = dto.Color;
        target.SyncIntervalMinutes = dto.SyncIntervalMinutes ?? target.SyncIntervalMinutes;
        target.UpdatedAt = DateTime.UtcNow;
    }
}
