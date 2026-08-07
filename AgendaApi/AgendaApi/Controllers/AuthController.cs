using AgendaApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgendaApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("google-login")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        var result = await _authService.GoogleLoginAsync(request);

        if (result.Status == AuthServiceStatus.Unauthorized)
        {
            return Unauthorized(new { message = result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request)
    {
        var result = await _authService.RefreshAsync(request);

        if (result.Status == AuthServiceStatus.Unauthorized)
        {
            return Unauthorized(new { message = result.ErrorMessage });
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
        {
            return Unauthorized();
        }

        var userDto = await _authService.GetCurrentUserAsync(userId);

        if (userDto == null)
        {
            return NotFound();
        }

        return Ok(userDto);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await _authService.LogoutAsync(request);

        return Ok(new { message = "Logged out successfully" });
    }
}

// DTOs
public class GoogleLoginRequest
{
    public required string GoogleIdToken { get; set; }
}

public class RefreshRequest
{
    public required string RefreshToken { get; set; }
}

public class LogoutRequest
{
    public required string RefreshToken { get; set; }
}

public class AuthResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public required UserDto User { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string Name { get; set; }
    public string? ProfilePictureUrl { get; set; }
}
