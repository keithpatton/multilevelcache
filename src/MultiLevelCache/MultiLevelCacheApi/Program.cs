using CacheTower.Providers.Redis;
using CacheTower.Serializers.SystemTextJson;
using MultiLevelCacheApi.Abstractions;
using MultiLevelCacheApi.Middleware;
using MultiLevelCacheApi.Options;
using MultiLevelCacheApi.Services;
using Polly;
using StackExchange.Redis;

namespace MultiLevelCacheApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();


            // *** MULTI-LEVEL CACHE CONFIGURATION START ***

            // add lazy initialised Redis Connection
            builder.Services.AddSingleton(serviceProvider =>
            {
                return new Lazy<ConnectionMultiplexer>(() =>
                {
                    var redisConnectionString = builder.Configuration.GetSection("Cache")["RedisConnectionString"];
                    return ConnectionMultiplexer.Connect(redisConnectionString);
                });
            });

            // add cache stack (mem + redis)
            builder.Services.AddCacheStack((serviceProvider, stackBuilder) =>
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


            // cache options class
            builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection("Cache"));

            // facade class around cache stack
            builder.Services.AddSingleton<ICacheService, CacheService>();

            // used by cache service for internal memory cache fallback (to avoid repeated backing store calls)
            builder.Services.AddMemoryCache();

            // *** MULTI-LEVEL CACHE CONFIGURATION END ***

            var app = builder.Build();
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
