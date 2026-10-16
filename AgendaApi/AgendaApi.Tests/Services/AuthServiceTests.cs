using System.Net;
using System.Text.Json;
using AgendaApi.Controllers;
using AgendaApi.Data;
using AgendaApi.Models;
using AgendaApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgendaApi.Tests.Services;

public sealed class AuthServiceTests : IDisposable
{
    private const string ClientId = "client-id.apps.googleusercontent.com";

    private readonly TestDatabase _database = new();
    private readonly List<AgendaDbContext> _contexts = new();
    private FakeHttpHandler _google = GoogleSays(sub: "google-ada", email: "ada@example.test");

    public void Dispose()
    {
        _contexts.ForEach(c => c.Dispose());
        _database.Dispose();
    }

    // ── Google login ────────────────────────────────────────────────────────

    [Fact]
    public async Task GoogleLogin_NewUser_CreatesTheUserAndIssuesTokens()
    {
        var result = await NewService().GoogleLoginAsync(new GoogleLoginRequest { GoogleIdToken = "id-token" });

        Assert.Equal(AuthServiceStatus.Success, result.Status);
        Assert.Equal("ada@example.test", result.Value!.User.Email);
        Assert.NotEmpty(result.Value.AccessToken);
        var user = Assert.Single(Users());
        Assert.Equal("google-ada", user.GoogleId);
        var refresh = Assert.Single(RefreshTokens());
        Assert.Equal(result.Value.RefreshToken, refresh.Token);
        Assert.InRange(refresh.ExpiresAt, DateTime.UtcNow.AddDays(29.9), DateTime.UtcNow.AddDays(30.1));
        Assert.Contains("id_token=id-token", _google.RequestedUris.Single().Query);
    }

    [Fact]
    public async Task GoogleLogin_ExistingUser_IsReusedAndLastLoginUpdated()
    {
        var existing = _database.AddUser("Ada", "ada@example.test");
        using (var context = _database.CreateContext())
        {
            var stored = context.Users.Single();
            stored.GoogleId = "google-ada";
            stored.LastLoginAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            context.SaveChanges();
        }

        var result = await NewService().GoogleLoginAsync(new GoogleLoginRequest { GoogleIdToken = "id-token" });

        Assert.Equal(existing.Id, result.Value!.User.Id);
        var user = Assert.Single(Users());
        Assert.True(user.LastLoginAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task GoogleLogin_WithUnverifiedEmail_IsRejected()
    {
        _google = GoogleSays(sub: "google-ada", email: "ada@example.test", emailVerified: "false");

        var result = await NewService().GoogleLoginAsync(new GoogleLoginRequest { GoogleIdToken = "id-token" });

        Assert.Equal(AuthServiceStatus.Unauthorized, result.Status);
        Assert.Empty(Users());
    }

    [Fact]
    public async Task GoogleLogin_ForAnotherApplicationsClientId_IsRejected()
    {
        _google = GoogleSays(sub: "google-ada", email: "ada@example.test", aud: "someone-elses-app.apps.googleusercontent.com");

        var result = await NewService().GoogleLoginAsync(new GoogleLoginRequest { GoogleIdToken = "id-token" });

        Assert.Equal(AuthServiceStatus.Unauthorized, result.Status);
        Assert.Empty(Users());
    }

    [Fact]
    public async Task GoogleLogin_WhenGoogleRejectsTheToken_IsRejected()
    {
        _google = FakeHttpHandler.Returning(HttpStatusCode.BadRequest, "{\"error\":\"invalid_token\"}", "application/json");

        var result = await NewService().GoogleLoginAsync(new GoogleLoginRequest { GoogleIdToken = "forged" });

        Assert.Equal(AuthServiceStatus.Unauthorized, result.Status);
        Assert.Empty(RefreshTokens());
    }

    // ── refresh ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_RotatesTheToken_AndRevokesTheOldOne()
    {
        var login = await LoginAsync();

        var refreshed = await NewService().RefreshAsync(new RefreshRequest { RefreshToken = login.RefreshToken });

        Assert.Equal(AuthServiceStatus.Success, refreshed.Status);
        Assert.NotEqual(login.RefreshToken, refreshed.Value!.RefreshToken);
        var old = RefreshTokens().Single(t => t.Token == login.RefreshToken);
        Assert.True(old.IsRevoked);
        Assert.NotNull(old.RevokedAt);
        Assert.False(RefreshTokens().Single(t => t.Token == refreshed.Value.RefreshToken).IsRevoked);
    }

    [Fact]
    public async Task Refresh_WithAnAlreadyUsedToken_IsRejected()
    {
        var login = await LoginAsync();
        await NewService().RefreshAsync(new RefreshRequest { RefreshToken = login.RefreshToken });

        var replay = await NewService().RefreshAsync(new RefreshRequest { RefreshToken = login.RefreshToken });

        Assert.Equal(AuthServiceStatus.Unauthorized, replay.Status);
    }

    [Fact]
    public async Task Refresh_WithAnExpiredToken_IsRejected()
    {
        var login = await LoginAsync();
        using (var context = _database.CreateContext())
        {
            context.RefreshTokens.Single().ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            context.SaveChanges();
        }

        var result = await NewService().RefreshAsync(new RefreshRequest { RefreshToken = login.RefreshToken });

        Assert.Equal(AuthServiceStatus.Unauthorized, result.Status);
    }

    [Fact]
    public async Task Refresh_WithAnUnknownToken_IsRejected()
    {
        var result = await NewService().RefreshAsync(new RefreshRequest { RefreshToken = "made-up" });

        Assert.Equal(AuthServiceStatus.Unauthorized, result.Status);
    }

    // ── logout and current user ─────────────────────────────────────────────

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        var login = await LoginAsync();

        await NewService().LogoutAsync(new LogoutRequest { RefreshToken = login.RefreshToken });

        Assert.True(RefreshTokens().Single().IsRevoked);
        var afterLogout = await NewService().RefreshAsync(new RefreshRequest { RefreshToken = login.RefreshToken });
        Assert.Equal(AuthServiceStatus.Unauthorized, afterLogout.Status);
    }

    [Fact]
    public async Task Logout_WithAnUnknownToken_DoesNothing()
    {
        await NewService().LogoutAsync(new LogoutRequest { RefreshToken = "made-up" });
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsTheUser_OrNullWhenUnknown()
    {
        var ada = _database.AddUser("Ada");

        Assert.Equal("Ada", (await NewService().GetCurrentUserAsync(ada.Id))?.Name);
        Assert.Null(await NewService().GetCurrentUserAsync(9999));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private AuthService NewService()
    {
        var context = _database.CreateContext();
        _contexts.Add(context);
        var google = new GoogleAuthService(new HttpClient(_google), NullLogger<GoogleAuthService>.Instance);
        var config = JwtServiceTests.Config();
        return new AuthService(context, new JwtService(config), google, config);
    }

    private async Task<AuthResponse> LoginAsync() =>
        (await NewService().GoogleLoginAsync(new GoogleLoginRequest { GoogleIdToken = "id-token" })).Value!;

    private static FakeHttpHandler GoogleSays(string sub, string email, string emailVerified = "true", string aud = ClientId) =>
        FakeHttpHandler.Returning(HttpStatusCode.OK, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["sub"] = sub,
            ["email"] = email,
            ["name"] = "Ada",
            ["picture"] = "https://example.test/ada.png",
            ["email_verified"] = emailVerified,
            ["aud"] = aud
        }), "application/json");

    private List<User> Users()
    {
        using var context = _database.CreateContext();
        return context.Users.AsNoTracking().ToList();
    }

    private List<RefreshToken> RefreshTokens()
    {
        using var context = _database.CreateContext();
        return context.RefreshTokens.AsNoTracking().ToList();
    }
}
