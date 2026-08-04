namespace AgendaApi.DTOs;

public class EventDto
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsAllDay { get; set; }
    public string? Color { get; set; }
    public bool IsRecurring { get; set; }
    public string? RecurrencePattern { get; set; }
    public int? RecurrenceInterval { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public string? RecurrenceRule { get; set; }
    public string? ExceptionDates { get; set; }
    public DateTime? RecurrenceId { get; set; }
    public int? ParentEventId { get; set; }
    public bool IsImportedFromGoogle { get; set; }
    public bool IsLocallyModified { get; set; }
    public bool IsFromSubscription { get; set; }
    public bool IsReadOnly { get; set; }
    public int? CalendarSubscriptionId { get; set; }
    public int UserId { get; set; }
}
