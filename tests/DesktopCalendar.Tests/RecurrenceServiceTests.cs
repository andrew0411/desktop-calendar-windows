using DesktopCalendar.Core.Models;
using DesktopCalendar.Core.Services;

namespace DesktopCalendar.Tests;

[TestClass]
public sealed class RecurrenceServiceTests
{
    private readonly RecurrenceService _service = new();

    [TestMethod]
    public void DailyRule_StopsAtInclusiveUntilDate()
    {
        var source = Create(new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero), "FREQ=DAILY;INTERVAL=2;UNTIL=20260807");
        var result = _service.Expand(source,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        CollectionAssert.AreEqual(new[] { 1, 3, 5, 7 }, result.Select(item => item.Start.Day).ToArray());
    }

    [TestMethod]
    public void MonthlyRule_SkipsMonthsWithoutOriginalDay()
    {
        var source = Create(new DateTimeOffset(2026, 8, 31, 9, 0, 0, TimeSpan.Zero), "FREQ=MONTHLY;INTERVAL=1;UNTIL=20261231");
        var result = _service.Expand(source,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));
        CollectionAssert.AreEqual(new[] { 8, 10, 12 }, result.Select(item => item.Start.Month).ToArray());
    }

    [TestMethod]
    public void BuildRule_UsesPortableRfcStyleFields()
    {
        var rule = RecurrenceService.BuildRule("weekly", 2, new DateOnly(2026, 12, 31));
        Assert.AreEqual("FREQ=WEEKLY;INTERVAL=2;UNTIL=20261231", rule);
    }

    private static CalendarEvent Create(DateTimeOffset start, string recurrence) => new()
    {
        Title = "테스트",
        Start = start,
        End = start.AddHours(1),
        IsAllDay = false,
        RecurrenceRule = recurrence
    };
}

