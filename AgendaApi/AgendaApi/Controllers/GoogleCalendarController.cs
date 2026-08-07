using AgendaApi.DTOs;
using AgendaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgendaApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class GoogleCalendarController : ControllerBase
{
    private readonly IGoogleCalendarService _googleCalendarService;
    private readonly GoogleCalendarConnectionService _connectionService;
    private readonly ILogger<GoogleCalendarController> _logger;

    public GoogleCalendarController(
        IGoogleCalendarService googleCalendarService,
        GoogleCalendarConnectionService connectionService,
        ILogger<GoogleCalendarController> logger)
    {
        _googleCalendarService = googleCalendarService;
        _connectionService = connectionService;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId;
    }

    /// <summary>
    /// Store Google Calendar access token for the user
    /// </summary>
    [HttpPost("connect")]
    public async Task<ActionResult> ConnectGoogleCalendar([FromBody] ConnectRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _connectionService.ConnectAsync(userId, request.AccessToken);

            if (result.Status == GoogleCalendarConnectionServiceStatus.NotFound)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new { message = "Successfully connected to Google Calendar" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing Google Calendar token");
            return StatusCode(500, new { message = "Failed to connect to Google Calendar" });
        }
    }

    /// <summary>
    /// Get all Google Calendars accessible to the user
    /// </summary>
    [HttpGet("calendars")]
    public async Task<ActionResult<List<GoogleCalendarInfo>>> GetCalendars()
    {
        try
        {
            var userId = GetCurrentUserId();
            var calendars = await _googleCalendarService.GetUserCalendarsAsync(userId);
            return Ok(calendars);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Google Calendars");
            return StatusCode(500, new { message = "Failed to retrieve calendars. Please ensure you've granted calendar access." });
        }
    }

    /// <summary>
    /// Import events from selected Google Calendars
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<ImportResult>> ImportCalendars([FromBody] ImportRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            if (request.Calendars == null || !request.Calendars.Any())
            {
                return BadRequest(new { message = "No calendars selected for import" });
            }

            var result = await _connectionService.ImportAsync(userId, request.Calendars);

            if (!result.Success)
            {
                return StatusCode(500, result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during calendar import");
            return StatusCode(500, new { message = "An error occurred during import. Please try again later." });
        }
    }
}
