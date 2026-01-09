using System.Text.Json.Serialization;

namespace AgendaApi.Models;

public class User
{
    public int Id { get; set; }
    public required string GoogleId { get; set; }
    public required string Email { get; set; }
    public required string Name { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }

    // Google Calendar integration
    [JsonIgnore]
    public string? GoogleAccessToken { get; set; }
    [JsonIgnore]
    public string? GoogleRefreshToken { get; set; }
    [JsonIgnore]
    public DateTime? GoogleTokenExpiry { get; set; }
    public DateTime? LastCalendarSync { get; set; }

    // User Preferences
    public bool ShowEventTitleInMonthView { get; set; } = false; // Default: show time

    // Navigation properties
    [JsonIgnore]
    public ICollection<Event> Events { get; set; } = new List<Event>();

    [JsonIgnore]
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
