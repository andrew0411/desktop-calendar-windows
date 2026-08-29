using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DesktopCalendar.Core.Models;

namespace DesktopCalendar.App.ViewModels;

public sealed class EventOccurrenceViewModel : ObservableObject
{
    private const string CompletedColorHex = "#FF63D6A0";
    private bool _isSelected;
    private double _fontSize;
    private string _colorHex;

    public EventOccurrenceViewModel(EventOccurrence occurrence, double defaultFontSize)
    {
        Occurrence = occurrence;
        _fontSize = occurrence.Source.TitleFontSize > 0 ? occurrence.Source.TitleFontSize : defaultFontSize;
        _colorHex = occurrence.Source.ColorHex;
    }

    public EventOccurrence Occurrence { get; }
    public string DisplayText => Occurrence.IsAllDay ? Occurrence.Title : $"{Occurrence.Start:HH:mm} {Occurrence.Title}";
    public string Emoji => Occurrence.Source.Emoji;
    public bool HasEmoji => !string.IsNullOrWhiteSpace(Emoji);
    public bool IsCompleted => Occurrence.Source.IsCompleted;
    public string ColorHex => IsCompleted ? CompletedColorHex : _colorHex;
    public string BackgroundHex => IsCompleted
        ? WithAlpha(ColorHex, "42")
        : Occurrence.Source.IsHighlighted
            ? EventHighlightPalette.Normalize(Occurrence.Source.HighlightColorHex)
            : WithAlpha(ColorHex, "2B");
    public string ToolTipText
    {
        get
        {
            var location = string.IsNullOrWhiteSpace(Occurrence.Source.Location) ? string.Empty : $"\n장소: {Occurrence.Source.Location}";
            var status = IsCompleted ? "\n✓ 완료한 일정" : string.Empty;
            var emoji = HasEmoji ? $"{Emoji} " : string.Empty;
            return $"{emoji}{DisplayText}{location}{status}";
        }
    }
    public FontWeight FontWeight => Occurrence.Source.IsBold ? FontWeights.Bold : FontWeights.Normal;
    public System.Windows.FontStyle FontStyle => Occurrence.Source.IsItalic ? FontStyles.Italic : FontStyles.Normal;
    public TextDecorationCollection? TextDecorations => Occurrence.Source.IsCompleted ? System.Windows.TextDecorations.Strikethrough : null;
    public double FontSize
    {
        get => _fontSize;
        private set => SetProperty(ref _fontSize, value);
    }
    public string HighlightButtonBackground => Occurrence.Source.IsHighlighted
        ? EventHighlightPalette.Normalize(Occurrence.Source.HighlightColorHex)
        : "#26000000";
    public string CompleteButtonBackground => Occurrence.Source.IsCompleted ? "#E658B881" : "#26000000";
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public void SetFontSize(double fontSize) => FontSize = fontSize;

    public void SetColorHex(string colorHex)
    {
        if (string.Equals(_colorHex, colorHex, StringComparison.OrdinalIgnoreCase))
            return;
        _colorHex = colorHex;
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(BackgroundHex));
    }

    private static string WithAlpha(string color, string alpha)
    {
        var rgb = color.TrimStart('#');
        if (rgb.Length == 8)
            rgb = rgb[2..];
        return rgb.Length == 6 ? $"#{alpha}{rgb}" : "#66000000";
    }
}

public sealed class HolidayViewModel
{
    public HolidayViewModel(PublicHoliday holiday) => Holiday = holiday;
    public PublicHoliday Holiday { get; }
    public string CountryText => Holiday.Country == HolidayCountry.SouthKorea ? "KR" : "US";
    public string Name => Holiday.Name;
    public string DisplayText => $"{CountryText}  {Name}";
    public string BackgroundHex => Holiday.Country == HolidayCountry.SouthKorea ? "#CCDB5864" : "#CC4F7FD9";
}

public sealed class WeatherDayViewModel
{
    private static readonly CultureInfo KoreanCulture = CultureInfo.GetCultureInfo("ko-KR");

    public WeatherDayViewModel(
        DailyWeatherForecast forecast,
        WeatherTemperatureUnit unit,
        string locationName,
        DateTimeOffset updatedUtc,
        IEnumerable<HourlyWeatherForecast> hourly)
    {
        Forecast = forecast;
        Unit = unit;
        LocationName = locationName;
        UpdatedUtc = updatedUtc;
        Hourly = hourly
            .Where(item => DateOnly.FromDateTime(item.LocalTime) == forecast.Date)
            .OrderBy(item => item.LocalTime)
            .Select(item => new WeatherHourViewModel(item, unit))
            .ToArray();
    }

    public DailyWeatherForecast Forecast { get; }
    public WeatherTemperatureUnit Unit { get; }
    public string LocationName { get; }
    public DateTimeOffset UpdatedUtc { get; }
    public IReadOnlyList<WeatherHourViewModel> Hourly { get; }
    public bool HasHourly => Hourly.Count > 0;
    public string Icon => WeatherPresentation.GetIcon(Forecast.WeatherCode);
    public string IconColorHex => WeatherPresentation.GetIconColor(Forecast.WeatherCode);
    public string MaximumText => $"↑{Forecast.MaximumTemperature:0}°";
    public string MinimumText => $"↓{Forecast.MinimumTemperature:0}°";
    public string TemperatureText => $"{Forecast.MaximumTemperature:0}°/{Forecast.MinimumTemperature:0}°";
    public string DisplayText => $"{Icon} {TemperatureText}";
    public string DateTitle => Forecast.Date.ToString("yyyy년 M월 d일 dddd", KoreanCulture);
    public string SummaryText => $"{WeatherPresentation.GetDescription(Forecast.WeatherCode)} · 최고 {Forecast.MaximumTemperature:0.#}{WeatherPresentation.GetUnitSymbol(Unit)} · 최저 {Forecast.MinimumTemperature:0.#}{WeatherPresentation.GetUnitSymbol(Unit)}";
    public string UpdatedText => $"마지막 업데이트: {UpdatedUtc.ToLocalTime():yyyy년 M월 d일 HH:mm}";
    public string ToolTipText =>
        $"{SummaryText}\n" +
        $"{LocationName}\n" +
        UpdatedText;
}

public sealed class WeatherHourViewModel
{
    private static readonly CultureInfo KoreanCulture = CultureInfo.GetCultureInfo("ko-KR");

    public WeatherHourViewModel(HourlyWeatherForecast forecast, WeatherTemperatureUnit unit)
    {
        Forecast = forecast;
        Unit = unit;
    }

    public HourlyWeatherForecast Forecast { get; }
    public WeatherTemperatureUnit Unit { get; }
    public string TimeText => Forecast.LocalTime.ToString("tt h시", KoreanCulture);
    public string Icon => WeatherPresentation.GetIcon(Forecast.WeatherCode);
    public string IconColorHex => WeatherPresentation.GetIconColor(Forecast.WeatherCode);
    public string Description => WeatherPresentation.GetDescription(Forecast.WeatherCode);
    public string TemperatureText => $"{Forecast.Temperature:0.#}°";
    public string ApparentTemperatureText => $"체감 {Forecast.ApparentTemperature:0.#}{WeatherPresentation.GetUnitSymbol(Unit)}";
    public string PrecipitationProbabilityText => $"강수 {Forecast.PrecipitationProbability:0}%";
    public string PrecipitationText => $"{Forecast.Precipitation:0.#} mm";
}

public sealed class CalendarDayViewModel : ObservableObject
{
    private EventOccurrenceViewModel? _selectedEvent;
    private WeatherDayViewModel? _weather;
    private bool _isActionMenuOpen;
    private bool _isHighlightPaletteOpen;
    private bool _isWeatherPopupOpen;
    private bool _isToday;

    public CalendarDayViewModel(DateOnly date, bool belongsToMonth, bool isToday)
    {
        Date = date;
        BelongsToMonth = belongsToMonth;
        _isToday = isToday;
    }

    public DateOnly Date { get; }
    public string DayText => Date.Day.ToString();
    public bool BelongsToMonth { get; }
    public bool IsToday
    {
        get => _isToday;
        private set => SetProperty(ref _isToday, value);
    }
    public bool IsSunday => Date.DayOfWeek == DayOfWeek.Sunday;
    public bool IsSaturday => Date.DayOfWeek == DayOfWeek.Saturday;
    public WeatherDayViewModel? Weather
    {
        get => _weather;
        private set
        {
            if (!SetProperty(ref _weather, value))
                return;
            if (value is null)
                IsWeatherPopupOpen = false;
            OnPropertyChanged(nameof(HasWeather));
        }
    }
    public bool HasWeather => Weather is not null;
    public bool IsWeatherPopupOpen
    {
        get => _isWeatherPopupOpen;
        set => SetProperty(ref _isWeatherPopupOpen, value);
    }
    public ObservableCollection<HolidayViewModel> Holidays { get; } = [];
    public IReadOnlyList<EventOccurrenceViewModel> AllEvents { get; private set; } = [];
    public EventOccurrenceViewModel? SelectedEvent => _selectedEvent;
    public bool HasEvents => AllEvents.Count > 0;
    public bool HasSelectedEvent => _selectedEvent is not null;
    public bool IsActionMenuOpen
    {
        get => _isActionMenuOpen;
        set
        {
            if (!SetProperty(ref _isActionMenuOpen, value))
                return;
            if (!value)
                IsHighlightPaletteOpen = false;
            OnPropertyChanged(nameof(ActionMenuGlyph));
        }
    }
    public bool IsHighlightPaletteOpen
    {
        get => _isHighlightPaletteOpen;
        set => SetProperty(ref _isHighlightPaletteOpen, value);
    }
    public IReadOnlyList<EventHighlightColor> HighlightColors => EventHighlightPalette.Colors;
    public string ActionMenuGlyph => IsActionMenuOpen ? "×" : "+";
    public string SelectedActionLabel => _selectedEvent is not null
        ? _selectedEvent.Occurrence.Title
        : HasEvents ? "상태를 바꿀 일정을 먼저 선택하세요" : "아직 등록된 일정이 없습니다";
    public string HighlightActionLabel => _selectedEvent?.Occurrence.Source.IsHighlighted == true ? "▰  하이라이트 해제" : "▰  하이라이트";
    public string CompleteActionLabel => _selectedEvent?.Occurrence.Source.IsCompleted == true ? "↺  완료 취소" : "✓  완료로 표시";

    public void SetEvents(IEnumerable<EventOccurrenceViewModel> events)
    {
        if (_selectedEvent is not null)
            _selectedEvent.IsSelected = false;
        _selectedEvent = null;
        IsActionMenuOpen = false;
        AllEvents = events.Take(100).ToArray();
        OnPropertyChanged(nameof(AllEvents));
        OnPropertyChanged(nameof(HasEvents));
        NotifySelectionChanged();
    }

    public void SelectEvent(EventOccurrenceViewModel item)
    {
        if (ReferenceEquals(_selectedEvent, item))
            return;
        if (_selectedEvent is not null)
            _selectedEvent.IsSelected = false;
        _selectedEvent = item;
        item.IsSelected = true;
        IsHighlightPaletteOpen = false;
        NotifySelectionChanged();
    }

    public void ToggleActionMenu()
    {
        IsActionMenuOpen = !IsActionMenuOpen;
    }

    public void ToggleHighlightPalette()
    {
        if (HasSelectedEvent)
            IsHighlightPaletteOpen = !IsHighlightPaletteOpen;
    }

    public void ToggleWeatherPopup()
    {
        IsActionMenuOpen = false;
        IsWeatherPopupOpen = !IsWeatherPopupOpen;
    }

    public void SetHolidays(IEnumerable<HolidayViewModel> holidays)
    {
        Holidays.Clear();
        foreach (var holiday in holidays)
            Holidays.Add(holiday);
    }

    public void SetWeather(WeatherDayViewModel? weather) => Weather = weather;

    public void SetIsToday(bool isToday) => IsToday = isToday;

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedEvent));
        OnPropertyChanged(nameof(HasSelectedEvent));
        OnPropertyChanged(nameof(SelectedActionLabel));
        OnPropertyChanged(nameof(HighlightActionLabel));
        OnPropertyChanged(nameof(CompleteActionLabel));
    }
}
