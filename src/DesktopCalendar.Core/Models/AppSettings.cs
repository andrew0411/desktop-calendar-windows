namespace DesktopCalendar.Core.Models;

public sealed record WindowPlacement
{
    public string? MonitorDeviceName { get; init; }
    public double Left { get; init; } = 0.50;
    public double Top { get; init; } = 0.05;
    public double Width { get; init; } = 0.48;
    public double Height { get; init; } = 0.88;
    public bool IsLayoutLocked { get; init; } = true;
}

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public string AppearanceMode { get; init; } = "Dark";
    public ThemeSettings Theme { get; init; } = new();
    public WeatherSettings Weather { get; init; } = new();
    public WindowPlacement Window { get; init; } = new();
    public bool StartWithWindows { get; init; }
}

public sealed record BackupBundle
{
    public int SchemaVersion { get; init; } = 1;
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public AppSettings Settings { get; init; } = new();
    public IReadOnlyList<CalendarEvent> Events { get; init; } = [];
}
