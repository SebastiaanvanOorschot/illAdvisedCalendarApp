using AgendaApi.Data;
using AgendaApi.Models;
using AgendaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace AgendaApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly AgendaDbContext _context;
    private readonly IRecurrenceService _recurrenceService;
    private readonly CalendarShareService _shareService;
    private readonly IMemoryCache _cache;

    public EventsController(AgendaDbContext context, IRecurrenceService recurrenceService, CalendarShareService shareService, IMemoryCache cache)
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

    private int GetCurrentUserId()
    {
        // Try to get the user ID from the "sub" claim (JWT standard)
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId;
    }

    private async Task<EventWithOwner> MapToEventWithOwner(Event e, int currentUserId)
    {
        var isOwnEvent = e.UserId == currentUserId;
        SharePermission? permission = null;

        if (!isOwnEvent)
        {
            var (hasAccess, eventPermission) = await _shareService.CheckEventAccessAsync(currentUserId, e.UserId);
            permission = eventPermission;
        }
        else
        {
            permission = SharePermission.ReadWrite; // Owners have full access
        }

        // Fetch subscription name if event is from a subscription
        string? subscriptionName = null;
        if (e.CalendarSubscriptionId.HasValue)
        {
            var subscription = await _context.CalendarSubscriptions
                .FirstOrDefaultAsync(s => s.Id == e.CalendarSubscriptionId.Value);
            subscriptionName = subscription?.Name;
        }

        return new EventWithOwner
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            StartDateTime = e.StartDateTime,
            EndDateTime = e.EndDateTime,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
            IsRecurring = e.IsRecurring,
            RecurrencePattern = e.RecurrencePattern,
            RecurrenceInterval = e.RecurrenceInterval,
            RecurrenceEndDate = e.RecurrenceEndDate,
            ParentEventId = e.ParentEventId,
            RecurrenceRule = e.RecurrenceRule,
            ExceptionDates = e.ExceptionDates,
            RecurrenceId = e.RecurrenceId,
            Color = e.Color,
            GoogleEventId = e.GoogleEventId,
            IsImportedFromGoogle = e.IsImportedFromGoogle,
            IsLocallyModified = e.IsLocallyModified,
            UserId = e.UserId,
            OwnerName = e.User?.Name,
            OwnerEmail = e.User?.Email,
            IsOwnEvent = isOwnEvent,
            Permission = permission,
            IsFromSubscription = e.IsFromSubscription,
            IsReadOnly = e.IsReadOnly,
            SubscriptionName = subscriptionName,
            CalendarSubscriptionId = e.CalendarSubscriptionId
        };
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EventWithOwner>>> GetEvents()
    {
        var userId = GetCurrentUserId();

        // Get user's own events
        var ownEvents = await _context.Events
            .Include(e => e.User)
            .Where(e => e.UserId == userId)
            .ToListAsync();

        // Get shared calendar owner IDs
        var sharedOwnerIds = await _shareService.GetSharedCalendarOwnerIdsAsync(userId);

        // Get events from shared calendars
        var sharedEvents = await _context.Events
            .Include(e => e.User)
            .Where(e => sharedOwnerIds.Contains(e.UserId))
            .ToListAsync();

        // Combine and map to EventWithOwner
        var allEventsList = ownEvents.Concat(sharedEvents).ToList();
        var allEvents = new List<EventWithOwner>();

        foreach (var e in allEventsList)
        {
            allEvents.Add(await MapToEventWithOwner(e, userId));
        }

        return Ok(allEvents);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<EventWithOwner>> GetEvent(int id)
    {
        var userId = GetCurrentUserId();
        var eventItem = await _context.Events.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id);

        if (eventItem == null)
        {
            return NotFound();
        }

        // Check if user has access to this event
        var (hasAccess, _) = await _shareService.CheckEventAccessAsync(userId, eventItem.UserId);
        if (!hasAccess)
        {
            return NotFound();
        }

        var eventWithOwner = await MapToEventWithOwner(eventItem, userId);
        return eventWithOwner;
    }

    [HttpGet("date/{date}")]
    public async Task<ActionResult<IEnumerable<Event>>> GetEventsByDate(DateTime date)
    {
        var userId = GetCurrentUserId();
        // Convert to local date to match how events are stored
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        var events = await _context.Events
            .Where(e => e.UserId == userId && e.StartDateTime >= startOfDay && e.StartDateTime < endOfDay)
            .OrderBy(e => e.StartDateTime)
            .ToListAsync();

        return events;
    }

    [HttpGet("range")]
    public async Task<ActionResult<IEnumerable<EventWithOwner>>> GetEventsByDateRange([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var userId = GetCurrentUserId();
        // Normalize dates to start of day and end of day
        var start = startDate.Date;
        var end = endDate.Date.AddDays(1);

        // Get shared calendar owner IDs
        var sharedOwnerIds = await _shareService.GetSharedCalendarOwnerIdsAsync(userId);
        var allOwnerIds = new List<int> { userId };
        allOwnerIds.AddRange(sharedOwnerIds);

        var events = await _context.Events
            .Include(e => e.User)
            .Where(e => allOwnerIds.Contains(e.UserId) && e.StartDateTime >= start && e.StartDateTime < end)
            .OrderBy(e => e.StartDateTime)
            .ToListAsync();

        var eventsWithOwner = new List<EventWithOwner>();
        foreach (var e in events)
        {
            eventsWithOwner.Add(await MapToEventWithOwner(e, userId));
        }

        return eventsWithOwner;
    }

    [HttpGet("occurrences")]
    public async Task<ActionResult<IEnumerable<EventOccurrence>>> GetEventOccurrences([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var userId = GetCurrentUserId();
        var cacheKey = OccurrencesCacheKey(userId, startDate, endDate);

        if (_cache.TryGetValue(cacheKey, out List<EventOccurrence>? cached) && cached != null)
        {
            Response.Headers["X-Timing"] = "cache-hit";
            return cached;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Get shared calendar owner IDs
        var sharedOwnerIds = await _shareService.GetSharedCalendarOwnerIdsAsync(userId);
        var allOwnerIds = new List<int> { userId };
        allOwnerIds.AddRange(sharedOwnerIds);

        var t1 = sw.ElapsedMilliseconds;

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

        var t2 = sw.ElapsedMilliseconds;

        var allOccurrences = new List<EventOccurrence>();

        // Reset per-request fast/slow counters (cast is safe — concrete type is always RecurrenceService)
        if (_recurrenceService is RecurrenceService rs) rs.ResetCounters();

        // Pre-fetch permissions for each unique shared owner — avoids N+1 DB calls inside the event loop
        var sharedOwnerPermissions = new Dictionary<int, SharePermission?>();
        foreach (var ownerId in sharedOwnerIds)
        {
            var (_, perm) = await _shareService.CheckEventAccessAsync(userId, ownerId);
            sharedOwnerPermissions[ownerId] = perm;
        }

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

        var t3 = sw.ElapsedMilliseconds;

        // Sort by occurrence start time
        var result = allOccurrences.OrderBy(o => o.OccurrenceStart).ToList();

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));

        // Diagnostic headers — remove once performance is confirmed
        var fastCount = (_recurrenceService is RecurrenceService rs2) ? rs2.FastCount : -1;
        var slowCount = (_recurrenceService is RecurrenceService rs3) ? rs3.SlowCount : -1;
        Response.Headers["X-Timing"] = $"shares={t1}ms db={t2 - t1}ms recurrence={t3 - t2}ms events={events.Count} fast={fastCount} slow={slowCount} total={t3}ms";

        return result;
    }

    [HttpPost]
    public async Task<ActionResult<Event>> CreateEvent(Event eventItem)
    {
        var userId = GetCurrentUserId();

        // Ensure datetimes are treated as UTC (Npgsql 6+ requires DateTimeKind.Utc for timestamptz columns)
        eventItem.StartDateTime = DateTime.SpecifyKind(eventItem.StartDateTime, DateTimeKind.Utc);
        eventItem.EndDateTime = DateTime.SpecifyKind(eventItem.EndDateTime, DateTimeKind.Utc);

        eventItem.UserId = userId;
        eventItem.CreatedAt = DateTime.UtcNow;
        eventItem.UpdatedAt = DateTime.UtcNow;

        _context.Events.Add(eventItem);
        await _context.SaveChangesAsync();

        BustOccurrencesCache(userId);

        // Return 200 OK instead of 201 Created for compatibility with NSwag client
        return Ok(eventItem);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEvent(int id, Event eventItem)
    {
        var userId = GetCurrentUserId();

        if (id != eventItem.Id)
        {
            return BadRequest();
        }

        // Verify the event exists
        var existingEvent = await _context.Events.FindAsync(id);
        if (existingEvent == null)
        {
            return NotFound();
        }

        // Check if user has access and permission to edit
        var (hasAccess, permission) = await _shareService.CheckEventAccessAsync(userId, existingEvent.UserId);
        if (!hasAccess)
        {
            return NotFound();
        }

        // Only owner or users with ReadWrite permission can edit
        if (existingEvent.UserId != userId && permission != SharePermission.ReadWrite)
        {
            return Forbid("You don't have permission to edit this event");
        }

        // Ensure datetimes are treated as UTC (Npgsql 6+ requires DateTimeKind.Utc for timestamptz columns)
        eventItem.StartDateTime = DateTime.SpecifyKind(eventItem.StartDateTime, DateTimeKind.Utc);
        eventItem.EndDateTime = DateTime.SpecifyKind(eventItem.EndDateTime, DateTimeKind.Utc);

        // Update the existing tracked entity's properties
        existingEvent.Title = eventItem.Title;
        existingEvent.Description = eventItem.Description;
        existingEvent.StartDateTime = eventItem.StartDateTime;
        existingEvent.EndDateTime = eventItem.EndDateTime;
        existingEvent.Color = eventItem.Color;
        existingEvent.IsRecurring = eventItem.IsRecurring;
        existingEvent.RecurrencePattern = eventItem.RecurrencePattern;
        existingEvent.RecurrenceInterval = eventItem.RecurrenceInterval;
        existingEvent.RecurrenceEndDate = eventItem.RecurrenceEndDate;
        existingEvent.RecurrenceRule = eventItem.RecurrenceRule;
        existingEvent.ExceptionDates = eventItem.ExceptionDates;
        existingEvent.RecurrenceId = eventItem.RecurrenceId;
        existingEvent.ParentEventId = eventItem.ParentEventId;

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
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        BustOccurrencesCache(userId);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var userId = GetCurrentUserId();
        var eventItem = await _context.Events.FindAsync(id);

        if (eventItem == null)
        {
            return NotFound();
        }

        // Check if user has access and permission to delete
        var (hasAccess, permission) = await _shareService.CheckEventAccessAsync(userId, eventItem.UserId);
        if (!hasAccess)
        {
            return NotFound();
        }

        // Only owner or users with ReadWrite permission can delete
        if (eventItem.UserId != userId && permission != SharePermission.ReadWrite)
        {
            return Forbid("You don't have permission to delete this event");
        }

        _context.Events.Remove(eventItem);
        await _context.SaveChangesAsync();

        BustOccurrencesCache(userId);

        return NoContent();
    }

    [HttpPost("{id}/add-exception")]
    public async Task<IActionResult> AddExceptionDate(int id, [FromBody] DateTime exceptionDate)
    {
        var userId = GetCurrentUserId();
        var eventItem = await _context.Events.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (eventItem == null)
        {
            return NotFound();
        }

        // Parse existing exception dates
        var exceptions = new List<DateTime>();
        if (!string.IsNullOrWhiteSpace(eventItem.ExceptionDates))
        {
            exceptions = eventItem.ExceptionDates
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => DateTime.Parse(s.Trim()))
                .ToList();
        }

        // Add new exception if not already present
        if (!exceptions.Any(e => e.Date == exceptionDate.Date))
        {
            exceptions.Add(exceptionDate);
            eventItem.ExceptionDates = string.Join(",", exceptions.Select(e => e.ToString("o")));
            eventItem.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    /// <summary>
    /// Edit a single occurrence of a recurring event.
    /// Creates a new event for the modified occurrence and adds an exception to the parent series.
    /// </summary>
    [HttpPost("{id}/edit-occurrence")]
    public async Task<ActionResult<Event>> EditOccurrence(int id, [FromBody] EditOccurrenceRequest request)
    {
        var userId = GetCurrentUserId();
        var parentEvent = await _context.Events.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

        if (parentEvent == null)
        {
            return NotFound();
        }

        // Verify this is a recurring event
        if (!parentEvent.IsRecurring && string.IsNullOrEmpty(parentEvent.RecurrenceRule))
        {
            return BadRequest("This is not a recurring event");
        }

        // Add exception date to parent event to hide the original occurrence
        var exceptions = new List<DateTime>();
        if (!string.IsNullOrWhiteSpace(parentEvent.ExceptionDates))
        {
            exceptions = parentEvent.ExceptionDates
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => DateTime.Parse(s.Trim()))
                .ToList();
        }

        if (!exceptions.Any(e => e.Date == request.OriginalOccurrenceDate.Date))
        {
            exceptions.Add(request.OriginalOccurrenceDate);
            parentEvent.ExceptionDates = string.Join(",", exceptions.Select(e => e.ToString("o")));
            parentEvent.UpdatedAt = DateTime.UtcNow;
        }

        // Create new event for the modified occurrence
        var modifiedEvent = new Event
        {
            Title = request.Title,
            Description = request.Description,
            StartDateTime = request.NewStartDateTime,
            EndDateTime = request.NewEndDateTime,
            Color = request.Color ?? parentEvent.Color,
            IsRecurring = false,
            RecurrenceId = request.OriginalOccurrenceDate, // Link to original occurrence
            ParentEventId = parentEvent.Id,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(modifiedEvent);
        await _context.SaveChangesAsync();

        return Ok(modifiedEvent);
    }

    private bool EventExists(int id)
    {
        return _context.Events.Any(e => e.Id == id);
    }
}

/// <summary>
/// Request model for editing a single occurrence of a recurring event
/// </summary>
public class EditOccurrenceRequest
{
    public DateTime OriginalOccurrenceDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime NewStartDateTime { get; set; }
    public DateTime NewEndDateTime { get; set; }
    public string? Color { get; set; }
}

/// <summary>
/// Event model with owner information for shared calendars
/// </summary>
public class EventWithOwner
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsRecurring { get; set; }
    public string? RecurrencePattern { get; set; }
    public int? RecurrenceInterval { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int? ParentEventId { get; set; }
    public string? RecurrenceRule { get; set; }
    public string? ExceptionDates { get; set; }
    public DateTime? RecurrenceId { get; set; }
    public string? Color { get; set; }
    public string? GoogleEventId { get; set; }
    public bool IsImportedFromGoogle { get; set; }
    public bool IsLocallyModified { get; set; }
    public int UserId { get; set; }

    // Owner information
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public bool IsOwnEvent { get; set; }
    public SharePermission? Permission { get; set; }

    // Subscription information
    public bool IsFromSubscription { get; set; }
    public bool IsReadOnly { get; set; }
    public string? SubscriptionName { get; set; }
    public int? CalendarSubscriptionId { get; set; }
}
