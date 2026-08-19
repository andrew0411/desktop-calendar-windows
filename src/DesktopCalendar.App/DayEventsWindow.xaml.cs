using System.Windows;
using System.Windows.Input;
using DesktopCalendar.App.Services;
using DesktopCalendar.App.ViewModels;

namespace DesktopCalendar.App;

public partial class DayEventsWindow : Window
{
    public DayEventsWindow(CalendarDayViewModel day)
    {
        InitializeComponent();
        UiThemeService.RegisterWindow(this);
        DateTitle.Text = $"{day.Date:yyyy년 M월 d일}";
        EventsList.ItemsSource = day.AllEvents;
    }

    public event EventHandler<EventOccurrenceViewModel>? EventSelected;

    private void EventsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EventsList.SelectedItem is EventOccurrenceViewModel item)
        {
            EventSelected?.Invoke(this, item);
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
