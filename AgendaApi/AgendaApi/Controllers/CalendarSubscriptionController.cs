using AgendaApi.DTOs;
using AgendaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgendaApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CalendarSubscriptionController : ControllerBase
{
    private readonly CalendarSubscriptionService _subscriptionService;

    public CalendarSubscriptionController(CalendarSubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    private int GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    // GET: api/CalendarSubscription
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CalendarSubscriptionDto>>> GetSubscriptions()
    {
        var userId = GetUserId();
        var subscriptions = await _subscriptionService.GetSubscriptionsAsync(userId);
        return Ok(subscriptions);
    }

    // GET: api/CalendarSubscription/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<CalendarSubscriptionDto>> GetSubscription(int id)
    {
        var userId = GetUserId();
        var subscription = await _subscriptionService.GetSubscriptionAsync(userId, id);

        if (subscription == null)
        {
            return NotFound();
        }

        return Ok(subscription);
    }

    // POST: api/CalendarSubscription
    [HttpPost]
    public async Task<ActionResult<CalendarSubscriptionDto>> CreateSubscription([FromBody] CreateCalendarSubscriptionDto dto)
    {
        var userId = GetUserId();
        var result = await _subscriptionService.CreateSubscriptionAsync(userId, dto);

        return result.Status switch
        {
            CalendarSubscriptionServiceStatus.Success => CreatedAtAction(nameof(GetSubscription), new { id = result.Value!.Id }, result.Value),
            CalendarSubscriptionServiceStatus.BadRequest => BadRequest(new { error = result.ErrorMessage }),
            _ => BadRequest(new { error = result.ErrorMessage })
        };
    }

    // PUT: api/CalendarSubscription/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSubscription(int id, [FromBody] UpdateCalendarSubscriptionDto dto)
    {
        var userId = GetUserId();
        var result = await _subscriptionService.UpdateSubscriptionAsync(userId, id, dto);

        return result.Status switch
        {
            CalendarSubscriptionServiceStatus.Success => NoContent(),
            CalendarSubscriptionServiceStatus.NotFound => NotFound(),
            CalendarSubscriptionServiceStatus.BadRequest => BadRequest(new { error = result.ErrorMessage }),
            _ => BadRequest(new { error = result.ErrorMessage })
        };
    }

    // DELETE: api/CalendarSubscription/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSubscription(int id)
    {
        var userId = GetUserId();
        var result = await _subscriptionService.DeleteSubscriptionAsync(userId, id);

        return result.Status switch
        {
            CalendarSubscriptionServiceStatus.Success => NoContent(),
            CalendarSubscriptionServiceStatus.NotFound => NotFound(),
            _ => BadRequest(new { error = result.ErrorMessage })
        };
    }

    // POST: api/CalendarSubscription/{id}/sync
    [HttpPost("{id}/sync")]
    public async Task<IActionResult> SyncSubscription(int id)
    {
        var userId = GetUserId();
        var result = await _subscriptionService.SyncSubscriptionAsync(userId, id);

        return result.Status switch
        {
            CalendarSubscriptionServiceStatus.Success => Ok(new { message = "Sync completed successfully", lastSyncedAt = result.Value }),
            CalendarSubscriptionServiceStatus.NotFound => NotFound(),
            CalendarSubscriptionServiceStatus.BadRequest => BadRequest(new { error = result.ErrorMessage }),
            CalendarSubscriptionServiceStatus.SyncFailed => StatusCode(500, new { error = result.ErrorMessage }),
            _ => BadRequest(new { error = result.ErrorMessage })
        };
    }

    // PUT: api/CalendarSubscription/{id}/toggle
    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> ToggleSubscription(int id)
    {
        var userId = GetUserId();
        var result = await _subscriptionService.ToggleSubscriptionAsync(userId, id);

        return result.Status switch
        {
            CalendarSubscriptionServiceStatus.Success => Ok(new { message = $"Subscription {(result.Value ? "activated" : "deactivated")}", isActive = result.Value }),
            CalendarSubscriptionServiceStatus.NotFound => NotFound(),
            _ => BadRequest(new { error = result.ErrorMessage })
        };
    }
}
