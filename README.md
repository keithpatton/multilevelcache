# Multi-level Cache with CacheTower examples

The MultiLevelCache solution file contains three projects.

## MultiLevelCache - Console Project

This is a basic console project acting as a test harness for CacheTower. 

In particular it demonstrates the ability to use Redis Console to co-ordinate eviction across all application instances using Redis Pub/Sub.

- Update {REDIS_CONN_STRING}} value in Program.cs
- F5 to start, this will add a custom object to cache (CacheTest)
- Follow instructions to execute Redis Console commands to remove the item from cache
- Press any key to see that the item has been automatically removed by Cache Tower

The eviction feature is afforded by using of the .WithRedisRemoteEviction option as part of the builder.

## MultLevelCacheApi - .Net Web Api

A .Net web api sample based on the asp.net core weather controller template. 

There are two api methods which demonstrate the basic functionality:

- GetForecast - Gets/Sets forecasts from/to cache and returns them
- GetForecastWithRefresh - Attempts to evict the forecasts from cache and ensures fresh forecasts are placed in cache and retruned.

This sample shows also:

- Configuration of Redis and Cache Tower in Program.cs with recommended options
- Using of an ICacheService/CacheService to wrap the CacheTower ICacheStack (e.g. allowing for custom key prefixes to be added)
- Use of CacheOptions class to encapsulate the core configuration values (e.g. TimetoLive, StaleAfter, KeyPrefix) injected into CacheService

- Note: You can add the Redis Connection String into the appsettings.json (or user secrets locally by updating as follows):
```
{
  "Cache": {
    "RedisConnectionString": "{REDIS_CONN_STRING}"
  }
}
```

## MultLevelCacheApi.Tests - MS Test Project
This project contains unit and integration tests for the Api

- Unit Tests mock the ICacheService and WeatherForecastController
- Integration Tests use the WebHost directly with all dependencies (e.g. Redis) in place
