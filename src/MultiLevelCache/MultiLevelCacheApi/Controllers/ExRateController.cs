using freecurrencyapi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MultiLevelCacheApi.Abstractions;
using System.Runtime.CompilerServices;
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
        public async Task<ActionResult<decimal>> GetExRateAsync(string fromCurrency, string toCurrency)
        {
            try
            {
                var usdRates = await GetOrSetExRate("USD");
                return Ok(GetExRateConversion(fromCurrency, toCurrency, usdRates!)); 
            }
            catch (ArgumentOutOfRangeException ex)
            {
                _logger.LogError(ex, "Unable to calculate exchange rate");
                return BadRequest("An invalid currency code was supplied"); 
            }
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
                // replace with professional api call!
                var fx = new Freecurrencyapi("fca_live_lO9fhw4RTrg2bs4ymt6oxCPh2DQblV2bbujmrir8");
                var rates = await Task.FromResult(fx.Latest(baseCurrency));

                using (JsonDocument doc = JsonDocument.Parse(rates))
                {
                    var root = doc.RootElement;
                    var ratesElement = root.GetProperty("data");
                    return ratesElement.Deserialize<Dictionary<string, decimal>>();
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

        /// <summary>
        /// Performs a currency conversion using rates collection
        /// </summary>
        /// <param name="fromCurrency">Source Currency</param>
        /// <param name="toCurrency">Target Currency</param>
        /// <param name="exchangeRates">Exchange Rates</param>
        /// <returns>The exchange rate between the two supplied currencies</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private decimal GetExRateConversion(string fromCurrency, string toCurrency, Dictionary<string, decimal> exchangeRates)
        {
            if (!exchangeRates.TryGetValue(fromCurrency, out decimal rateFromCurrencyToBaseRate))
            {
                throw new ArgumentOutOfRangeException(nameof(fromCurrency), $"Currency code {fromCurrency} not found in exchange rates.");
            }

            if (!exchangeRates.TryGetValue(toCurrency, out decimal rateToCurrencyToBaseRate))
            {
                throw new ArgumentOutOfRangeException(nameof(toCurrency), $"Currency code {toCurrency} not found in exchange rates.");
            }

            return rateToCurrencyToBaseRate / rateFromCurrencyToBaseRate;
        }

    }
}
