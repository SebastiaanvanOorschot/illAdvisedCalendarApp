namespace AgendaApi.DTOs;

public class EditOccurrenceDto
{
    public DateTime OriginalOccurrenceDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime NewStartDateTime { get; set; }
    public DateTime NewEndDateTime { get; set; }
    public string? Color { get; set; }
}
