using AgendaApi.Data;
using AgendaApi.Models;
using AgendaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgendaApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CalendarSubscriptionController : ControllerBase
{
    private readonly AgendaDbContext _context;
    private readonly ICalSyncService _iCalSyncService;
    private readonly ILogger<CalendarSubscriptionController> _logger;

    public CalendarSubscriptionController(
        AgendaDbContext context,
        ICalSyncService iCalSyncService,
        ILogger<CalendarSubscriptionController> logger)
    {
        _context = context;
        _iCalSyncService = iCalSyncService;
        _logger = logger;
    }

    private int GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    // GET: api/CalendarSubscription
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CalendarSubscription>>> GetSubscriptions()
    {
        var userId = GetUserId();
        var subscriptions = await _context.CalendarSubscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(subscriptions);
    }

    // GET: api/CalendarSubscription/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<CalendarSubscription>> GetSubscription(int id)
    {
        var userId = GetUserId();
        var subscription = await _context.CalendarSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
        {
            return NotFound();
        }

        return Ok(subscription);
    }

    // POST: api/CalendarSubscription
    [HttpPost]
    public async Task<ActionResult<CalendarSubscription>> CreateSubscription([FromBody] CalendarSubscriptionRequest request)
    {
        var userId = GetUserId();

        // Validate URL format
        if (!Uri.TryCreate(request.ICalUrl, UriKind.Absolute, out _))
        {
            return BadRequest(new { error = "Invalid iCal URL format" });
        }

        var subscription = new CalendarSubscription
        {
            Name = request.Name,
            ICalUrl = request.ICalUrl,
            Color = request.Color,
            SyncIntervalMinutes = request.SyncIntervalMinutes ?? 60,
            IsActive = true,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.CalendarSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        // Perform initial sync
        _logger.LogInformation("Performing initial sync for new subscription {SubscriptionId}", subscription.Id);
        var (success, error) = await _iCalSyncService.SyncSubscriptionAsync(subscription);

        if (!success)
        {
            _logger.LogWarning("Initial sync failed for subscription {SubscriptionId}: {Error}",
                subscription.Id, error);
            // Don't fail the creation, just log the error
        }

        return CreatedAtAction(nameof(GetSubscription), new { id = subscription.Id }, subscription);
    }

    // PUT: api/CalendarSubscription/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSubscription(int id, [FromBody] CalendarSubscriptionRequest request)
    {
        var userId = GetUserId();
        var subscription = await _context.CalendarSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
        {
            return NotFound();
        }

        // Validate URL format
        if (!Uri.TryCreate(request.ICalUrl, UriKind.Absolute, out _))
        {
            return BadRequest(new { error = "Invalid iCal URL format" });
        }

        subscription.Name = request.Name;
        subscription.ICalUrl = request.ICalUrl;
        subscription.Color = request.Color;
        subscription.SyncIntervalMinutes = request.SyncIntervalMinutes ?? subscription.SyncIntervalMinutes;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/CalendarSubscription/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSubscription(int id)
    {
        var userId = GetUserId();
        var subscription = await _context.CalendarSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
        {
            return NotFound();
        }

        // Delete associated events (cascade delete should handle this, but being explicit)
        var associatedEvents = await _context.Events
            .Where(e => e.CalendarSubscriptionId == id)
            .ToListAsync();

        _context.Events.RemoveRange(associatedEvents);
        _context.CalendarSubscriptions.Remove(subscription);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted subscription {SubscriptionId} and {EventCount} associated events",
            id, associatedEvents.Count);

        return NoContent();
    }

    // POST: api/CalendarSubscription/{id}/sync
    [HttpPost("{id}/sync")]
    public async Task<IActionResult> SyncSubscription(int id)
    {
        var userId = GetUserId();
        var subscription = await _context.CalendarSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
        {
            return NotFound();
        }

        if (!subscription.IsActive)
        {
            return BadRequest(new { error = "Cannot sync inactive subscription" });
        }

        _logger.LogInformation("Manual sync requested for subscription {SubscriptionId}", id);
        var (success, error) = await _iCalSyncService.SyncSubscriptionAsync(subscription);

        if (!success)
        {
            return StatusCode(500, new { error = error });
        }

        return Ok(new
        {
            message = "Sync completed successfully",
            lastSyncedAt = subscription.LastSyncedAt
        });
    }

    // PUT: api/CalendarSubscription/{id}/toggle
    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> ToggleSubscription(int id)
    {
        var userId = GetUserId();
        var subscription = await _context.CalendarSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
        {
            return NotFound();
        }

        subscription.IsActive = !subscription.IsActive;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"Subscription {(subscription.IsActive ? "activated" : "deactivated")}",
            isActive = subscription.IsActive
        });
    }
}

// DTOs
public class CalendarSubscriptionRequest
{
    public required string Name { get; set; }
    public required string ICalUrl { get; set; }
    public string? Color { get; set; }
    public int? SyncIntervalMinutes { get; set; }
}
