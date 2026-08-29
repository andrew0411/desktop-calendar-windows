using System.IO;
using DesktopCalendar.Core.Models;
using DesktopCalendar.Infrastructure;
using Microsoft.Data.Sqlite;

namespace DesktopCalendar.Tests;

[TestClass]
public sealed class InfrastructureTests
{
    private string _root = null!;
    private AppPaths _paths = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "DesktopCalendarTests", Guid.NewGuid().ToString("N"));
        _paths = new AppPaths(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public async Task SettingsStore_RoundTripsAndUsesDefaults()
    {
        var store = new JsonSettingsStore(_paths);
        var defaults = await store.LoadAsync();
        Assert.AreEqual("맑은 고딕", defaults.Theme.Date.Family);
        Assert.AreEqual("Dark", defaults.AppearanceMode);
        Assert.IsFalse(defaults.Weather.IsEnabled);

        var changed = defaults with { AppearanceMode = "Light", Theme = defaults.Theme with { BackgroundOpacity = 0.55 } };
        await store.SaveAsync(changed);
        var loaded = await store.LoadAsync();
        Assert.AreEqual(0.55, loaded.Theme.BackgroundOpacity, 0.001);
        Assert.AreEqual("Light", loaded.AppearanceMode);
    }

    [TestMethod]
    public async Task Repository_CrudAndReplaceArePersistent()
    {
        var repository = new SqliteEventRepository(_paths);
        await repository.InitializeAsync();
        var first = CreateEvent("첫 일정", new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero)) with
        {
            Emoji = "📚",
            Location = "회의실 A",
            IsCompleted = true,
            IsHighlighted = true,
            HighlightColorHex = EventHighlightPalette.Colors.Single(color => color.Name == "빨강").Hex,
            IsBold = true,
            IsItalic = true,
            TitleFontSize = 19
        };
        await repository.UpsertAsync(first);
        var saved = await repository.GetAllAsync();
        Assert.HasCount(1, saved);
        Assert.AreEqual("📚", saved[0].Emoji);
        Assert.AreEqual("회의실 A", saved[0].Location);
        Assert.IsTrue(saved[0].IsCompleted);
        Assert.IsTrue(saved[0].IsHighlighted);
        Assert.AreEqual(EventHighlightPalette.Colors.Single(color => color.Name == "빨강").Hex, saved[0].HighlightColorHex);
        Assert.IsTrue(saved[0].IsBold);
        Assert.IsTrue(saved[0].IsItalic);
        Assert.AreEqual(19, saved[0].TitleFontSize, 0.001);

        var second = CreateEvent("교체 일정", new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.Zero));
        await repository.ReplaceAllAsync([second]);
        var result = await repository.GetAllAsync();
        Assert.HasCount(1, result);
        Assert.AreEqual("교체 일정", result[0].Title);

        await repository.DeleteAsync(second.Id);
        Assert.IsEmpty(await repository.GetAllAsync());
    }

    [TestMethod]
    public async Task BackupService_ExportsAndValidatesBundle()
    {
        var service = new JsonBackupService(_paths);
        var path = Path.Combine(_root, "manual.json");
        var bundle = new BackupBundle { Events = [CreateEvent("백업", DateTimeOffset.Now)] };
        await service.ExportAsync(path, bundle);
        var restored = await service.ImportAsync(path);
        Assert.HasCount(1, restored.Events);
        Assert.AreEqual("백업", restored.Events[0].Title);
    }

    [TestMethod]
    public async Task BackupService_CreatesUniqueSafetyBackupBeforeRestore()
    {
        var service = new JsonBackupService(_paths);
        var bundle = new BackupBundle { Events = [CreateEvent("복원 전", DateTimeOffset.Now)] };
        await service.CreateSafetyBackupAsync(bundle);
        var files = Directory.GetFiles(_paths.BackupsDirectory, "desktop-calendar-before-restore-*.json");
        Assert.HasCount(1, files);
    }

    [TestMethod]
    public async Task Repository_MigratesVersionOneWithoutLosingEvents()
    {
        _paths.EnsureDirectories();
        await using (var connection = new SqliteConnection($"Data Source={_paths.DatabasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE events (
                    id TEXT PRIMARY KEY NOT NULL, title TEXT NOT NULL, notes TEXT NOT NULL,
                    start_value TEXT NOT NULL, end_value TEXT NOT NULL, is_all_day INTEGER NOT NULL,
                    time_zone_id TEXT NOT NULL, color_hex TEXT NOT NULL, recurrence_rule TEXT NULL,
                    created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL
                );
                INSERT INTO events VALUES(
                    '11111111-1111-1111-1111-111111111111', '기존 일정', '',
                    '2026-08-15T00:00:00.0000000+00:00', '2026-08-15T23:59:59.9999999+00:00', 1,
                    'UTC', '#FF4F8EF7', NULL,
                    '2026-08-01T00:00:00.0000000+00:00', '2026-08-01T00:00:00.0000000+00:00'
                );
                PRAGMA user_version=1;
                """;
            await command.ExecuteNonQueryAsync();
        }

        var repository = new SqliteEventRepository(_paths);
        await repository.InitializeAsync();
        var events = await repository.GetAllAsync();

        Assert.HasCount(1, events);
        Assert.AreEqual("기존 일정", events[0].Title);
        Assert.AreEqual(string.Empty, events[0].Emoji);
        Assert.AreEqual(string.Empty, events[0].Location);
        Assert.IsFalse(events[0].IsCompleted);
        Assert.AreEqual(EventHighlightPalette.DefaultColorHex, events[0].HighlightColorHex);
        Assert.IsTrue(Directory.GetFiles(_paths.BackupsDirectory, "calendar-pre-migration-*.db").Length == 1);
    }

    private static CalendarEvent CreateEvent(string title, DateTimeOffset start) => new()
    {
        Title = title,
        Start = start,
        End = start.AddDays(1).AddTicks(-1),
        IsAllDay = true
    };
}
