using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopCalendar.App.Services;
using DesktopCalendar.App.ViewModels;
using DesktopCalendar.Core.Models;
using Forms = System.Windows.Forms;

namespace DesktopCalendar.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly DesktopHost _desktopHost;
    private readonly DispatcherTimer _desktopTimer;
    private readonly DispatcherTimer _placementTimer;
    private nint _handle;
    private HwndSource? _hwndSource;
    private CalendarDayViewModel? _lastClickedDay;
    private long _lastDayClickTimestamp;
    private bool _allowClose;
    private bool _applyingPlacement;
    private bool _isLayoutDragging;

    public MainWindow(MainWindowViewModel viewModel, DesktopHost desktopHost)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _desktopHost = desktopHost;
        DataContext = viewModel;
        _desktopTimer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.ApplicationIdle, DesktopTimer_Tick, Dispatcher);
        _placementTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, PlacementTimer_Tick, Dispatcher) { IsEnabled = false };
    }

    public event EventHandler? SettingsRequested;
    public event EventHandler? LayoutEditingChanged;
    public bool IsLayoutEditing => !_viewModel.IsLayoutLocked;

    public void AllowClose() => _allowClose = true;

    public async Task GoTodayAsync()
    {
        await _viewModel.GoTodayAsync();
        UpdateVisibleEventCapacity();
    }

    public async Task ToggleLayoutEditingAsync()
    {
        await _viewModel.SetLayoutLockAsync(!_viewModel.IsLayoutLocked);
        ApplyLayoutMode();
        LayoutEditingChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReattachDesktop()
    {
        _desktopHost.Detach();
        AttachToDesktop();
    }

    public void ApplySettingsAndPlacement()
    {
        ApplyLayoutMode();
        AttachToDesktop();
    }

    public void EnsureDesktopAttached()
    {
        if (!_desktopHost.IsAttached || !_desktopHost.IsDesktopAvailable())
            AttachToDesktop();
        else
            _desktopHost.RestoreZOrder();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            return;
        }
        _desktopTimer.Stop();
        _placementTimer.Stop();
        _hwndSource?.RemoveHook(WindowMessageHook);
        base.OnClosing(e);
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_handle);
        _hwndSource.AddHook(WindowMessageHook);
        ApplyPlacement();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyLayoutMode();
        AttachToDesktop();
        var normalized = NormalizePlacement(_viewModel.Settings.Window);
        if (normalized != _viewModel.Settings.Window)
            await _viewModel.SetWindowPlacementAsync(normalized);
        _desktopTimer.Start();
        UpdateVisibleEventCapacity();
    }

    private void ApplyLayoutMode()
    {
        ResizeMode = ResizeMode.NoResize;
        _desktopHost.SetLayoutEditing(!_viewModel.IsLayoutLocked);
        EditBorder.BorderThickness = _viewModel.IsLayoutLocked ? new Thickness(0) : new Thickness(2);
        LayoutChrome.Visibility = _viewModel.IsLayoutLocked ? Visibility.Collapsed : Visibility.Visible;
        MoveThumb.Visibility = _viewModel.IsLayoutLocked ? Visibility.Collapsed : Visibility.Visible;
        LayoutButton.Content = _viewModel.IsLayoutLocked ? "↔ 위치·크기" : "✓ 배치 완료";
        _viewModel.ToolbarVisible = !_viewModel.IsLayoutLocked || IsMouseOver;
    }

    private void ApplyPlacement()
    {
        _applyingPlacement = true;
        var placement = NormalizePlacement(_viewModel.Settings.Window);
        var screen = Forms.Screen.AllScreens.FirstOrDefault(item => item.DeviceName == placement.MonitorDeviceName) ?? Forms.Screen.PrimaryScreen!;
        var dpi = VisualTreeHelper.GetDpi(this);
        var bounds = screen.Bounds;
        Left = bounds.X / dpi.DpiScaleX + placement.Left * bounds.Width / dpi.DpiScaleX;
        Top = bounds.Y / dpi.DpiScaleY + placement.Top * bounds.Height / dpi.DpiScaleY;
        Width = Math.Max(MinWidth, placement.Width * bounds.Width / dpi.DpiScaleX);
        Height = Math.Max(MinHeight, placement.Height * bounds.Height / dpi.DpiScaleY);
        _applyingPlacement = false;
    }

    private void AttachToDesktop()
    {
        if (_handle == nint.Zero)
            return;
        var screen = GetTargetScreen();
        var placement = NormalizePlacement(_viewModel.Settings.Window);
        var x = screen.Bounds.X + (int)Math.Round(placement.Left * screen.Bounds.Width);
        var y = screen.Bounds.Y + (int)Math.Round(placement.Top * screen.Bounds.Height);
        var width = (int)Math.Round(placement.Width * screen.Bounds.Width);
        var height = (int)Math.Round(placement.Height * screen.Bounds.Height);
        _applyingPlacement = true;
        try
        {
            _desktopHost.Attach(_handle, x, y, width, height);
        }
        finally
        {
            _applyingPlacement = false;
        }
    }

    private Forms.Screen GetTargetScreen()
    {
        var name = _viewModel.Settings.Window.MonitorDeviceName;
        return Forms.Screen.AllScreens.FirstOrDefault(item => item.DeviceName == name) ?? Forms.Screen.PrimaryScreen!;
    }

    private static WindowPlacement NormalizePlacement(WindowPlacement placement)
    {
        var width = Math.Clamp(placement.Width, 0.2, 1);
        var height = Math.Clamp(placement.Height, 0.25, 1);
        return placement with
        {
            Left = Math.Clamp(placement.Left, 0, Math.Max(0, 1 - width)),
            Top = Math.Clamp(placement.Top, 0, Math.Max(0, 1 - height)),
            Width = width,
            Height = height
        };
    }

    private async void PreviousMonth_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.MoveMonthAsync(-1);
        UpdateVisibleEventCapacity();
    }

    private async void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.MoveMonthAsync(1);
        UpdateVisibleEventCapacity();
    }

    private async void Today_Click(object sender, RoutedEventArgs e)
    {
        await GoTodayAsync();
    }
    private async void LayoutEdit_Click(object sender, RoutedEventArgs e) => await ToggleLayoutEditingAsync();
    private void Settings_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => _viewModel.ToolbarVisible = true;
    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) => _viewModel.ToolbarVisible = !_viewModel.IsLayoutLocked;

    private void Window_Deactivated(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(_desktopHost.RestoreZOrder, DispatcherPriority.Background);

    private async void LayoutThumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsLayoutLocked || sender is not Thumb { Tag: string direction })
            return;
        e.Handled = true;
        _isLayoutDragging = true;
        try
        {
            Activate();
            _desktopHost.BeginSystemDrag(direction);
        }
        finally
        {
            _isLayoutDragging = false;
        }
        UpdateVisibleEventCapacity();
        await SaveCurrentPlacementAsync();
        _desktopHost.RestoreZOrder();
    }

    private void DayCellInput_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CalendarDayViewModel day })
            return;

        var timestamp = Environment.TickCount64;
        var isManualDoubleClick = ReferenceEquals(_lastClickedDay, day) &&
                                  timestamp - _lastDayClickTimestamp <= Forms.SystemInformation.DoubleClickTime;
        _lastClickedDay = day;
        _lastDayClickTimestamp = timestamp;
        if (!isManualDoubleClick)
            return;

        _lastClickedDay = null;
        e.Handled = true;
        OpenNewEventEditor(day.Date);
    }

    private void Event_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: EventOccurrenceViewModel item, Tag: CalendarDayViewModel day })
            return;

        _lastClickedDay = null;
        day.SelectEvent(item);
        e.Handled = true;
        if (e.ClickCount == 2)
            OpenEventEditor(item.Occurrence.Source);
    }

    private void Event_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed &&
            sender is FrameworkElement { DataContext: EventOccurrenceViewModel item, Tag: CalendarDayViewModel day })
            day.SelectEvent(item);
    }

    private void Events_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not System.Windows.Controls.ItemsControl { DataContext: CalendarDayViewModel day } events)
            return;
        var current = events.InputHitTest(e.GetPosition(events)) as DependencyObject;
        while (current is not null && !ReferenceEquals(current, events))
        {
            if (current is FrameworkElement { DataContext: EventOccurrenceViewModel item })
            {
                day.SelectEvent(item);
                return;
            }
            current = VisualTreeHelper.GetParent(current);
        }
    }

    private void EventActionsToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CalendarDayViewModel day })
            day.ToggleActionMenu();
    }

    private void NewEventFromMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CalendarDayViewModel day })
            return;
        day.IsActionMenuOpen = false;
        OpenNewEventEditor(day.Date);
    }

    private void MoreEvents_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CalendarDayViewModel day })
        {
            var window = new DayEventsWindow(day) { Owner = this };
            window.EventSelected += (_, item) => OpenEventEditor(item.Occurrence.Source);
            window.ShowDialog();
        }
    }

    private async void SelectedHighlight_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CalendarDayViewModel { SelectedEvent: { } item } })
        {
            await _viewModel.SaveEventAsync(item.Occurrence.Source with { IsHighlighted = !item.Occurrence.Source.IsHighlighted });
            UpdateVisibleEventCapacity();
        }
    }

    private async void SelectedComplete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CalendarDayViewModel { SelectedEvent: { } item } })
        {
            await _viewModel.SaveEventAsync(item.Occurrence.Source with { IsCompleted = !item.Occurrence.Source.IsCompleted });
            UpdateVisibleEventCapacity();
        }
    }

    private void OpenNewEventEditor(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        var offset = TimeZoneInfo.Local.GetUtcOffset(local);
        OpenEventEditor(new CalendarEvent
        {
            Start = new DateTimeOffset(local, offset),
            End = new DateTimeOffset(local.AddDays(1).AddTicks(-1), offset),
            IsAllDay = true,
            TimeZoneId = TimeZoneInfo.Local.Id,
            ColorHex = _viewModel.Theme.DefaultEventColorHex,
            TitleFontSize = _viewModel.Theme.Event.Size
        }, isNew: true);
    }

    private async void OpenEventEditor(CalendarEvent item, bool isNew = false)
    {
        try
        {
            var editor = new EventEditorWindow(item, _viewModel.Theme, isNew) { Owner = this };
            if (editor.ShowDialog() != true)
                return;
            if (editor.DeleteRequested)
                await _viewModel.DeleteEventAsync(item.Id);
            else if (editor.Result is not null)
                await _viewModel.SaveEventAsync(editor.Result);
            UpdateVisibleEventCapacity();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"일정 편집 창을 열거나 저장하지 못했습니다.\n\n{ex.Message}", "일정 편집 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_PlacementChanged(object? sender, EventArgs e)
    {
        UpdateVisibleEventCapacity();
        if (_applyingPlacement || _isLayoutDragging || _viewModel.IsLayoutLocked || !IsLoaded)
            return;
        _placementTimer.Stop();
        _placementTimer.Start();
    }

    private async void PlacementTimer_Tick(object? sender, EventArgs e)
    {
        _placementTimer.Stop();
        await SaveCurrentPlacementAsync();
        AttachToDesktop();
    }

    private async Task SaveCurrentPlacementAsync()
    {
        if (!_desktopHost.TryGetWindowBounds(out var x, out var y, out var width, out var height))
            return;
        var screen = Forms.Screen.FromRectangle(new System.Drawing.Rectangle(x, y, width, height));
        var widthRatio = Math.Clamp((double)width / screen.Bounds.Width, 0.2, 1);
        var heightRatio = Math.Clamp((double)height / screen.Bounds.Height, 0.25, 1);
        var placement = _viewModel.Settings.Window with
        {
            MonitorDeviceName = screen.DeviceName,
            Left = Math.Clamp((double)(x - screen.Bounds.X) / screen.Bounds.Width, 0, Math.Max(0, 1 - widthRatio)),
            Top = Math.Clamp((double)(y - screen.Bounds.Y) / screen.Bounds.Height, 0, Math.Max(0, 1 - heightRatio)),
            Width = widthRatio,
            Height = heightRatio
        };
        await _viewModel.SetWindowPlacementAsync(placement);
    }

    private void DesktopTimer_Tick(object? sender, EventArgs e)
    {
        if (!_desktopHost.IsAttached || !_desktopHost.IsDesktopAvailable())
            AttachToDesktop();
    }

    private void UpdateVisibleEventCapacity()
    {
        if (ActualHeight <= 0)
            return;
        var cellHeight = Math.Max(48, (ActualHeight - 138) / 6);
        var capacity = Math.Clamp((int)Math.Floor((cellHeight - 58) / 26), 1, 10);
        foreach (var day in _viewModel.Days)
            day.SetMaxVisibleEvents(capacity);
    }

    private nint WindowMessageHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        const int wmMouseActivate = 0x0021;
        const int maNoActivate = 3;
        if (message != wmMouseActivate || !_viewModel.IsLayoutLocked)
            return nint.Zero;

        // A desktop widget must receive the click without moving into the
        // foreground Z band. Date entry activates explicitly when it needs
        // keyboard focus.
        handled = true;
        return new nint(maNoActivate);
    }

}
