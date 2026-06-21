using Iptv.App.ViewModels;
using Iptv.Persistence;

namespace Iptv.App.Tests;

public sealed class RecentPlaylistSourceManagerTests
{
    [Fact]
    public void Normalize_DeduplicatesPrioritizesPinnedAndLimitsResults()
    {
        var manager = new RecentPlaylistSourceManager(maximumSources: 2);
        DateTimeOffset now = DateTimeOffset.Parse("2026-06-21T12:00:00Z");
        var older = new RecentPlaylistSourceViewModel(
            RecentPlaylistSourceKind.RemoteUrl,
            "Older",
            "https://example.test/live.m3u",
            now.AddHours(-2));
        var pinnedDuplicate = older with
        {
            DisplayName = "Pinned",
            LastUsedAt = now.AddHours(-3),
            IsPinned = true
        };
        var newest = new RecentPlaylistSourceViewModel(
            RecentPlaylistSourceKind.LocalFile,
            "Local",
            "C:\\lists\\playlist.m3u",
            now);
        var extra = new RecentPlaylistSourceViewModel(
            RecentPlaylistSourceKind.RemoteUrl,
            "Extra",
            "https://example.test/extra.m3u",
            now.AddMinutes(-5));

        IReadOnlyList<RecentPlaylistSourceViewModel> normalized = manager.Normalize([older, pinnedDuplicate, newest, extra]);

        Assert.Equal(2, normalized.Count);
        Assert.Equal("Pinned", normalized[0].DisplayName);
        Assert.True(normalized[0].IsPinned);
        Assert.Equal("Local", normalized[1].DisplayName);
    }

    [Fact]
    public void Remember_PreservesExistingNameAndPinWhileUpdatingLastUsed()
    {
        var manager = new RecentPlaylistSourceManager(maximumSources: 10);
        DateTimeOffset oldTime = DateTimeOffset.Parse("2026-06-20T12:00:00Z");
        DateTimeOffset newTime = DateTimeOffset.Parse("2026-06-21T12:00:00Z");
        var existing = new RecentPlaylistSourceViewModel(
            RecentPlaylistSourceKind.RemoteUrl,
            "Provider A",
            "https://example.test/live.m3u",
            oldTime,
            IsPinned: true);
        var incoming = existing with { DisplayName = "Auto Name", IsPinned = false };

        RecentPlaylistSourceViewModel merged = manager.Remember([existing], incoming, newTime, out IReadOnlyList<RecentPlaylistSourceViewModel> updatedSources);

        RecentPlaylistSourceViewModel updated = Assert.Single(updatedSources);
        Assert.Equal("Provider A", merged.DisplayName);
        Assert.Equal("Provider A", updated.DisplayName);
        Assert.True(updated.IsPinned);
        Assert.Equal(newTime, updated.LastUsedAt);
    }

    [Fact]
    public void IsUsable_RejectsInvalidRemoteUrl()
    {
        var source = new RecentPlaylistSourceViewModel(
            RecentPlaylistSourceKind.RemoteUrl,
            "Bad",
            "not a url",
            DateTimeOffset.UtcNow);

        Assert.False(RecentPlaylistSourceManager.IsUsable(source));
    }
}
