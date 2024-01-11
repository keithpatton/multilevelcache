Introduction
-
*Serko.Cache.MultiLevel* is a .NET library affording a multi-level cache based on [CacheTower](https://github.com/TurnerSoftware/CacheTower).

It provides a highly performant approach to caching using Memory and Redis Cache including additional resiliency.


Distributed Caching Guidelines
-

Please see [Distributed Caching](https://serko.atlassian.net/wiki/spaces/PFI/pages/3768320264/Distributed+Caching) provides for additional detail on Distributed Caching within Eos.


Installation
-
Install with NuGet.
```powershell
Install-Package Serko.Cache.MultiLevel
```
Or via `dotnet` cli.
```bash
dotnet add package Serko.Cache.MultiLevel
```

Prerequisites
-
- Azure Redis Cache instance available
- Existing .Net app from which you would like to employ caching support

Setting up 
-
Use the AddMemoryWithRedisCache extension method to set up the services in your .NET application's Program.cs:

```
    builder.Services.AddMemoryWithRedisCache(
        cacheOptions: opts => builder.Configuration.GetSection("CacheOptions").Bind(opts),
        redisCacheOptions: opts => builder.Configuration.GetSection("RedisCacheOptions").Bind(opts)
    );
```

Configuration
-

Below is an example of how to structure configuration settings to bind to the Options required by the library:

For {SECRET} values, secrets.json can be used locally and be replaced from Key Vault or equivalent in other environments.

```
  "CacheOptions": {
    "KeyPrefix": "KeyPrefix_",
    "TimeToLiveDefault": "01:00:00", // 1 hour
    "StaleAfterDefault": "00:30:00", // 30 minutes
    "StoreBufferDefault": "01:00:00" // 1 hour
  }
  "RedisCacheOptions": {
    "Host": "cachetest123.redis.cache.windows.net",
    "Port": "6380",
    "Password": "{SECRET}",
    "PrincipalId": "",
    "RetryCount": 3,
    "RetryDelay": "00:00:02", // exp back off after this
    "CircuitBreakerFailureThreshold": 0.1,
    "CircuitBreakerSamplingDuration": "00:15:00",
    "CircuitBreakerDurationOfBreak": "00:05:00",
    "CircuitBreakerMinimumThroughput": 100
  }
```

Implementation Guidance
--

After setting things up within app startup, you can then injest and use ICacheService.

The example controller code below shows how to use the CacheService.GetOrSetAsync method to retrieve value from cache or from backing store.

```
using Microsoft.AspNetCore.Mvc;
using Serko.Cache.MultiLevel.Abstractions;

namespace MultiLevelCacheApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly ICacheService _cacheService;

        private const string _cacheKey = "WeatherForecastService";

        public WeatherForecastController(ILogger<WeatherForecastController> logger, ICacheService cacheService)
        {
            _logger = logger;
            _cacheService = cacheService;
        }

        [HttpGet("GetForecast")]
        public async Task<IEnumerable<WeatherForecast>> GetAsync()
        {
            return await GetOrSetForecastCache();
        }

        /// <summary>
        /// ensures forecast is retrieved from cache, fetching if necessary
        /// </summary>
        private async Task<IEnumerable<WeatherForecast>> GetOrSetForecastCache()
        {
            var weatherForecast = await _cacheService.GetOrSetAsync<IEnumerable<WeatherForecast>>(
                cacheKey: _cacheKey,
                valueFactory: async (oldForecast) => { return await FetchForecastAsync(oldForecast); });

            return weatherForecast;
        }

        /// <summary>
        /// fetches forecasts from underlying store (e.g. database or api call)
        /// </summary>
        private async Task<IEnumerable<WeatherForecast>> FetchForecastAsync(IEnumerable<WeatherForecast> oldWeatherForecast)
        {
            // Simulating a delay, e.g., for a database call or external API request
            // Note: This method can be called on a background thread when refreshing so carefully consider thread safety/context
            await Task.Delay(500);

            var forecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();

            return forecast;
        }
    }
}


```

Application Security
--
It is recommended to make use of managed identity (passwordless) connection strings where possible. 

For Azure Redis Cache, in the RedisCacheOptions options, supply the application's PrincipalId to make use of managed identity (otherwise you need to supply the Password).
