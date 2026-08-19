using DesktopCalendar.Core.Services;

namespace DesktopCalendar.Tests;

[TestClass]
public sealed class CalendarGridTests
{
    [TestMethod]
    [DataRow(2024, 2)]
    [DataRow(2025, 1)]
    [DataRow(2026, 8)]
    [DataRow(2030, 12)]
    public void BuildMonth_AlwaysReturnsSixSundayFirstWeeks(int year, int month)
    {
        var result = CalendarGridService.BuildMonth(new DateOnly(year, month, 1));
        Assert.HasCount(42, result);
        Assert.AreEqual(DayOfWeek.Sunday, result[0].DayOfWeek);
        Assert.AreEqual(DayOfWeek.Saturday, result[^1].DayOfWeek);
        Assert.IsTrue(result.Contains(new DateOnly(year, month, 1)));
    }

    [TestMethod]
    public void BuildMonth_HandlesLeapDay()
    {
        var result = CalendarGridService.BuildMonth(new DateOnly(2024, 2, 1));
        Assert.IsTrue(result.Contains(new DateOnly(2024, 2, 29)));
    }
}
