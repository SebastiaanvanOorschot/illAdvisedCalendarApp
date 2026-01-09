using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using AgendaApi.Data;
using AgendaApi.Models;
using Microsoft.EntityFrameworkCore;
using AgendaEvent = AgendaApi.Models.Event;
using GoogleCalendarEvent = Google.Apis.Calendar.v3.Data.Event;

namespace AgendaApi.Services;

public interface IGoogleCalendarService
{
    Task<List<GoogleCalendarInfo>> GetUserCalendarsAsync(int userId);
    Task<List<AgendaEvent>> ImportCalendarEventsAsync(int userId, string calendarId, string color, DateTime startDate, DateTime endDate);
}

public class GoogleCalendarService : IGoogleCalendarService
{
    private readonly AgendaDbContext _context;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(AgendaDbContext context, ILogger<GoogleCalendarService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Creates a Google Calendar service instance using the user's stored access token
    /// </summary>
    private async Task<CalendarService?> GetCalendarServiceAsync(int userId)
    {
        _logger.LogInformation($"GetCalendarServiceAsync called for user {userId}");

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogError($"User {userId} not found in database");
            return null;
        }

        _logger.LogInformation($"User {userId} found. GoogleAccessToken is {(string.IsNullOrEmpty(user.GoogleAccessToken) ? "empty" : "present")}");

        if (string.IsNullOrEmpty(user.GoogleAccessToken))
        {
            _logger.LogError($"User {userId} has no Google access token");
            return null;
        }

        // Check if token is expired and refresh if needed
        if (user.GoogleTokenExpiry.HasValue && user.GoogleTokenExpiry.Value <= DateTime.UtcNow)
        {
            _logger.LogInformation($"Access token expired for user {userId}, refreshing...");
            await RefreshAccessTokenAsync(user);
        }
        else
        {
            _logger.LogInformation($"Access token for user {userId} is valid. Expiry: {user.GoogleTokenExpiry}");
        }

        try
        {
            _logger.LogInformation($"Creating GoogleCredential from access token for user {userId}");
            var credential = GoogleCredential.FromAccessToken(user.GoogleAccessToken);

            _logger.LogInformation($"Creating CalendarService for user {userId}");
            var service = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "IllAdvisedCalendarApp",
            });

            _logger.LogInformation($"CalendarService created successfully for user {userId}");
            return service;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating CalendarService for user {userId}");
            return null;
        }
    }

    /// <summary>
    /// Refreshes the user's Google access token using their refresh token
    /// </summary>
    private async Task RefreshAccessTokenAsync(User user)
    {
        if (string.IsNullOrEmpty(user.GoogleRefreshToken))
        {
            throw new Exception("No refresh token available");
        }

        // Note: This is a simplified version. In production, you should use Google's TokenResponse.RefreshTokenAsync
        // For now, we'll assume the token refresh is handled by the auth flow
        _logger.LogWarning($"Token refresh not fully implemented. User {user.Id} may need to re-authenticate.");
    }

    /// <summary>
    /// Get all calendars accessible to the user
    /// </summary>
    public async Task<List<GoogleCalendarInfo>> GetUserCalendarsAsync(int userId)
    {
        _logger.LogInformation($"GetUserCalendarsAsync called for user {userId}");

        var service = await GetCalendarServiceAsync(userId);
        if (service == null)
        {
            _logger.LogError($"Failed to create Calendar service for user {userId}");
            throw new Exception("Failed to create Calendar service. Please ensure you have connected your Google Calendar.");
        }

        var calendars = new List<GoogleCalendarInfo>();

        try
        {
            _logger.LogInformation($"Calling Google Calendar API to list calendars for user {userId}");

            var request = service.CalendarList.List();
            request.ShowHidden = false;
            request.MinAccessRole = CalendarListResource.ListRequest.MinAccessRoleEnum.Reader;

            var calendarList = await request.ExecuteAsync();

            foreach (var calendarItem in calendarList.Items)
            {
                calendars.Add(new GoogleCalendarInfo
                {
                    Id = calendarItem.Id,
                    Name = calendarItem.Summary,
                    Description = calendarItem.Description,
                    Primary = calendarItem.Primary ?? false,
                    BackgroundColor = calendarItem.BackgroundColor
                });
            }

            _logger.LogInformation($"Retrieved {calendars.Count} calendars for user {userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving calendars for user {userId}. Error type: {ex.GetType().Name}, Message: {ex.Message}");
            throw;
        }

        return calendars;
    }

    /// <summary>
    /// Import events from a specific Google Calendar
    /// </summary>
    public async Task<List<AgendaEvent>> ImportCalendarEventsAsync(int userId, string calendarId, string color, DateTime startDate, DateTime endDate)
    {
        var service = await GetCalendarServiceAsync(userId);
        if (service == null)
        {
            throw new Exception("Failed to create Calendar service");
        }

        var importedEvents = new List<AgendaEvent>();

        try
        {
            var request = service.Events.List(calendarId);
            request.TimeMin = startDate;
            request.TimeMax = endDate;
            request.SingleEvents = false; // Get recurring events as recurring (not expanded)
            // Note: OrderBy is not available when SingleEvents = false
            // request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

            var events = await request.ExecuteAsync();

            // Batch query: Get all Google event IDs we're about to import
            var googleEventIds = events.Items
                .Where(e => e.Status != "cancelled")
                .Select(e => e.Id)
                .ToList();

            // Single query to check which events already exist across ALL users (not just current user)
            // This prevents duplicates when multiple users import the same shared Google Calendar
            var existingEventLookup = await _context.Events
                .Where(e => e.GoogleCalendarId == calendarId && googleEventIds.Contains(e.GoogleEventId))
                .ToDictionaryAsync(e => e.GoogleEventId!, e => e);

            _logger.LogInformation($"Found {existingEventLookup.Count} existing events out of {googleEventIds.Count} from calendar {calendarId}");

            foreach (var googleEvent in events.Items)
            {
                // Skip cancelled events
                if (googleEvent.Status == "cancelled")
                    continue;

                var agendaEvent = await ConvertGoogleEventToAgendaEventAsync(userId, googleEvent, color, calendarId, existingEventLookup);
                if (agendaEvent != null)
                {
                    importedEvents.Add(agendaEvent);
                }
            }

            _logger.LogInformation($"Imported {importedEvents.Count} events from calendar {calendarId} for user {userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error importing events from calendar {calendarId} for user {userId}");
            throw;
        }

        return importedEvents;
    }

    /// <summary>
    /// Convert a Google Calendar event to an AgendaApi Event model
    /// </summary>
    private async Task<AgendaEvent?> ConvertGoogleEventToAgendaEventAsync(
        int userId,
        GoogleCalendarEvent googleEvent,
        string color,
        string calendarId,
        Dictionary<string, AgendaEvent> existingEventLookup)
    {
        try
        {
            // Check if this event already exists (imported by any user)
            existingEventLookup.TryGetValue(googleEvent.Id, out var existingEvent);

            // If event exists but was imported by another user, skip it (prevents duplicates)
            if (existingEvent != null && existingEvent.UserId != userId)
            {
                _logger.LogInformation($"Event {googleEvent.Id} already imported by user {existingEvent.UserId}, skipping for user {userId}");
                return null;
            }

            // If event was locally modified by current user, skip updating it
            if (existingEvent != null && existingEvent.UserId == userId && existingEvent.IsLocallyModified)
            {
                _logger.LogInformation($"Preserving locally modified event {googleEvent.Id}");
                return existingEvent;
            }

            DateTime startDateTime;
            DateTime endDateTime;

            // Handle all-day events
            if (googleEvent.Start.DateTime == null)
            {
                // All-day event
                startDateTime = DateTime.Parse(googleEvent.Start.Date);
                endDateTime = DateTime.Parse(googleEvent.End.Date);
            }
            else
            {
                startDateTime = googleEvent.Start.DateTime.Value;
                endDateTime = googleEvent.End.DateTime.Value;
            }

            var agendaEvent = existingEvent ?? new AgendaEvent
            {
                Title = string.Empty, // Will be set below
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                GoogleCalendarId = calendarId,
                GoogleEventId = googleEvent.Id,
                IsImportedFromGoogle = true,
                IsLocallyModified = false
            };

            // Update properties
            agendaEvent.Title = googleEvent.Summary ?? "Untitled Event";
            agendaEvent.Description = googleEvent.Description;
            agendaEvent.StartDateTime = startDateTime;
            agendaEvent.EndDateTime = endDateTime;
            agendaEvent.Color = color; // Use the color assigned by user for this calendar
            agendaEvent.UpdatedAt = DateTime.UtcNow;

            // Handle recurring events
            if (googleEvent.Recurrence != null && googleEvent.Recurrence.Count > 0)
            {
                agendaEvent.IsRecurring = true;
                // Google Calendar stores RRULE in the Recurrence list
                // Example: ["RRULE:FREQ=DAILY;COUNT=10"]
                var rruleLine = googleEvent.Recurrence.FirstOrDefault(r => r.StartsWith("RRULE:"));
                if (rruleLine != null)
                {
                    // Remove "RRULE:" prefix
                    agendaEvent.RecurrenceRule = rruleLine.Substring(6);
                }

                // Handle EXDATE (exception dates)
                var exdateLine = googleEvent.Recurrence.FirstOrDefault(r => r.StartsWith("EXDATE"));
                if (exdateLine != null)
                {
                    // Parse EXDATE and convert to our comma-separated format
                    // EXDATE format: EXDATE;VALUE=DATE:20241225,20241226
                    var exdates = ParseExdates(exdateLine);
                    if (exdates.Any())
                    {
                        agendaEvent.ExceptionDates = string.Join(",", exdates.Select(d => d.ToString("o")));
                    }
                }
            }

            return agendaEvent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error converting Google event {googleEvent.Id}");
            return null;
        }
    }

    /// <summary>
    /// Parse EXDATE string from Google Calendar recurrence rules
    /// </summary>
    private List<DateTime> ParseExdates(string exdateLine)
    {
        var dates = new List<DateTime>();
        try
        {
            // EXDATE can be in various formats:
            // EXDATE;VALUE=DATE:20241225,20241226
            // EXDATE:20241225T100000Z,20241226T100000Z

            var parts = exdateLine.Split(':');
            if (parts.Length < 2) return dates;

            var datesPart = parts[1];
            var dateStrings = datesPart.Split(',');

            foreach (var dateStr in dateStrings)
            {
                if (DateTime.TryParse(dateStr.Trim(), out var date))
                {
                    dates.Add(date);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing EXDATE");
        }

        return dates;
    }
}

/// <summary>
/// Model representing a Google Calendar
/// </summary>
public class GoogleCalendarInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Primary { get; set; }
    public string? BackgroundColor { get; set; }
}
