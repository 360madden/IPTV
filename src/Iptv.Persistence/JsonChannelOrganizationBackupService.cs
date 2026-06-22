using System.Text.Json;
using Iptv.Core.Channels;
using Iptv.Core.Playback;

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
        Dictionary<string, ProviderPlaybackProfile> playbackProfiles = NormalizePlaybackProfiles(preferences.SourcePlaybackProfiles);
        Dictionary<string, AppAppearancePreset> appearancePresets = NormalizeAppearancePresets(preferences.SourceAppearancePresets);
        Dictionary<string, string[]> sourceHiddenGroups = NormalizeSourceHiddenGroups(preferences.SourceDefaultHiddenGroups);
        string[] lockedGroups = NormalizeGroups(preferences.LockedGroups);

        return preferences with
        {
            SortMode = Enum.IsDefined(preferences.SortMode)
                ? preferences.SortMode
                : ChannelSortMode.FavoritesFirst,
            CustomGroups = customGroups,
            ChannelViewDensity = Enum.IsDefined(preferences.ChannelViewDensity)
                ? preferences.ChannelViewDensity
                : ChannelViewDensity.Comfortable,
            SourceProfileNames = profileNames,
            SourcePlaybackProfiles = playbackProfiles,
            SourceAppearancePresets = appearancePresets,
            SourceDefaultHiddenGroups = sourceHiddenGroups,
            RefreshIntervalMinutes = NormalizeRefreshInterval(preferences.RefreshIntervalMinutes),
            LockedGroups = lockedGroups,
            ParentalPinSalt = NormalizeSecret(preferences.ParentalPinSalt),
            ParentalPinHash = NormalizeSecret(preferences.ParentalPinHash),
            XmltvGuideUrl = NormalizeRemoteUrl(preferences.XmltvGuideUrl)
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
            HasExplicitVisibility = snapshot.Any(state => state.HasExplicitVisibility || state.IsHidden),
            CustomGroup = snapshot.LastOrDefault(state => !string.IsNullOrWhiteSpace(state.CustomGroup))?.CustomGroup,
            CustomSortIndex = snapshot.LastOrDefault(state => state.CustomSortIndex.HasValue)?.CustomSortIndex,
            LastWatchedAt = snapshot
                .Where(state => state.LastWatchedAt.HasValue)
                .Select(state => state.LastWatchedAt)
                .DefaultIfEmpty()
                .Max(),
            ResumeProgressPercent = snapshot.LastOrDefault(state => state.ResumeProgressPercent.HasValue)?.ResumeProgressPercent
        };
    }

    private static ChannelUserState NormalizeState(ChannelUserState state)
    {
        return state with
        {
            CustomGroup = NormalizeCustomGroup(state.CustomGroup),
            CustomSortIndex = state.CustomSortIndex < 0 ? null : state.CustomSortIndex,
            ResumeProgressPercent = NormalizeResumeProgress(state.ResumeProgressPercent)
        };
    }

    private static bool HasUserState(ChannelUserState state)
    {
        return state.ChannelId != Guid.Empty &&
            (state.IsFavorite ||
                state.IsHidden ||
                state.HasExplicitVisibility ||
                !string.IsNullOrWhiteSpace(state.CustomGroup) ||
                state.CustomSortIndex.HasValue ||
                state.LastWatchedAt.HasValue ||
                state.ResumeProgressPercent.HasValue);
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

    private static Dictionary<string, ProviderPlaybackProfile> NormalizePlaybackProfiles(
        IDictionary<string, ProviderPlaybackProfile>? playbackProfiles)
    {
        var normalized = new Dictionary<string, ProviderPlaybackProfile>(StringComparer.OrdinalIgnoreCase);
        if (playbackProfiles is null)
        {
            return normalized;
        }

        foreach ((string sourceId, ProviderPlaybackProfile profile) in playbackProfiles)
        {
            if (string.IsNullOrWhiteSpace(sourceId) || profile is null)
            {
                continue;
            }

            BufferingPreset bufferingPreset = Enum.IsDefined(profile.BufferingPreset)
                ? profile.BufferingPreset
                : BufferingPreset.Balanced;
            normalized[sourceId.Trim()] = new ProviderPlaybackProfile
            {
                RetryCount = Math.Clamp(profile.RetryCount, 0, 3),
                BufferingPreset = bufferingPreset,
                HardwareDecodingDisabled = profile.HardwareDecodingDisabled
            };
        }

        return normalized;
    }

    private static Dictionary<string, AppAppearancePreset> NormalizeAppearancePresets(
        IDictionary<string, AppAppearancePreset>? appearancePresets)
    {
        var normalized = new Dictionary<string, AppAppearancePreset>(StringComparer.OrdinalIgnoreCase);
        if (appearancePresets is null)
        {
            return normalized;
        }

        foreach ((string sourceId, AppAppearancePreset preset) in appearancePresets)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                continue;
            }

            AppAppearancePreset normalizedPreset = Enum.IsDefined(preset) ? preset : AppAppearancePreset.Custom;
            if (normalizedPreset != AppAppearancePreset.Custom)
            {
                normalized[sourceId.Trim()] = normalizedPreset;
            }
        }

        return normalized;
    }

    private static Dictionary<string, string[]> NormalizeSourceHiddenGroups(IDictionary<string, string[]>? sourceHiddenGroups)
    {
        var normalized = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (sourceHiddenGroups is null)
        {
            return normalized;
        }

        foreach ((string sourceId, string[] groups) in sourceHiddenGroups)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                continue;
            }

            string[] hiddenGroups = NormalizeGroups(groups);
            if (hiddenGroups.Length > 0)
            {
                normalized[sourceId.Trim()] = hiddenGroups;
            }
        }

        return normalized;
    }

    private static string[] NormalizeGroups(IEnumerable<string>? groups)
    {
        return (groups ?? [])
            .Select(NormalizeCustomGroup)
            .Where(group => group is not null)
            .Select(group => group!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int NormalizeRefreshInterval(int minutes)
    {
        return Math.Clamp(minutes <= 0 ? 60 : minutes, 5, 24 * 60);
    }

    private static int? NormalizeResumeProgress(int? value)
    {
        return value is null ? null : Math.Clamp(value.Value, 0, 100);
    }

    private static string? NormalizeSecret(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeRemoteUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri) &&
            uri.Scheme is "http" or "https"
            ? uri.ToString()
            : null;
    }
}
