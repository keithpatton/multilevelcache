using MultiLevelCacheApi.Middleware;
using Serko.Cache.MultiLevel.Extensions;

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
            builder.Services.AddMemoryWithRedisCache(
                cacheOptions: opts => builder.Configuration.GetSection("Cache").Bind(opts)
            );
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
