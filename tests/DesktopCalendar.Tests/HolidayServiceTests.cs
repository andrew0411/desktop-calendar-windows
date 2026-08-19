using DesktopCalendar.Core.Models;
using DesktopCalendar.Core.Services;

namespace DesktopCalendar.Tests;

[TestClass]
public sealed class HolidayServiceTests
{
    private readonly HolidayService _service = new();

    [TestMethod]
    public void Korea_UsesLunarDatesAndCurrentSubstituteRules()
    {
        var holidays = _service.GetHolidays(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31))
            .Where(item => item.Country == HolidayCountry.SouthKorea)
            .ToArray();

        Assert.IsTrue(holidays.Any(item => item.Date == new DateOnly(2026, 2, 17) && item.Name == "설날"));
        Assert.IsTrue(holidays.Any(item => item.Date == new DateOnly(2026, 5, 1) && item.Name == "노동절"));
        Assert.IsTrue(holidays.Any(item => item.Date == new DateOnly(2026, 8, 15) && item.Name == "광복절"));
        Assert.IsTrue(holidays.Any(item => item.Date == new DateOnly(2026, 8, 17) && item.Name.Contains("대체공휴일")));
        Assert.IsTrue(holidays.Any(item => item.Date == new DateOnly(2026, 9, 25) && item.Name == "추석"));
    }

    [TestMethod]
    public void UnitedStates_UsesFederalWeekdaysAndObservedDates()
    {
        var holidays = _service.GetHolidays(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31))
            .Where(item => item.Country == HolidayCountry.UnitedStates)
            .ToArray();

        Assert.IsTrue(holidays.Any(item => item.Date == new DateOnly(2026, 1, 19) && item.Name == "마틴 루터 킹의 날"));
        Assert.IsTrue(holidays.Any(item => item.Date == new DateOnly(2026, 7, 3) && item.Name.Contains("대체휴일") && item.IsObserved));
        Assert.IsTrue(holidays.Any(item => item.Date == new DateOnly(2026, 7, 4) && item.Name == "독립기념일"));
        Assert.IsTrue(holidays.Any(item => item.Date == new DateOnly(2026, 11, 26) && item.Name == "추수감사절"));
    }

    [TestMethod]
    public void Range_ReturnsOnlyRequestedDates()
    {
        var start = new DateOnly(2026, 8, 1);
        var end = new DateOnly(2026, 8, 31);
        var holidays = _service.GetHolidays(start, end);

        Assert.IsTrue(holidays.Count > 0);
        Assert.IsTrue(holidays.All(item => item.Date >= start && item.Date <= end));
    }
}
