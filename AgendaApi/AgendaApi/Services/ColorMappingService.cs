namespace AgendaApi.Services;

/// <summary>
/// Maps Google Calendar color IDs to app color hex codes
/// </summary>
public static class ColorMappingService
{
    private static readonly Dictionary<string, string> GoogleColorMap = new()
    {
        { "1", "#8800FF" },  // Lavender (#a4bdfc) → Purple
        { "2", "#00FF00" },  // Sage (#7ae7bf) → Green
        { "3", "#8800FF" },  // Grape (#dbadff) → Purple
        { "4", "#FF0000" },  // Flamingo (#ff887c) → Red
        { "5", "#FFFF00" },  // Banana (#fbd75b) → Yellow
        { "6", "#FF8800" },  // Tangerine (#ffb878) → Orange
        { "7", "#00FFFF" },  // Peacock (#46d6db) → Cyan
        { "8", "#000000" },  // Graphite (#e1e1e1) → Black
        { "9", "#0000FF" },  // Blueberry (#5484ed) → Blue
        { "10", "#00FF00" }, // Basil (#51b749) → Green
        { "11", "#FF0000" }, // Tomato (#dc2127) → Red
    };

    /// <summary>
    /// Convert Google Calendar colorId to app hex color
    /// </summary>
    /// <param name="googleColorId">Google Calendar color ID (1-11)</param>
    /// <returns>Hex color code or black as default</returns>
    public static string MapGoogleColorToAppColor(string? googleColorId)
    {
        if (string.IsNullOrEmpty(googleColorId))
            return "#000000"; // Default black

        return GoogleColorMap.TryGetValue(googleColorId, out var color)
            ? color
            : "#000000";
    }
}
