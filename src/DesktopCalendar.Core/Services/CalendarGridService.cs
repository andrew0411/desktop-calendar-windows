namespace DesktopCalendar.Core.Services;

public static class CalendarGridService
{
    public static IReadOnlyList<DateOnly> BuildMonth(DateOnly month)
    {
        var first = new DateOnly(month.Year, month.Month, 1);
        var start = first.AddDays(-(int)first.DayOfWeek);
        return Enumerable.Range(0, 42).Select(start.AddDays).ToArray();
    }
}

