using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgendaApi.Services;

public class GoogleAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleAuthService> _logger;
    private const string GoogleTokenInfoUrl = "https://oauth2.googleapis.com/tokeninfo";

    public GoogleAuthService(HttpClient httpClient, ILogger<GoogleAuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<GoogleTokenInfo?> ValidateGoogleTokenAsync(string googleIdToken)
    {
        try
        {
            _logger.LogInformation("Validating Google token...");
            var response = await _httpClient.GetAsync($"{GoogleTokenInfoUrl}?id_token={googleIdToken}");

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"Google API Response Status: {response.StatusCode}");
            _logger.LogInformation($"Google API Response: {content}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Google token validation failed with status {response.StatusCode}");
                return null;
            }

            var tokenInfo = JsonSerializer.Deserialize<GoogleTokenInfo>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            _logger.LogInformation($"Token validated successfully for email: {tokenInfo?.Email}");
            return tokenInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Google token");
            return null;
        }
    }
}

public class GoogleTokenInfo
{
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = string.Empty; // Google user ID

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("picture")]
    public string Picture { get; set; } = string.Empty;

    [JsonPropertyName("email_verified")]
    public string EmailVerifiedRaw { get; set; } = string.Empty;

    [JsonIgnore]
    public bool Email_Verified => EmailVerifiedRaw == "true" || EmailVerifiedRaw == "True";

    [JsonPropertyName("aud")]
    public string Aud { get; set; } = string.Empty; // Client ID
}
