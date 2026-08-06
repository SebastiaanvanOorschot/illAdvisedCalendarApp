using AgendaApi.Data;
using AgendaApi.DTOs;
using AgendaApi.Mapping;
using AgendaApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace AgendaApi.Services;

public class EventService
{
    private readonly AgendaDbContext _context;
    private readonly IRecurrenceService _recurrenceService;
    private readonly CalendarShareService _shareService;
    private readonly IMemoryCache _cache;

    public EventService(AgendaDbContext context, IRecurrenceService recurrenceService, CalendarShareService shareService, IMemoryCache cache)
    {
        _context = context;
        _recurrenceService = recurrenceService;
        _shareService = shareService;
        _cache = cache;
    }

    // ── Cache helpers ────────────────────────────────────────────────────────
    // Each user gets a "buster" value stored in the cache.  Occurrence cache
    // keys include the buster so that any event mutation instantly invalidates
    // all previously cached results for that user without needing to enumerate
    // them.  Stale entries expire naturally via the 10-minute TTL.

    private string OccurrencesCacheKey(int userId, DateTime start, DateTime end)
    {
        var buster = _cache.GetOrCreate($"occ_buster:{userId}", e =>
        {
            e.Priority = CacheItemPriority.NeverRemove;
            return DateTime.UtcNow.Ticks;
        });
        return $"occ:{userId}:{buster}:{start:yyyyMMddHHmm}:{end:yyyyMMddHHmm}";
    }

    private void BustOccurrencesCache(int userId)
    {
        // Overwrite the buster — all existing occurrence cache keys become stale
        _cache.Set($"occ_buster:{userId}", DateTime.UtcNow.Ticks,
            new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
    }
    // ────────────────────────────────────────────────────────────────────────

    // ── Bulk-fetch helpers (avoid N+1 queries) ─────────────────────────────

    private async Task<Dictionary<int, string?>> GetSubscriptionNamesAsync(IEnumerable<Event> events)
    {
        var subscriptionIds = events
            .Where(e => e.CalendarSubscriptionId.HasValue)
            .Select(e => e.CalendarSubscriptionId!.Value)
            .Distinct()
            .ToList();

        if (subscriptionIds.Count == 0)
            return new Dictionary<int, string?>();

        return await _context.CalendarSubscriptions
            .Where(s => subscriptionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => (string?)s.Name);
    }

    private async Task<Dictionary<int, SharePermission?>> GetOwnerPermissionsAsync(int userId, List<int> sharedOwnerIds)
    {
        var permissions = new Dictionary<int, SharePermission?>();
        foreach (var ownerId in sharedOwnerIds)
        {
            var (_, perm) = await _shareService.CheckEventAccessAsync(userId, ownerId);
            permissions[ownerId] = perm;
        }
        return permissions;
    }

    private static EventWithOwnerDto MapWithOwner(Event e, int userId, Dictionary<int, SharePermission?> sharedOwnerPermissions, Dictionary<int, string?> subscriptionNames)
    {
        var isOwnEvent = e.UserId == userId;
        var permission = isOwnEvent
            ? SharePermission.ReadWrite
            : sharedOwnerPermissions.GetValueOrDefault(e.UserId);
        var subscriptionName = e.CalendarSubscriptionId.HasValue
            ? subscriptionNames.GetValueOrDefault(e.CalendarSubscriptionId.Value)
            : null;

        return EventMapper.ToWithOwnerDto(e, isOwnEvent, permission, subscriptionName);
    }
    // ────────────────────────────────────────────────────────────────────────

    public async Task<List<EventWithOwnerDto>> GetEventsWithOwnerAsync(int userId)
    {
        var ownEvents = await _context.Events
            .Include(e => e.User)
            .Where(e => e.UserId == userId)
            .ToListAsync();

        var sharedOwnerIds = await _shareService.GetSharedCalendarOwnerIdsAsync(userId);

        var sharedEvents = await _context.Events
            .Include(e => e.User)
            .Where(e => sharedOwnerIds.Contains(e.UserId))
            .ToListAsync();

        var allEvents = ownEvents.Concat(sharedEvents).ToList();

        var subscriptionNames = await GetSubscriptionNamesAsync(allEvents);
        var sharedOwnerPermissions = await GetOwnerPermissionsAsync(userId, sharedOwnerIds);

        return allEvents
            .Select(e => MapWithOwner(e, userId, sharedOwnerPermissions, subscriptionNames))
            .ToList();
    }

    public async Task<EventWithOwnerDto?> GetEventWithOwnerAsync(int userId, int id)
    {
        var eventItem = await _context.Events.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id);
        if (eventItem == null)
            return null;

        var (hasAccess, permission) = await _shareService.CheckEventAccessAsync(userId, eventItem.UserId);
        if (!hasAccess)
            return null;

        string? subscriptionName = null;
        if (eventItem.CalendarSubscriptionId.HasValue)
        {
            var subscription = await _context.CalendarSubscriptions
                .FirstOrDefaultAsync(s => s.Id == eventItem.CalendarSubscriptionId.Value);
            subscriptionName = subscription?.Name;
        }

        var isOwnEvent = eventItem.UserId == userId;
        return EventMapper.ToWithOwnerDto(eventItem, isOwnEvent, permission, subscriptionName);
    }

    public async Task<List<EventDto>> GetEventsByDateAsync(int userId, DateTime date)
    {
        // Convert to local date to match how events are stored
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        var events = await _context.Events
            .Where(e => e.UserId == userId && e.StartDateTime >= startOfDay && e.StartDateTime < endOfDay)
            .OrderBy(e => e.StartDateTime)
            .ToListAsync();

        return events.Select(EventMapper.ToDto).ToList();
    }

    public async Task<List<EventWithOwnerDto>> GetEventsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate)
    {
        // Normalize dates to start of day and end of day
        var start = startDate.Date;
        var end = endDate.Date.AddDays(1);

        var sharedOwnerIds = await _shareService.GetSharedCalendarOwnerIdsAsync(userId);
        var allOwnerIds = new List<int> { userId };
        allOwnerIds.AddRange(sharedOwnerIds);

        var events = await _context.Events
            .Include(e => e.User)
            .Where(e => allOwnerIds.Contains(e.UserId) && e.StartDateTime >= start && e.StartDateTime < end)
            .OrderBy(e => e.StartDateTime)
            .ToListAsync();

        var subscriptionNames = await GetSubscriptionNamesAsync(events);
        var sharedOwnerPermissions = await GetOwnerPermissionsAsync(userId, sharedOwnerIds);

        return events
            .Select(e => MapWithOwner(e, userId, sharedOwnerPermissions, subscriptionNames))
            .ToList();
    }

    public async Task<List<EventOccurrenceDto>> GetOccurrencesAsync(int userId, DateTime startDate, DateTime endDate)
    {
        var cacheKey = OccurrencesCacheKey(userId, startDate, endDate);

        if (_cache.TryGetValue(cacheKey, out List<EventOccurrenceDto>? cached) && cached != null)
        {
            return cached;
        }

        var sharedOwnerIds = await _shareService.GetSharedCalendarOwnerIdsAsync(userId);
        var allOwnerIds = new List<int> { userId };
        allOwnerIds.AddRange(sharedOwnerIds);

        // Get events that could potentially have occurrences in the date range
        // This includes:
        // 1. Non-recurring events that fall within the range
        // 2. Recurring events that start before or during the range and either have no end date or end after the range starts
        var events = await _context.Events
            .Include(e => e.User)
            .Where(e => allOwnerIds.Contains(e.UserId) && (
                // Non-recurring events within range
                (!e.IsRecurring && string.IsNullOrEmpty(e.RecurrenceRule) && e.StartDateTime >= startDate && e.StartDateTime < endDate) ||
                // Recurring events that could have occurrences in range
                ((e.IsRecurring || !string.IsNullOrEmpty(e.RecurrenceRule)) && e.StartDateTime < endDate &&
                    (e.RecurrenceEndDate == null || e.RecurrenceEndDate >= startDate))
            ))
            .ToListAsync();

        var allOccurrences = new List<EventOccurrence>();

        // Pre-fetch permissions for each unique shared owner — avoids N+1 DB calls inside the event loop
        var sharedOwnerPermissions = await GetOwnerPermissionsAsync(userId, sharedOwnerIds);

        // Calculate occurrences for each event
        foreach (var evt in events)
        {
            var occurrences = _recurrenceService.GetOccurrences(evt, startDate, endDate);

            var isOwnEvent = evt.UserId == userId;
            var permission = isOwnEvent
                ? SharePermission.ReadWrite
                : sharedOwnerPermissions.GetValueOrDefault(evt.UserId);

            foreach (var occurrence in occurrences)
            {
                occurrence.OwnerName = evt.User?.Name;
                occurrence.OwnerEmail = evt.User?.Email;
                occurrence.IsOwnEvent = isOwnEvent;
                occurrence.Permission = permission;
            }

            allOccurrences.AddRange(occurrences);
        }

        // Sort by occurrence start time and map to DTOs before caching
        var result = allOccurrences
            .OrderBy(o => o.OccurrenceStart)
            .Select(EventMapper.ToDto)
            .ToList();

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));

        return result;
    }

    public async Task<EventDto> CreateEventAsync(int userId, CreateEventDto dto)
    {
        var entity = EventMapper.ToEntity(dto);

        // Ensure datetimes are treated as UTC (Npgsql 6+ requires DateTimeKind.Utc for timestamptz columns)
        entity.StartDateTime = DateTime.SpecifyKind(entity.StartDateTime, DateTimeKind.Utc);
        if (entity.EndDateTime.HasValue)
            entity.EndDateTime = DateTime.SpecifyKind(entity.EndDateTime.Value, DateTimeKind.Utc);

        entity.UserId = userId;
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        _context.Events.Add(entity);
        await _context.SaveChangesAsync();

        BustOccurrencesCache(userId);

        return EventMapper.ToDto(entity);
    }

    public async Task<EventServiceResult> UpdateEventAsync(int userId, int id, UpdateEventDto dto)
    {
        var existingEvent = await _context.Events.FindAsync(id);
        if (existingEvent == null)
            return EventServiceResult.NotFoundResult();

        // Check if user has access and permission to edit
        var (hasAccess, permission) = await _shareService.CheckEventAccessAsync(userId, existingEvent.UserId);
        if (!hasAccess)
            return EventServiceResult.NotFoundResult();

        // Only owner or users with ReadWrite permission can edit
        if (existingEvent.UserId != userId && permission != SharePermission.ReadWrite)
            return EventServiceResult.ForbiddenResult("You don't have permission to edit this event");

        EventMapper.ApplyUpdate(existingEvent, dto);

        // Ensure datetimes are treated as UTC (Npgsql 6+ requires DateTimeKind.Utc for timestamptz columns)
        existingEvent.StartDateTime = DateTime.SpecifyKind(existingEvent.StartDateTime, DateTimeKind.Utc);
        if (existingEvent.EndDateTime.HasValue)
            existingEvent.EndDateTime = DateTime.SpecifyKind(existingEvent.EndDateTime.Value, DateTimeKind.Utc);

        existingEvent.UpdatedAt = DateTime.UtcNow;

        // Mark as locally modified if it was imported from Google Calendar
        if (existingEvent.IsImportedFromGoogle)
        {
            existingEvent.IsLocallyModified = true;
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!EventExists(id))
            {
                return EventServiceResult.NotFoundResult();
            }
            else
            {
                throw;
            }
        }

        BustOccurrencesCache(userId);

        return EventServiceResult.Ok();
    }

    public async Task<EventServiceResult> DeleteEventAsync(int userId, int id)
    {
        var eventItem = await _context.Events.FindAsync(id);
        if (eventItem == null)
            return EventServiceResult.NotFoundResult();

        // Check if user has access and permission to delete
        var (hasAccess, permission) = await _shareService.CheckEventAccessAsync(userId, eventItem.UserId);
        if (!hasAccess)
            return EventServiceResult.NotFoundResult();

        // Only owner or users with ReadWrite permission can delete
        if (eventItem.UserId != userId && permission != SharePermission.ReadWrite)
            return EventServiceResult.ForbiddenResult("You don't have permission to delete this event");

        _context.Events.Remove(eventItem);
        await _context.SaveChangesAsync();

        BustOccurrencesCache(userId);

        return EventServiceResult.Ok();
    }

    public async Task<EventServiceResult> AddExceptionDateAsync(int userId, int id, DateTime exceptionDate)
    {
        var eventItem = await _context.Events.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
        if (eventItem == null)
            return EventServiceResult.NotFoundResult();

        // Parse existing exception dates
        var exceptions = new List<DateTime>();
        if (!string.IsNullOrWhiteSpace(eventItem.ExceptionDates))
        {
            exceptions = eventItem.ExceptionDates
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => DateTime.Parse(s.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
                .ToList();
        }

        // Add new exception if not already present
        if (!exceptions.Any(e => e.Date == exceptionDate.Date))
        {
            exceptions.Add(exceptionDate);
            eventItem.ExceptionDates = string.Join(",", exceptions.Select(e => e.ToString("o")));
            eventItem.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            BustOccurrencesCache(userId);
        }

        return EventServiceResult.Ok();
    }

    public async Task<EventServiceResult<EventDto>> EditOccurrenceAsync(int userId, int id, EditOccurrenceDto dto)
    {
        var parentEvent = await _context.Events.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
        if (parentEvent == null)
            return EventServiceResult<EventDto>.NotFoundResult();

        // Verify this is a recurring event
        if (!parentEvent.IsRecurring && string.IsNullOrEmpty(parentEvent.RecurrenceRule))
            return EventServiceResult<EventDto>.BadRequestResult("This is not a recurring event");

        // Add exception date to parent event to hide the original occurrence
        var exceptions = new List<DateTime>();
        if (!string.IsNullOrWhiteSpace(parentEvent.ExceptionDates))
        {
            exceptions = parentEvent.ExceptionDates
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => DateTime.Parse(s.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind))
                .ToList();
        }

        if (!exceptions.Any(e => e.Date == dto.OriginalOccurrenceDate.Date))
        {
            exceptions.Add(dto.OriginalOccurrenceDate);
            parentEvent.ExceptionDates = string.Join(",", exceptions.Select(e => e.ToString("o")));
            parentEvent.UpdatedAt = DateTime.UtcNow;
        }

        // Create new event for the modified occurrence
        var modifiedEvent = new Event
        {
            Title = dto.Title,
            Description = dto.Description,
            StartDateTime = dto.NewStartDateTime,
            EndDateTime = dto.NewEndDateTime,
            Color = dto.Color ?? parentEvent.Color,
            IsRecurring = false,
            RecurrenceId = dto.OriginalOccurrenceDate, // Link to original occurrence
            ParentEventId = parentEvent.Id,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(modifiedEvent);
        await _context.SaveChangesAsync();

        BustOccurrencesCache(userId);

        return EventServiceResult<EventDto>.Ok(EventMapper.ToDto(modifiedEvent));
    }

    private bool EventExists(int id)
    {
        return _context.Events.Any(e => e.Id == id);
    }
}

public enum EventServiceStatus
{
    Success,
    NotFound,
    Forbidden,
    BadRequest
}

/// <summary>
/// Non-generic result for service operations that don't return data (update/delete/etc.)
/// </summary>
public class EventServiceResult
{
    public EventServiceStatus Status { get; init; }
    public string? ErrorMessage { get; init; }

    public static EventServiceResult Ok() => new() { Status = EventServiceStatus.Success };
    public static EventServiceResult NotFoundResult() => new() { Status = EventServiceStatus.NotFound };
    public static EventServiceResult ForbiddenResult(string message) => new() { Status = EventServiceStatus.Forbidden, ErrorMessage = message };
    public static EventServiceResult BadRequestResult(string message) => new() { Status = EventServiceStatus.BadRequest, ErrorMessage = message };
}

/// <summary>
/// Generic result for service operations that return data on success (e.g. EditOccurrence).
/// </summary>
public class EventServiceResult<T>
{
    public EventServiceStatus Status { get; init; }
    public T? Value { get; init; }
    public string? ErrorMessage { get; init; }

    public static EventServiceResult<T> Ok(T value) => new() { Status = EventServiceStatus.Success, Value = value };
    public static EventServiceResult<T> NotFoundResult() => new() { Status = EventServiceStatus.NotFound };
    public static EventServiceResult<T> ForbiddenResult(string message) => new() { Status = EventServiceStatus.Forbidden, ErrorMessage = message };
    public static EventServiceResult<T> BadRequestResult(string message) => new() { Status = EventServiceStatus.BadRequest, ErrorMessage = message };
}
