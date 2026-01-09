namespace AgendaApi.Models;

public class MonthImage
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int Month { get; set; } // 1-12
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public DateTime UploadedAt { get; set; }

    public User User { get; set; } = null!;
}
