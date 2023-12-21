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
        public async Task<decimal> GetExRateAsync(string fromCurrency, string toCurrency)
        {
            var usdRates = await GetOrSetExRate("USD"); // from cache or api (will be memory most of the time)
            return GetExRateConversion(fromCurrency, toCurrency, usdRates!); // in memory conversion
        }

        /// <summary>
        /// ensures forecast is retrieved from cache, fetching if necessary
        /// </summary>
        private async Task<Dictionary<string, decimal>?> GetOrSetExRate(string baseCurrency)
        {
            var rates = await _cacheService.GetOrSetAsync<Dictionary<string, decimal>>(
                cacheKey: baseCurrency,
                valueFactory: async (oldRates) => { return await FetchExRateAsync(oldRates, baseCurrency); },
                settings: _cacheService.GetCacheSettingsDefault());

            return rates;
        }

        /// <summary>
        /// fetches currency from api
        /// </summary>
        private async Task<Dictionary<string, decimal>?> FetchExRateAsync(Dictionary<string, decimal> oldRates, string baseCurrency)
        {
            try
            {
                var fx = new Freecurrencyapi("fca_live_lO9fhw4RTrg2bs4ymt6oxCPh2DQblV2bbujmrir8");
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

        private decimal GetExRateConversion(string fromCurrency, string toCurrency, Dictionary<string, decimal> exchangeRates)
        {
            if (!exchangeRates.ContainsKey(fromCurrency) || !exchangeRates.ContainsKey(toCurrency))
            {
                throw new ArgumentException("Currency code not found in exchange rates.");
            }

            decimal rateFromCurrencyToUSD = exchangeRates[fromCurrency];
            decimal rateToCurrencyToUSD = exchangeRates[toCurrency];

            decimal rateFromCurrencyToCurrency = rateToCurrencyToUSD / rateFromCurrencyToUSD;

            return rateFromCurrencyToCurrency;
        }


    }
}
