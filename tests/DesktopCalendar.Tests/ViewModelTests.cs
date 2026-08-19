using DesktopCalendar.App.ViewModels;
using DesktopCalendar.Core.Abstractions;
using DesktopCalendar.Core.Models;
using DesktopCalendar.Core.Services;

namespace DesktopCalendar.Tests;

[TestClass]
public sealed class ViewModelTests
{
    [TestMethod]
    [DataRow("1400", "14:00")]
    [DataRow("1530", "15:30")]
    [DataRow("930", "09:30")]
    [DataRow("9:05", "09:05")]
    [DataRow(" 08:15 ", "08:15")]
    public void TimeInputParser_NormalizesFriendlyTimeFormats(string input, string expected)
    {
        Assert.IsTrue(TimeInputParser.TryNormalize(input, out var normalized));
        Assert.AreEqual(expected, normalized);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("14")]
    [DataRow("2400")]
    [DataRow("1260")]
    [DataRow("noon")]
    public void TimeInputParser_RejectsInvalidTimes(string input)
    {
        Assert.IsFalse(TimeInputParser.TryNormalize(input, out _));
    }

    [TestMethod]
    public async Task MainViewModel_UpdatesTodayWithoutUserAction()
    {
        var viewModel = new MainWindowViewModel(
            new EmptyEventRepository(),
            new MemorySettingsStore(),
            new RecurrenceService(),
            new HolidayService(),
            new EmptyBackupService(),
            new EmptyWeatherService());
        await viewModel.InitializeAsync();
        var originalToday = viewModel.Days.Single(day => day.IsToday).Date;
        var nextDay = originalToday.AddDays(1);

        var changed = await viewModel.RefreshTodayAsync(nextDay);

        Assert.IsTrue(changed);
        Assert.AreEqual(nextDay, viewModel.Days.Single(day => day.IsToday).Date);
        Assert.IsFalse(await viewModel.RefreshTodayAsync(nextDay));
    }

    [TestMethod]
    public async Task MainViewModel_EventFontSizePreviewRestoresAndApplyUpdatesExistingEvents()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = today.ToDateTime(TimeOnly.MinValue);
        var offset = TimeZoneInfo.Local.GetUtcOffset(start);
        var existing = new CalendarEvent
        {
            Title = "기존 일정",
            Start = new DateTimeOffset(start, offset),
            End = new DateTimeOffset(start.AddDays(1).AddTicks(-1), offset),
            TitleFontSize = 16
        };
        var repository = new MemoryEventRepository([existing]);
        var viewModel = new MainWindowViewModel(
            repository,
            new MemorySettingsStore(),
            new RecurrenceService(),
            new HolidayService(),
            new EmptyBackupService(),
            new EmptyWeatherService());
        await viewModel.InitializeAsync();
        var original = viewModel.Settings;
        var applied = original with
        {
            Theme = original.Theme with { Event = original.Theme.Event with { Size = 10 } }
        };

        Assert.AreEqual(16, GetTodayEvent(viewModel).FontSize, 0.001);

        viewModel.PreviewSettings(applied);
        Assert.AreEqual(10, GetTodayEvent(viewModel).FontSize, 0.001);
        Assert.AreEqual(16, repository.Items.Single().TitleFontSize, 0.001);

        viewModel.RestoreSettingsPreview(original);
        Assert.AreEqual(16, GetTodayEvent(viewModel).FontSize, 0.001);

        viewModel.PreviewSettings(applied);
        await viewModel.ApplySettingsAsync(applied, applyEventFontSizeToAll: true);

        Assert.AreEqual(10, repository.Items.Single().TitleFontSize, 0.001);
        Assert.AreEqual(10, GetTodayEvent(viewModel).FontSize, 0.001);
    }

    [TestMethod]
    public async Task MainViewModel_UpgradesOldSettingsAndRepairsExistingEventFontSizesOnce()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = today.ToDateTime(TimeOnly.MinValue);
        var offset = TimeZoneInfo.Local.GetUtcOffset(start);
        var repository = new MemoryEventRepository([
            new CalendarEvent
            {
                Title = "업그레이드 전 일정",
                Start = new DateTimeOffset(start, offset),
                End = new DateTimeOffset(start.AddDays(1).AddTicks(-1), offset),
                TitleFontSize = 16
            }
        ]);
        var oldSettings = new AppSettings
        {
            SchemaVersion = 1,
            Theme = new ThemeSettings
            {
                Event = new FontStyleSetting("맑은 고딕", 10, "#FFF9FAFD")
            }
        };
        var settingsStore = new MemorySettingsStore(oldSettings);
        var viewModel = new MainWindowViewModel(
            repository,
            settingsStore,
            new RecurrenceService(),
            new HolidayService(),
            new EmptyBackupService(),
            new EmptyWeatherService());

        await viewModel.InitializeAsync();

        Assert.AreEqual(AppSettings.CurrentSchemaVersion, viewModel.Settings.SchemaVersion);
        Assert.AreEqual(AppSettings.CurrentSchemaVersion, settingsStore.SavedSettings.SchemaVersion);
        Assert.AreEqual(10, repository.Items.Single().TitleFontSize, 0.001);
        Assert.AreEqual(10, GetTodayEvent(viewModel).FontSize, 0.001);
    }

    [TestMethod]
    public void CalendarDay_AllEventsRemainSelectableWhenMoreThanFourExist()
    {
        var date = new DateOnly(2026, 8, 19);
        var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var events = Enumerable.Range(1, 5)
            .Select(index => new CalendarEvent
            {
                Title = $"일정 {index}",
                Start = start,
                End = start.AddHours(1)
            })
            .Select(item => new EventOccurrenceViewModel(new EventOccurrence(item, item.Start, item.End), 12))
            .ToArray();
        var day = new CalendarDayViewModel(date, true, true);

        day.SetEvents(events);
        day.SelectEvent(events[^1]);

        Assert.HasCount(5, day.AllEvents);
        Assert.AreSame(events[^1], day.SelectedEvent);
        Assert.IsTrue(events[^1].IsSelected);
    }

    [TestMethod]
    public void WeatherDayViewModel_ProvidesPopupHeaderAndHourlyDetails()
    {
        var localTime = new DateTime(2026, 8, 19, 14, 25, 0, DateTimeKind.Unspecified);
        var updated = new DateTimeOffset(localTime, TimeZoneInfo.Local.GetUtcOffset(localTime));
        var viewModel = new WeatherDayViewModel(
            new DailyWeatherForecast(new DateOnly(2026, 8, 19), 3, 28.4, 19.2),
            WeatherTemperatureUnit.Celsius,
            "Chicago",
            updated,
            [
                new HourlyWeatherForecast(new DateTime(2026, 8, 19, 14, 0, 0), 3, 27.1, 28.0, 10, 0),
                new HourlyWeatherForecast(new DateTime(2026, 8, 20, 14, 0, 0), 61, 24.5, 25.0, 80, 1.2)
            ]);

        StringAssert.Contains(viewModel.ToolTipText, "마지막 업데이트: 2026년 8월 19일 14:25");
        Assert.IsTrue(viewModel.HasHourly);
        Assert.HasCount(1, viewModel.Hourly);
        Assert.AreEqual("27.1°", viewModel.Hourly[0].TemperatureText);
        Assert.AreEqual("강수 10%", viewModel.Hourly[0].PrecipitationProbabilityText);
        Assert.AreEqual("0 mm", viewModel.Hourly[0].PrecipitationText);
    }

    private static EventOccurrenceViewModel GetTodayEvent(MainWindowViewModel viewModel) =>
        viewModel.Days.Single(day => day.IsToday).AllEvents.Single();

    private sealed class EmptyEventRepository : IEventRepository
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<CalendarEvent>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CalendarEvent>>([]);
        public Task<IReadOnlyList<CalendarEvent>> GetPotentiallyVisibleAsync(DateTimeOffset rangeStart, DateTimeOffset rangeEnd, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CalendarEvent>>([]);
        public Task UpsertAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReplaceAllAsync(IEnumerable<CalendarEvent> events, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class MemoryEventRepository(IEnumerable<CalendarEvent> items) : IEventRepository
    {
        private List<CalendarEvent> _items = items.ToList();

        public IReadOnlyList<CalendarEvent> Items => _items;
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<CalendarEvent>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CalendarEvent>>(_items.ToArray());
        public Task<IReadOnlyList<CalendarEvent>> GetPotentiallyVisibleAsync(DateTimeOffset rangeStart, DateTimeOffset rangeEnd, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CalendarEvent>>(_items.ToArray());
        public Task UpsertAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(item => item.Id == calendarEvent.Id);
            _items.Add(calendarEvent);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }
        public Task ReplaceAllAsync(IEnumerable<CalendarEvent> events, CancellationToken cancellationToken = default)
        {
            _items = events.ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class MemorySettingsStore(AppSettings? initial = null) : ISettingsStore
    {
        private AppSettings _settings = initial ?? new AppSettings();

        public AppSettings SavedSettings => _settings;
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class EmptyBackupService : IBackupService
    {
        public Task CreateDailyBackupAsync(BackupBundle bundle, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CreateSafetyBackupAsync(BackupBundle bundle, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExportAsync(string path, BackupBundle bundle, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<BackupBundle> ImportAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(new BackupBundle());
    }

    private sealed class EmptyWeatherService : IWeatherService
    {
        public Task<IReadOnlyList<WeatherLocation>> SearchLocationsAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WeatherLocation>>([]);
        public Task<WeatherSnapshot> GetForecastAsync(WeatherLocation location, WeatherTemperatureUnit unit, CancellationToken cancellationToken = default) =>
            Task.FromResult(new WeatherSnapshot());
        public Task<WeatherSnapshot?> LoadCacheAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<WeatherSnapshot?>(null);
        public Task SaveCacheAsync(WeatherSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
