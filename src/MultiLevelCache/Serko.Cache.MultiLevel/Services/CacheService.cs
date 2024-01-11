using CacheTower;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Wrap;
using Serko.Cache.MultiLevel.Abstractions;
using Serko.Cache.MultiLevel.Options;
using StackExchange.Redis;

namespace Serko.Cache.MultiLevel.Services
{
    /// <summary>
    /// Application caching provider
    /// </summary>
    /// <remarks>
    /// Wraps CacheTower's CacheStack implementation
    /// </remarks>
    public class CacheService : ICacheService
    {
        private ICacheStack _cacheStack;
        private CacheOptions _cacheOptions;
        private readonly ILogger<CacheService> _logger;
        private readonly IMemoryCache _memCache;
        private readonly AsyncPolicyWrap _resiliencePolicy;

        public CacheService(ICacheStack cacheStack, IMemoryCache memCache, IOptions<CacheOptions> options, ILogger<CacheService> logger)
        {

            _logger = logger;
            _cacheStack = cacheStack;
            _cacheOptions = options.Value;
            _memCache = memCache;

            // retry for Redis specific exceptions only coming from the Redis Cache Layer
            // note: be careful as the GetOrSetAsync covers the ValueFactory 
            // this  could raise wide range of exceptions and you may not want this to be retried
            var retryPolicy = Policy
                .Handle<RedisException>()
                .Or<RedisTimeoutException>()
                .WaitAndRetryAsync(
                    3, // Number of retries
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential back-off
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(exception, $"Retry Attempt {retryCount}");
                    }
                );

            var circuitBreakerPolicy = Policy
                    .Handle<RedisException>()
                    .Or<RedisTimeoutException>()
                    .AdvancedCircuitBreakerAsync(
                        failureThreshold: 0.1, // 10% failure rate
                        samplingDuration: TimeSpan.FromMinutes(15), // Over a 15-minute period
                        minimumThroughput: 100, // Minimum number of actions within the sampling period
                        durationOfBreak: TimeSpan.FromMinutes(5), // Circuit stays open for 5 minutes
                    onBreak: (exception, timespan) =>
                    {
                        _logger.LogWarning($"Circuit broken due to {exception.GetType().Name}");
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit reset");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("Circuit is half-open");
                    }
                );

            _resiliencePolicy = circuitBreakerPolicy.WrapAsync(retryPolicy);

        }

        public async ValueTask EvictAsync(string cacheKey)
        {
            await _resiliencePolicy.ExecuteAsync(async () =>
            {
                await _cacheStack.EvictAsync(CreateCacheKey(cacheKey));
            });
        }

        public async ValueTask<T> GetOrSetAsync<T>(string cacheKey, Func<T, Task<T>> valueFactory, TimeSpan? timeToLive = null, TimeSpan? staleAfter = null)
        {
            try
            {
                CacheSettings? cacheSettings = null;
                timeToLive ??= _cacheOptions.TimeToLiveDefault;
                staleAfter ??= _cacheOptions.StaleAfterDefault;
                if (staleAfter == null)
                {
                    cacheSettings = new CacheSettings(timeToLive.Value);
                }
                else
                {
                    cacheSettings = new CacheSettings(timeToLive.Value, staleAfter.Value);
                }

                return await _resiliencePolicy.ExecuteAsync(async () =>
                {
                    var cacheValue = await _cacheStack.GetOrSetAsync(CreateCacheKey(cacheKey), valueFactory, cacheSettings.Value);
                    if (_cacheOptions.StoreBufferDefault.HasValue)
                    {
                        _ = _memCache.Set(CreateCacheKey(cacheKey), cacheValue, _cacheOptions.StoreBufferDefault.Value);
                    }
                    return cacheValue;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to get/set value via cachetower for {cacheKey}.");
                if (_cacheOptions.StoreBufferDefault.HasValue && _memCache.TryGetValue<T?>(CreateCacheKey(cacheKey), out var storeBufferValue))
                {
                    _logger.LogWarning($"Returning {cacheKey} value from store buffer memory cache");
                    return storeBufferValue!;
                }
                else
                {
                    _logger.LogWarning($"Ensuring {cacheKey} value retrieved from backing store and placed in store buffer memory cache");
                    var storeValue = await valueFactory(default!);
                    if (_cacheOptions.StoreBufferDefault.HasValue)
                    {
                        _ = _memCache.Set(CreateCacheKey(cacheKey), storeValue, _cacheOptions.StoreBufferDefault.Value);
                    }
                    return storeValue!;
                }
            }
        }

        private string CreateCacheKey(string cacheKey)
        {
            return $"{_cacheOptions.KeyPrefix}{cacheKey}";
        }

    }
}
