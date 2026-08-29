using System.Globalization;
using DesktopCalendar.Core.Abstractions;
using DesktopCalendar.Core.Models;
using Microsoft.Data.Sqlite;

namespace DesktopCalendar.Infrastructure;

public sealed class SqliteEventRepository(AppPaths paths) : IEventRepository
{
    private const int CurrentSchemaVersion = 4;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureDirectories();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken);
        await ExecuteAsync(connection, "PRAGMA foreign_keys=ON;", cancellationToken);

        var currentVersion = Convert.ToInt32(await ScalarAsync(connection, "PRAGMA user_version;", cancellationToken), CultureInfo.InvariantCulture);
        if (currentVersion < CurrentSchemaVersion && File.Exists(paths.DatabasePath) && new FileInfo(paths.DatabasePath).Length > 0)
        {
            var destination = Path.Combine(paths.BackupsDirectory, $"calendar-pre-migration-{DateTime.Now:yyyyMMdd-HHmmss}.db");
            File.Copy(paths.DatabasePath, destination, overwrite: false);
        }

        if (currentVersion == 0)
        {
            const string schema = """
                CREATE TABLE IF NOT EXISTS events (
                    id TEXT PRIMARY KEY NOT NULL,
                    title TEXT NOT NULL,
                    emoji TEXT NOT NULL DEFAULT '',
                    notes TEXT NOT NULL,
                    location TEXT NOT NULL DEFAULT '',
                    start_value TEXT NOT NULL,
                    end_value TEXT NOT NULL,
                    is_all_day INTEGER NOT NULL,
                    time_zone_id TEXT NOT NULL,
                    color_hex TEXT NOT NULL,
                    is_completed INTEGER NOT NULL DEFAULT 0,
                    is_highlighted INTEGER NOT NULL DEFAULT 0,
                    highlight_color_hex TEXT NOT NULL DEFAULT '#FFFFF176',
                    is_bold INTEGER NOT NULL DEFAULT 0,
                    is_italic INTEGER NOT NULL DEFAULT 0,
                    title_font_size REAL NOT NULL DEFAULT 0,
                    recurrence_rule TEXT NULL,
                    created_utc TEXT NOT NULL,
                    updated_utc TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_events_start ON events(start_value);
                PRAGMA user_version=4;
                """;
            await ExecuteAsync(connection, schema, cancellationToken);
        }
        else
        {
            if (currentVersion < 2)
            {
                const string migration = """
                    ALTER TABLE events ADD COLUMN location TEXT NOT NULL DEFAULT '';
                    ALTER TABLE events ADD COLUMN is_completed INTEGER NOT NULL DEFAULT 0;
                    ALTER TABLE events ADD COLUMN is_highlighted INTEGER NOT NULL DEFAULT 0;
                    ALTER TABLE events ADD COLUMN is_bold INTEGER NOT NULL DEFAULT 0;
                    ALTER TABLE events ADD COLUMN is_italic INTEGER NOT NULL DEFAULT 0;
                    ALTER TABLE events ADD COLUMN title_font_size REAL NOT NULL DEFAULT 0;
                    PRAGMA user_version=2;
                    """;
                await ExecuteAsync(connection, migration, cancellationToken);
                currentVersion = 2;
            }

            if (currentVersion < 3)
            {
                const string migration = """
                    ALTER TABLE events ADD COLUMN emoji TEXT NOT NULL DEFAULT '';
                    PRAGMA user_version=3;
                    """;
                await ExecuteAsync(connection, migration, cancellationToken);
                currentVersion = 3;
            }

            if (currentVersion < 4)
            {
                const string migration = """
                    ALTER TABLE events ADD COLUMN highlight_color_hex TEXT NOT NULL DEFAULT '#FFFFF176';
                    PRAGMA user_version=4;
                    """;
                await ExecuteAsync(connection, migration, cancellationToken);
            }
        }
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await ReadAsync(connection, "SELECT * FROM events ORDER BY start_value, title;", cancellationToken);
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetPotentiallyVisibleAsync(DateTimeOffset rangeStart, DateTimeOffset rangeEnd, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT * FROM events
            WHERE (recurrence_rule IS NOT NULL AND recurrence_rule <> '')
               OR (start_value < $rangeEnd AND end_value >= $rangeStart)
            ORDER BY start_value, title;
            """;
        command.Parameters.AddWithValue("$rangeStart", Format(rangeStart));
        command.Parameters.AddWithValue("$rangeEnd", Format(rangeEnd));
        return await ReadAsync(command, cancellationToken);
    }

    public async Task UpsertAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
    {
        Validate(calendarEvent);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO events(id,title,emoji,notes,location,start_value,end_value,is_all_day,time_zone_id,color_hex,is_completed,is_highlighted,highlight_color_hex,is_bold,is_italic,title_font_size,recurrence_rule,created_utc,updated_utc)
            VALUES($id,$title,$emoji,$notes,$location,$start,$end,$allDay,$timeZone,$color,$completed,$highlighted,$highlightColor,$bold,$italic,$fontSize,$recurrence,$created,$updated)
            ON CONFLICT(id) DO UPDATE SET
                title=excluded.title, emoji=excluded.emoji, notes=excluded.notes, location=excluded.location, start_value=excluded.start_value,
                end_value=excluded.end_value, is_all_day=excluded.is_all_day,
                time_zone_id=excluded.time_zone_id, color_hex=excluded.color_hex,
                is_completed=excluded.is_completed, is_highlighted=excluded.is_highlighted, highlight_color_hex=excluded.highlight_color_hex,
                is_bold=excluded.is_bold, is_italic=excluded.is_italic, title_font_size=excluded.title_font_size,
                recurrence_rule=excluded.recurrence_rule, updated_utc=excluded.updated_utc;
            """;
        AddParameters(command, calendarEvent);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM events WHERE id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ReplaceAllAsync(IEnumerable<CalendarEvent> events, CancellationToken cancellationToken = default)
    {
        var items = events.ToArray();
        foreach (var item in items)
            Validate(item);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM events;";
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var item in items)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO events(id,title,emoji,notes,location,start_value,end_value,is_all_day,time_zone_id,color_hex,is_completed,is_highlighted,highlight_color_hex,is_bold,is_italic,title_font_size,recurrence_rule,created_utc,updated_utc)
                VALUES($id,$title,$emoji,$notes,$location,$start,$end,$allDay,$timeZone,$color,$completed,$highlighted,$highlightColor,$bold,$italic,$fontSize,$recurrence,$created,$updated);
                """;
            AddParameters(command, item);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private SqliteConnection CreateConnection() => new($"Data Source={paths.DatabasePath};Mode=ReadWriteCreate;Cache=Shared;Pooling=False");

    private static void AddParameters(SqliteCommand command, CalendarEvent item)
    {
        command.Parameters.AddWithValue("$id", item.Id.ToString("D"));
        command.Parameters.AddWithValue("$title", item.Title.Trim());
        command.Parameters.AddWithValue("$emoji", item.Emoji ?? string.Empty);
        command.Parameters.AddWithValue("$notes", item.Notes ?? string.Empty);
        command.Parameters.AddWithValue("$location", item.Location ?? string.Empty);
        command.Parameters.AddWithValue("$start", Format(item.Start));
        command.Parameters.AddWithValue("$end", Format(item.End));
        command.Parameters.AddWithValue("$allDay", item.IsAllDay ? 1 : 0);
        command.Parameters.AddWithValue("$timeZone", item.TimeZoneId);
        command.Parameters.AddWithValue("$color", item.ColorHex);
        command.Parameters.AddWithValue("$completed", item.IsCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$highlighted", item.IsHighlighted ? 1 : 0);
        command.Parameters.AddWithValue("$highlightColor", EventHighlightPalette.Normalize(item.HighlightColorHex));
        command.Parameters.AddWithValue("$bold", item.IsBold ? 1 : 0);
        command.Parameters.AddWithValue("$italic", item.IsItalic ? 1 : 0);
        command.Parameters.AddWithValue("$fontSize", item.TitleFontSize);
        command.Parameters.AddWithValue("$recurrence", (object?)item.RecurrenceRule ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", Format(item.CreatedUtc));
        command.Parameters.AddWithValue("$updated", Format(item.UpdatedUtc));
    }

    private static async Task<IReadOnlyList<CalendarEvent>> ReadAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await ReadAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<CalendarEvent>> ReadAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var result = new List<CalendarEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CalendarEvent
            {
                Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
                Title = reader.GetString(reader.GetOrdinal("title")),
                Emoji = reader.GetString(reader.GetOrdinal("emoji")),
                Notes = reader.GetString(reader.GetOrdinal("notes")),
                Location = reader.GetString(reader.GetOrdinal("location")),
                Start = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("start_value")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                End = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("end_value")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                IsAllDay = reader.GetInt32(reader.GetOrdinal("is_all_day")) == 1,
                TimeZoneId = reader.GetString(reader.GetOrdinal("time_zone_id")),
                ColorHex = reader.GetString(reader.GetOrdinal("color_hex")),
                IsCompleted = reader.GetInt32(reader.GetOrdinal("is_completed")) == 1,
                IsHighlighted = reader.GetInt32(reader.GetOrdinal("is_highlighted")) == 1,
                HighlightColorHex = EventHighlightPalette.Normalize(reader.GetString(reader.GetOrdinal("highlight_color_hex"))),
                IsBold = reader.GetInt32(reader.GetOrdinal("is_bold")) == 1,
                IsItalic = reader.GetInt32(reader.GetOrdinal("is_italic")) == 1,
                TitleFontSize = reader.GetDouble(reader.GetOrdinal("title_font_size")),
                RecurrenceRule = reader.IsDBNull(reader.GetOrdinal("recurrence_rule")) ? null : reader.GetString(reader.GetOrdinal("recurrence_rule")),
                CreatedUtc = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("created_utc")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                UpdatedUtc = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("updated_utc")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        }
        return result;
    }

    private static void Validate(CalendarEvent item)
    {
        if (item.Id == Guid.Empty)
            throw new ArgumentException("일정 ID가 필요합니다.", nameof(item));
        if (string.IsNullOrWhiteSpace(item.Title))
            throw new ArgumentException("일정 제목이 필요합니다.", nameof(item));
        if (item.End < item.Start)
            throw new ArgumentException("종료 시각은 시작 시각보다 빠를 수 없습니다.", nameof(item));
    }

    private static string Format(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(cancellationToken);
    }
}
