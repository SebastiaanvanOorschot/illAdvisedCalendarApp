using AgendaApi.Data;
using AgendaApi.Models;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.EntityFrameworkCore;

namespace AgendaApi.Services;

public class ICalSyncService
{
    private readonly AgendaDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ICalSyncService> _logger;

    public ICalSyncService(
        AgendaDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<ICalSyncService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<(bool Success, string? Error)> SyncSubscriptionAsync(CalendarSubscription subscription)
    {
        try
        {
            _logger.LogInformation("Starting sync for subscription {SubscriptionId}: {Name}",
                subscription.Id, subscription.Name);

            // Fetch iCal data
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var response = await httpClient.GetAsync(subscription.ICalUrl);
            if (!response.IsSuccessStatusCode)
            {
                var error = $"HTTP {response.StatusCode}: Failed to fetch iCal feed";
                _logger.LogError("Failed to fetch iCal for subscription {SubscriptionId}: {Error}",
                    subscription.Id, error);
                return (false, error);
            }

            var icalContent = await response.Content.ReadAsStringAsync();

            // Parse iCal content
            var calendar = Calendar.Load(icalContent);
            if (calendar == null || calendar.Events == null)
            {
                var error = "Failed to parse iCal content or no events found";
                _logger.LogWarning("Invalid iCal content for subscription {SubscriptionId}", subscription.Id);
                return (false, error);
            }

            // Get existing events for this subscription
            var existingEvents = await _context.Events
                .Where(e => e.CalendarSubscriptionId == subscription.Id)
                .ToListAsync();

            var existingEventIds = existingEvents
                .Where(e => e.ExternalEventId != null)
                .ToDictionary(e => e.ExternalEventId!, e => e);

            var processedEventIds = new HashSet<string>();
            var eventsCreated = 0;
            var eventsUpdated = 0;

            // Process each event from the iCal feed
            foreach (var calEvent in calendar.Events)
            {
                if (string.IsNullOrEmpty(calEvent.Uid))
                {
                    _logger.LogWarning("Skipping event without UID in subscription {SubscriptionId}", subscription.Id);
                    continue;
                }

                processedEventIds.Add(calEvent.Uid);

                if (existingEventIds.TryGetValue(calEvent.Uid, out var existingEvent))
                {
                    // Update existing event if changed
                    if (UpdateEventFromICalEvent(existingEvent, calEvent, subscription))
                    {
                        existingEvent.UpdatedAt = DateTime.UtcNow;
                        eventsUpdated++;
                    }
                }
                else
                {
                    // Create new event
                    var newEvent = CreateEventFromICalEvent(calEvent, subscription);
                    if (newEvent != null)
                    {
                        _context.Events.Add(newEvent);
                        eventsCreated++;
                    }
                }
            }

            // Remove events that no longer exist in the feed
            var eventsToDelete = existingEvents
                .Where(e => e.ExternalEventId != null && !processedEventIds.Contains(e.ExternalEventId))
                .ToList();

            if (eventsToDelete.Any())
            {
                _context.Events.RemoveRange(eventsToDelete);
                _logger.LogInformation("Removing {Count} deleted events from subscription {SubscriptionId}",
                    eventsToDelete.Count, subscription.Id);
            }

            // Update subscription sync status
            subscription.LastSyncedAt = DateTime.UtcNow;
            subscription.LastSyncError = null;
            subscription.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Sync completed for subscription {SubscriptionId}: {Created} created, {Updated} updated, {Deleted} deleted",
                subscription.Id, eventsCreated, eventsUpdated, eventsToDelete.Count);

            return (true, null);
        }
        catch (Exception ex)
        {
            var error = $"Error syncing subscription: {ex.Message}";
            _logger.LogError(ex, "Error syncing subscription {SubscriptionId}", subscription.Id);

            subscription.LastSyncError = error;
            subscription.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return (false, error);
        }
    }

    private Event? CreateEventFromICalEvent(CalendarEvent calEvent, CalendarSubscription subscription)
    {
        try
        {
            var startDate = calEvent.Start?.AsUtc ?? DateTime.UtcNow;
            var endDate = calEvent.End?.AsUtc ?? startDate.AddHours(1);

            // Handle all-day events
            if (calEvent.Start?.IsUtc == false && calEvent.Start?.Value.TimeOfDay == TimeSpan.Zero)
            {
                // All-day event
                startDate = DateTime.SpecifyKind(calEvent.Start.Value.Date, DateTimeKind.Utc);
                endDate = calEvent.End != null
                    ? DateTime.SpecifyKind(calEvent.End.Value.Date, DateTimeKind.Utc)
                    : startDate.AddDays(1);
            }

            return new Event
            {
                Title = calEvent.Summary ?? "Untitled Event",
                Description = calEvent.Description,
                StartDateTime = startDate,
                EndDateTime = endDate,
                Color = subscription.Color,
                CalendarSubscriptionId = subscription.Id,
                ExternalEventId = calEvent.Uid,
                IsFromSubscription = true,
                IsReadOnly = true,
                UserId = subscription.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                // Copy recurrence information if present
                RecurrenceRule = calEvent.RecurrenceRules?.FirstOrDefault()?.ToString(),
                IsRecurring = calEvent.RecurrenceRules?.Any() == true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating event from iCal event {EventUid}", calEvent.Uid);
            return null;
        }
    }

    private bool UpdateEventFromICalEvent(Event existingEvent, CalendarEvent calEvent, CalendarSubscription subscription)
    {
        var changed = false;

        var newTitle = calEvent.Summary ?? "Untitled Event";
        if (existingEvent.Title != newTitle)
        {
            existingEvent.Title = newTitle;
            changed = true;
        }

        if (existingEvent.Description != calEvent.Description)
        {
            existingEvent.Description = calEvent.Description;
            changed = true;
        }

        var startDate = calEvent.Start?.AsUtc ?? existingEvent.StartDateTime;
        var endDate = calEvent.End?.AsUtc ?? existingEvent.EndDateTime;

        // Handle all-day events
        if (calEvent.Start?.IsUtc == false && calEvent.Start?.Value.TimeOfDay == TimeSpan.Zero)
        {
            startDate = DateTime.SpecifyKind(calEvent.Start.Value.Date, DateTimeKind.Utc);
            endDate = calEvent.End != null
                ? DateTime.SpecifyKind(calEvent.End.Value.Date, DateTimeKind.Utc)
                : startDate.AddDays(1);
        }

        if (existingEvent.StartDateTime != startDate)
        {
            existingEvent.StartDateTime = startDate;
            changed = true;
        }

        if (existingEvent.EndDateTime != endDate)
        {
            existingEvent.EndDateTime = endDate;
            changed = true;
        }

        var newRecurrenceRule = calEvent.RecurrenceRules?.FirstOrDefault()?.ToString();
        if (existingEvent.RecurrenceRule != newRecurrenceRule)
        {
            existingEvent.RecurrenceRule = newRecurrenceRule;
            existingEvent.IsRecurring = calEvent.RecurrenceRules?.Any() == true;
            changed = true;
        }

        return changed;
    }
}
