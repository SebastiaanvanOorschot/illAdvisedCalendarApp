using AgendaApi.Controllers;
using AgendaApi.Data;
using AgendaApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AgendaApi.Services;

public class AuthService
{
    private readonly AgendaDbContext _context;
    private readonly JwtService _jwtService;
    private readonly GoogleAuthService _googleAuthService;
    private readonly IConfiguration _configuration;

    public AuthService(
        AgendaDbContext context,
        JwtService jwtService,
        GoogleAuthService googleAuthService,
        IConfiguration configuration)
    {
        _context = context;
        _jwtService = jwtService;
        _googleAuthService = googleAuthService;
        _configuration = configuration;
    }

    public async Task<AuthServiceResult<AuthResponse>> GoogleLoginAsync(GoogleLoginRequest request)
    {
        // Validate the Google ID token
        var googleTokenInfo = await _googleAuthService.ValidateGoogleTokenAsync(request.GoogleIdToken);

        if (googleTokenInfo == null || !googleTokenInfo.Email_Verified)
        {
            return AuthServiceResult<AuthResponse>.UnauthorizedResult("Invalid Google token");
        }

        // Verify the token is for our application
        var expectedClientId = _configuration["Google:ClientId"];
        if (googleTokenInfo.Aud != expectedClientId)
        {
            return AuthServiceResult<AuthResponse>.UnauthorizedResult("Token not issued for this application");
        }

        // Find or create user
        var user = await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == googleTokenInfo.Sub);

        if (user == null)
        {
            // Create new user
            user = new User
            {
                GoogleId = googleTokenInfo.Sub,
                Email = googleTokenInfo.Email,
                Name = googleTokenInfo.Name,
                ProfilePictureUrl = googleTokenInfo.Picture,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        else
        {
            // Update last login time
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        // Generate tokens
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // Save refresh token
        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30), // 30 days
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        return AuthServiceResult<AuthResponse>.Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                ProfilePictureUrl = user.ProfilePictureUrl
            }
        });
    }

    public async Task<AuthServiceResult<AuthResponse>> RefreshAsync(RefreshRequest request)
    {
        var refreshToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (refreshToken == null || !refreshToken.IsActive)
        {
            return AuthServiceResult<AuthResponse>.UnauthorizedResult("Invalid or expired refresh token");
        }

        var user = refreshToken.User;

        // Generate new tokens
        var newAccessToken = _jwtService.GenerateAccessToken(user);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        // Revoke old refresh token
        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = DateTime.UtcNow;

        // Save new refresh token
        var newRefreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            IsRevoked = false
        };

        _context.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync();

        return AuthServiceResult<AuthResponse>.Ok(new AuthResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                ProfilePictureUrl = user.ProfilePictureUrl
            }
        });
    }

    public async Task<UserDto?> GetCurrentUserAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            return null;
        }

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            ProfilePictureUrl = user.ProfilePictureUrl
        };
    }

    public async Task LogoutAsync(LogoutRequest request)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (refreshToken != null)
        {
            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}

public enum AuthServiceStatus
{
    Success,
    Unauthorized
}

/// <summary>
/// Generic result for service operations that return data on success (e.g. login/refresh).
/// </summary>
public class AuthServiceResult<T>
{
    public AuthServiceStatus Status { get; init; }
    public T? Value { get; init; }
    public string? ErrorMessage { get; init; }

    public static AuthServiceResult<T> Ok(T value) => new() { Status = AuthServiceStatus.Success, Value = value };
    public static AuthServiceResult<T> UnauthorizedResult(string message) => new() { Status = AuthServiceStatus.Unauthorized, ErrorMessage = message };
}
