using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopCalendar.App.Controls;
using DesktopCalendar.App.Services;
using DesktopCalendar.Core.Models;
using Forms = System.Windows.Forms;

namespace DesktopCalendar.App;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _original;
    private readonly Action<AppSettings> _preview;
    private readonly Func<string, Task> _export;
    private readonly Func<string, Task> _import;
    private readonly Func<string, Task<IReadOnlyList<WeatherLocation>>> _searchWeatherLocations;
    private readonly Func<Task<WeatherLocation>> _getCurrentLocation;
    private readonly Func<WeatherSettings, Task<WeatherSnapshot>> _refreshWeather;
    private WeatherLocation? _resolvedWeatherLocation;
    private string _resolvedWeatherQuery = string.Empty;
    private DateTimeOffset? _weatherLastUpdatedUtc;
    private bool _weatherBusy;
    private bool _loading = true;

    public SettingsWindow(
        AppSettings settings,
        Action<AppSettings> preview,
        Func<string, Task> export,
        Func<string, Task> import,
        Func<string, Task<IReadOnlyList<WeatherLocation>>> searchWeatherLocations,
        Func<Task<WeatherLocation>> getCurrentLocation,
        Func<WeatherSettings, Task<WeatherSnapshot>> refreshWeather)
    {
        InitializeComponent();
        UiThemeService.RegisterWindow(this);
        _original = settings;
        _preview = preview;
        _export = export;
        _import = import;
        _searchWeatherLocations = searchWeatherLocations;
        _getCurrentLocation = getCurrentLocation;
        _refreshWeather = refreshWeather;

        var installed = Fonts.SystemFontFamilies.Select(item => item.Source).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var defaultFamily = installed.FirstOrDefault(item => item.Equals("맑은 고딕", StringComparison.CurrentCultureIgnoreCase) || item.Equals("Malgun Gothic", StringComparison.OrdinalIgnoreCase))
                            ?? "맑은 고딕";
        var curatedFonts = new[]
        {
            new FontChoice("맑은 고딕 · 기본", defaultFamily),
            new FontChoice("Pretendard", "Pretendard"),
            new FontChoice("Noto Sans KR", "Noto Sans KR"),
            new FontChoice("나눔고딕", "나눔고딕"),
            new FontChoice("Segoe UI", "Segoe UI"),
            new FontChoice("Arial", "Arial"),
            new FontChoice("Calibri", "Calibri"),
            new FontChoice("Georgia", "Georgia"),
            new FontChoice("Times New Roman", "Times New Roman")
        }.Where(item => installed.Contains(item.Family)).ToArray();

        foreach (var combo in new[] { MonthFont, WeekdayFont, DateFont, EventFont })
            combo.ItemsSource = curatedFonts;

        MonitorCombo.ItemsSource = Forms.Screen.AllScreens
            .OrderBy(item => GetDisplayNumber(item.DeviceName))
            .Select(item => new DisplayChoice($"디스플레이{GetDisplayNumber(item.DeviceName)}", item.DeviceName))
            .ToArray();
        LoadSettings(settings);
    }

    public AppSettings? AppliedSettings { get; private set; }
    public bool Imported { get; private set; }

    protected override void OnClosed(EventArgs e)
    {
        if (AppliedSettings is null && !Imported)
            _preview(_original);
        base.OnClosed(e);
    }

    private void LoadSettings(AppSettings settings)
    {
        _loading = true;
        var appearance = UiThemeService.Normalize(settings.AppearanceMode);
        DarkModeRadio.IsChecked = appearance == UiThemeService.Dark;
        LightModeRadio.IsChecked = appearance == UiThemeService.Light;
        MonitorCombo.SelectedValue = settings.Window.MonitorDeviceName ?? Forms.Screen.PrimaryScreen?.DeviceName;
        SetFont(MonthFont, MonthSize, MonthColor, settings.Theme.MonthTitle);
        SetFont(WeekdayFont, WeekdaySize, WeekdayColor, settings.Theme.Weekday);
        SetFont(DateFont, DateSize, DateColor, settings.Theme.Date);
        SetFont(EventFont, EventSize, EventColor, settings.Theme.Event);
        SundayColor.SelectedColor = settings.Theme.SundayColorHex;
        SaturdayColor.SelectedColor = settings.Theme.SaturdayColorHex;
        TodayColor.SelectedColor = settings.Theme.TodayColorHex;
        DefaultEventColor.SelectedColor = settings.Theme.DefaultEventColorHex;
        BackgroundColor.SelectedColor = settings.Theme.BackgroundColorHex;
        GridColor.SelectedColor = settings.Theme.GridColorHex;
        BackgroundOpacity.Value = settings.Theme.BackgroundOpacity;
        GridThickness.Value = settings.Theme.GridThickness;
        WeatherEnabledCheck.IsChecked = settings.Weather.IsEnabled;
        ManualWeatherRadio.IsChecked = settings.Weather.LocationMode == WeatherLocationMode.Manual;
        AutomaticWeatherRadio.IsChecked = settings.Weather.LocationMode == WeatherLocationMode.Automatic;
        CelsiusRadio.IsChecked = settings.Weather.TemperatureUnit == WeatherTemperatureUnit.Celsius;
        FahrenheitRadio.IsChecked = settings.Weather.TemperatureUnit == WeatherTemperatureUnit.Fahrenheit;
        WeatherLocationBox.Text = settings.Weather.SearchText;
        _resolvedWeatherQuery = settings.Weather.SearchText;
        _weatherLastUpdatedUtc = settings.Weather.LastUpdatedUtc;
        _resolvedWeatherLocation = settings.Weather.HasResolvedLocation
            ? new WeatherLocation(
                string.IsNullOrWhiteSpace(settings.Weather.DisplayName) ? "설정 위치" : settings.Weather.DisplayName,
                string.Empty,
                string.Empty,
                settings.Weather.Latitude!.Value,
                settings.Weather.Longitude!.Value,
                "auto")
            : null;
        AutoStartCheck.IsChecked = settings.StartWithWindows;
        UpdateSliderLabels();
        UpdateWeatherPanels();
        UpdateWeatherStatus();
        _loading = false;
    }

    private bool TryBuild(out AppSettings settings)
    {
        settings = _original;
        ValidationText.Text = string.Empty;
        if (!TryFont(MonthFont, MonthSize, MonthColor, out var month) ||
            !TryFont(WeekdayFont, WeekdaySize, WeekdayColor, out var weekday) ||
            !TryFont(DateFont, DateSize, DateColor, out var date) ||
            !TryFont(EventFont, EventSize, EventColor, out var calendarEvent))
        {
            ValidationText.Text = "글자 크기는 8~48 사이의 숫자로 입력하세요.";
            return false;
        }

        var weatherEnabled = WeatherEnabledCheck.IsChecked == true;
        if (weatherEnabled && _resolvedWeatherLocation is null)
        {
            ValidationText.Text = AutomaticWeatherRadio.IsChecked == true
                ? "현재 위치 확인 버튼을 눌러 날씨 위치를 확인하세요."
                : "도시를 입력하고 위치 찾기 버튼을 눌러 위치를 선택하세요.";
            return false;
        }

        var weather = _original.Weather with
        {
            IsEnabled = weatherEnabled,
            LocationMode = AutomaticWeatherRadio.IsChecked == true ? WeatherLocationMode.Automatic : WeatherLocationMode.Manual,
            TemperatureUnit = FahrenheitRadio.IsChecked == true ? WeatherTemperatureUnit.Fahrenheit : WeatherTemperatureUnit.Celsius,
            SearchText = WeatherLocationBox.Text.Trim(),
            DisplayName = _resolvedWeatherLocation?.DisplayName ?? _original.Weather.DisplayName,
            Latitude = _resolvedWeatherLocation?.Latitude ?? _original.Weather.Latitude,
            Longitude = _resolvedWeatherLocation?.Longitude ?? _original.Weather.Longitude,
            LastUpdatedUtc = _weatherLastUpdatedUtc,
            RefreshHours = 12
        };

        settings = _original with
        {
            AppearanceMode = LightModeRadio.IsChecked == true ? UiThemeService.Light : UiThemeService.Dark,
            Theme = new ThemeSettings
            {
                MonthTitle = month,
                Weekday = weekday,
                Date = date,
                Event = calendarEvent,
                SundayColorHex = SundayColor.SelectedColor,
                SaturdayColorHex = SaturdayColor.SelectedColor,
                TodayColorHex = TodayColor.SelectedColor,
                DefaultEventColorHex = DefaultEventColor.SelectedColor,
                BackgroundColorHex = BackgroundColor.SelectedColor,
                BackgroundOpacity = BackgroundOpacity.Value,
                GridColorHex = GridColor.SelectedColor,
                GridThickness = GridThickness.Value
            },
            Weather = weather,
            Window = _original.Window with { MonitorDeviceName = MonitorCombo.SelectedValue?.ToString() },
            StartWithWindows = AutoStartCheck.IsChecked == true
        };
        return true;
    }

    private void AnyChanged(object sender, RoutedEventArgs e)
    {
        UpdateSliderLabels();
        if (!_loading && TryBuild(out var settings))
            _preview(settings);
    }

    private void AnyChanged(object sender, SelectionChangedEventArgs e) => AnyChanged(sender, (RoutedEventArgs)e);
    private void AnyChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => AnyChanged(sender, (RoutedEventArgs)e);
    private void AnyChanged(object sender, TextChangedEventArgs e) => AnyChanged(sender, (RoutedEventArgs)e);
    private void ColorChanged(object sender, RoutedPropertyChangedEventArgs<string> e) => AnyChanged(sender, e);

    private void WeatherControlChanged(object sender, RoutedEventArgs e)
    {
        UpdateWeatherPanels();
        AnyChanged(sender, e);
    }

    private void WeatherModeChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            _resolvedWeatherLocation = null;
            _weatherLastUpdatedUtc = null;
        }
        UpdateWeatherPanels();
        AnyChanged(sender, e);
    }

    private void WeatherLocation_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading)
            return;
        var query = WeatherLocationBox.Text.Trim();
        if (!query.Equals(_resolvedWeatherQuery, StringComparison.CurrentCultureIgnoreCase))
        {
            _resolvedWeatherLocation = null;
            _weatherLastUpdatedUtc = null;
            WeatherLocationResults.Visibility = Visibility.Collapsed;
            WeatherStatusText.Text = "입력한 위치를 확인해 주세요.";
        }
        AnyChanged(sender, e);
    }

    private async void SearchWeather_Click(object sender, RoutedEventArgs e)
    {
        var query = WeatherLocationBox.Text.Trim();
        if (query.Length < 2)
        {
            WeatherStatusText.Text = "도시명이나 우편번호를 두 글자 이상 입력하세요.";
            return;
        }
        try
        {
            SetWeatherBusy(true);
            WeatherStatusText.Text = "위치를 찾는 중…";
            var results = await _searchWeatherLocations(query);
            WeatherLocationResults.ItemsSource = results;
            WeatherLocationResults.Visibility = results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (results.Count == 0)
            {
                _resolvedWeatherLocation = null;
                WeatherStatusText.Text = "일치하는 위치를 찾지 못했습니다. 도시와 국가를 함께 입력해 보세요.";
                return;
            }
            WeatherLocationResults.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            WeatherStatusText.Text = $"위치 검색 실패: {ex.Message}";
        }
        finally
        {
            SetWeatherBusy(false);
        }
    }

    private void WeatherLocationResult_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (WeatherLocationResults.SelectedItem is not WeatherLocation location)
            return;
        _resolvedWeatherLocation = location;
        _resolvedWeatherQuery = WeatherLocationBox.Text.Trim();
        _weatherLastUpdatedUtc = null;
        WeatherStatusText.Text = $"확인됨: {location.DisplayName}";
        if (!_loading && TryBuild(out var settings))
            _preview(settings);
    }

    private async void DetectWeatherLocation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetWeatherBusy(true);
            WeatherStatusText.Text = "Windows에서 현재 위치를 확인하는 중…";
            _resolvedWeatherLocation = await _getCurrentLocation();
            _resolvedWeatherQuery = string.Empty;
            _weatherLastUpdatedUtc = null;
            WeatherStatusText.Text = $"현재 위치 확인됨 · 위도 {_resolvedWeatherLocation.Latitude:0.###}, 경도 {_resolvedWeatherLocation.Longitude:0.###}";
            if (TryBuild(out var settings))
                _preview(settings);
        }
        catch (Exception ex)
        {
            _resolvedWeatherLocation = null;
            WeatherStatusText.Text = ex.Message;
        }
        finally
        {
            SetWeatherBusy(false);
        }
    }

    private async void RefreshWeather_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuild(out var settings))
            return;
        if (!settings.Weather.IsEnabled)
        {
            WeatherStatusText.Text = "먼저 날씨 표시를 켜세요.";
            return;
        }
        try
        {
            SetWeatherBusy(true);
            WeatherStatusText.Text = "날씨를 업데이트하는 중…";
            _preview(settings);
            var snapshot = await _refreshWeather(settings.Weather);
            _weatherLastUpdatedUtc = snapshot.UpdatedUtc;
            WeatherStatusText.Text = $"{snapshot.LocationName} · {snapshot.UpdatedUtc.ToLocalTime():M월 d일 HH:mm} 업데이트";
            if (TryBuild(out var refreshedSettings))
                _preview(refreshedSettings);
        }
        catch (Exception ex)
        {
            WeatherStatusText.Text = $"날씨 업데이트 실패: {ex.Message}";
        }
        finally
        {
            SetWeatherBusy(false);
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuild(out var settings))
            return;
        AppliedSettings = settings;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private void Reset_Click(object sender, RoutedEventArgs e) =>
        LoadSettings(_original with { AppearanceMode = UiThemeService.Dark, Theme = new ThemeSettings() });

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Desktop Calendar 백업 (*.json)|*.json",
            FileName = $"desktop-calendar-backup-{DateTime.Today:yyyy-MM-dd}.json"
        };
        if (dialog.ShowDialog(this) != true)
            return;
        try { await _export(dialog.FileName); ValidationText.Text = "백업을 내보냈습니다."; }
        catch (Exception ex) { ValidationText.Text = $"백업 실패: {ex.Message}"; }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Desktop Calendar 백업 (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true)
            return;
        if (System.Windows.MessageBox.Show(this, "현재 일정과 설정을 백업 파일의 내용으로 교체할까요?", "백업 복원", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            await _import(dialog.FileName);
            Imported = true;
            Close();
        }
        catch (Exception ex) { ValidationText.Text = $"복원 실패: {ex.Message}"; }
    }

    private void UpdateSliderLabels()
    {
        if (OpacityValueText is not null)
            OpacityValueText.Text = $"{BackgroundOpacity.Value:P0}";
        if (ThicknessValueText is not null)
            ThicknessValueText.Text = $"{GridThickness.Value:0.##} px";
    }

    private void UpdateWeatherPanels()
    {
        if (WeatherOptionsPanel is null)
            return;
        var enabled = WeatherEnabledCheck.IsChecked == true;
        WeatherOptionsPanel.IsEnabled = enabled && !_weatherBusy;
        ManualWeatherPanel.Visibility = ManualWeatherRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        AutomaticWeatherPanel.Visibility = AutomaticWeatherRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateWeatherStatus()
    {
        if (_weatherLastUpdatedUtc is { } updated)
            WeatherStatusText.Text = $"{_resolvedWeatherLocation?.DisplayName ?? "설정 위치"} · {updated.ToLocalTime():M월 d일 HH:mm} 업데이트";
        else if (_resolvedWeatherLocation is not null)
            WeatherStatusText.Text = $"확인됨: {_resolvedWeatherLocation.DisplayName}";
        else
            WeatherStatusText.Text = "위치를 설정하면 예보를 불러올 수 있습니다.";
    }

    private void SetWeatherBusy(bool busy)
    {
        _weatherBusy = busy;
        UpdateWeatherPanels();
    }

    private static void SetFont(System.Windows.Controls.ComboBox combo, System.Windows.Controls.TextBox size, PaletteColorPicker color, FontStyleSetting value)
    {
        var desiredFamily = value.Family.Equals("맑은 고딕", StringComparison.CurrentCultureIgnoreCase)
            ? combo.Items.OfType<FontChoice>().FirstOrDefault()?.Family
            : value.Family;
        combo.SelectedValue = desiredFamily;
        if (combo.SelectedIndex < 0)
            combo.SelectedIndex = 0;
        size.Text = value.Size.ToString("0.#");
        color.SelectedColor = value.ColorHex;
    }

    private static bool TryFont(System.Windows.Controls.ComboBox combo, System.Windows.Controls.TextBox size, PaletteColorPicker color, out FontStyleSetting setting)
    {
        setting = new FontStyleSetting("맑은 고딕", 12, "#FFFFFFFF");
        if (combo.SelectedValue is not string family || !double.TryParse(size.Text, out var pointSize) || pointSize is < 8 or > 48)
            return false;
        setting = new FontStyleSetting(family, pointSize, color.SelectedColor);
        return true;
    }

    private static int GetDisplayNumber(string deviceName)
    {
        var digits = new string(deviceName.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out var number) ? number : 1;
    }

    private sealed record FontChoice(string Label, string Family);
    private sealed record DisplayChoice(string Label, string DeviceName);
}
