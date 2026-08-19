namespace DesktopCalendar.Core.Models;

public sealed record CalendarEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = string.Empty;
    public string Emoji { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public bool IsAllDay { get; init; } = true;
    public string TimeZoneId { get; init; } = TimeZoneInfo.Local.Id;
    public string ColorHex { get; init; } = "#FF4F8EF7";
    public bool IsCompleted { get; init; }
    public bool IsHighlighted { get; init; }
    public bool IsBold { get; init; }
    public bool IsItalic { get; init; }
    public double TitleFontSize { get; init; }
    public string? RecurrenceRule { get; init; }
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record EventOccurrence(CalendarEvent Source, DateTimeOffset Start, DateTimeOffset End)
{
    public bool IsAllDay => Source.IsAllDay;
    public string Title => Source.Title;
}
