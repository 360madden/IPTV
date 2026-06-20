using System.Text.Json;

namespace Iptv.Persistence.SmartGroups;

public sealed class JsonSmartGroupPresetFileService : ISmartGroupPresetFileService
{
    private const int CurrentVersion = 1;
    private const long MaximumImportBytes = 2L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task ExportAsync(string path, IEnumerable<SmartGroupRulePreset> presets, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Export path is required.", nameof(path));
        }

        ArgumentNullException.ThrowIfNull(presets);

        string directory = Path.GetDirectoryName(path) ?? ".";
        Directory.CreateDirectory(directory);
        var export = new SmartGroupPresetExport
        {
            Version = CurrentVersion,
            ExportedAt = DateTimeOffset.UtcNow,
            Presets = NormalizePresets(presets).ToArray()
        };

        string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, export, JsonOptions, cancellationToken).ConfigureAwait(false);
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

    public async Task<IReadOnlyList<SmartGroupRulePreset>> ImportAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Import path is required.", nameof(path));
        }

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Smart group preset file was not found.", path);
        }

        if (fileInfo.Length > MaximumImportBytes)
        {
            throw new InvalidDataException("Smart group preset file is too large to import safely.");
        }

        await using FileStream stream = File.OpenRead(path);
        SmartGroupPresetExport? export = await JsonSerializer
            .DeserializeAsync<SmartGroupPresetExport>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (export is null)
        {
            throw new InvalidDataException("Smart group preset file is empty or invalid.");
        }

        if (export.Version != CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported smart group preset version {export.Version}.");
        }

        return NormalizePresets(export.Presets ?? []).ToArray();
    }

    private static IEnumerable<SmartGroupRulePreset> NormalizePresets(IEnumerable<SmartGroupRulePreset> presets)
    {
        return presets
            .Select(NormalizePreset)
            .Where(preset => preset is not null)
            .Select(preset => preset!)
            .GroupBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static SmartGroupRulePreset? NormalizePreset(SmartGroupRulePreset? preset)
    {
        if (preset is null)
        {
            return null;
        }

        string? name = NormalizeText(preset.Name);
        string? matchText = NormalizeText(preset.MatchText);
        string? destination = NormalizeText(preset.DestinationGroup);
        if (name is null || matchText is null || destination is null)
        {
            return null;
        }

        return preset with
        {
            Name = name,
            MatchText = matchText,
            DestinationGroup = destination,
            MatchMode = Enum.IsDefined(preset.MatchMode)
                ? preset.MatchMode
                : SmartRuleMatchMode.ContainsAny
        };
    }

    private static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length == 0 ? null : normalized;
    }
}
