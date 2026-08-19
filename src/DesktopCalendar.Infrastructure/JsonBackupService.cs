using System.Text.Json;
using DesktopCalendar.Core.Abstractions;
using DesktopCalendar.Core.Models;

namespace DesktopCalendar.Infrastructure;

public sealed class JsonBackupService(AppPaths paths) : IBackupService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task CreateDailyBackupAsync(BackupBundle bundle, CancellationToken cancellationToken = default)
    {
        paths.EnsureDirectories();
        var path = Path.Combine(paths.BackupsDirectory, $"desktop-calendar-{DateTime.Today:yyyy-MM-dd}.json");
        if (!File.Exists(path))
            await WriteAsync(path, bundle, cancellationToken);

        foreach (var stale in new DirectoryInfo(paths.BackupsDirectory)
                     .GetFiles("desktop-calendar-*.json")
                     .OrderByDescending(file => file.LastWriteTimeUtc)
                     .Skip(7))
            stale.Delete();
    }

    public Task ExportAsync(string path, BackupBundle bundle, CancellationToken cancellationToken = default) =>
        WriteAsync(path, bundle, cancellationToken);

    public Task CreateSafetyBackupAsync(BackupBundle bundle, CancellationToken cancellationToken = default)
    {
        paths.EnsureDirectories();
        var path = Path.Combine(paths.BackupsDirectory, $"desktop-calendar-before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        return WriteAsync(path, bundle, cancellationToken);
    }

    public async Task<BackupBundle> ImportAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var bundle = await JsonSerializer.DeserializeAsync<BackupBundle>(stream, Options, cancellationToken)
                     ?? throw new InvalidDataException("백업 파일이 비어 있습니다.");
        if (bundle.SchemaVersion != 1)
            throw new InvalidDataException("지원하지 않는 백업 버전입니다.");
        if (bundle.Events.Any(item => item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Title) || item.End < item.Start))
            throw new InvalidDataException("백업에 유효하지 않은 일정이 포함되어 있습니다.");
        return bundle;
    }

    private static async Task WriteAsync(string path, BackupBundle bundle, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        var temporaryPath = $"{path}.tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, bundle, Options, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        File.Move(temporaryPath, path, overwrite: true);
    }
}
