using System.Text.Json.Serialization;

namespace AgendaApi.Models;

public class CalendarShareInvite
{
    public int Id { get; set; }

    // The user who sent the invite
    public int SenderUserId { get; set; }

    // Email of the person being invited (may or may not be a registered user yet)
    public required string RecipientEmail { get; set; }

    // The user who received the invite (nullable if they haven't registered yet)
    public int? RecipientUserId { get; set; }

    // Permission level being offered
    public SharePermission Permission { get; set; }

    // Status of the invite
    public InviteStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    // Navigation properties
    [JsonIgnore]
    public User? SenderUser { get; set; }

    [JsonIgnore]
    public User? RecipientUser { get; set; }
}

public enum InviteStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Cancelled = 3
}
