using AgendaApi.Models;

namespace AgendaApi.DTOs;

public class EventWithOwnerDto : EventDto
{
    public string? OwnerName { get; set; }
    public string? OwnerEmail { get; set; }
    public bool IsOwnEvent { get; set; }
    public SharePermission? Permission { get; set; }
    public string? SubscriptionName { get; set; }
}
