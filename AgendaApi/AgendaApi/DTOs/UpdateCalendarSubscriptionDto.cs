namespace AgendaApi.DTOs;

public class UpdateCalendarSubscriptionDto
{
    public required string Name { get; set; }
    public required string ICalUrl { get; set; }
    public string? Color { get; set; }
    public int? SyncIntervalMinutes { get; set; }
}
