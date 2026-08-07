using AgendaApi.DTOs;
using AgendaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgendaApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserPreferencesController : ControllerBase
{
    private readonly UserPreferencesService _userPreferencesService;

    public UserPreferencesController(UserPreferencesService userPreferencesService)
    {
        _userPreferencesService = userPreferencesService;
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
        var result = await _userPreferencesService.GetPreferencesAsync(userId);

        if (result.Status == UserPreferencesServiceStatus.NotFound)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(result.Value);
    }

    [HttpPut]
    public async Task<ActionResult<UserPreferencesDto>> UpdatePreferences([FromBody] UpdateUserPreferencesRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _userPreferencesService.UpdatePreferencesAsync(userId, request);

        if (result.Status == UserPreferencesServiceStatus.NotFound)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(result.Value);
    }
}
