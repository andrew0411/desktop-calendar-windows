using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopCalendar.Core.Abstractions;
using DesktopCalendar.Core.Models;

namespace DesktopCalendar.Infrastructure;

public sealed class OpenMeteoWeatherService : IWeatherService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly AppPaths _paths;
    private readonly HttpClient _httpClient;

    public OpenMeteoWeatherService(AppPaths paths, HttpClient? httpClient = null)
    {
        _paths = paths;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<IReadOnlyList<WeatherLocation>> SearchLocationsAsync(string query, CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        if (query.Length < 2)
            return [];

        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query)}&count=8&language=ko&format=json";
        var response = await _httpClient.GetFromJsonAsync<GeocodingResponse>(url, JsonOptions, cancellationToken);
        return response?.Results?
            .Where(item => item.Latitude is >= -90 and <= 90 && item.Longitude is >= -180 and <= 180)
            .Select(item => new WeatherLocation(
                item.Name ?? query,
                item.Admin1 ?? string.Empty,
                item.Country ?? string.Empty,
                item.Latitude,
                item.Longitude,
                item.TimeZone ?? "auto"))
            .ToArray() ?? [];
    }

    public async Task<WeatherSnapshot> GetForecastAsync(
        WeatherLocation location,
        WeatherTemperatureUnit unit,
        CancellationToken cancellationToken = default)
    {
        var latitude = location.Latitude.ToString("0.####", CultureInfo.InvariantCulture);
        var longitude = location.Longitude.ToString("0.####", CultureInfo.InvariantCulture);
        var apiUnit = unit == WeatherTemperatureUnit.Fahrenheit ? "fahrenheit" : "celsius";
        var url = "https://api.open-meteo.com/v1/forecast" +
                  $"?latitude={latitude}&longitude={longitude}" +
                  "&daily=weather_code,temperature_2m_max,temperature_2m_min" +
                  $"&temperature_unit={apiUnit}&timezone=auto&forecast_days=16";

        var response = await _httpClient.GetFromJsonAsync<ForecastResponse>(url, JsonOptions, cancellationToken)
                       ?? throw new InvalidDataException("날씨 서버에서 빈 응답을 받았습니다.");
        var daily = response.Daily ?? throw new InvalidDataException("일별 날씨 데이터가 없습니다.");
        var count = new[] { daily.Time?.Length ?? 0, daily.WeatherCode?.Length ?? 0, daily.Maximum?.Length ?? 0, daily.Minimum?.Length ?? 0 }.Min();
        if (count == 0)
            throw new InvalidDataException("표시할 수 있는 날씨 예보가 없습니다.");

        var forecasts = new List<DailyWeatherForecast>(count);
        for (var index = 0; index < count; index++)
        {
            if (DateOnly.TryParseExact(daily.Time![index], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                forecasts.Add(new DailyWeatherForecast(date, daily.WeatherCode![index], daily.Maximum![index], daily.Minimum![index]));
        }

        return new WeatherSnapshot
        {
            UpdatedUtc = DateTimeOffset.UtcNow,
            LocationName = location.DisplayName,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            TemperatureUnit = unit,
            Daily = forecasts
        };
    }

    public async Task<WeatherSnapshot?> LoadCacheAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();
        if (!File.Exists(_paths.WeatherCachePath))
            return null;
        try
        {
            await using var stream = File.OpenRead(_paths.WeatherCachePath);
            return await JsonSerializer.DeserializeAsync<WeatherSnapshot>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public async Task SaveCacheAsync(WeatherSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();
        var temporaryPath = $"{_paths.WeatherCachePath}.tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        File.Move(temporaryPath, _paths.WeatherCachePath, overwrite: true);
    }

    private sealed class GeocodingResponse
    {
        [JsonPropertyName("results")]
        public GeocodingResult[]? Results { get; init; }
    }

    private sealed class GeocodingResult
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
        [JsonPropertyName("admin1")]
        public string? Admin1 { get; init; }
        [JsonPropertyName("country")]
        public string? Country { get; init; }
        [JsonPropertyName("latitude")]
        public double Latitude { get; init; }
        [JsonPropertyName("longitude")]
        public double Longitude { get; init; }
        [JsonPropertyName("timezone")]
        public string? TimeZone { get; init; }
    }

    private sealed class ForecastResponse
    {
        [JsonPropertyName("daily")]
        public DailyResponse? Daily { get; init; }
    }

    private sealed class DailyResponse
    {
        [JsonPropertyName("time")]
        public string[]? Time { get; init; }
        [JsonPropertyName("weather_code")]
        public int[]? WeatherCode { get; init; }
        [JsonPropertyName("temperature_2m_max")]
        public double[]? Maximum { get; init; }
        [JsonPropertyName("temperature_2m_min")]
        public double[]? Minimum { get; init; }
    }
}
