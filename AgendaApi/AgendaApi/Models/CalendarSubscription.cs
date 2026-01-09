using System.Text.Json.Serialization;

namespace AgendaApi.Models;

public class CalendarSubscription
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string ICalUrl { get; set; }
    public string? Color { get; set; } // Hex color code (e.g., "#FF69B4")
    public int SyncIntervalMinutes { get; set; } = 60; // Default: hourly
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // User ownership
    public int UserId { get; set; }

    [JsonIgnore]
    public User? User { get; set; }
}
