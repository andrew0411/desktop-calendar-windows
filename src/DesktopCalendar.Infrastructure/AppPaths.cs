namespace DesktopCalendar.Infrastructure;

public sealed class AppPaths
{
    public AppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCalendar");
        DatabasePath = Path.Combine(Root, "calendar.db");
        SettingsPath = Path.Combine(Root, "settings.json");
        WeatherCachePath = Path.Combine(Root, "weather-cache.json");
        BackupsDirectory = Path.Combine(Root, "Backups");
    }

    public string Root { get; }
    public string DatabasePath { get; }
    public string SettingsPath { get; }
    public string WeatherCachePath { get; }
    public string BackupsDirectory { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(BackupsDirectory);
    }
}
