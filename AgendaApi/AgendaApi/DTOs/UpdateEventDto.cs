namespace AgendaApi.DTOs;

public class UpdateEventDto
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
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
}
