using Iptv.Persistence;
using System.Text.Json;

namespace Iptv.Persistence.RecentPlaylists;

public sealed class JsonRecentPlaylistSourceFileService : IRecentPlaylistSourceFileService
{
    private const int CurrentVersion = 1;
    private const long MaximumImportBytes = 2L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<RecentPlaylistSourcesExport> ImportAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Recent playlist source import path is empty.", nameof(path));
        }

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Recent playlist source file was not found.", path);
        }

        if (fileInfo.Length > MaximumImportBytes)
        {
            throw new InvalidDataException("Recent playlist source file is too large to import safely.");
        }

        await using FileStream stream = File.OpenRead(path);
        RecentPlaylistSourcesExport? export = await JsonSerializer
            .DeserializeAsync<RecentPlaylistSourcesExport>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (export is null)
        {
            throw new InvalidDataException("Recent playlist source file was empty or invalid.");
        }

        if (export.Version != CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported recent playlist source export version {export.Version}.");
        }

        return Normalize(export);
    }

    public async Task ExportAsync(string path, RecentPlaylistSourcesExport export, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Recent playlist source export path is empty.", nameof(path));
        }

        ArgumentNullException.ThrowIfNull(export);

        string directory = Path.GetDirectoryName(path) ?? ".";
        Directory.CreateDirectory(directory);

        string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer
                    .SerializeAsync(stream, Normalize(export) with { Version = CurrentVersion, ExportedAt = DateTimeOffset.UtcNow }, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static RecentPlaylistSourcesExport Normalize(RecentPlaylistSourcesExport export)
    {
        RecentPlaylistSourcePreference[] sources = (export.Sources ?? [])
            .Select(Normalize)
            .Where(source => source is not null)
            .Select(source => source!)
            .GroupBy(source => $"{source.Kind}|{source.Value}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(source => source.IsPinned).ThenByDescending(source => source.LastUsedAt).First())
            .OrderByDescending(source => source.IsPinned)
            .ThenByDescending(source => source.LastUsedAt)
            .Take(50)
            .ToArray();

        return export with
        {
            Version = CurrentVersion,
            Sources = sources
        };
    }

    private static RecentPlaylistSourcePreference? Normalize(RecentPlaylistSourcePreference? source)
    {
        if (source is null || string.IsNullOrWhiteSpace(source.Value) || !Enum.IsDefined(source.Kind))
        {
            return null;
        }

        string value = source.Value.Trim();
        string displayName = string.Join(' ', (source.DisplayName ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (displayName.Length == 0)
        {
            displayName = source.Kind == RecentPlaylistSourceKind.RemoteUrl ? "Playlist URL" : Path.GetFileName(value);
        }

        return source with
        {
            DisplayName = displayName,
            Value = value,
            LastUsedAt = source.LastUsedAt == default ? DateTimeOffset.UtcNow : source.LastUsedAt
        };
    }
}
