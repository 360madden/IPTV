using Iptv.Core.Playback;
using Iptv.Persistence;

namespace Iptv.App.ViewModels;

public static class SourcePlaybackProfileManager
{
    public static ProviderPlaybackProfile Normalize(ProviderPlaybackProfile profile)
    {
        BufferingPreset bufferingPreset = Enum.IsDefined(profile.BufferingPreset)
            ? profile.BufferingPreset
            : BufferingPreset.Balanced;
        return new ProviderPlaybackProfile
        {
            RetryCount = Math.Clamp(profile.RetryCount, 0, 3),
            BufferingPreset = bufferingPreset,
            HardwareDecodingDisabled = profile.HardwareDecodingDisabled
        };
    }

    public static ProviderPlaybackProfile BuildCurrentSettingsProfile(
        ProviderPlaybackProfile? existingProfile,
        BufferingPreset bufferingPreset,
        bool hardwareDecodingDisabled)
    {
        ProviderPlaybackProfile existing = existingProfile is null
            ? new ProviderPlaybackProfile()
            : Normalize(existingProfile);

        return Normalize(existing with
        {
            BufferingPreset = bufferingPreset,
            HardwareDecodingDisabled = hardwareDecodingDisabled
        });
    }

    public static ProviderPlaybackProfile SaveCurrentSettings(
        IDictionary<string, ProviderPlaybackProfile> playbackProfiles,
        string sourceId,
        BufferingPreset bufferingPreset,
        bool hardwareDecodingDisabled)
    {
        ArgumentNullException.ThrowIfNull(playbackProfiles);
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Source ID is required.", nameof(sourceId));
        }

        string key = sourceId.Trim();
        playbackProfiles.TryGetValue(key, out ProviderPlaybackProfile? existingProfile);
        ProviderPlaybackProfile profile = BuildCurrentSettingsProfile(
            existingProfile,
            bufferingPreset,
            hardwareDecodingDisabled);
        playbackProfiles[key] = profile;
        return profile;
    }

    public static string FormatConflictValue(ProviderPlaybackProfile profile)
    {
        ProviderPlaybackProfile normalized = Normalize(profile);
        return $"{normalized.RetryCount:N0}/{normalized.BufferingPreset}/{FormatHardwareDecodingMode(normalized.HardwareDecodingDisabled)}";
    }

    public static string FormatSettings(ProviderPlaybackProfile profile)
    {
        ProviderPlaybackProfile normalized = Normalize(profile);
        return $"{normalized.RetryCount:N0} retries, {normalized.BufferingPreset} buffer, {FormatHardwareDecodingMode(normalized.HardwareDecodingDisabled)}";
    }

    public static string FormatSavedStatus(string sourceDisplayName, ProviderPlaybackProfile profile)
    {
        return $"Saved playback profile for '{NormalizeSourceDisplayName(sourceDisplayName)}': {FormatSettings(profile)}.";
    }

    public static string FormatAppliedStatus(string sourceDisplayName, ProviderPlaybackProfile profile, bool isSavedProfile)
    {
        string profileKind = isSavedProfile ? "saved source profile" : "current playback controls";
        return $"Playback profile: {profileKind} for '{NormalizeSourceDisplayName(sourceDisplayName)}' ({FormatSettings(profile)}).";
    }

    private static string FormatHardwareDecodingMode(bool hardwareDecodingDisabled)
    {
        return hardwareDecodingDisabled ? "hardware decoding disabled" : "hardware decoding enabled";
    }

    private static string NormalizeSourceDisplayName(string sourceDisplayName)
    {
        return string.IsNullOrWhiteSpace(sourceDisplayName)
            ? "selected source"
            : sourceDisplayName.Trim();
    }
}
