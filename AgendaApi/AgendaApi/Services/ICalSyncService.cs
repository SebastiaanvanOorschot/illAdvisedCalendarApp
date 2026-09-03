using AgendaApi.Data;
using AgendaApi.Models;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AgendaApi.Services;

public class ICalSyncService
{
    /// <summary>
    /// Upper bound on the iCal body we will read. A busy multi-year feed is a few hundred KB;
    /// 5 MB leaves plenty of headroom while capping what a user-supplied URL can make us buffer.
    /// </summary>
    private const long MaxResponseBytes = 5 * 1024 * 1024;

    private const int ResponseBufferSize = 8192;

    /// <summary>
    /// Content types we consider plausible for an iCal feed. Feeds that report something else
    /// are logged but still parsed — see the note in SyncSubscriptionAsync.
    /// </summary>
    private static readonly string[] PlausibleContentTypes = { "text/calendar", "text/plain" };

    // Messages returned to the caller (and stored in LastSyncError). Deliberately generic:
    // URLs, status codes and exception details stay in the log.
    private const string InvalidUrlError = "Invalid iCal URL. Only http and https URLs are supported.";
    private const string FetchFailedError = "Could not fetch the iCal feed.";
    private const string FeedTooLargeError = "The iCal feed is too large to process.";
    private const string ParseFailedError = "Failed to parse iCal content or no events found";
    private const string SyncFailedError = "An error occurred while syncing this calendar.";

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

            // Only absolute http(s) URLs may be fetched on the user's behalf.
            if (!Uri.TryCreate(subscription.ICalUrl, UriKind.Absolute, out var icalUri) ||
                (icalUri.Scheme != Uri.UriSchemeHttp && icalUri.Scheme != Uri.UriSchemeHttps))
            {
                _logger.LogWarning("Rejected iCal URL with unsupported scheme for subscription {SubscriptionId}",
                    subscription.Id);
                return (false, InvalidUrlError);
            }

            // Fetch iCal data
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            using var response = await httpClient.GetAsync(icalUri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch iCal for subscription {SubscriptionId}: HTTP {StatusCode}",
                    subscription.Id, (int)response.StatusCode);
                return (false, FetchFailedError);
            }

            // Not a hard rejection: plenty of real feeds serve a wrong or missing Content-Type,
            // so a mismatch is logged and the body is still handed to the parser.
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType == null ||
                !PlausibleContentTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Unexpected Content-Type '{ContentType}' for subscription {SubscriptionId}; parsing anyway",
                    mediaType ?? "(none)", subscription.Id);
            }

            if (response.Content.Headers.ContentLength > MaxResponseBytes)
            {
                _logger.LogError(
                    "iCal feed for subscription {SubscriptionId} declares {Length} bytes, over the {Limit} byte limit",
                    subscription.Id, response.Content.Headers.ContentLength, MaxResponseBytes);
                return (false, FeedTooLargeError);
            }

            var (icalContent, exceededLimit) = await ReadCappedAsync(response);
            if (exceededLimit)
            {
                _logger.LogError("iCal feed for subscription {SubscriptionId} exceeded the {Limit} byte limit",
                    subscription.Id, MaxResponseBytes);
                return (false, FeedTooLargeError);
            }

            // Parse iCal content
            var calendar = Calendar.Load(icalContent);
            if (calendar == null || calendar.Events == null)
            {
                _logger.LogWarning("Invalid iCal content for subscription {SubscriptionId}", subscription.Id);
                return (false, ParseFailedError);
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
            _logger.LogError(ex, "Error syncing subscription {SubscriptionId}", subscription.Id);

            subscription.LastSyncError = SyncFailedError;
            subscription.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return (false, SyncFailedError);
        }
    }

    /// <summary>
    /// Streams the response body, giving up as soon as it grows past <see cref="MaxResponseBytes"/>
    /// so an oversized or endless feed can never be buffered in full.
    /// </summary>
    private static async Task<(string Content, bool ExceededLimit)> ReadCappedAsync(HttpResponseMessage response)
    {
        using var stream = await response.Content.ReadAsStreamAsync();
        using var buffered = new MemoryStream();

        var buffer = new byte[ResponseBufferSize];
        int read;
        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            if (buffered.Length + read > MaxResponseBytes)
                return (string.Empty, true);

            buffered.Write(buffer, 0, read);
        }

        buffered.Position = 0;
        using var reader = new StreamReader(buffered, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return (await reader.ReadToEndAsync(), false);
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
