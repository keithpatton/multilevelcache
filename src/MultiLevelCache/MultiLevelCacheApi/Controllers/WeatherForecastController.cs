using Microsoft.AspNetCore.Mvc;
using MultiLevelCacheApi.Abstractions;

namespace MultiLevelCacheApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly ICacheService _cacheService;

        private const string _cacheKey = "weather_forecast";

        public WeatherForecastController(ILogger<WeatherForecastController> logger, ICacheService cacheService)
        {
            _logger = logger;
            _cacheService = cacheService;
        }

        [HttpGet("GetForecast")]
        public async Task<IEnumerable<WeatherForecast>> GetAsync()
        {
            return await GetOrSetForecastCache();
        }

        [HttpGet("GetForecastWithRefresh")]
        public async Task<IEnumerable<WeatherForecast>> GetWithRefreshAsync()
        {
            // ensure forecast removed from cache
            await _cacheService.EvictAsync(_cacheKey);
            return await GetOrSetForecastCache();
        }

        /// <summary>
        /// ensures forecast is retrieved from cache, fetching if necessary
        /// </summary>
        private async Task<IEnumerable<WeatherForecast>> GetOrSetForecastCache()
        {
            var weatherForecast = await _cacheService.GetOrSetAsync<IEnumerable<WeatherForecast>>(
                cacheKey: _cacheKey,
                valueFactory: async (oldForecast) => { return await FetchForecastAsync(oldForecast); },
                settings: _cacheService.GetCacheSettingsDefault());

            return weatherForecast;
        }

        /// <summary>
        /// fetches forecasts from underlying store (e.g. database or api call)
        /// </summary>
        private async Task<IEnumerable<WeatherForecast>> FetchForecastAsync(IEnumerable<WeatherForecast> oldWeatherForecast)
        {
            // Simulating a delay, e.g., for a database call or external API request
            // Note: This method can be called on a background thread when refreshing so carefully consider thread safety/context
            await Task.Delay(500);

            var forecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();

            return forecast;
        }
    }
}
