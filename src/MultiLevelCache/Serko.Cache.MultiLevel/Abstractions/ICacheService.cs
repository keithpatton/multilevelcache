using CacheTower;

namespace Serko.Cache.MultiLevel.Abstractions
{
    /// <summary>
    /// Application caching abstraction
    /// </summary>
    public interface ICacheService
    {
        ValueTask<T> GetOrSetAsync<T>(string cacheKey, Func<T, Task<T>> valueFactory, TimeSpan? timeToLive = null, TimeSpan? staleAfter = null);

        ValueTask EvictAsync(string cacheKey);
    }
}