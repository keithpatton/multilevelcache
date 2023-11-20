using CacheTower;
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

        public CacheService(ICacheStack cacheStack, IOptions<CacheOptions> options)
        {
            _cacheStack = cacheStack;
            _cacheOptions = options.Value;

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
                        // Optional: Log the retry attempt
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
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _cacheStack.GetOrSetAsync<T>(CreateCacheKey(cacheKey), valueFactory, settings);
            });
        }

        private string CreateCacheKey(string cacheKey)
        {
            return $"{_cacheOptions.KeyPrefix}{cacheKey}";
        }

    }
}
