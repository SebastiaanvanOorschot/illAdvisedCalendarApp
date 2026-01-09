using AgendaApi.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgendaApi.Services;

public class WeatherService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WeatherService> _logger;
    private readonly Dictionary<string, CachedWeatherData> _cache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(1);

    public WeatherService(HttpClient httpClient, ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<WeatherResponse?> GetForecastAsync(double latitude, double longitude)
    {
        var cacheKey = $"{latitude:F2},{longitude:F2}";

        // Check cache
        if (_cache.TryGetValue(cacheKey, out var cachedData) &&
            DateTime.UtcNow - cachedData.Timestamp < _cacheDuration)
        {
            _logger.LogInformation("Returning cached weather data for {Latitude}, {Longitude}", latitude, longitude);
            return cachedData.Data;
        }

        try
        {
            // Call Open-Meteo API - use InvariantCulture to ensure decimal point (not comma)
            var url = $"https://api.open-meteo.com/v1/forecast" +
                     $"?latitude={latitude.ToString("F6", CultureInfo.InvariantCulture)}" +
                     $"&longitude={longitude.ToString("F6", CultureInfo.InvariantCulture)}" +
                     $"&daily=temperature_2m_max,temperature_2m_min,weathercode,precipitation_probability_max" +
                     $"&timezone=auto" +
                     $"&forecast_days=14";

            _logger.LogInformation("Fetching weather data from Open-Meteo for {Latitude}, {Longitude}", latitude, longitude);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<OpenMeteoResponse>(content);

            if (apiResponse?.Daily == null)
            {
                _logger.LogWarning("Received null or invalid weather data");
                return null;
            }

            // Transform to our model
            var weatherResponse = new WeatherResponse
            {
                Latitude = latitude,
                Longitude = longitude,
                Daily = new List<Models.WeatherForecast>()
            };

            for (int i = 0; i < apiResponse.Daily.Time.Count && i < 14; i++)
            {
                weatherResponse.Daily.Add(new Models.WeatherForecast
                {
                    Date = DateTime.Parse(apiResponse.Daily.Time[i]),
                    TemperatureMax = apiResponse.Daily.Temperature2mMax[i],
                    TemperatureMin = apiResponse.Daily.Temperature2mMin[i],
                    WeatherCode = apiResponse.Daily.Weathercode[i],
                    PrecipitationProbability = apiResponse.Daily.PrecipitationProbabilityMax[i]
                });
            }

            // Cache the result
            _cache[cacheKey] = new CachedWeatherData
            {
                Data = weatherResponse,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Successfully fetched and cached weather data");
            return weatherResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching weather data from Open-Meteo");
            return null;
        }
    }

    private class CachedWeatherData
    {
        public WeatherResponse Data { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }

    // Open-Meteo API response structure
    private class OpenMeteoResponse
    {
        [JsonPropertyName("daily")]
        public DailyData? Daily { get; set; }
    }

    private class DailyData
    {
        [JsonPropertyName("time")]
        public List<string> Time { get; set; } = new();

        [JsonPropertyName("temperature_2m_max")]
        public List<double> Temperature2mMax { get; set; } = new();

        [JsonPropertyName("temperature_2m_min")]
        public List<double> Temperature2mMin { get; set; } = new();

        [JsonPropertyName("weathercode")]
        public List<int> Weathercode { get; set; } = new();

        [JsonPropertyName("precipitation_probability_max")]
        public List<double> PrecipitationProbabilityMax { get; set; } = new();
    }
}
