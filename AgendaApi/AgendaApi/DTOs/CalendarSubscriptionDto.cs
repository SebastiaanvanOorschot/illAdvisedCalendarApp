namespace AgendaApi.DTOs;

public class CalendarSubscriptionDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string ICalUrl { get; set; }
    public string? Color { get; set; }
    public int SyncIntervalMinutes { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int UserId { get; set; }
}
