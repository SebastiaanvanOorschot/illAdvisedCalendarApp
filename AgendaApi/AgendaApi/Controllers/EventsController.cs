using AgendaApi.DTOs;
using AgendaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgendaApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly EventService _eventService;

    public EventsController(EventService eventService)
    {
        _eventService = eventService;
    }

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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<EventWithOwnerDto>>> GetEvents()
    {
        var userId = GetCurrentUserId();
        var events = await _eventService.GetEventsWithOwnerAsync(userId);
        return Ok(events);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<EventWithOwnerDto>> GetEvent(int id)
    {
        var userId = GetCurrentUserId();
        var eventWithOwner = await _eventService.GetEventWithOwnerAsync(userId, id);

        if (eventWithOwner == null)
        {
            return NotFound();
        }

        return Ok(eventWithOwner);
    }

    [HttpGet("date/{date}")]
    public async Task<ActionResult<IEnumerable<EventDto>>> GetEventsByDate(DateTime date)
    {
        var userId = GetCurrentUserId();
        var events = await _eventService.GetEventsByDateAsync(userId, date);
        return Ok(events);
    }

    [HttpGet("range")]
    public async Task<ActionResult<IEnumerable<EventWithOwnerDto>>> GetEventsByDateRange([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var userId = GetCurrentUserId();
        var events = await _eventService.GetEventsByDateRangeAsync(userId, startDate, endDate);
        return Ok(events);
    }

    [HttpGet("occurrences")]
    public async Task<ActionResult<IEnumerable<EventOccurrenceDto>>> GetEventOccurrences([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        var userId = GetCurrentUserId();
        var occurrences = await _eventService.GetOccurrencesAsync(userId, startDate, endDate);
        return Ok(occurrences);
    }

    [HttpPost]
    public async Task<ActionResult<EventDto>> CreateEvent(CreateEventDto dto)
    {
        var userId = GetCurrentUserId();
        var created = await _eventService.CreateEventAsync(userId, dto);

        // Return 200 OK instead of 201 Created for compatibility with NSwag client
        return Ok(created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEvent(int id, UpdateEventDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _eventService.UpdateEventAsync(userId, id, dto);

        return result.Status switch
        {
            EventServiceStatus.Success => NoContent(),
            EventServiceStatus.NotFound => NotFound(),
            EventServiceStatus.Forbidden => Forbid(result.ErrorMessage ?? string.Empty),
            EventServiceStatus.BadRequest => BadRequest(result.ErrorMessage),
            _ => BadRequest(result.ErrorMessage)
        };
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var userId = GetCurrentUserId();
        var result = await _eventService.DeleteEventAsync(userId, id);

        return result.Status switch
        {
            EventServiceStatus.Success => NoContent(),
            EventServiceStatus.NotFound => NotFound(),
            EventServiceStatus.Forbidden => Forbid(result.ErrorMessage ?? string.Empty),
            _ => BadRequest(result.ErrorMessage)
        };
    }

    [HttpPost("{id}/add-exception")]
    public async Task<IActionResult> AddExceptionDate(int id, [FromBody] DateTime exceptionDate)
    {
        var userId = GetCurrentUserId();
        var result = await _eventService.AddExceptionDateAsync(userId, id, exceptionDate);

        return result.Status switch
        {
            EventServiceStatus.Success => NoContent(),
            EventServiceStatus.NotFound => NotFound(),
            _ => BadRequest(result.ErrorMessage)
        };
    }

    /// <summary>
    /// Edit a single occurrence of a recurring event.
    /// Creates a new event for the modified occurrence and adds an exception to the parent series.
    /// </summary>
    [HttpPost("{id}/edit-occurrence")]
    public async Task<ActionResult<EventDto>> EditOccurrence(int id, [FromBody] EditOccurrenceDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _eventService.EditOccurrenceAsync(userId, id, dto);

        return result.Status switch
        {
            EventServiceStatus.Success => Ok(result.Value),
            EventServiceStatus.NotFound => NotFound(),
            EventServiceStatus.BadRequest => BadRequest(result.ErrorMessage),
            EventServiceStatus.Forbidden => Forbid(result.ErrorMessage ?? string.Empty),
            _ => BadRequest(result.ErrorMessage)
        };
    }
}
