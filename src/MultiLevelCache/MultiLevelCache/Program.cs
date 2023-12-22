using Microsoft.Extensions.DependencyInjection;
using CacheTower;
using CacheTower.Providers.Redis;
using CacheTower.Serializers.SystemTextJson;
using StackExchange.Redis;

namespace MultiLevelCache
{
    /// <summary>
    /// CacheTower Example
    /// </summary>
    internal class Program
    {

        // note: would be retrived using secure method
        private const string RedisConnString = "{REDIS_CONN_STRING}";
        private const string KeyPrefix = "KeyPrefix_";
  
        private static ICacheStack? _cacheStack;

        static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);

            // note: would normally be constructor injected
            var serviceProvider = services.BuildServiceProvider();
            _cacheStack = serviceProvider.GetRequiredService<ICacheStack>();

            // Ensures CacheTest object is retrieved asynchronously and added to acche
            // It will expire after 60 mins (timeToLive) and be re-fetched in background after 30 mins (staleAfter)
            var cacheTest = await _cacheStack.GetOrSetAsync<CacheTest>(
                cacheKey: $"{KeyPrefix}test",
                valueFactory: async (oldCacheTest) => { return await RetrieveDataAsync(oldCacheTest); },
                settings: new CacheSettings(timeToLive: TimeSpan.FromMinutes(60), staleAfter: TimeSpan.FromMinutes(30))
            );

            // Here we use Redis Cache Console to test the cache invalidation outside of the application
            // note: if you use CacheTower apis within your application to explicitly evict and flush it will also work, but it's common to evict from Redis also
            Console.WriteLine($"CacheTest object has been added to cache");
            Console.WriteLine("Now, issue the following commands via Redis Cache Console: ");
            Console.WriteLine("DEL KeyPrefix_test");
            Console.WriteLine("PUBLISH CacheTower.RemoteEviction KeyPrefix_test");
            
            Console.ReadKey();

            // Now we attempt to retrieve the value which should be null as CacheTower will have removed in all cache levels (both memory and redis cache)
            var cacheTest2 = await _cacheStack.GetAsync<CacheTest>($"{KeyPrefix}test");

            if (cacheTest2 is null)
            {
                Console.WriteLine("Cache test sucessfully evicted by CacheTower in both memory and redis cache via Redis Console");
            }
            else
            {
                // This should not fire if you have executed the redis console steps
                Console.WriteLine("Eek! Cache test NOT invalidated by CacheTower in memory and redis cache. Ddi you issue the commands as above?");
            }
        }

        /// <summary>
        /// A custom object you want to store in cache
        /// </summary>
        public record CacheTest
        {
            public string Test { get; set; } = string.Empty;
        }

        /// <summary>
        /// A custom method which asyncrhonously fetches object for caching
        /// </summary>
        private static async Task<CacheTest> RetrieveDataAsync(CacheTest oldCacheTest)
        {
            // note: You can optional use the oldCacheTest value from cache if necessary, not doing that here, just added param so you can see possibility
            var cacheTest = new CacheTest() { Test = "TestValue" };
            // Simulate some asynchronous operation (e.g. database call)
            await Task.Delay(100); 
            return cacheTest;
        }

        /// <summary>
        /// Testing only to highlight the CacheTower.RemoteEviction Channel redis subscription is working
        /// </summary>
        private static void SubscribeToCacheTowerRemoteEviction(IConnectionMultiplexer redisConnection)
        {
            var subscriber = redisConnection.GetSubscriber();
            subscriber.Subscribe(new RedisChannel($"CacheTower.RemoteEviction", RedisChannel.PatternMode.Literal), (channel, message) =>
            {
                Console.WriteLine($"CacheTower.RemoteEviction: {message}");
                Console.WriteLine("Press any key to continue....");
            });     
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            var redisConnection = ConnectionMultiplexer.Connect(RedisConnString);

            // for testing purposes subscribe to cachetower remote eviction
            SubscribeToCacheTowerRemoteEviction(redisConnection);

            services.AddCacheStack(builder => builder
                // uses local system memory as cache layer
                .AddMemoryCacheLayer()
                // uses Redis as distributed cache layer
                .AddRedisCacheLayer(redisConnection, new RedisCacheLayerOptions(SystemTextJsonCacheSerializer.Instance))
                // uses Redis for distributed locking (to avoid cache stampedes)
                .WithRedisDistributedLocking(redisConnection)
                // ensures cache invalidation is propagated across instances and layers
                .WithRedisRemoteEviction(redisConnection)
                // cleans up cache every 7 days across all layers (memory and Redis)
                .WithCleanupFrequency(TimeSpan.FromDays(7))
            );

        }

    }

}

