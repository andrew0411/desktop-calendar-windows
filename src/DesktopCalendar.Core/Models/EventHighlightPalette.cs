namespace DesktopCalendar.Core.Models;

public static class EventHighlightPalette
{
    public const string DefaultColorHex = "#FFFFF176";
    public const string TextColorHex = "#FF25262B";

    public static IReadOnlyList<EventHighlightColor> Colors { get; } =
    [
        new("노랑", DefaultColorHex),
        new("주황", "#FFFFB86B"),
        new("빨강", "#FFFF8A8A"),
        new("민트", "#FF7FE0C3"),
        new("라벤더", "#FFC5A3FF")
    ];

    public static string Normalize(string? colorHex) =>
        Colors.FirstOrDefault(color => string.Equals(color.Hex, colorHex, StringComparison.OrdinalIgnoreCase))?.Hex
        ?? DefaultColorHex;
}

public sealed record EventHighlightColor(string Name, string Hex);
