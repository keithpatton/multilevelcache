using freecurrencyapi;
using Microsoft.AspNetCore.Mvc;
using MultiLevelCacheApi.Abstractions;
using System.Text.Json;

namespace MultiLevelCacheApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ExRateController : ControllerBase
    {


        private readonly ILogger<WeatherForecastController> _logger;
        private readonly ICacheService _cacheService;


        public ExRateController(ILogger<WeatherForecastController> logger, ICacheService cacheService)
        {
            _logger = logger;
            _cacheService = cacheService;
        }

        [HttpGet("GetExRate")]
        public async Task<Dictionary<string, decimal>?> GetAsync(string baseCurrency)
        {
            return await GetOrSetExRate(baseCurrency);
        }


        /// <summary>
        /// ensures forecast is retrieved from cache, fetching if necessary
        /// </summary>
        private async Task<Dictionary<string, decimal>?> GetOrSetExRate(string baseCurrency)
        {
            var rates = await _cacheService.GetOrSetAsync<Dictionary<string, decimal>>(
                cacheKey: baseCurrency,
                valueFactory: async (oldRates) => { return await FetchExRate(oldRates, baseCurrency); },
                settings: _cacheService.GetCacheSettingsDefault());

            return rates;
        }

        /// <summary>
        /// fetches currency from api
        /// </summary>
        private async Task<Dictionary<string, decimal>?> FetchExRate(Dictionary<string, decimal> oldRates, string baseCurrency)
        {
            try
            {
                var fx = new Freecurrencyapi("API KEY");
                var rates = fx.Latest(baseCurrency);

                using (JsonDocument doc = JsonDocument.Parse(rates))
                {
                    var root = doc.RootElement;
                    var ratesElement = root.GetProperty("data");
                    var ratesDictionary = ratesElement.Deserialize<Dictionary<string, decimal>>();
                    return await Task.FromResult(ratesDictionary);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Error deserializing JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"General error: {ex.Message}");
            }
            return oldRates;
        }

    }
}
