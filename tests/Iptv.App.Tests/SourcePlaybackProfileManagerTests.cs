using Iptv.App.ViewModels;
using Iptv.Core.Playback;
using Iptv.Persistence;

namespace Iptv.App.Tests;

public sealed class SourcePlaybackProfileManagerTests
{
    [Fact]
    public void Normalize_ClampsRetryAndInvalidBufferWhilePreservingDecodeMode()
    {
        var profile = new ProviderPlaybackProfile
        {
            RetryCount = 10,
            BufferingPreset = (BufferingPreset)99,
            HardwareDecodingDisabled = true
        };

        ProviderPlaybackProfile normalized = SourcePlaybackProfileManager.Normalize(profile);

        Assert.Equal(3, normalized.RetryCount);
        Assert.Equal(BufferingPreset.Balanced, normalized.BufferingPreset);
        Assert.True(normalized.HardwareDecodingDisabled);
    }

    [Fact]
    public void SaveCurrentSettings_PreservesExistingRetryAndStoresCurrentRecoverySettings()
    {
        const string sourceId = "source-a";
        var profiles = new Dictionary<string, ProviderPlaybackProfile>(StringComparer.OrdinalIgnoreCase)
        {
            [sourceId] = new()
            {
                RetryCount = 2,
                BufferingPreset = BufferingPreset.LowLatency,
                HardwareDecodingDisabled = false
            }
        };

        ProviderPlaybackProfile saved = SourcePlaybackProfileManager.SaveCurrentSettings(
            profiles,
            sourceId,
            BufferingPreset.PoorNetwork,
            hardwareDecodingDisabled: true);

        Assert.Equal(2, saved.RetryCount);
        Assert.Equal(BufferingPreset.PoorNetwork, saved.BufferingPreset);
        Assert.True(saved.HardwareDecodingDisabled);
        Assert.Equal(saved, profiles[sourceId]);
    }

    [Fact]
    public void FormatAppliedStatus_LabelsSavedAndCurrentProfileSources()
    {
        var profile = new ProviderPlaybackProfile
        {
            RetryCount = 1,
            BufferingPreset = BufferingPreset.PoorNetwork,
            HardwareDecodingDisabled = true
        };

        string saved = SourcePlaybackProfileManager.FormatAppliedStatus(" Provider A ", profile, isSavedProfile: true);
        string current = SourcePlaybackProfileManager.FormatAppliedStatus("Provider A", profile, isSavedProfile: false);

        Assert.Contains("saved source profile", saved, StringComparison.Ordinal);
        Assert.Contains("current playback controls", current, StringComparison.Ordinal);
        Assert.Contains("PoorNetwork buffer", saved, StringComparison.Ordinal);
        Assert.Contains("hardware decoding disabled", saved, StringComparison.Ordinal);
    }
}
