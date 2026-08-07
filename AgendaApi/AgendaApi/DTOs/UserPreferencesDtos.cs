namespace AgendaApi.DTOs;

public class UserPreferencesDto
{
    public bool ShowEventTitleInMonthView { get; set; }
}

public class UpdateUserPreferencesRequest
{
    public bool? ShowEventTitleInMonthView { get; set; }
}
