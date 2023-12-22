using freecurrencyapi;
using Microsoft.AspNetCore.Mvc;
using MultiLevelCacheApi.Abstractions;
using MultiLevelCacheApi.Exceptions;
using Polly;
using Polly.Retry;
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
        private readonly AsyncRetryPolicy _vendorFxApiRetryPolicy;

        public ExRateController(ILogger<WeatherForecastController> logger, ICacheService cacheService)
        {
            _logger = logger;
            _cacheService = cacheService;

            // retry for vendor fx api  
            _vendorFxApiRetryPolicy = Policy
                .Handle<HttpRequestException>()
                .WaitAndRetryAsync(
                    3, // Number of retries
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential back-off
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception, $"Retry Attempt {retryCount}");
                    }
                );
        }

        [HttpGet("GetExRate")]

        public async Task<ActionResult<decimal>> GetExRateAsync(string fromCurrency, string toCurrency)
        {
            try
            {
                var exRates = await GetOrSetExRates(_baseCurrency);

                // Check for If-Modified-Since header
                if (Request.Headers.TryGetValue("If-Modified-Since", out var ifModifiedSinceValue) &&
                    DateTimeOffset.TryParse(ifModifiedSinceValue, out var ifModifiedSince))
                {
                    if (exRates.LastModified <= ifModifiedSince)
                    {
                        return StatusCode(304); // Not Modified
                    }
                }

                Response.Headers["Cache-Control"] = $"max-age={(exRates.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds}";
                Response.Headers["Last-Modified"] = exRates.LastModified.ToString("R");

                return Ok(GetExRateConversion(fromCurrency, toCurrency, exRates));
            }
            catch (CurrencyArgumentOutOfRangeException ex)
            {
                _logger.LogError(ex, "Unable to perform currency conversion");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// ensures forecast is retrieved from cache, fetching from backing store if necessary
        /// </summary>
        private async Task<ExRateData> GetOrSetExRates(string baseCurrency)
        {
            var rates = await _cacheService.GetOrSetAsync<ExRateData>(
                cacheKey: baseCurrency,
                valueFactory: async (oldRates) => { return await FetchExRatesAsync(oldRates, baseCurrency); },
                settings: _cacheService.GetCacheSettingsDefault());

            return rates;
        }

        /// <summary>
        /// fetches currency from api
        /// </summary>
        private async Task<ExRateData> FetchExRatesAsync(ExRateData oldRates, string baseCurrency)
        {
            try
            {
                // replace with professional api call!
                var fx = new Freecurrencyapi("fca_live_lO9fhw4RTrg2bs4ymt6oxCPh2DQblV2bbujmrir8");
                var ratesString = string.Empty;

                await _vendorFxApiRetryPolicy.ExecuteAsync(async () =>
                {
                    ratesString = await Task.FromResult(fx.Latest(baseCurrency));
                });

                if (!string.IsNullOrWhiteSpace(ratesString))
                {
                    using (JsonDocument doc = JsonDocument.Parse(ratesString))
                    {
                        var root = doc.RootElement;
                        var ratesElement = root.GetProperty("data");
                        var rates = ratesElement.Deserialize<Dictionary<string, decimal>>();
                        if (rates != null && rates.Any())
                        {
                            var lastModified = DateTimeOffset.UtcNow; // Or fetch this from the API response if available
                            var expiresOn = DateTimeOffset.UtcNow.Add(_cacheService.GetCacheSettingsDefault().TimeToLive); // Expiration based on TTL settings
                            return new ExRateData(rates, lastModified, expiresOn);
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
            if (oldRates != null && oldRates.Rates != null && oldRates.Rates.Any())
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
        private decimal GetExRateConversion(string fromCurrency, string toCurrency, ExRateData exchangeRates)
        {
            if (!exchangeRates.Rates.TryGetValue(fromCurrency, out decimal rateFromCurrencyToBaseRate))
            {
                throw new CurrencyArgumentOutOfRangeException(nameof(fromCurrency), $"Currency code {fromCurrency} not found in exchange rates.");
            }

            if (!exchangeRates.Rates.TryGetValue(toCurrency, out decimal rateToCurrencyToBaseRate))
            {
                throw new CurrencyArgumentOutOfRangeException(nameof(toCurrency), $"Currency code {toCurrency} not found in exchange rates.");
            }

            return rateToCurrencyToBaseRate / rateFromCurrencyToBaseRate;
        }

        /// <summary>
        /// exchange rate data
        /// </summary>
        public record ExRateData
        {
            public Dictionary<string, decimal> Rates { get; set; }
            public DateTimeOffset LastModified { get; set; }
            public DateTimeOffset ExpiresOn { get; set; }

            public ExRateData(Dictionary<string, decimal> rates, DateTimeOffset lastModified, DateTimeOffset expiresOn)
            {
                Rates = rates;
                LastModified = lastModified;
                ExpiresOn = expiresOn;
            }
        }

    }
}