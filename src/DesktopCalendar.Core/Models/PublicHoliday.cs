namespace DesktopCalendar.Core.Models;

public enum HolidayCountry
{
    SouthKorea,
    UnitedStates
}

public sealed record PublicHoliday(
    DateOnly Date,
    string Name,
    HolidayCountry Country,
    bool IsObserved = false);
