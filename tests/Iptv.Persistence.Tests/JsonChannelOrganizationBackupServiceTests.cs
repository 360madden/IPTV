using Iptv.Core.Channels;
using Iptv.Core.Playback;

namespace Iptv.Persistence.Tests;

public sealed class JsonChannelOrganizationBackupServiceTests
{
    [Fact]
    public async Task ExportAndImportAsync_RoundTripsOrganizationBackup()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-org-backup-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            string path = Path.Combine(tempDirectory, "organization.json");
            var service = new JsonChannelOrganizationBackupService();
            Guid channelId = Guid.CreateVersion7();

            await service.ExportAsync(
                path,
                new ChannelOrganizationBackup
                {
                    Preferences = new ChannelOrganizationPreferences
                    {
                        SortMode = ChannelSortMode.CustomOrder,
                        CustomGroups = ["  News  ", "News", "Sports"],
                        LargeLibraryMode = true,
                        ChannelViewDensity = ChannelViewDensity.Compact,
                        SourceProfileNames = new Dictionary<string, string>
                        {
                            ["source-a"] = "Provider A"
                        },
                        SourcePlaybackProfiles = new Dictionary<string, ProviderPlaybackProfile>
                        {
                            ["source-a"] = new() { RetryCount = 2, BufferingPreset = BufferingPreset.LowLatency }
                        },
                        RefreshScheduleEnabled = true,
                        RefreshIntervalMinutes = 120,
                        ParentalPinSalt = "salt",
                        ParentalPinHash = "hash",
                        LockedGroups = ["Kids", "Kids", "Sports"]
                    },
                    ChannelStates =
                    [
                        new ChannelUserState
                        {
                            ChannelId = channelId,
                            IsFavorite = true,
                            IsHidden = true,
                            CustomGroup = "News",
                            CustomSortIndex = 2,
                            LastWatchedAt = DateTimeOffset.UtcNow,
                            ResumeProgressPercent = 88
                        }
                    ]
                },
                CancellationToken.None);

            ChannelOrganizationBackup imported = await service.ImportAsync(path, CancellationToken.None);

            Assert.Equal(1, imported.Version);
            Assert.Equal(ChannelSortMode.CustomOrder, imported.Preferences.SortMode);
            Assert.Equal(["News", "Sports"], imported.Preferences.CustomGroups);
            Assert.True(imported.Preferences.LargeLibraryMode);
            Assert.Equal(ChannelViewDensity.Compact, imported.Preferences.ChannelViewDensity);
            Assert.Equal("Provider A", imported.Preferences.SourceProfileNames["source-a"]);
            Assert.Equal(2, imported.Preferences.SourcePlaybackProfiles["source-a"].RetryCount);
            Assert.Equal(BufferingPreset.LowLatency, imported.Preferences.SourcePlaybackProfiles["source-a"].BufferingPreset);
            Assert.True(imported.Preferences.RefreshScheduleEnabled);
            Assert.Equal(120, imported.Preferences.RefreshIntervalMinutes);
            Assert.Equal("salt", imported.Preferences.ParentalPinSalt);
            Assert.Equal("hash", imported.Preferences.ParentalPinHash);
            Assert.Equal(["Kids", "Sports"], imported.Preferences.LockedGroups);
            ChannelUserState state = Assert.Single(imported.ChannelStates);
            Assert.Equal(channelId, state.ChannelId);
            Assert.True(state.IsFavorite);
            Assert.True(state.IsHidden);
            Assert.Equal("News", state.CustomGroup);
            Assert.Equal(2, state.CustomSortIndex);
            Assert.NotNull(state.LastWatchedAt);
            Assert.Equal(88, state.ResumeProgressPercent);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ImportAsync_RejectsUnsupportedVersion()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"iptv-org-backup-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDirectory);
            string path = Path.Combine(tempDirectory, "organization.json");
            await File.WriteAllTextAsync(path, """{"version":99}""", CancellationToken.None);
            var service = new JsonChannelOrganizationBackupService();

            await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(path, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
