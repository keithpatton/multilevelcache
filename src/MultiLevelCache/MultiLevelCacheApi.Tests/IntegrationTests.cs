using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MultiLevelCacheApi;
using MultiLevelCacheApi.Options;
using Newtonsoft.Json;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.Configure<CacheOptions>(options =>
            {
                // reduce time for testing background refresh is working
                options.StaleAfterDefault = MultiLevelCacheApi.Tests.WeatherForecastControllerIntegrationTests.StaleAfterDefault;
            });
        });
    }
}

namespace MultiLevelCacheApi.Tests
{ 
    [TestClass]
    public class WeatherForecastControllerIntegrationTests
    {

        public static TimeSpan StaleAfterDefault = TimeSpan.FromSeconds(5);
        private readonly HttpClient _client;

        public WeatherForecastControllerIntegrationTests()
        {
            var factory = new CustomWebApplicationFactory();
            _client = factory.CreateClient();
        }

        [TestMethod]
        public async Task GetAsync_ReturnsCachedData_OnSecondCall()
        {
            // First call
            var firstResponse = await _client.GetAsync("/WeatherForecast/GetForecast");
            firstResponse.EnsureSuccessStatusCode();
            var firstResponseString = await firstResponse.Content.ReadAsStringAsync();
            var firstForecasts = JsonConvert.DeserializeObject<IEnumerable<WeatherForecast>>(firstResponseString);

            // Second call
            var secondResponse = await _client.GetAsync("/WeatherForecast/GetForecast");
            secondResponse.EnsureSuccessStatusCode();
            var secondResponseString = await secondResponse.Content.ReadAsStringAsync();
            var secondForecasts = JsonConvert.DeserializeObject<IEnumerable<WeatherForecast>>(secondResponseString);

            // Assert
            Assert.AreEqual(JsonConvert.SerializeObject(firstForecasts), JsonConvert.SerializeObject(secondForecasts));
        }

        [TestMethod]
        public async Task GetWithRefreshAsync_ReturnsNewData_OnEachCall()
        {
            // First call
            var firstResponse = await _client.GetAsync("/WeatherForecast/GetForecastWithRefresh");
            firstResponse.EnsureSuccessStatusCode();
            var firstResponseString = await firstResponse.Content.ReadAsStringAsync();
            var firstForecasts = JsonConvert.DeserializeObject<IEnumerable<WeatherForecast>>(firstResponseString);

            // Second call
            var secondResponse = await _client.GetAsync("/WeatherForecast/GetForecastWithRefresh");
            secondResponse.EnsureSuccessStatusCode();
            var secondResponseString = await secondResponse.Content.ReadAsStringAsync();
            var secondForecasts = JsonConvert.DeserializeObject<IEnumerable<WeatherForecast>>(secondResponseString);

            // Assert
            Assert.AreNotEqual(JsonConvert.SerializeObject(firstForecasts), JsonConvert.SerializeObject(secondForecasts));
        }

        [TestMethod]
        public async Task GetAsync_BackgroundRefresh_Success()
        {
            // Initial request to trigger caching
            await _client.GetAsync("/WeatherForecast/GetForecast");

            // Wait for the data to become stale
            await Task.Delay(StaleAfterDefault);

            // Polling loop
            IEnumerable<WeatherForecast> lastForecasts = Enumerable.Empty<WeatherForecast>();
            IEnumerable<WeatherForecast> forecasts = Enumerable.Empty<WeatherForecast>();
            for (int i = 0; i < 60; i++)
            {
                var response = await _client.GetAsync("/WeatherForecast/GetForecast");
                var data = await response.Content.ReadAsStringAsync();
                forecasts = JsonConvert.DeserializeObject<IEnumerable<WeatherForecast>>(data)!;

                if (lastForecasts != null && (JsonConvert.SerializeObject(forecasts) != JsonConvert.SerializeObject(lastForecasts)))
                {
                    // Data has been background refreshed!
                    break;
                }
                lastForecasts = forecasts!;
                await Task.Delay(1000);
            }

            Assert.AreNotEqual(JsonConvert.SerializeObject(lastForecasts), JsonConvert.SerializeObject(forecasts));
        }

    }

}