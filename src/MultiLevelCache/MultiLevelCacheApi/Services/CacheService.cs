using CacheTower;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MultiLevelCacheApi.Abstractions;
using MultiLevelCacheApi.Options;
using Polly;
using Polly.Retry;
using StackExchange.Redis;

namespace MultiLevelCacheApi.Services
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
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly ILogger<CacheService> _logger;
        private readonly IMemoryCache _memCache;

        public CacheService(ICacheStack cacheStack, IMemoryCache memCache, IOptions<CacheOptions> options, ILogger<CacheService> logger)
        {

            _logger = logger;
            _cacheStack = cacheStack;
            _cacheOptions = options.Value;
            _memCache = memCache;

            // retry for Redis specific exceptions only coming from the Redis Cache Layer
            // note: be careful as the GetOrSetAsync covers the ValueFactory 
            // this  could raise wide range of exceptions and you may not want this to be retried
            _retryPolicy = Policy
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
        }

        public CacheSettings GetCacheSettingsDefault()
        {
            return new CacheSettings(_cacheOptions.TimeToLiveDefault, _cacheOptions.StaleAfterDefault);
        }

        public async ValueTask EvictAsync(string cacheKey)
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                await _cacheStack.EvictAsync(CreateCacheKey(cacheKey));
            });
        }

        public async ValueTask<T> GetOrSetAsync<T>(string cacheKey, Func<T, Task<T>> valueFactory, CacheSettings settings)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    var cacheValue = await _cacheStack.GetOrSetAsync(CreateCacheKey(cacheKey), valueFactory, settings);
                    _memCache.Set(CreateCacheKey(cacheKey), cacheValue, _cacheOptions.StoreBufferDefault);
                    return cacheValue;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unable to get/set value via cachetower for {cacheKey}.");
                if (_memCache.TryGetValue<T?>(CreateCacheKey(cacheKey), out var storeValue))
                {
                    _logger.LogWarning($"Returning {cacheKey} value from store buffer memory cache");
                    return storeValue!;
                }
                else
                {
                    _logger.LogWarning($"Ensuring {cacheKey} value retrieved from backing store and placed in store buffer memory cache");
                    storeValue = await valueFactory(default!);
                    _memCache.Set(CreateCacheKey(cacheKey), storeValue, _cacheOptions.StoreBufferDefault);
                }
                return storeValue;
            }
        }

        private string CreateCacheKey(string cacheKey)
        {
            return $"{_cacheOptions.KeyPrefix}{cacheKey}";
        }

    }
}
