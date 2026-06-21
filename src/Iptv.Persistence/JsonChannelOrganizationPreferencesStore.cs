using System.Text.Json;
using Iptv.Core.Channels;
using Iptv.Core.Playback;

namespace Iptv.Persistence;

public sealed class JsonChannelOrganizationPreferencesStore : IChannelOrganizationPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly SemaphoreSlim ioGate = new(1, 1);

    public JsonChannelOrganizationPreferencesStore(string? appDataDirectory = null)
    {
        string root = appDataDirectory ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IptvViewer");
        Directory.CreateDirectory(root);
        filePath = Path.Combine(root, "channel-organization-preferences.json");
    }

    public async Task<ChannelOrganizationPreferences> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new ChannelOrganizationPreferences();
        }

        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using FileStream stream = File.OpenRead(filePath);
            ChannelOrganizationPreferences? preferences = await JsonSerializer
                .DeserializeAsync<ChannelOrganizationPreferences>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return Normalize(preferences);
        }
        catch (JsonException)
        {
            return new ChannelOrganizationPreferences();
        }
        catch (IOException)
        {
            return new ChannelOrganizationPreferences();
        }
        finally
        {
            ioGate.Release();
        }
    }

    public async Task SaveAsync(ChannelOrganizationPreferences preferences, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        ChannelOrganizationPreferences normalized = Normalize(preferences);
        string directory = Path.GetDirectoryName(filePath) ?? ".";
        Directory.CreateDirectory(directory);

        string tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        await ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            ioGate.Release();
        }
    }

    private static ChannelOrganizationPreferences Normalize(ChannelOrganizationPreferences? preferences)
    {
        if (preferences is null)
        {
            return new ChannelOrganizationPreferences();
        }

        string[] customGroups = preferences.CustomGroups
            .Select(NormalizeCustomGroup)
            .Where(group => group is not null)
            .Select(group => group!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Dictionary<string, string> profileNames = NormalizeProfileNames(preferences.SourceProfileNames);
        Dictionary<string, ProviderPlaybackProfile> playbackProfiles = NormalizePlaybackProfiles(preferences.SourcePlaybackProfiles);
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
            SourceDefaultHiddenGroups = sourceHiddenGroups,
            RefreshIntervalMinutes = NormalizeRefreshInterval(preferences.RefreshIntervalMinutes),
            LockedGroups = lockedGroups,
            ParentalPinSalt = NormalizeSecret(preferences.ParentalPinSalt),
            ParentalPinHash = NormalizeSecret(preferences.ParentalPinHash),
            XmltvGuideUrl = NormalizeRemoteUrl(preferences.XmltvGuideUrl)
        };
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
                BufferingPreset = bufferingPreset
            };
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
