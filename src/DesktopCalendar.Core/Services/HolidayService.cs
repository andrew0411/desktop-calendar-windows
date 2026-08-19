using System.Globalization;
using DesktopCalendar.Core.Abstractions;
using DesktopCalendar.Core.Models;

namespace DesktopCalendar.Core.Services;

public sealed class HolidayService : IHolidayService
{
    private static readonly KoreanLunisolarCalendar KoreanCalendar = new();

    public IReadOnlyList<PublicHoliday> GetHolidays(DateOnly rangeStart, DateOnly rangeEnd)
    {
        if (rangeEnd < rangeStart)
            throw new ArgumentOutOfRangeException(nameof(rangeEnd));

        var holidays = new List<PublicHoliday>();
        for (var year = rangeStart.Year - 1; year <= rangeEnd.Year + 1; year++)
        {
            holidays.AddRange(BuildKoreanHolidays(year));
            holidays.AddRange(BuildUnitedStatesHolidays(year));
        }

        return holidays
            .Where(item => item.Date >= rangeStart && item.Date <= rangeEnd)
            .Distinct()
            .OrderBy(item => item.Date)
            .ThenBy(item => item.Country)
            .ThenBy(item => item.IsObserved)
            .ToArray();
    }

    private static IReadOnlyList<PublicHoliday> BuildUnitedStatesHolidays(int year)
    {
        var result = new List<PublicHoliday>();

        AddUsFixedHoliday(result, new DateOnly(year, 1, 1), "신정");
        result.Add(new PublicHoliday(NthWeekday(year, 1, DayOfWeek.Monday, 3), "마틴 루터 킹의 날", HolidayCountry.UnitedStates));
        result.Add(new PublicHoliday(NthWeekday(year, 2, DayOfWeek.Monday, 3), "워싱턴 탄생일", HolidayCountry.UnitedStates));
        result.Add(new PublicHoliday(LastWeekday(year, 5, DayOfWeek.Monday), "메모리얼 데이", HolidayCountry.UnitedStates));
        AddUsFixedHoliday(result, new DateOnly(year, 6, 19), "준틴스");
        AddUsFixedHoliday(result, new DateOnly(year, 7, 4), "독립기념일");
        result.Add(new PublicHoliday(NthWeekday(year, 9, DayOfWeek.Monday, 1), "노동절", HolidayCountry.UnitedStates));
        result.Add(new PublicHoliday(NthWeekday(year, 10, DayOfWeek.Monday, 2), "콜럼버스 데이", HolidayCountry.UnitedStates));
        AddUsFixedHoliday(result, new DateOnly(year, 11, 11), "재향군인의 날");
        result.Add(new PublicHoliday(NthWeekday(year, 11, DayOfWeek.Thursday, 4), "추수감사절", HolidayCountry.UnitedStates));
        AddUsFixedHoliday(result, new DateOnly(year, 12, 25), "크리스마스");
        return result;
    }

    private static void AddUsFixedHoliday(List<PublicHoliday> result, DateOnly date, string name)
    {
        result.Add(new PublicHoliday(date, name, HolidayCountry.UnitedStates));
        var observed = date.DayOfWeek switch
        {
            DayOfWeek.Saturday => date.AddDays(-1),
            DayOfWeek.Sunday => date.AddDays(1),
            _ => date
        };
        if (observed != date)
            result.Add(new PublicHoliday(observed, $"{name} 대체휴일", HolidayCountry.UnitedStates, true));
    }

    private static IReadOnlyList<PublicHoliday> BuildKoreanHolidays(int year)
    {
        var result = new List<KoreanHoliday>();

        AddKorean(result, new DateOnly(year, 1, 1), "신정");
        AddKorean(result, new DateOnly(year, 3, 1), "삼일절", SubstituteRule.SaturdayOrSunday);
        if (year >= 2026)
            AddKorean(result, new DateOnly(year, 5, 1), "노동절", SubstituteRule.SaturdayOrSunday);
        AddKorean(result, new DateOnly(year, 5, 5), "어린이날", SubstituteRule.SaturdayOrSunday);
        AddKorean(result, new DateOnly(year, 6, 6), "현충일");
        AddKorean(result, new DateOnly(year, 8, 15), "광복절", SubstituteRule.SaturdayOrSunday);
        AddKorean(result, new DateOnly(year, 10, 3), "개천절", SubstituteRule.SaturdayOrSunday);
        AddKorean(result, new DateOnly(year, 10, 9), "한글날", SubstituteRule.SaturdayOrSunday);
        AddKorean(result, new DateOnly(year, 12, 25), "크리스마스", SubstituteRule.SaturdayOrSunday);

        if (TryFindLunarDate(year, 1, 1, out var lunarNewYear))
        {
            AddKorean(result, lunarNewYear.AddDays(-1), "설날 연휴", SubstituteRule.SundayOnly);
            AddKorean(result, lunarNewYear, "설날", SubstituteRule.SundayOnly);
            AddKorean(result, lunarNewYear.AddDays(1), "설날 연휴", SubstituteRule.SundayOnly);
        }
        if (TryFindLunarDate(year, 4, 8, out var buddhasBirthday))
            AddKorean(result, buddhasBirthday, "부처님 오신 날", SubstituteRule.SaturdayOrSunday);
        if (TryFindLunarDate(year, 8, 15, out var chuseok))
        {
            AddKorean(result, chuseok.AddDays(-1), "추석 연휴", SubstituteRule.SundayOnly);
            AddKorean(result, chuseok, "추석", SubstituteRule.SundayOnly);
            AddKorean(result, chuseok.AddDays(1), "추석 연휴", SubstituteRule.SundayOnly);
        }

        var occupied = result.Select(item => item.Date).ToHashSet();
        var substitutes = new List<PublicHoliday>();
        foreach (var group in result.GroupBy(item => item.Date).OrderBy(group => group.Key))
        {
            var weekendTriggers = group.Count(item => item.Rule == SubstituteRule.SaturdayOrSunday &&
                item.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
            weekendTriggers += group.Count(item => item.Rule == SubstituteRule.SundayOnly && item.Date.DayOfWeek == DayOfWeek.Sunday);

            var overlapTriggers = group.Count() > 1
                ? Math.Max(0, group.Count(item => item.Rule != SubstituteRule.None) - 1)
                : 0;
            var substituteCount = Math.Max(weekendTriggers, overlapTriggers);
            for (var index = 0; index < substituteCount; index++)
            {
                var date = NextKoreanBusinessDay(group.Key.AddDays(1), occupied);
                occupied.Add(date);
                var source = group.First(item => item.Rule != SubstituteRule.None);
                substitutes.Add(new PublicHoliday(date, $"{source.Name} 대체공휴일", HolidayCountry.SouthKorea, true));
            }
        }

        return result.Select(item => new PublicHoliday(item.Date, item.Name, HolidayCountry.SouthKorea))
            .Concat(substitutes)
            .ToArray();
    }

    private static DateOnly NextKoreanBusinessDay(DateOnly date, HashSet<DateOnly> occupied)
    {
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || occupied.Contains(date))
            date = date.AddDays(1);
        return date;
    }

    private static void AddKorean(List<KoreanHoliday> holidays, DateOnly date, string name, SubstituteRule rule = SubstituteRule.None) =>
        holidays.Add(new KoreanHoliday(date, name, rule));

    private static DateOnly NthWeekday(int year, int month, DayOfWeek dayOfWeek, int occurrence)
    {
        var date = new DateOnly(year, month, 1);
        var offset = ((int)dayOfWeek - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(offset + (occurrence - 1) * 7);
    }

    private static DateOnly LastWeekday(int year, int month, DayOfWeek dayOfWeek)
    {
        var date = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var offset = ((int)date.DayOfWeek - (int)dayOfWeek + 7) % 7;
        return date.AddDays(-offset);
    }

    private static bool TryFindLunarDate(int gregorianYear, int lunarMonth, int lunarDay, out DateOnly result)
    {
        result = default;
        if (gregorianYear < KoreanCalendar.MinSupportedDateTime.Year || gregorianYear > KoreanCalendar.MaxSupportedDateTime.Year)
            return false;

        var first = new DateTime(gregorianYear, 1, 1);
        var last = new DateTime(gregorianYear, 12, 31);
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            var lunarYear = KoreanCalendar.GetYear(date);
            var month = KoreanCalendar.GetMonth(date);
            var leapMonth = KoreanCalendar.GetLeapMonth(lunarYear);
            if (leapMonth > 0)
            {
                if (month == leapMonth)
                    continue;
                if (month > leapMonth)
                    month--;
            }
            if (month == lunarMonth && KoreanCalendar.GetDayOfMonth(date) == lunarDay)
            {
                result = DateOnly.FromDateTime(date);
                return true;
            }
        }
        return false;
    }

    private enum SubstituteRule { None, SundayOnly, SaturdayOrSunday }
    private sealed record KoreanHoliday(DateOnly Date, string Name, SubstituteRule Rule);
}
