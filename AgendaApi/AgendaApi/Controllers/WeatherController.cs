using AgendaApi.Models;
using AgendaApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeatherController : ControllerBase
{
    private readonly WeatherService _weatherService;
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(WeatherService weatherService, ILogger<WeatherController> logger)
    {
        _weatherService = weatherService;
        _logger = logger;
    }

    [HttpGet("forecast")]
    public async Task<ActionResult<WeatherResponse>> GetForecast([FromQuery] double latitude, [FromQuery] double longitude)
    {
        if (latitude < -90 || latitude > 90)
        {
            return BadRequest("Latitude must be between -90 and 90");
        }

        if (longitude < -180 || longitude > 180)
        {
            return BadRequest("Longitude must be between -180 and 180");
        }

        _logger.LogInformation("Weather forecast requested for coordinates: {Latitude}, {Longitude}", latitude, longitude);

        var forecast = await _weatherService.GetForecastAsync(latitude, longitude);

        if (forecast == null)
        {
            return StatusCode(500, "Failed to fetch weather data");
        }

        return Ok(forecast);
    }
}
