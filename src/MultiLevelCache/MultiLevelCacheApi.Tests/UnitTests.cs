
using CacheTower;
using Microsoft.Extensions.Logging;
using Moq;
using MultiLevelCacheApi.Controllers;
using Serko.Cache.MultiLevel.Abstractions;

namespace MultiLevelCacheApi.Tests
{
    [TestClass]
    public class WeatherForecastControllerUnitTests
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private Mock<ICacheService> _mockCacheService;
        private WeatherForecastController _controller;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private readonly string _cacheKey = "weather_forecast";

        private static readonly string[] Summaries = new[]
{
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        [TestInitialize]
        public void SetUp()
        {
            _mockCacheService = new Mock<ICacheService>();
            _controller = new WeatherForecastController(Mock.Of<ILogger<WeatherForecastController>>(), _mockCacheService.Object);

            // Sample forecast data
            var cachedForecasts = FetchForecasts();

            // Set up the cache service behavior
            _mockCacheService.Setup(x => x.GetOrSetAsync(_cacheKey, It.IsAny<Func<IEnumerable<WeatherForecast>, Task<IEnumerable<WeatherForecast>>>>(), It.IsAny<CacheSettings>()))
                             .ReturnsAsync(() => cachedForecasts);

            // Setup to simulate eviction
            _mockCacheService.Setup(x => x.EvictAsync(_cacheKey))
                             .Callback(() => cachedForecasts = FetchForecasts()); // Change the forecast data on eviction
        }

        private IEnumerable<WeatherForecast> FetchForecasts()
        {
            var forecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
            return forecast;
        }

        [TestMethod]
        public async Task GetAsync_ReturnsCachedData_OnSecondCall()
        {
            var firstCall = await _controller.GetAsync();
            var secondCall = await _controller.GetAsync();

            CollectionAssert.AreEqual(firstCall.ToList(), secondCall.ToList());
        }

        [TestMethod]
        public async Task GetWithRefreshAsync_ReturnsNewData_OnEachCall()
        {
            var firstCall = await _controller.GetWithRefreshAsync();
            var secondCall = await _controller.GetWithRefreshAsync();

           
            CollectionAssert.AreNotEqual(firstCall.ToList(), secondCall.ToList());
        }
    }
}