using System.Text.Json;
using DesktopCalendar.Core.Abstractions;
using DesktopCalendar.Core.Models;

namespace DesktopCalendar.Infrastructure;

public sealed class JsonSettingsStore(AppPaths paths) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureDirectories();
        if (!File.Exists(paths.SettingsPath))
            return new AppSettings();

        try
        {
            await using var stream = File.OpenRead(paths.SettingsPath);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, cancellationToken) ?? new AppSettings();
        }
        catch (JsonException)
        {
            var corruptPath = $"{paths.SettingsPath}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(paths.SettingsPath, corruptPath, overwrite: true);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        paths.EnsureDirectories();
        var temporaryPath = $"{paths.SettingsPath}.tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        File.Move(temporaryPath, paths.SettingsPath, overwrite: true);
    }
}

