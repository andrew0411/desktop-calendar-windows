using System.Globalization;

namespace DesktopCalendar.Core.Services;

public static class TimeInputParser
{
    private static readonly string[] AcceptedFormats = ["H:mm", "HH:mm"];

    public static bool TryParse(string? text, out TimeOnly time)
    {
        var candidate = text?.Trim() ?? string.Empty;
        if (candidate.All(character => character is >= '0' and <= '9'))
        {
            candidate = candidate.Length switch
            {
                3 => $"{candidate[..1]}:{candidate[1..]}",
                4 => $"{candidate[..2]}:{candidate[2..]}",
                _ => candidate
            };
        }

        return TimeOnly.TryParseExact(
            candidate,
            AcceptedFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);
    }

    public static bool TryNormalize(string? text, out string normalized)
    {
        if (TryParse(text, out var time))
        {
            normalized = time.ToString("HH:mm", CultureInfo.InvariantCulture);
            return true;
        }

        normalized = string.Empty;
        return false;
    }
}
