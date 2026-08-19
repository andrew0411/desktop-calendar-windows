using System.Globalization;
using DesktopCalendar.Core.Abstractions;
using DesktopCalendar.Core.Models;

namespace DesktopCalendar.Core.Services;

public sealed class RecurrenceService : IRecurrenceService
{
    private const int MaximumOccurrences = 10_000;

    public IReadOnlyList<EventOccurrence> Expand(CalendarEvent source, DateTimeOffset rangeStart, DateTimeOffset rangeEnd)
    {
        if (rangeEnd <= rangeStart)
            throw new ArgumentException("범위 종료 시각은 시작 시각보다 늦어야 합니다.");

        if (string.IsNullOrWhiteSpace(source.RecurrenceRule))
            return Overlaps(source.Start, source.End, rangeStart, rangeEnd)
                ? [new EventOccurrence(source, source.Start, source.End)]
                : [];

        var rule = ParseRule(source.RecurrenceRule);
        var duration = source.End - source.Start;
        var current = source.Start;
        var results = new List<EventOccurrence>();
        var count = 0;

        while (current < rangeEnd && count++ < MaximumOccurrences)
        {
            if (rule.Until is null || DateOnly.FromDateTime(current.LocalDateTime) <= rule.Until)
            {
                var end = current + duration;
                if (Overlaps(current, end, rangeStart, rangeEnd))
                    results.Add(new EventOccurrence(source, current, end));
            }

            var next = Next(current, rule.Frequency, rule.Interval);
            if (next is null || next <= current || (rule.Until is not null && DateOnly.FromDateTime(next.Value.LocalDateTime) > rule.Until))
                break;
            current = next.Value;
        }

        return results;
    }

    public static string BuildRule(string frequency, int interval, DateOnly? until)
    {
        var normalized = frequency.ToUpperInvariant();
        if (normalized is not ("DAILY" or "WEEKLY" or "MONTHLY" or "YEARLY"))
            throw new ArgumentOutOfRangeException(nameof(frequency));
        if (interval < 1)
            throw new ArgumentOutOfRangeException(nameof(interval));

        var rule = $"FREQ={normalized};INTERVAL={interval}";
        return until is null ? rule : $"{rule};UNTIL={until:yyyyMMdd}";
    }

    private static ParsedRule ParseRule(string value)
    {
        var pairs = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].ToUpperInvariant(), parts => parts[1], StringComparer.OrdinalIgnoreCase);

        if (!pairs.TryGetValue("FREQ", out var frequency) || frequency is not ("DAILY" or "WEEKLY" or "MONTHLY" or "YEARLY"))
            throw new FormatException("지원하지 않는 반복 규칙입니다.");

        var interval = pairs.TryGetValue("INTERVAL", out var rawInterval) && int.TryParse(rawInterval, out var parsedInterval)
            ? Math.Max(1, parsedInterval)
            : 1;
        DateOnly? until = null;
        if (pairs.TryGetValue("UNTIL", out var rawUntil))
        {
            if (!DateOnly.TryParseExact(rawUntil, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedUntil))
                throw new FormatException("반복 종료일 형식이 잘못되었습니다.");
            until = parsedUntil;
        }

        return new ParsedRule(frequency, interval, until);
    }

    private static DateTimeOffset? Next(DateTimeOffset value, string frequency, int interval) => frequency switch
    {
        "DAILY" => value.AddDays(interval),
        "WEEKLY" => value.AddDays(interval * 7),
        "MONTHLY" => AddMonthsSkippingInvalid(value, interval),
        "YEARLY" => AddYearsSkippingInvalid(value, interval),
        _ => null
    };

    private static DateTimeOffset? AddMonthsSkippingInvalid(DateTimeOffset value, int months)
    {
        var monthStart = new DateTime(value.Year, value.Month, 1, value.Hour, value.Minute, value.Second, value.Millisecond, DateTimeKind.Unspecified).AddMonths(months);
        if (value.Day > DateTime.DaysInMonth(monthStart.Year, monthStart.Month))
        {
            var nextValid = monthStart;
            for (var i = 0; i < 120 && value.Day > DateTime.DaysInMonth(nextValid.Year, nextValid.Month); i++)
                nextValid = nextValid.AddMonths(months);
            monthStart = nextValid;
        }
        return new DateTimeOffset(monthStart.Year, monthStart.Month, value.Day, value.Hour, value.Minute, value.Second, value.Offset);
    }

    private static DateTimeOffset? AddYearsSkippingInvalid(DateTimeOffset value, int years)
    {
        var year = value.Year + years;
        while (value.Month == 2 && value.Day == 29 && !DateTime.IsLeapYear(year))
            year += years;
        return new DateTimeOffset(year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Offset);
    }

    private static bool Overlaps(DateTimeOffset start, DateTimeOffset end, DateTimeOffset rangeStart, DateTimeOffset rangeEnd) =>
        start < rangeEnd && end >= rangeStart;

    private sealed record ParsedRule(string Frequency, int Interval, DateOnly? Until);
}
