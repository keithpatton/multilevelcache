using freecurrencyapi;
using Microsoft.AspNetCore.Mvc;
using MultiLevelCacheApi.Abstractions;
using MultiLevelCacheApi.Exceptions;
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
        private const string _baseCurrency = "USD";

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
                var exRates = await GetOrSetExRates(_baseCurrency);
                return Ok(GetExRateConversion(fromCurrency, toCurrency, exRates));
            }
            catch (CurrencyArgumentOutOfRangeException ex)
            {
                _logger.LogError(ex, "Unable to perform currency conversion");
                return BadRequest(ex.Message);
            }
            catch (ExRatesFetchException ex)
            {
                _logger.LogError(ex, "Error occurred while fetching exchange rates");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error occurred while processing your request.");
            }
        }

        /// <summary>
        /// ensures forecast is retrieved from cache, fetching from backing store if necessary
        /// </summary>
        private async Task<Dictionary<string, decimal>> GetOrSetExRates(string baseCurrency)
        {
            var rates = await _cacheService.GetOrSetAsync<Dictionary<string, decimal>>(
                cacheKey: baseCurrency,
                valueFactory: async (oldRates) => { return await FetchExRatesAsync(oldRates, baseCurrency); },
                settings: _cacheService.GetCacheSettingsDefault());

            return rates;
        }

        /// <summary>
        /// fetches currency from api
        /// </summary>
        private async Task<Dictionary<string, decimal>> FetchExRatesAsync(Dictionary<string, decimal> oldRates, string baseCurrency)
        {
            try
            {
                // replace with professional api call and use Polly Retry for resilience on the api call
                var fx = new Freecurrencyapi("fca_live_lO9fhw4RTrg2bs4ymt6oxCPh2DQblV2bbujmrir8");
                var ratesString = await Task.FromResult(fx.Latest(baseCurrency));

                if (!string.IsNullOrWhiteSpace(ratesString))
                {
                    using (JsonDocument doc = JsonDocument.Parse(ratesString))
                    {
                        var root = doc.RootElement;
                        var ratesElement = root.GetProperty("data");
                        var rates = ratesElement.Deserialize<Dictionary<string, decimal>>();
                        if (rates != null && rates.Any())
                        {
                            return rates;
                        }
                        else
                        {
                            _logger.LogError($"Vendor api returned unexpected data for {baseCurrency} ex rates: {ratesString}");
                        }
                    }
                }
                else
                {
                    _logger.LogError($"Vendor api returned no data for {baseCurrency} ex rates");
                }               
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Error deserializing ex rates JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching ex rates: {ex.Message}");
            }

            // return old rates from cache if possible if there's a problem with the api call
            if (oldRates != null && oldRates.Any())
            {
                _logger.LogWarning("Returning old rates from cache as there was a problem retrieving fresh rates");
                return oldRates;
            }

            // all hope is lost, we weren't able to fetch any rates fresh from vendor api or from cache
            throw new ExRatesFetchException($"Unable to fetch exchange rates for {baseCurrency}");
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
                throw new CurrencyArgumentOutOfRangeException(nameof(fromCurrency), $"Currency code {fromCurrency} not found in exchange rates.");
            }

            if (!exchangeRates.TryGetValue(toCurrency, out decimal rateToCurrencyToBaseRate))
            {
                throw new CurrencyArgumentOutOfRangeException(nameof(toCurrency), $"Currency code {toCurrency} not found in exchange rates.");
            }

            return rateToCurrencyToBaseRate / rateFromCurrencyToBaseRate;
        }

    }
}
