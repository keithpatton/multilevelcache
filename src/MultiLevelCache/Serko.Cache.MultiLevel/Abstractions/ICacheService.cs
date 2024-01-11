using CacheTower;

namespace Serko.Cache.MultiLevel.Abstractions
{
    /// <summary>
    /// Application caching abstraction
    /// </summary>
    public interface ICacheService
    {
        ValueTask<T> GetOrSetAsync<T>(string cacheKey, Func<T, Task<T>> valueFactory, CacheSettings settings);

        ValueTask EvictAsync(string cacheKey);

        CacheSettings GetCacheSettingsDefault();
    }
}
