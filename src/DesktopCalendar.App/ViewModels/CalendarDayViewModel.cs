using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using DesktopCalendar.Core.Models;

namespace DesktopCalendar.App.ViewModels;

public sealed class EventOccurrenceViewModel : ObservableObject
{
    private const string CompletedColorHex = "#FF63D6A0";
    private bool _isSelected;

    public EventOccurrenceViewModel(EventOccurrence occurrence, double defaultFontSize)
    {
        Occurrence = occurrence;
        FontSize = occurrence.Source.TitleFontSize > 0 ? occurrence.Source.TitleFontSize : defaultFontSize;
    }

    public EventOccurrence Occurrence { get; }
    public string DisplayText => Occurrence.IsAllDay ? Occurrence.Title : $"{Occurrence.Start:HH:mm} {Occurrence.Title}";
    public string Emoji => Occurrence.Source.Emoji;
    public bool HasEmoji => !string.IsNullOrWhiteSpace(Emoji);
    public bool IsCompleted => Occurrence.Source.IsCompleted;
    public string ColorHex => IsCompleted ? CompletedColorHex : Occurrence.Source.ColorHex;
    public string BackgroundHex => WithAlpha(ColorHex, IsCompleted ? "42" : Occurrence.Source.IsHighlighted ? "A6" : "2B");
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
    public double FontSize { get; }
    public string HighlightButtonBackground => Occurrence.Source.IsHighlighted ? "#E6E7B84B" : "#26000000";
    public string CompleteButtonBackground => Occurrence.Source.IsCompleted ? "#E658B881" : "#26000000";
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
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
    public WeatherDayViewModel(DailyWeatherForecast forecast, WeatherTemperatureUnit unit, string locationName)
    {
        Forecast = forecast;
        Unit = unit;
        LocationName = locationName;
    }

    public DailyWeatherForecast Forecast { get; }
    public WeatherTemperatureUnit Unit { get; }
    public string LocationName { get; }
    public string Icon => WeatherPresentation.GetIcon(Forecast.WeatherCode);
    public string IconColorHex => WeatherPresentation.GetIconColor(Forecast.WeatherCode);
    public string MaximumText => $"{Forecast.MaximumTemperature:0}°";
    public string MinimumText => $"{Forecast.MinimumTemperature:0}°";
    public string TemperatureText => $"{Forecast.MaximumTemperature:0}°/{Forecast.MinimumTemperature:0}°";
    public string DisplayText => $"{Icon} {TemperatureText}";
    public string ToolTipText => $"{WeatherPresentation.GetDescription(Forecast.WeatherCode)} · 최고 {Forecast.MaximumTemperature:0.#}{WeatherPresentation.GetUnitSymbol(Unit)} · 최저 {Forecast.MinimumTemperature:0.#}{WeatherPresentation.GetUnitSymbol(Unit)}\n{LocationName}";
}

public sealed class CalendarDayViewModel : ObservableObject
{
    private int _maxVisibleEvents = 3;
    private EventOccurrenceViewModel? _selectedEvent;
    private WeatherDayViewModel? _weather;
    private bool _isActionMenuOpen;

    public CalendarDayViewModel(DateOnly date, bool belongsToMonth, bool isToday)
    {
        Date = date;
        BelongsToMonth = belongsToMonth;
        IsToday = isToday;
    }

    public DateOnly Date { get; }
    public string DayText => Date.Day.ToString();
    public bool BelongsToMonth { get; }
    public bool IsToday { get; }
    public bool IsSunday => Date.DayOfWeek == DayOfWeek.Sunday;
    public bool IsSaturday => Date.DayOfWeek == DayOfWeek.Saturday;
    public WeatherDayViewModel? Weather
    {
        get => _weather;
        private set
        {
            if (!SetProperty(ref _weather, value))
                return;
            OnPropertyChanged(nameof(HasWeather));
        }
    }
    public bool HasWeather => Weather is not null;
    public ObservableCollection<EventOccurrenceViewModel> VisibleEvents { get; } = [];
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
            OnPropertyChanged(nameof(ActionMenuGlyph));
        }
    }
    public string ActionMenuGlyph => IsActionMenuOpen ? "×" : "+";
    public string SelectedActionLabel => _selectedEvent is not null
        ? _selectedEvent.Occurrence.Title
        : HasEvents ? "상태를 바꿀 일정을 먼저 선택하세요" : "아직 등록된 일정이 없습니다";
    public string HighlightActionLabel => _selectedEvent?.Occurrence.Source.IsHighlighted == true ? "▰  하이라이트 해제" : "▰  하이라이트";
    public string CompleteActionLabel => _selectedEvent?.Occurrence.Source.IsCompleted == true ? "↺  완료 취소" : "✓  완료로 표시";
    public int HiddenCount => Math.Max(0, AllEvents.Count - VisibleEvents.Count);
    public bool HasHiddenEvents => HiddenCount > 0;
    public string HiddenText => $"+{HiddenCount}개";

    public void SetEvents(IEnumerable<EventOccurrenceViewModel> events)
    {
        if (_selectedEvent is not null)
            _selectedEvent.IsSelected = false;
        _selectedEvent = null;
        IsActionMenuOpen = false;
        AllEvents = events.Take(100).ToArray();
        RefreshVisibleEvents();
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
        NotifySelectionChanged();
    }

    public void ToggleActionMenu()
    {
        IsActionMenuOpen = !IsActionMenuOpen;
    }

    public void SetMaxVisibleEvents(int value)
    {
        value = Math.Clamp(value, 1, 10);
        if (_maxVisibleEvents == value)
            return;
        _maxVisibleEvents = value;
        RefreshVisibleEvents();
    }

    public void SetHolidays(IEnumerable<HolidayViewModel> holidays)
    {
        Holidays.Clear();
        foreach (var holiday in holidays)
            Holidays.Add(holiday);
    }

    public void SetWeather(WeatherDayViewModel? weather) => Weather = weather;

    private void RefreshVisibleEvents()
    {
        VisibleEvents.Clear();
        foreach (var item in AllEvents.Take(_maxVisibleEvents))
            VisibleEvents.Add(item);
        OnPropertyChanged(nameof(HiddenCount));
        OnPropertyChanged(nameof(HasHiddenEvents));
        OnPropertyChanged(nameof(HiddenText));
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedEvent));
        OnPropertyChanged(nameof(HasSelectedEvent));
        OnPropertyChanged(nameof(SelectedActionLabel));
        OnPropertyChanged(nameof(HighlightActionLabel));
        OnPropertyChanged(nameof(CompleteActionLabel));
    }
}
