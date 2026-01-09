using AgendaApi.Data;
using AgendaApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AgendaApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserPreferencesController : ControllerBase
{
    private readonly AgendaDbContext _context;

    public UserPreferencesController(AgendaDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim!);
    }

    [HttpGet]
    public async Task<ActionResult<UserPreferencesDto>> GetPreferences()
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(new UserPreferencesDto
        {
            ShowEventTitleInMonthView = user.ShowEventTitleInMonthView
        });
    }

    [HttpPut]
    public async Task<ActionResult<UserPreferencesDto>> UpdatePreferences([FromBody] UpdateUserPreferencesRequest request)
    {
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Update preferences
        if (request.ShowEventTitleInMonthView.HasValue)
        {
            user.ShowEventTitleInMonthView = request.ShowEventTitleInMonthView.Value;
        }

        await _context.SaveChangesAsync();

        return Ok(new UserPreferencesDto
        {
            ShowEventTitleInMonthView = user.ShowEventTitleInMonthView
        });
    }
}

// DTOs
public class UserPreferencesDto
{
    public bool ShowEventTitleInMonthView { get; set; }
}

public class UpdateUserPreferencesRequest
{
    public bool? ShowEventTitleInMonthView { get; set; }
}
