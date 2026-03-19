using System.Text.Json.Serialization;

namespace AgendaApi.Models;

public class Event
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Recurring event properties
    public bool IsRecurring { get; set; }
    public string? RecurrencePattern { get; set; } // daily, weekly, monthly, yearly
    public int? RecurrenceInterval { get; set; } // every X days/weeks/months/years
    public DateTime? RecurrenceEndDate { get; set; }
    public int? ParentEventId { get; set; } // Reference to the parent recurring event

    // RRULE support (RFC 5545 iCalendar standard)
    public string? RecurrenceRule { get; set; } // RRULE string for complex recurrence patterns

    // Exception handling for recurring events (RFC 5545)
    public string? ExceptionDates { get; set; } // Comma-separated ISO 8601 datetime strings for EXDATE
    public DateTime? RecurrenceId { get; set; } // For modified occurrences - references the original occurrence date

    // All-day event flag (no specific start/end time)
    public bool IsAllDay { get; set; }

    // Color for visual organization
    public string? Color { get; set; } // Hex color code (e.g., "#FF0000")

    // Google Calendar integration
    public string? GoogleCalendarId { get; set; } // Calendar ID from Google (e.g., "primary", "example@group.calendar.google.com")
    public string? GoogleEventId { get; set; } // Unique identifier from Google Calendar
    public bool IsImportedFromGoogle { get; set; } // Track if event was imported from Google
    public bool IsLocallyModified { get; set; } // Track if imported event was modified locally (prevents sync overwrites)

    // iCal subscription integration
    public int? CalendarSubscriptionId { get; set; } // Reference to CalendarSubscription if event is from external calendar
    public string? ExternalEventId { get; set; } // UID from iCal file
    public bool IsFromSubscription { get; set; } // Track if event was imported from subscription
    public bool IsReadOnly { get; set; } // Prevents editing of external events

    // User ownership
    public int UserId { get; set; }

    [JsonIgnore]
    public User? User { get; set; }
}
