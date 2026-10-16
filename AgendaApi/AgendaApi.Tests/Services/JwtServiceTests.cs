using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AgendaApi.Models;
using AgendaApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AgendaApi.Tests.Services;

public class JwtServiceTests
{
    public const string SecretKey = "test-secret-key-that-is-long-enough-for-hs256!";
    public const string Issuer = "agenda-api-tests";
    public const string Audience = "agenda-frontend-tests";

    private readonly JwtService _service = new(Config());

    [Fact]
    public void GeneratedAccessToken_ValidatesAndCarriesTheUserClaims()
    {
        var token = _service.GenerateAccessToken(User());

        var principal = _service.ValidateToken(token);

        Assert.NotNull(principal);
        Assert.Equal("7", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("ada@example.test", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("google-ada", principal.FindFirstValue("google_id"));
    }

    [Fact]
    public void GeneratedAccessToken_ExpiresAfterOneHour()
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(_service.GenerateAccessToken(User()));

        Assert.InRange(token.ValidTo, DateTime.UtcNow.AddMinutes(59), DateTime.UtcNow.AddMinutes(61));
    }

    [Fact]
    public void EveryAccessToken_HasAUniqueId()
    {
        var first = new JwtSecurityTokenHandler().ReadJwtToken(_service.GenerateAccessToken(User()));
        var second = new JwtSecurityTokenHandler().ReadJwtToken(_service.GenerateAccessToken(User()));

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Token_SignedWithAnotherKey_IsRejected()
    {
        var otherService = new JwtService(Config(secretKey: "a-completely-different-secret-key-for-hs256!!"));

        Assert.Null(_service.ValidateToken(otherService.GenerateAccessToken(User())));
    }

    [Theory]
    [InlineData("someone-else", Audience)]
    [InlineData(Issuer, "another-frontend")]
    public void Token_ForAnotherIssuerOrAudience_IsRejected(string issuer, string audience)
    {
        var otherService = new JwtService(Config(issuer: issuer, audience: audience));

        Assert.Null(_service.ValidateToken(otherService.GenerateAccessToken(User())));
    }

    [Fact]
    public void ExpiredToken_IsRejected_WithoutClockSkew()
    {
        var token = Handcrafted(expires: DateTime.UtcNow.AddSeconds(-1));

        Assert.Null(_service.ValidateToken(token));
    }

    [Fact]
    public void TamperedPayload_IsRejected()
    {
        var parts = _service.GenerateAccessToken(User()).Split('.');
        var payload = Base64UrlEncoder.Decode(parts[1]).Replace("\"7\"", "\"1\"");
        var tampered = $"{parts[0]}.{Base64UrlEncoder.Encode(payload)}.{parts[2]}";

        Assert.Null(_service.ValidateToken(tampered));
    }

    [Fact]
    public void Garbage_IsRejected()
    {
        Assert.Null(_service.ValidateToken("not.a.jwt"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingSecretKey_FailsAtConstruction(string? secretKey)
    {
        Assert.Throws<InvalidOperationException>(() => new JwtService(Config(secretKey: secretKey)));
    }

    [Fact]
    public void RefreshTokens_Are64RandomBytes_AndUnique()
    {
        var tokens = Enumerable.Range(0, 50).Select(_ => _service.GenerateRefreshToken()).ToList();

        Assert.All(tokens, t => Assert.Equal(64, Convert.FromBase64String(t).Length));
        Assert.Equal(tokens.Count, tokens.Distinct().Count());
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    public static IConfiguration Config(string? secretKey = SecretKey, string issuer = Issuer, string audience = Audience) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = secretKey,
                ["Jwt:Issuer"] = issuer,
                ["Jwt:Audience"] = audience,
                ["Google:ClientId"] = "client-id.apps.googleusercontent.com"
            })
            .Build();

    private static User User() => new()
    {
        Id = 7,
        GoogleId = "google-ada",
        Email = "ada@example.test",
        Name = "Ada"
    };

    private static string Handcrafted(DateTime expires)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(Issuer, Audience, new[] { new Claim("sub", "7") },
            notBefore: expires.AddHours(-1), expires: expires, signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
