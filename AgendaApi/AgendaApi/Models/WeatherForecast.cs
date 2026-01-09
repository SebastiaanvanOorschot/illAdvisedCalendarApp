namespace AgendaApi.Models;

public class WeatherForecast
{
    public DateTime Date { get; set; }
    public double TemperatureMax { get; set; }
    public double TemperatureMin { get; set; }
    public int WeatherCode { get; set; }
    public double PrecipitationProbability { get; set; }
}

public class WeatherResponse
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public List<WeatherForecast> Daily { get; set; } = new();
}
