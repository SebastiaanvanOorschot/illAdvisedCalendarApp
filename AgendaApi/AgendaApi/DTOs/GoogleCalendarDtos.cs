namespace AgendaApi.DTOs;

/// <summary>
/// Request model for connecting Google Calendar
/// </summary>
public class ConnectRequest
{
    public string AccessToken { get; set; } = string.Empty;
}

/// <summary>
/// Request model for importing calendars
/// </summary>
public class ImportRequest
{
    public List<CalendarImportItem> Calendars { get; set; } = new();
}

public class CalendarImportItem
{
    public string CalendarId { get; set; } = string.Empty;
    public string Color { get; set; } = "#000000";
}

/// <summary>
/// Result of calendar import operation
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public int TotalImported { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}
