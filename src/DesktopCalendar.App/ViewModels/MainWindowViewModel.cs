using System.Collections.ObjectModel;
using DesktopCalendar.Core.Abstractions;
using DesktopCalendar.Core.Models;
using DesktopCalendar.Core.Services;

namespace DesktopCalendar.App.ViewModels;

public sealed class MainWindowViewModel(
    IEventRepository repository,
    ISettingsStore settingsStore,
    IRecurrenceService recurrenceService,
    IHolidayService holidayService,
    IBackupService backupService,
    IWeatherService weatherService) : ObservableObject
{
    private readonly SemaphoreSlim _weatherRefreshLock = new(1, 1);
    private DateOnly _displayMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private AppSettings _settings = new();
    private bool _toolbarVisible;
    private WeatherSnapshot? _weatherSnapshot;
    private string _weatherStatus = string.Empty;
    private bool _isWeatherRefreshing;

    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];
    public string MonthTitle => $"{_displayMonth:yyyy년 M월}";
    public AppSettings Settings
    {
        get => _settings;
        private set
        {
            _settings = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Theme));
            OnPropertyChanged(nameof(WeatherSettings));
            OnPropertyChanged(nameof(IsLayoutLocked));
        }
    }
    public ThemeSettings Theme => Settings.Theme;
    public WeatherSettings WeatherSettings => Settings.Weather;
    public bool IsLayoutLocked => Settings.Window.IsLayoutLocked;
    public string WeatherStatus
    {
        get => _weatherStatus;
        private set => SetProperty(ref _weatherStatus, value);
    }
    public bool IsWeatherRefreshing
    {
        get => _isWeatherRefreshing;
        private set => SetProperty(ref _isWeatherRefreshing, value);
    }
    public bool ToolbarVisible
    {
        get => _toolbarVisible;
        set => SetProperty(ref _toolbarVisible, value);
    }

    public async Task InitializeAsync()
    {
        await repository.InitializeAsync();
        Settings = await settingsStore.LoadAsync();
        _weatherSnapshot = await weatherService.LoadCacheAsync();
        await RefreshAsync();
        await CreateDailyBackupAsync();
    }

    public async Task MoveMonthAsync(int offset)
    {
        _displayMonth = _displayMonth.AddMonths(offset);
        OnPropertyChanged(nameof(MonthTitle));
        await RefreshAsync();
    }

    public async Task GoTodayAsync()
    {
        _displayMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        OnPropertyChanged(nameof(MonthTitle));
        await RefreshAsync();
    }

    public async Task<CalendarEvent> CreateQuickEventAsync(DateOnly date, string title)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        var offset = TimeZoneInfo.Local.GetUtcOffset(local);
        var item = new CalendarEvent
        {
            Title = title.Trim(),
            Start = new DateTimeOffset(local, offset),
            End = new DateTimeOffset(local.AddDays(1).AddTicks(-1), offset),
            IsAllDay = true,
            TimeZoneId = TimeZoneInfo.Local.Id,
            ColorHex = Theme.DefaultEventColorHex
        };
        await repository.UpsertAsync(item);
        await RefreshAsync();
        return item;
    }

    public async Task SaveEventAsync(CalendarEvent item)
    {
        await repository.UpsertAsync(item with { UpdatedUtc = DateTimeOffset.UtcNow });
        await RefreshAsync();
    }

    public async Task DeleteEventAsync(Guid id)
    {
        await repository.DeleteAsync(id);
        await RefreshAsync();
    }

    public async Task ApplySettingsAsync(AppSettings settings)
    {
        Settings = settings;
        ApplyWeatherToDays();
        await settingsStore.SaveAsync(settings);
    }

    public void PreviewSettings(AppSettings settings)
    {
        Settings = settings;
        ApplyWeatherToDays();
    }

    public async Task<WeatherSnapshot> RefreshWeatherAsync(WeatherSettings? weather = null, bool force = true)
    {
        var requested = weather ?? Settings.Weather;
        if (!requested.IsEnabled)
            throw new InvalidOperationException("날씨 표시가 꺼져 있습니다.");
        if (!requested.HasResolvedLocation)
            throw new InvalidOperationException("먼저 날씨 위치를 확인하세요.");

        await _weatherRefreshLock.WaitAsync();
        try
        {
            IsWeatherRefreshing = true;
            if (!force && IsMatchingSnapshot(_weatherSnapshot, requested) && IsFresh(_weatherSnapshot!, requested.RefreshHours))
            {
                ApplyWeatherToDays();
                return _weatherSnapshot!;
            }

            var location = new WeatherLocation(
                string.IsNullOrWhiteSpace(requested.DisplayName) ? "설정 위치" : requested.DisplayName,
                string.Empty,
                string.Empty,
                requested.Latitude!.Value,
                requested.Longitude!.Value,
                "auto");
            var snapshot = await weatherService.GetForecastAsync(location, requested.TemperatureUnit);
            await weatherService.SaveCacheAsync(snapshot);
            _weatherSnapshot = snapshot;
            WeatherStatus = $"{snapshot.LocationName} · {snapshot.UpdatedUtc.ToLocalTime():M월 d일 HH:mm} 업데이트";

            if (weather is null)
            {
                Settings = Settings with { Weather = Settings.Weather with { LastUpdatedUtc = snapshot.UpdatedUtc } };
                await settingsStore.SaveAsync(Settings);
            }
            ApplyWeatherToDays();
            return snapshot;
        }
        finally
        {
            IsWeatherRefreshing = false;
            _weatherRefreshLock.Release();
        }
    }

    public async Task<bool> RefreshWeatherIfNeededAsync(bool force = false)
    {
        if (!Settings.Weather.IsEnabled || !Settings.Weather.HasResolvedLocation)
        {
            ApplyWeatherToDays();
            return false;
        }
        try
        {
            await RefreshWeatherAsync(force: force);
            return true;
        }
        catch (Exception ex)
        {
            WeatherStatus = _weatherSnapshot is null
                ? $"날씨를 불러오지 못했습니다: {ex.Message}"
                : $"마지막 날씨를 표시합니다 · 업데이트 실패: {ex.Message}";
            ApplyWeatherToDays();
            return false;
        }
    }

    public async Task ExportAsync(string path)
    {
        var events = await repository.GetAllAsync();
        await backupService.ExportAsync(path, new BackupBundle { Settings = Settings, Events = events });
    }

    public async Task ImportAsync(string path)
    {
        var beforeEvents = await repository.GetAllAsync();
        await backupService.CreateSafetyBackupAsync(new BackupBundle { Settings = Settings, Events = beforeEvents });
        var bundle = await backupService.ImportAsync(path);
        await repository.ReplaceAllAsync(bundle.Events);
        await ApplySettingsAsync(bundle.Settings);
        await RefreshAsync();
    }

    public async Task SetLayoutLockAsync(bool locked)
    {
        await ApplySettingsAsync(Settings with { Window = Settings.Window with { IsLayoutLocked = locked } });
    }

    public async Task SetWindowPlacementAsync(WindowPlacement placement) =>
        await ApplySettingsAsync(Settings with { Window = placement });

    private async Task RefreshAsync()
    {
        var dates = CalendarGridService.BuildMonth(_displayMonth);
        var startDate = dates[0];
        var endDate = dates[^1].AddDays(1);
        var rangeStart = ToOffset(startDate, TimeOnly.MinValue);
        var rangeEnd = ToOffset(endDate, TimeOnly.MinValue);
        var sources = await repository.GetPotentiallyVisibleAsync(rangeStart, rangeEnd);
        var holidays = holidayService.GetHolidays(startDate, endDate.AddDays(-1))
            .GroupBy(item => item.Date)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var occurrences = sources
            .SelectMany(item => recurrenceService.Expand(item, rangeStart, rangeEnd))
            .OrderBy(item => item.IsAllDay ? 0 : 1)
            .ThenBy(item => item.Start)
            .ThenBy(item => item.Source.CreatedUtc)
            .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        Days.Clear();
        foreach (var date in dates)
        {
            var day = new CalendarDayViewModel(date, date.Month == _displayMonth.Month, date == DateOnly.FromDateTime(DateTime.Today));
            day.SetEvents(occurrences
                .Where(item => DateOnly.FromDateTime(item.Start.LocalDateTime) <= date && DateOnly.FromDateTime(item.End.LocalDateTime) >= date)
                .Select(item => new EventOccurrenceViewModel(item, Theme.Event.Size)));
            if (holidays.TryGetValue(date, out var dayHolidays))
                day.SetHolidays(dayHolidays.Select(item => new HolidayViewModel(item)));
            Days.Add(day);
        }
        ApplyWeatherToDays();
    }

    private void ApplyWeatherToDays()
    {
        var settings = Settings.Weather;
        var canShow = settings.IsEnabled && IsMatchingSnapshot(_weatherSnapshot, settings);
        var forecasts = canShow
            ? _weatherSnapshot!.Daily.ToDictionary(item => item.Date)
            : new Dictionary<DateOnly, DailyWeatherForecast>();
        foreach (var day in Days)
        {
            day.SetWeather(forecasts.TryGetValue(day.Date, out var forecast)
                ? new WeatherDayViewModel(forecast, settings.TemperatureUnit, _weatherSnapshot!.LocationName)
                : null);
        }
    }

    private static bool IsMatchingSnapshot(WeatherSnapshot? snapshot, WeatherSettings settings) =>
        snapshot is not null && settings.HasResolvedLocation &&
        snapshot.TemperatureUnit == settings.TemperatureUnit &&
        Math.Abs(snapshot.Latitude - settings.Latitude!.Value) < 0.02 &&
        Math.Abs(snapshot.Longitude - settings.Longitude!.Value) < 0.02;

    private static bool IsFresh(WeatherSnapshot snapshot, int refreshHours) =>
        DateTimeOffset.UtcNow - snapshot.UpdatedUtc < TimeSpan.FromHours(Math.Clamp(refreshHours, 1, 24));

    private async Task CreateDailyBackupAsync()
    {
        var events = await repository.GetAllAsync();
        await backupService.CreateDailyBackupAsync(new BackupBundle { Settings = Settings, Events = events });
    }

    private static DateTimeOffset ToOffset(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }
}
