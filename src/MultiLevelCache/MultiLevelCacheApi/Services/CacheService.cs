using CacheTower;
using Microsoft.Extensions.Options;
using MultiLevelCacheApi.Abstractions;
using MultiLevelCacheApi.Options;

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

        public CacheService(ICacheStack cacheStack, IOptions<CacheOptions> options)
        {
            _cacheStack = cacheStack;
            _cacheOptions = options.Value;
        }

        public CacheSettings GetCacheSettingsDefault()
        {
            return new CacheSettings(_cacheOptions.TimeToLiveDefault, _cacheOptions.StaleAfterDefault);
        }

        public ValueTask EvictAsync(string cacheKey)
        {
            return _cacheStack.EvictAsync(CreateCacheKey(cacheKey));
        }

        public ValueTask<T> GetOrSetAsync<T>(string cacheKey, Func<T, Task<T>> valueFactory, CacheSettings settings)
        {
            return _cacheStack.GetOrSetAsync<T>(CreateCacheKey(cacheKey), valueFactory, settings);
        }

        private string CreateCacheKey(string cacheKey)
        {
            return $"{_cacheOptions.KeyPrefix}{cacheKey}";
        }

    }
}
