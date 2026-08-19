using System.IO;
using System.Windows;
using DesktopCalendar.App.Services;
using DesktopCalendar.App.ViewModels;
using DesktopCalendar.Core.Abstractions;
using DesktopCalendar.Core.Models;
using DesktopCalendar.Core.Services;
using DesktopCalendar.Infrastructure;
using System.Windows.Threading;

namespace DesktopCalendar.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceService? _singleInstance;
    private DesktopHost? _desktopHost;
    private TrayIconService? _tray;
    private StartupService? _startup;
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _viewModel;
    private SettingsWindow? _settingsWindow;
    private IWeatherService? _weatherService;
    private ICurrentLocationService? _currentLocationService;
    private DispatcherTimer? _weatherTimer;
    private bool _exiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.IsFirstInstance)
        {
            _singleInstance.SignalExistingInstance();
            Shutdown();
            return;
        }

        try
        {
            var paths = new AppPaths();
            var repository = new SqliteEventRepository(paths);
            var settingsStore = new JsonSettingsStore(paths);
            var backupService = new JsonBackupService(paths);
            _weatherService = new OpenMeteoWeatherService(paths);
            _currentLocationService = new WindowsLocationService();
            _viewModel = new MainWindowViewModel(repository, settingsStore, new RecurrenceService(), new HolidayService(), backupService, _weatherService);
            await _viewModel.InitializeAsync();
            UiThemeService.Apply(_viewModel.Settings.AppearanceMode);

            _startup = new StartupService();
            if (_viewModel.Settings.StartWithWindows != _startup.IsEnabled)
                await _viewModel.ApplySettingsAsync(_viewModel.Settings with { StartWithWindows = _startup.IsEnabled });

            _desktopHost = new DesktopHost();
            _mainWindow = new MainWindow(_viewModel, _desktopHost);
            MainWindow = _mainWindow;
            _mainWindow.SettingsRequested += (_, _) => OpenSettings();
            _mainWindow.ExitRequested += (_, _) => ExitApplication();
            _mainWindow.LayoutEditingChanged += (_, _) => UpdateTray();
            _mainWindow.Show();
            _weatherTimer = new DispatcherTimer(TimeSpan.FromMinutes(30), DispatcherPriority.Background, WeatherTimer_Tick, Dispatcher);
            _weatherTimer.Start();
            _ = RefreshWeatherAfterStartupAsync();

            _tray = new TrayIconService(
                () => Dispatcher.InvokeAsync(async () => await _mainWindow.GoTodayAsync()),
                () => Dispatcher.InvokeAsync(async () => { await _mainWindow.ToggleLayoutEditingAsync(); UpdateTray(); }),
                () => Dispatcher.Invoke(OpenSettings),
                () => Dispatcher.Invoke(_mainWindow.ReattachDesktop),
                enabled => Dispatcher.InvokeAsync(async () => await SetAutoStartAsync(enabled)),
                () => Dispatcher.Invoke(ExitApplication));
            UpdateTray();
            _singleInstance.Listen(() => Dispatcher.Invoke(() =>
            {
                _mainWindow.EnsureDesktopAttached();
                OpenSettings();
            }));
            if (e.Args.Contains("--settings", StringComparer.OrdinalIgnoreCase))
                _ = Dispatcher.BeginInvoke(OpenSettings);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"바탕화면 캘린더를 시작할 수 없습니다.\n\n{ex.Message}", "시작 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            ExitApplication();
        }
    }

    private async void OpenSettings()
    {
        try
        {
            await OpenSettingsCoreAsync();
        }
        catch (Exception ex)
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCalendar");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "error.log"), $"{DateTimeOffset.Now:O} 설정 창 오류{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            System.Windows.MessageBox.Show($"설정 창을 열 수 없습니다.\n\n{ex.Message}", "설정 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task OpenSettingsCoreAsync()
    {
        if (_viewModel is null || _mainWindow is null || _startup is null || _weatherService is null || _currentLocationService is null)
            return;
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        var original = _viewModel.Settings;
        try
        {
            _settingsWindow = new SettingsWindow(
                original,
                settings =>
                {
                    var placementChanged = _viewModel.Settings.Window != settings.Window;
                    _viewModel.PreviewSettings(settings);
                    UiThemeService.Apply(settings.AppearanceMode);
                    if (placementChanged)
                        _mainWindow.ApplySettingsAndPlacement();
                },
                _viewModel.ExportAsync,
                _viewModel.ImportAsync,
                query => _weatherService.SearchLocationsAsync(query),
                () => _currentLocationService.GetCurrentLocationAsync(),
                weather => _viewModel.RefreshWeatherAsync(weather));
            var closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _settingsWindow.Closed += (_, _) => closed.TrySetResult(true);
            _settingsWindow.Show();
            await closed.Task;
            if (_settingsWindow.AppliedSettings is { } applied)
            {
                _startup.SetEnabled(applied.StartWithWindows);
                var eventFontSizeChanged = Math.Abs(original.Theme.Event.Size - applied.Theme.Event.Size) > 0.001;
                if (!eventFontSizeChanged)
                    _viewModel.RestoreSettingsPreview(applied);
                await _viewModel.ApplySettingsAsync(applied, eventFontSizeChanged);
                await _viewModel.RefreshWeatherIfNeededAsync();
            }
            else if (_settingsWindow.Imported)
            {
                _startup.SetEnabled(_viewModel.Settings.StartWithWindows);
                await _viewModel.RefreshWeatherIfNeededAsync();
            }
            else if (!_settingsWindow.Imported)
            {
                _viewModel.RestoreSettingsPreview(original);
                await _viewModel.RefreshWeatherIfNeededAsync();
            }
            UiThemeService.Apply(_viewModel.Settings.AppearanceMode);
            UpdateTray();
        }
        finally
        {
            _settingsWindow = null;
            _mainWindow.EnsureDesktopAttached();
        }
    }

    private async Task SetAutoStartAsync(bool enabled)
    {
        if (_startup is null || _viewModel is null)
            return;
        try
        {
            _startup.SetEnabled(enabled);
            await _viewModel.ApplySettingsAsync(_viewModel.Settings with { StartWithWindows = enabled });
            UpdateTray();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"자동 실행 설정을 변경하지 못했습니다.\n\n{ex.Message}", "설정 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateTray()
    {
        if (_tray is null || _viewModel is null || _mainWindow is null || _startup is null)
            return;
        _tray.SetLayoutEditing(_mainWindow.IsLayoutEditing);
        _tray.SetAutoStart(_startup.IsEnabled);
    }

    private async Task RefreshWeatherAfterStartupAsync()
    {
        if (_viewModel is null)
            return;
        await _viewModel.RefreshWeatherIfNeededAsync();
    }

    private async void WeatherTimer_Tick(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            await _viewModel.RefreshWeatherIfNeededAsync();
    }

    private void ExitApplication()
    {
        if (_exiting)
            return;
        _exiting = true;
        _weatherTimer?.Stop();
        _settingsWindow?.Close();
        _tray?.Dispose();
        _desktopHost?.Dispose();
        if (_mainWindow is not null)
        {
            _mainWindow.AllowClose();
            _mainWindow.Close();
        }
        _singleInstance?.Dispose();
        Shutdown();
    }
}
