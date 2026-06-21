using System.Text.Json;
using Iptv.Core.Playback;

namespace Iptv.Persistence.SourceProfiles;

public sealed class JsonSourceProfileFileService : ISourceProfileFileService
{
    private const long MaximumImportBytes = 2L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<SourceProfileExport> ImportAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Source profile import path is empty.", nameof(path));
        }

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Source profile file was not found.", path);
        }

        if (fileInfo.Length > MaximumImportBytes)
        {
            throw new InvalidDataException("Source profile file is too large to import safely.");
        }

        await using FileStream stream = File.OpenRead(path);
        SourceProfileExport? export = await JsonSerializer
            .DeserializeAsync<SourceProfileExport>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (export is null)
        {
            throw new InvalidDataException("Source profile file was empty or invalid.");
        }

        if (export.Version != 1)
        {
            throw new InvalidDataException($"Unsupported source profile export version {export.Version}.");
        }

        return Normalize(export);
    }

    public async Task ExportAsync(string path, SourceProfileExport export, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Source profile export path is empty.", nameof(path));
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
                    .SerializeAsync(stream, Normalize(export) with { Version = 1 }, JsonOptions, cancellationToken)
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

    private static SourceProfileExport Normalize(SourceProfileExport export)
    {
        return new SourceProfileExport
        {
            Version = 1,
            SourceProfileNames = (export.SourceProfileNames ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase),
            SourcePlaybackProfiles = (export.SourcePlaybackProfiles ?? new Dictionary<string, ProviderPlaybackProfile>(StringComparer.OrdinalIgnoreCase))
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(pair => pair.Key.Trim(), pair => Normalize(pair.Value), StringComparer.OrdinalIgnoreCase)
        };
    }

    private static ProviderPlaybackProfile Normalize(ProviderPlaybackProfile profile)
    {
        return new ProviderPlaybackProfile
        {
            RetryCount = Math.Clamp(profile.RetryCount, 0, 3),
            BufferingPreset = Enum.IsDefined(profile.BufferingPreset)
                ? profile.BufferingPreset
                : BufferingPreset.Balanced
        };
    }
}
