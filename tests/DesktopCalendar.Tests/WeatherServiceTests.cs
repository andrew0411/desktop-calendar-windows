using System.Net;
using System.Text;
using DesktopCalendar.Core.Models;
using DesktopCalendar.Infrastructure;

namespace DesktopCalendar.Tests;

[TestClass]
public sealed class WeatherServiceTests
{
    private string _root = null!;
    private AppPaths _paths = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "DesktopCalendarWeatherTests", Guid.NewGuid().ToString("N"));
        _paths = new AppPaths(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public async Task WeatherService_SearchesLocationAndCachesForecast()
    {
        var handler = new StubHttpHandler(request =>
        {
            var json = request.RequestUri!.Host.StartsWith("geocoding", StringComparison.OrdinalIgnoreCase)
                ? """
                  {"results":[{"name":"시카고","admin1":"일리노이","country":"미국","latitude":41.85,"longitude":-87.65,"timezone":"America/Chicago"}]}
                  """
                : """
                  {"daily":{"time":["2026-08-18","2026-08-19"],"weather_code":[3,61],"temperature_2m_max":[28.4,25.1],"temperature_2m_min":[19.2,18.6]}}
                  """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        var service = new OpenMeteoWeatherService(_paths, new HttpClient(handler));

        var locations = await service.SearchLocationsAsync("Chicago");
        Assert.HasCount(1, locations);
        Assert.AreEqual("시카고, 일리노이, 미국", locations[0].DisplayName);

        var snapshot = await service.GetForecastAsync(locations[0], WeatherTemperatureUnit.Celsius);
        Assert.HasCount(2, snapshot.Daily);
        Assert.AreEqual(new DateOnly(2026, 8, 18), snapshot.Daily[0].Date);
        Assert.AreEqual(3, snapshot.Daily[0].WeatherCode);
        Assert.AreEqual(28.4, snapshot.Daily[0].MaximumTemperature, 0.001);

        await service.SaveCacheAsync(snapshot);
        var cached = await service.LoadCacheAsync();
        Assert.IsNotNull(cached);
        Assert.AreEqual(snapshot.LocationName, cached.LocationName);
        Assert.HasCount(2, cached.Daily);
    }

    [TestMethod]
    public void WeatherPresentation_MapsWeatherCodesToUsefulKoreanText()
    {
        Assert.AreEqual("☀️", WeatherPresentation.GetIcon(0));
        Assert.AreEqual("🌧️", WeatherPresentation.GetIcon(61));
        Assert.AreEqual("비", WeatherPresentation.GetDescription(61));
        Assert.AreEqual("⛈️", WeatherPresentation.GetIcon(95));
    }

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
