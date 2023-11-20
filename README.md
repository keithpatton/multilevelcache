# Multi-level Cache with CacheTower examples

The MultiLevelCache solution file contains three projects.

## [MultiLevelCache - Console Project](https://github.com/keithpatton/multilevelcache/blob/main/src/MultiLevelCache/MultiLevelCache/Program.cs)

This is a basic console project acting as a test harness for CacheTower. 

In particular it demonstrates the ability to use Redis Console to co-ordinate eviction across all application instances.

- Update {REDIS_CONN_STRING}} value in [Program.cs](https://github.com/keithpatton/multilevelcache/blob/main/src/MultiLevelCache/MultiLevelCache/Program.cs)
- F5 to start, this will add a custom object to cache (CacheTest)
- Follow instructions to execute Redis Console commands to remove the item from cache
- Press any key to see that the item has been automatically removed by Cache Tower

The eviction feature is afforded by using of the .WithRedisRemoteEviction option as part of the builder.

## [MultLevelCacheApi - .Net Web Api](https://github.com/keithpatton/multilevelcache/tree/main/src/MultiLevelCache/MultiLevelCacheApi)

A .Net web api sample based on the asp.net core weather controller template. 

There are two [api methods](https://github.com/keithpatton/multilevelcache/blob/main/src/MultiLevelCache/MultiLevelCacheApi/Controllers/WeatherForecastController.cs) which demonstrate the basic functionality:

- GetForecast - Gets/Sets forecasts from/to cache and returns them
- GetForecastWithRefresh - Attempts to evict the forecasts from cache and ensures fresh forecasts are placed in cache and returned.

This sample shows also:

- Configuration of Redis and Cache Tower in [Program.cs](https://github.com/keithpatton/multilevelcache/blob/main/src/MultiLevelCache/MultiLevelCacheApi/Program.cs) with recommended options
- Using of an [ICacheService](https://github.com/keithpatton/multilevelcache/blob/main/src/MultiLevelCache/MultiLevelCacheApi/Abstractions/ICacheService.cs)/[CacheService](https://github.com/keithpatton/multilevelcache/blob/main/src/MultiLevelCache/MultiLevelCacheApi/Services/CacheService.cs) to wrap the CacheTower ICacheStack (e.g. allowing for custom key prefixes to be added)
- Polly Retry Policy added to CacheService to improve resiliency for Redis Cache failures
- Use of [CacheOptions](https://github.com/keithpatton/multilevelcache/blob/main/src/MultiLevelCache/MultiLevelCacheApi/Options/CacheOptions.cs) class to encapsulate the core configuration values (e.g. TimetoLive, StaleAfter, KeyPrefix) injected into CacheService

- Note: You can add the Redis Connection String into the [appsettings.json](https://github.com/keithpatton/multilevelcache/blob/main/src/MultiLevelCache/MultiLevelCacheApi/appsettings.json) (or user secrets locally by updating as follows):
```
{
  "Cache": {
    "RedisConnectionString": "{REDIS_CONN_STRING}"
  }
}
```

## [MultLevelCacheApi.Tests - MS Test Project](https://github.com/keithpatton/multilevelcache/tree/main/src/MultiLevelCache/MultiLevelCacheApi.Tests)
This project contains unit and integration tests for the Api

- [Unit Tests](https://github.com/keithpatton/multilevelcache/blob/main/src/MultiLevelCache/MultiLevelCacheApi.Tests/UnitTests.cs) mock the ICacheService and WeatherForecastController
- [Integration Tests](https://github.com/keithpatton/multilevelcache/blob/main/src/MultiLevelCache/MultiLevelCacheApi.Tests/IntegrationTests.cs) use the WebHost directly with all dependencies (e.g. Redis) in place
