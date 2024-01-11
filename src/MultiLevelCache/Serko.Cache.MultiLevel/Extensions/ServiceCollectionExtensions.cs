using Azure.Identity;
using CacheTower.Providers.Redis;
using CacheTower.Serializers.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serko.Cache.MultiLevel.Abstractions;
using Serko.Cache.MultiLevel.Options;
using Serko.Cache.MultiLevel.Services;
using StackExchange.Redis;

namespace Serko.Cache.MultiLevel.Extensions
{
    /// <summary>
    /// Contains extension methods for IServiceCollection to add event ingestion services.
    /// </summary>
    public static class ServiceCollectionExtensions
    {

        /// <summary>
        /// Adds memory and Redis cache services to the specified IServiceCollection.
        /// </summary>
        /// <param name="services">The IServiceCollection to add services to.</param>
        /// <param name="cacheOptions">An optional action to configure CacheOptions.</param>
        /// <returns>The IServiceCollection for chaining.</returns>
        public static IServiceCollection AddMemoryWithRedisCache(
           this IServiceCollection services,
           Action<CacheOptions>? cacheOptions = null,
           Action<RedisCacheOptions>? redisCacheOptions = null) 
        {
            return AddMemoryWithRedisCache<MemoryWithRedisCacheService>(services, cacheOptions, redisCacheOptions);
        }

        /// <summary>
        /// Adds memory and Redis cache services using a custom cache service.
        /// </summary>
        /// <typeparam name="T">The type of the custom cache service.</typeparam>
        /// <param name="services">The IServiceCollection to add services to.</param>
        /// <param name="cacheOptions">An optional action to configure CacheOptions.</param>
        /// <returns>The IServiceCollection for chaining.</returns>
        public static IServiceCollection AddMemoryWithRedisCache<T>(
            this IServiceCollection services,
            Action<CacheOptions>? cacheOptions = null,
            Action<RedisCacheOptions>? redisCacheOptions = null
        ) where T : class, ICacheService
        {
            services.Configure<CacheOptions>(opts => cacheOptions?.Invoke(opts));
            services.Configure<RedisCacheOptions>(opts => redisCacheOptions?.Invoke(opts));
            services.AddSingleton<ICacheService, T>();

            // used by cache service for internal memory cache fallback (to avoid repeated backing store calls)
            services.AddMemoryCache();

            // add lazy initialised Redis Connection
            services.AddSingleton(serviceProvider =>
            {
                return new Lazy<ConnectionMultiplexer>(() =>
                {
                    var redisCacheOptions = serviceProvider.GetRequiredService<IOptions<RedisCacheOptions>>().Value;
                    var configurationOptions = ConfigurationOptions.Parse($"{redisCacheOptions.Host}:{redisCacheOptions.Port}");
                    configurationOptions.AbortOnConnectFail = false;
                    configurationOptions.Ssl = true;
                    if (!string.IsNullOrWhiteSpace(redisCacheOptions.PrincipalId))
                    {
                        configurationOptions.ConfigureForAzureWithTokenCredentialAsync(redisCacheOptions.PrincipalId, new DefaultAzureCredential()).GetAwaiter().GetResult();
                    }
                    else
                    {
                        configurationOptions.Password = redisCacheOptions.Password;
                    }
                    return ConnectionMultiplexer.Connect(configurationOptions);
                });
            });

            // add cache stack (mem + redis)
            services.AddCacheStack((serviceProvider, stackBuilder) =>
            {
                var redisConnection = serviceProvider.GetRequiredService<Lazy<ConnectionMultiplexer>>().Value;
                stackBuilder
                    // uses local system memory as cache layer
                    .AddMemoryCacheLayer()
                    // uses Redis as distributed cache layer
                    .AddRedisCacheLayer(redisConnection, new RedisCacheLayerOptions(SystemTextJsonCacheSerializer.Instance))
                    // uses Redis for distributed locking (to avoid cache stampedes)
                    .WithRedisDistributedLocking(redisConnection)
                    // ensures cache invalidation is propagated across instances and layers
                    .WithRedisRemoteEviction(redisConnection)
                    // deletes expired data in caches (excluding redis)
                    .WithCleanupFrequency(TimeSpan.FromMinutes(15));
            });

            return services;
        }
    }
}