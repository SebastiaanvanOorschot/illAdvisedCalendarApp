using AgendaApi.Data;
using AgendaApi.DTOs;
using AgendaApi.Mapping;
using Microsoft.EntityFrameworkCore;

namespace AgendaApi.Services;

public class CalendarSubscriptionService
{
    private readonly AgendaDbContext _context;
    private readonly ICalSyncService _iCalSyncService;
    private readonly ILogger<CalendarSubscriptionService> _logger;

    public CalendarSubscriptionService(
        AgendaDbContext context,
        ICalSyncService iCalSyncService,
        ILogger<CalendarSubscriptionService> logger)
    {
        _context = context;
        _iCalSyncService = iCalSyncService;
        _logger = logger;
    }

    public async Task<List<CalendarSubscriptionDto>> GetSubscriptionsAsync(int userId)
    {
        var subscriptions = await _context.CalendarSubscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return subscriptions.Select(CalendarSubscriptionMapper.ToDto).ToList();
    }

    public async Task<CalendarSubscriptionDto?> GetSubscriptionAsync(int userId, int id)
    {
        var subscription = await _context.CalendarSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        return subscription == null ? null : CalendarSubscriptionMapper.ToDto(subscription);
    }

    public async Task<CalendarSubscriptionServiceResult<CalendarSubscriptionDto>> CreateSubscriptionAsync(int userId, CreateCalendarSubscriptionDto dto)
    {
        if (!Uri.TryCreate(dto.ICalUrl, UriKind.Absolute, out _))
            return CalendarSubscriptionServiceResult<CalendarSubscriptionDto>.BadRequestResult("Invalid iCal URL format");

        var subscription = CalendarSubscriptionMapper.ToEntity(dto, userId);

        _context.CalendarSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Performing initial sync for new subscription {SubscriptionId}", subscription.Id);
        var (success, error) = await _iCalSyncService.SyncSubscriptionAsync(subscription);

        if (!success)
        {
            _logger.LogWarning("Initial sync failed for subscription {SubscriptionId}: {Error}",
                subscription.Id, error);
            // Don't fail the creation, just log the error
        }

        return CalendarSubscriptionServiceResult<CalendarSubscriptionDto>.Ok(CalendarSubscriptionMapper.ToDto(subscription));
    }

    public async Task<CalendarSubscriptionServiceResult> UpdateSubscriptionAsync(int userId, int id, UpdateCalendarSubscriptionDto dto)
    {
        var subscription = await _context.CalendarSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
            return CalendarSubscriptionServiceResult.NotFoundResult();

        if (!Uri.TryCreate(dto.ICalUrl, UriKind.Absolute, out _))
            return CalendarSubscriptionServiceResult.BadRequestResult("Invalid iCal URL format");

        CalendarSubscriptionMapper.ApplyUpdate(subscription, dto);

        await _context.SaveChangesAsync();

        return CalendarSubscriptionServiceResult.Ok();
    }

    public async Task<CalendarSubscriptionServiceResult> DeleteSubscriptionAsync(int userId, int id)
    {
        var subscription = await _context.CalendarSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
            return CalendarSubscriptionServiceResult.NotFoundResult();

        // Delete associated events first (EF Core handles this fine)
        var events = await _context.Events
            .Where(e => e.CalendarSubscriptionId == id)
            .ToListAsync();

        if (events.Any())
        {
            _context.Events.RemoveRange(events);
            await _context.SaveChangesAsync();
        }

        // Now delete the subscription
        _context.CalendarSubscriptions.Remove(subscription);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted subscription {SubscriptionId} and {EventCount} associated events",
            id, events.Count);

        return CalendarSubscriptionServiceResult.Ok();
    }

    public async Task<CalendarSubscriptionServiceResult<DateTime?>> SyncSubscriptionAsync(int userId, int id)
    {
        var subscription = await _context.CalendarSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
            return CalendarSubscriptionServiceResult<DateTime?>.NotFoundResult();

        if (!subscription.IsActive)
            return CalendarSubscriptionServiceResult<DateTime?>.BadRequestResult("Cannot sync inactive subscription");

        _logger.LogInformation("Manual sync requested for subscription {SubscriptionId}", id);
        var (success, error) = await _iCalSyncService.SyncSubscriptionAsync(subscription);

        if (!success)
            return CalendarSubscriptionServiceResult<DateTime?>.SyncFailedResult(error ?? "Sync failed");

        return CalendarSubscriptionServiceResult<DateTime?>.Ok(subscription.LastSyncedAt);
    }

    public async Task<CalendarSubscriptionServiceResult<bool>> ToggleSubscriptionAsync(int userId, int id)
    {
        var subscription = await _context.CalendarSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
            return CalendarSubscriptionServiceResult<bool>.NotFoundResult();

        subscription.IsActive = !subscription.IsActive;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return CalendarSubscriptionServiceResult<bool>.Ok(subscription.IsActive);
    }
}

public enum CalendarSubscriptionServiceStatus
{
    Success,
    NotFound,
    BadRequest,
    SyncFailed
}

/// <summary>
/// Non-generic result for service operations that don't return data (update/delete/etc.)
/// </summary>
public class CalendarSubscriptionServiceResult
{
    public CalendarSubscriptionServiceStatus Status { get; init; }
    public string? ErrorMessage { get; init; }

    public static CalendarSubscriptionServiceResult Ok() => new() { Status = CalendarSubscriptionServiceStatus.Success };
    public static CalendarSubscriptionServiceResult NotFoundResult() => new() { Status = CalendarSubscriptionServiceStatus.NotFound };
    public static CalendarSubscriptionServiceResult BadRequestResult(string message) => new() { Status = CalendarSubscriptionServiceStatus.BadRequest, ErrorMessage = message };
}

/// <summary>
/// Generic result for service operations that return data on success (e.g. create/sync/toggle).
/// </summary>
public class CalendarSubscriptionServiceResult<T>
{
    public CalendarSubscriptionServiceStatus Status { get; init; }
    public T? Value { get; init; }
    public string? ErrorMessage { get; init; }

    public static CalendarSubscriptionServiceResult<T> Ok(T value) => new() { Status = CalendarSubscriptionServiceStatus.Success, Value = value };
    public static CalendarSubscriptionServiceResult<T> NotFoundResult() => new() { Status = CalendarSubscriptionServiceStatus.NotFound };
    public static CalendarSubscriptionServiceResult<T> BadRequestResult(string message) => new() { Status = CalendarSubscriptionServiceStatus.BadRequest, ErrorMessage = message };
    public static CalendarSubscriptionServiceResult<T> SyncFailedResult(string message) => new() { Status = CalendarSubscriptionServiceStatus.SyncFailed, ErrorMessage = message };
}
