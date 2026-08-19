namespace DesktopCalendar.Core.Models;

public enum WeatherLocationMode
{
    Manual,
    Automatic
}

public enum WeatherTemperatureUnit
{
    Celsius,
    Fahrenheit
}

public sealed record WeatherSettings
{
    public bool IsEnabled { get; init; }
    public WeatherLocationMode LocationMode { get; init; } = WeatherLocationMode.Manual;
    public WeatherTemperatureUnit TemperatureUnit { get; init; } = WeatherTemperatureUnit.Celsius;
    public string SearchText { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public DateTimeOffset? LastUpdatedUtc { get; init; }
    public int RefreshHours { get; init; } = 12;

    public bool HasResolvedLocation => Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180;
}

public sealed record WeatherLocation(
    string Name,
    string Admin1,
    string Country,
    double Latitude,
    double Longitude,
    string TimeZone)
{
    public string DisplayName => string.Join(", ", new[] { Name, Admin1, Country }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.CurrentCultureIgnoreCase));
}

public sealed record DailyWeatherForecast(
    DateOnly Date,
    int WeatherCode,
    double MaximumTemperature,
    double MinimumTemperature);

public sealed record HourlyWeatherForecast(
    DateTime LocalTime,
    int WeatherCode,
    double Temperature,
    double ApparentTemperature,
    double PrecipitationProbability,
    double Precipitation);

public sealed record WeatherSnapshot
{
    public DateTimeOffset UpdatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public string LocationName { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public WeatherTemperatureUnit TemperatureUnit { get; init; } = WeatherTemperatureUnit.Celsius;
    public IReadOnlyList<DailyWeatherForecast> Daily { get; init; } = [];
    public IReadOnlyList<HourlyWeatherForecast> Hourly { get; init; } = [];
}

public static class WeatherPresentation
{
    public static string GetIcon(int code) => code switch
    {
        0 => "☀️",
        1 or 2 => "🌤️",
        3 => "☁️",
        45 or 48 => "🌫️",
        >= 51 and <= 67 => "🌧️",
        >= 71 and <= 77 => "🌨️",
        >= 80 and <= 82 => "🌦️",
        85 or 86 => "🌨️",
        >= 95 and <= 99 => "⛈️",
        _ => "🌡️"
    };

    public static string GetDescription(int code) => code switch
    {
        0 => "맑음",
        1 => "대체로 맑음",
        2 => "구름 조금",
        3 => "흐림",
        45 or 48 => "안개",
        >= 51 and <= 57 => "이슬비",
        >= 61 and <= 67 => "비",
        >= 71 and <= 77 => "눈",
        >= 80 and <= 82 => "소나기",
        85 or 86 => "눈 소나기",
        >= 95 and <= 99 => "뇌우",
        _ => "날씨 정보"
    };

    public static string GetIconColor(int code) => code switch
    {
        0 => "#FFFFD166",
        1 or 2 => "#FFFFC85A",
        3 => "#FFB8C4D6",
        45 or 48 => "#FFA8B1C0",
        >= 51 and <= 67 => "#FF6DAAFF",
        >= 71 and <= 77 => "#FFD9EDFF",
        >= 80 and <= 82 => "#FF73B9FF",
        85 or 86 => "#FFE5F3FF",
        >= 95 and <= 99 => "#FFC69CFF",
        _ => "#FFCAD2E0"
    };

    public static string GetUnitSymbol(WeatherTemperatureUnit unit) =>
        unit == WeatherTemperatureUnit.Fahrenheit ? "℉" : "℃";
}
