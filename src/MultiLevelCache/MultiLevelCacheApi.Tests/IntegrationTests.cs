using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MultiLevelCacheApi;
using Newtonsoft.Json;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Customize services for testing as needed
        });
    }
}

namespace MultiLevelCacheApi.Tests
{ 
    [TestClass]
    public class WeatherForecastControllerIntegrationTests
    {
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

    }

}

