namespace DesktopCalendar.Core.Models;

public sealed record FontStyleSetting(string Family, double Size, string ColorHex);

public sealed record ThemeSettings
{
    public FontStyleSetting MonthTitle { get; init; } = new("맑은 고딕", 25, "#FFF7F8FC");
    public FontStyleSetting Weekday { get; init; } = new("맑은 고딕", 12, "#FFBFC8D8");
    public FontStyleSetting Date { get; init; } = new("맑은 고딕", 13, "#FFF2F4F8");
    public FontStyleSetting Event { get; init; } = new("맑은 고딕", 12, "#FFF9FAFD");
    public string SaturdayColorHex { get; init; } = "#FF71A7FF";
    public string SundayColorHex { get; init; } = "#FFFF626F";
    public string TodayColorHex { get; init; } = "#B35E72E8";
    public string DefaultEventColorHex { get; init; } = "#FF6F83F1";
    public string BackgroundColorHex { get; init; } = "#F0131826";
    public double BackgroundOpacity { get; init; } = 0.88;
    public string GridColorHex { get; init; } = "#24FFFFFF";
    public double GridThickness { get; init; } = 1;
}
