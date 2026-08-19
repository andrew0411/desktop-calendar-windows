using DesktopCalendar.Core.Models;

namespace DesktopCalendar.Core.Abstractions;

public interface IEventRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CalendarEvent>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CalendarEvent>> GetPotentiallyVisibleAsync(DateTimeOffset rangeStart, DateTimeOffset rangeEnd, CancellationToken cancellationToken = default);
    Task UpsertAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReplaceAllAsync(IEnumerable<CalendarEvent> events, CancellationToken cancellationToken = default);
}

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public interface IRecurrenceService
{
    IReadOnlyList<EventOccurrence> Expand(CalendarEvent source, DateTimeOffset rangeStart, DateTimeOffset rangeEnd);
}

public interface IHolidayService
{
    IReadOnlyList<PublicHoliday> GetHolidays(DateOnly rangeStart, DateOnly rangeEnd);
}

public interface IWeatherService
{
    Task<IReadOnlyList<WeatherLocation>> SearchLocationsAsync(string query, CancellationToken cancellationToken = default);
    Task<WeatherSnapshot> GetForecastAsync(WeatherLocation location, WeatherTemperatureUnit unit, CancellationToken cancellationToken = default);
    Task<WeatherSnapshot?> LoadCacheAsync(CancellationToken cancellationToken = default);
    Task SaveCacheAsync(WeatherSnapshot snapshot, CancellationToken cancellationToken = default);
}

public interface ICurrentLocationService
{
    Task<WeatherLocation> GetCurrentLocationAsync(CancellationToken cancellationToken = default);
}

public interface IBackupService
{
    Task CreateDailyBackupAsync(BackupBundle bundle, CancellationToken cancellationToken = default);
    Task CreateSafetyBackupAsync(BackupBundle bundle, CancellationToken cancellationToken = default);
    Task ExportAsync(string path, BackupBundle bundle, CancellationToken cancellationToken = default);
    Task<BackupBundle> ImportAsync(string path, CancellationToken cancellationToken = default);
}

public interface IDesktopHost : IDisposable
{
    bool IsAttached { get; }
    event EventHandler<bool>? AttachmentChanged;
    bool Attach(nint windowHandle, int x, int y, int width, int height);
    bool TryGetWindowBounds(out int x, out int y, out int width, out int height);
    void Detach();
    bool IsDesktopAvailable();
}
