using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopCalendar.Core.Models;
using DesktopCalendar.Core.Services;
using DesktopCalendar.App.Services;
using DesktopCalendar.App.ViewModels;

namespace DesktopCalendar.App;

public partial class EventEditorWindow : Window
{
    private readonly CalendarEvent _original;
    private readonly IReadOnlyList<EventEmojiOption> _emojiOptions = EventEmojiCatalog.CreateOptions();
    private double _titleFontSize;
    private string _selectedEmoji = string.Empty;

    public EventEditorWindow(CalendarEvent original, ThemeSettings theme, bool isNew = false)
    {
        InitializeComponent();
        UiThemeService.RegisterWindow(this);
        _original = original;
        _titleFontSize = Math.Clamp(original.TitleFontSize > 0 ? original.TitleFontSize : theme.Event.Size, 8, 40);

        Title = isNew ? "일정 추가" : "일정 편집";
        EditorHeading.Text = Title;
        DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
        TitleBox.Text = original.Title;
        EmojiCategoryCombo.ItemsSource = EventEmojiCatalog.Categories;
        _selectedEmoji = original.Emoji?.Trim() ?? string.Empty;
        UpdateEmojiSelection();
        EmojiCategoryCombo.SelectedItem = _emojiOptions.FirstOrDefault(item => item.Emoji == _selectedEmoji)?.Category
                                          ?? "추천";
        NotesBox.Text = original.Notes;
        LocationBox.Text = original.Location;
        StartDatePicker.SelectedDate = original.Start.LocalDateTime.Date;
        EndDatePicker.SelectedDate = original.End.LocalDateTime.Date;
        AllDayCheckBox.IsChecked = original.IsAllDay;
        StartTimeBox.Text = original.Start.ToString("HH:mm");
        EndTimeBox.Text = original.End.ToString("HH:mm");
        EventColorPicker.SelectedColor = string.IsNullOrWhiteSpace(original.ColorHex) ? theme.DefaultEventColorHex : original.ColorHex;
        BoldToggle.IsChecked = original.IsBold;
        ItalicToggle.IsChecked = original.IsItalic;
        LoadRecurrence(original.RecurrenceRule);
        UpdateTimePanel();
        UpdateTitlePreview();
        Loaded += (_, _) =>
        {
            TitleBox.Focus();
            Keyboard.Focus(TitleBox);
            TitleBox.CaretIndex = TitleBox.Text.Length;
        };
    }

    public CalendarEvent? Result { get; private set; }
    public bool DeleteRequested { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            ValidationText.Text = "제목을 입력하세요.";
            TitleBox.Focus();
            return;
        }
        if (StartDatePicker.SelectedDate is null || EndDatePicker.SelectedDate is null)
        {
            ValidationText.Text = "시작일과 종료일을 선택하세요.";
            return;
        }

        var allDay = AllDayCheckBox.IsChecked == true;
        if (!TryCreateDateTime(StartDatePicker.SelectedDate.Value, StartTimeBox.Text, allDay, isEnd: false, out var start) ||
            !TryCreateDateTime(EndDatePicker.SelectedDate.Value, EndTimeBox.Text, allDay, isEnd: true, out var end))
        {
            ValidationText.Text = "시간을 HH:mm 형식으로 입력하세요.";
            return;
        }
        if (end < start)
        {
            ValidationText.Text = "종료 시각은 시작 시각보다 늦어야 합니다.";
            return;
        }

        string? rule = null;
        var frequency = (FrequencyCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (!string.IsNullOrWhiteSpace(frequency))
        {
            if (!int.TryParse(IntervalBox.Text, out var interval) || interval < 1 || interval > 999)
            {
                ValidationText.Text = "반복 간격은 1~999 사이의 숫자여야 합니다.";
                return;
            }
            rule = RecurrenceService.BuildRule(frequency, interval,
                UntilDatePicker.SelectedDate is DateTime until ? DateOnly.FromDateTime(until) : null);
        }

        Result = _original with
        {
            Title = TitleBox.Text.Trim(),
            Emoji = _selectedEmoji,
            Notes = NotesBox.Text ?? string.Empty,
            Location = LocationBox.Text?.Trim() ?? string.Empty,
            Start = start,
            End = end,
            IsAllDay = allDay,
            TimeZoneId = TimeZoneInfo.Local.Id,
            ColorHex = EventColorPicker.SelectedColor,
            IsBold = BoldToggle.IsChecked == true,
            IsItalic = ItalicToggle.IsChecked == true,
            TitleFontSize = _titleFontSize,
            RecurrenceRule = rule,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show(this, "이 일정을 삭제할까요? 반복 일정이면 전체 시리즈가 삭제됩니다.", "일정 삭제", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        DeleteRequested = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void AllDay_Changed(object sender, RoutedEventArgs e) => UpdateTimePanel();

    private void Frequency_Changed(object sender, SelectionChangedEventArgs e)
    {
        var enabled = !string.IsNullOrWhiteSpace((FrequencyCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString());
        IntervalBox.IsEnabled = enabled;
        UntilDatePicker.IsEnabled = enabled;
    }

    private void FormatToggle_Click(object sender, RoutedEventArgs e) => UpdateTitlePreview();
    private void IncreaseFont_Click(object sender, RoutedEventArgs e) => AdjustFontSize(1);
    private void DecreaseFont_Click(object sender, RoutedEventArgs e) => AdjustFontSize(-1);

    private void EmojiCategory_Changed(object sender, SelectionChangedEventArgs e)
    {
        var category = EmojiCategoryCombo.SelectedItem?.ToString() ?? EventEmojiCatalog.AllCategory;
        EmojiItemsControl.ItemsSource = category == EventEmojiCatalog.AllCategory
            ? _emojiOptions
            : _emojiOptions.Where(item => item.Category == category).ToArray();
    }

    private void Emoji_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { DataContext: EventEmojiOption option })
        {
            _selectedEmoji = option.Emoji;
            UpdateEmojiSelection();
        }
    }

    private void ClearEmoji_Click(object sender, RoutedEventArgs e)
    {
        _selectedEmoji = string.Empty;
        UpdateEmojiSelection();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
        }
        else if (e.Key == Key.B && modifiers.HasFlag(ModifierKeys.Control))
        {
            BoldToggle.IsChecked = BoldToggle.IsChecked != true;
            UpdateTitlePreview();
            e.Handled = true;
        }
        else if (e.Key == Key.I && modifiers.HasFlag(ModifierKeys.Control))
        {
            ItalicToggle.IsChecked = ItalicToggle.IsChecked != true;
            UpdateTitlePreview();
            e.Handled = true;
        }
        else if (e.Key == Key.OemPeriod && modifiers.HasFlag(ModifierKeys.Control) && modifiers.HasFlag(ModifierKeys.Shift))
        {
            AdjustFontSize(1);
            e.Handled = true;
        }
        else if (e.Key == Key.OemComma && modifiers.HasFlag(ModifierKeys.Control) && modifiers.HasFlag(ModifierKeys.Shift))
        {
            AdjustFontSize(-1);
            e.Handled = true;
        }
    }

    private void AdjustFontSize(double delta)
    {
        _titleFontSize = Math.Clamp(_titleFontSize + delta, 8, 40);
        UpdateTitlePreview();
    }

    private void UpdateTitlePreview()
    {
        TitleBox.FontWeight = BoldToggle.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
        TitleBox.FontStyle = ItalicToggle.IsChecked == true ? FontStyles.Italic : FontStyles.Normal;
        TitleBox.FontSize = _titleFontSize;
        FontSizeText.Text = $"크기 {_titleFontSize:0}";
    }

    private void UpdateEmojiSelection()
    {
        foreach (var option in _emojiOptions)
            option.IsSelected = option.Emoji == _selectedEmoji;

        var selected = _emojiOptions.FirstOrDefault(item => item.Emoji == _selectedEmoji);
        SelectedEmojiText.Text = string.IsNullOrWhiteSpace(_selectedEmoji) ? "＋" : _selectedEmoji;
        SelectedEmojiNameText.Text = string.IsNullOrWhiteSpace(_selectedEmoji)
            ? "선택 안 함"
            : selected?.Name ?? "선택한 이모지";
    }

    private void UpdateTimePanel() => TimePanel.IsEnabled = AllDayCheckBox.IsChecked != true;

    private void LoadRecurrence(string? rule)
    {
        FrequencyCombo.SelectedIndex = 0;
        if (string.IsNullOrWhiteSpace(rule))
            return;
        var values = rule.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);
        var frequency = values.GetValueOrDefault("FREQ", string.Empty);
        foreach (var item in FrequencyCombo.Items.OfType<ComboBoxItem>())
            if (string.Equals(item.Tag?.ToString(), frequency, StringComparison.OrdinalIgnoreCase))
                FrequencyCombo.SelectedItem = item;
        IntervalBox.Text = values.GetValueOrDefault("INTERVAL", "1");
        if (values.TryGetValue("UNTIL", out var until) && DateTime.TryParseExact(until, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            UntilDatePicker.SelectedDate = date;
    }

    private static bool TryCreateDateTime(DateTime date, string text, bool allDay, bool isEnd, out DateTimeOffset value)
    {
        TimeOnly time;
        if (allDay)
            time = isEnd ? TimeOnly.MaxValue : TimeOnly.MinValue;
        else if (!TimeOnly.TryParseExact(text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            value = default;
            return false;
        }
        var local = date.Date.Add(time.ToTimeSpan());
        value = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
        return true;
    }
}
