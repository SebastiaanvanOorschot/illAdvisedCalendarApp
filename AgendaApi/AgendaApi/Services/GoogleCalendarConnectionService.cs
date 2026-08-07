using AgendaApi.Data;
using AgendaApi.DTOs;

namespace AgendaApi.Services;

public class GoogleCalendarConnectionService
{
    private readonly AgendaDbContext _context;
    private readonly IGoogleCalendarService _googleCalendarService;
    private readonly ILogger<GoogleCalendarConnectionService> _logger;

    public GoogleCalendarConnectionService(
        AgendaDbContext context,
        IGoogleCalendarService googleCalendarService,
        ILogger<GoogleCalendarConnectionService> logger)
    {
        _context = context;
        _googleCalendarService = googleCalendarService;
        _logger = logger;
    }

    public async Task<GoogleCalendarConnectionServiceResult> ConnectAsync(int userId, string accessToken)
    {
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
            return GoogleCalendarConnectionServiceResult.NotFoundResult();

        // Store the calendar access token
        // Note: This is the same field as the main Google token, but now with calendar scope
        user.GoogleAccessToken = accessToken;
        user.GoogleTokenExpiry = DateTime.UtcNow.AddHours(1); // Google tokens typically expire in 1 hour

        await _context.SaveChangesAsync();

        _logger.LogInformation($"Stored Google Calendar token for user {userId}");

        return GoogleCalendarConnectionServiceResult.Ok();
    }

    public async Task<ImportResult> ImportAsync(int userId, List<CalendarImportItem> calendars)
    {
        var totalImported = 0;
        var errors = new List<string>();

        // Calculate date range for import
        var startDate = DateTime.Today; // Today
        var endDate = DateTime.Today.AddYears(1); // +1 year for non-recurring events

        // Use a transaction to ensure all-or-nothing import
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            foreach (var calendarRequest in calendars)
            {
                try
                {
                    _logger.LogInformation($"Importing calendar {calendarRequest.CalendarId} with color {calendarRequest.Color}");

                    var events = await _googleCalendarService.ImportCalendarEventsAsync(
                        userId,
                        calendarRequest.CalendarId,
                        calendarRequest.Color,
                        startDate,
                        endDate
                    );

                    // Save or update events in database
                    foreach (var evt in events)
                    {
                        // The service already handles the logic for existing vs new events
                        // If evt.Id > 0, it's an existing event that was already updated by the service
                        // If evt.Id == 0, it's a new event that needs to be added
                        if (evt.Id == 0)
                        {
                            // New event - add to database
                            _context.Events.Add(evt);
                        }
                        // Existing events are already tracked and updated by the service
                        totalImported++;
                    }

                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error importing calendar {calendarRequest.CalendarId}");
                    errors.Add($"Failed to import calendar {calendarRequest.CalendarId}: {ex.Message}");
                    // Rollback transaction on any error
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            // Commit transaction if all calendars imported successfully
            await transaction.CommitAsync();

            return new ImportResult
            {
                Success = true,
                TotalImported = totalImported,
                Message = $"Successfully imported {totalImported} events",
                Errors = errors
            };
        }
        catch (Exception)
        {
            // Transaction already rolled back in catch block above
            return new ImportResult
            {
                Success = false,
                TotalImported = 0,
                Message = "Import failed. No events were imported.",
                Errors = errors
            };
        }
    }
}

public enum GoogleCalendarConnectionServiceStatus
{
    Success,
    NotFound
}

public class GoogleCalendarConnectionServiceResult
{
    public GoogleCalendarConnectionServiceStatus Status { get; init; }

    public static GoogleCalendarConnectionServiceResult Ok() => new() { Status = GoogleCalendarConnectionServiceStatus.Success };
    public static GoogleCalendarConnectionServiceResult NotFoundResult() => new() { Status = GoogleCalendarConnectionServiceStatus.NotFound };
}
