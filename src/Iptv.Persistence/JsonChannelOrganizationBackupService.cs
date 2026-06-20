using System.Text.Json;
using Iptv.Core.Channels;

namespace Iptv.Persistence;

public sealed class JsonChannelOrganizationBackupService : IChannelOrganizationBackupService
{
    private const int CurrentVersion = 1;
    private const long MaximumImportBytes = 128L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task ExportAsync(string path, ChannelOrganizationBackup backup, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Export path is required.", nameof(path));
        }

        ArgumentNullException.ThrowIfNull(backup);

        string directory = Path.GetDirectoryName(path) ?? ".";
        Directory.CreateDirectory(directory);
        ChannelOrganizationBackup normalized = Normalize(backup) with
        {
            Version = CurrentVersion,
            ExportedAt = DateTimeOffset.UtcNow
        };

        string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken).ConfigureAwait(false);
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

    public async Task<ChannelOrganizationBackup> ImportAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Import path is required.", nameof(path));
        }

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Organization backup file was not found.", path);
        }

        if (fileInfo.Length > MaximumImportBytes)
        {
            throw new InvalidDataException("Organization backup is too large to import safely.");
        }

        await using FileStream stream = File.OpenRead(path);
        ChannelOrganizationBackup? backup = await JsonSerializer
            .DeserializeAsync<ChannelOrganizationBackup>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (backup is null)
        {
            throw new InvalidDataException("Organization backup is empty or invalid.");
        }

        if (backup.Version != CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported organization backup version {backup.Version}.");
        }

        return Normalize(backup);
    }

    private static ChannelOrganizationBackup Normalize(ChannelOrganizationBackup backup)
    {
        ChannelOrganizationPreferences preferences = NormalizePreferences(backup.Preferences);
        ChannelUserState[] states = (backup.ChannelStates ?? [])
            .Select(NormalizeState)
            .Where(HasUserState)
            .GroupBy(state => state.ChannelId)
            .Select(MergeStates)
            .OrderBy(state => state.ChannelId)
            .ToArray();

        return backup with
        {
            Preferences = preferences,
            ChannelStates = states
        };
    }

    private static ChannelOrganizationPreferences NormalizePreferences(ChannelOrganizationPreferences? preferences)
    {
        preferences ??= new ChannelOrganizationPreferences();
        string[] customGroups = (preferences.CustomGroups ?? [])
            .Select(NormalizeCustomGroup)
            .Where(group => group is not null)
            .Select(group => group!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Dictionary<string, string> profileNames = NormalizeProfileNames(preferences.SourceProfileNames);

        return preferences with
        {
            SortMode = Enum.IsDefined(preferences.SortMode)
                ? preferences.SortMode
                : ChannelSortMode.FavoritesFirst,
            CustomGroups = customGroups,
            ChannelViewDensity = Enum.IsDefined(preferences.ChannelViewDensity)
                ? preferences.ChannelViewDensity
                : ChannelViewDensity.Comfortable,
            SourceProfileNames = profileNames
        };
    }

    private static ChannelUserState MergeStates(IEnumerable<ChannelUserState> states)
    {
        ChannelUserState[] snapshot = states.ToArray();
        ChannelUserState first = snapshot[0];
        return first with
        {
            IsFavorite = snapshot.Any(state => state.IsFavorite),
            IsHidden = snapshot.Any(state => state.IsHidden),
            CustomGroup = snapshot.LastOrDefault(state => !string.IsNullOrWhiteSpace(state.CustomGroup))?.CustomGroup,
            CustomSortIndex = snapshot.LastOrDefault(state => state.CustomSortIndex.HasValue)?.CustomSortIndex,
            LastWatchedAt = snapshot
                .Where(state => state.LastWatchedAt.HasValue)
                .Select(state => state.LastWatchedAt)
                .DefaultIfEmpty()
                .Max()
        };
    }

    private static ChannelUserState NormalizeState(ChannelUserState state)
    {
        return state with
        {
            CustomGroup = NormalizeCustomGroup(state.CustomGroup),
            CustomSortIndex = state.CustomSortIndex < 0 ? null : state.CustomSortIndex
        };
    }

    private static bool HasUserState(ChannelUserState state)
    {
        return state.ChannelId != Guid.Empty &&
            (state.IsFavorite ||
                state.IsHidden ||
                !string.IsNullOrWhiteSpace(state.CustomGroup) ||
                state.CustomSortIndex.HasValue ||
                state.LastWatchedAt.HasValue);
    }

    private static string? NormalizeCustomGroup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length == 0 ? null : normalized;
    }

    private static Dictionary<string, string> NormalizeProfileNames(IDictionary<string, string>? profileNames)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (profileNames is null)
        {
            return normalized;
        }

        foreach ((string sourceId, string profileName) in profileNames)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                continue;
            }

            string? name = NormalizeCustomGroup(profileName);
            if (name is not null)
            {
                normalized[sourceId.Trim()] = name;
            }
        }

        return normalized;
    }
}
