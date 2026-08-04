using AgendaApi.Models;

namespace AgendaApi.DTOs;

public class EventOccurrenceDto
{
    public int EventId { get; set; }
    public DateTime OccurrenceStart { get; set; }
    public DateTime? OccurrenceEnd { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public bool IsRecurring { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public bool IsOwnEvent { get; set; }
    public SharePermission? Permission { get; set; }
}
