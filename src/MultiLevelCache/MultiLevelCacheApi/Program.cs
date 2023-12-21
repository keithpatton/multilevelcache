using CacheTower.Providers.Redis;
using CacheTower.Serializers.SystemTextJson;
using MultiLevelCacheApi.Abstractions;
using MultiLevelCacheApi.Options;
using MultiLevelCacheApi.Services;
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

            // create redis connection
            var redisConnectionString = builder.Configuration.GetSection("Cache")["RedisConnectionString"];
            var redisConnection = ConnectionMultiplexer.Connect(redisConnectionString);

            // register cache tower
            builder.Services.AddCacheStack(builder => builder
                // uses local system memory as cache layer
                .AddMemoryCacheLayer()
                // uses Redis as distributed cache layer
                .AddRedisCacheLayer(redisConnection, new RedisCacheLayerOptions(SystemTextJsonCacheSerializer.Instance))
                    // ensures cache invalidation is propagated across instances and layers
                .WithRedisDistributedLocking(redisConnection)
                // ensures cache invalidation is propagated across instances and layers
                .WithRedisRemoteEviction(redisConnection)
                // cleans up cache every 7 days across all layers (memory and Redis)
                .WithCleanupFrequency(TimeSpan.FromDays(7))
            );

            // cache options class
            builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection("Cache"));

            // facade class around cache stack
            builder.Services.AddSingleton<ICacheService, CacheService>();


            // *** MULTI-LEVEL CACHE CONFIGURATION END ***

            var app = builder.Build();
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
