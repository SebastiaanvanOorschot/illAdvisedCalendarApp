using System.Text.Json.Serialization;

namespace AgendaApi.Models;

public class CalendarShare
{
    public int Id { get; set; }

    // The user who owns the calendar
    public int OwnerUserId { get; set; }

    // The user who has access to the calendar
    public int SharedWithUserId { get; set; }

    // Permission level granted
    public SharePermission Permission { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public User? OwnerUser { get; set; }
    public User? SharedWithUser { get; set; }
}
