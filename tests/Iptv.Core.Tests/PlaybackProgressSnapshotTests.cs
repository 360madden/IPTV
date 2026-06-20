using Iptv.Core.Playback;

namespace Iptv.Core.Tests;

public sealed class PlaybackProgressSnapshotTests
{
    [Fact]
    public void ProgressPercent_UsesTimeAndLengthWhenAvailable()
    {
        var snapshot = new PlaybackProgressSnapshot(null, 30_000, 120_000, 0.10f, DateTimeOffset.UtcNow);

        Assert.Equal(25, snapshot.ProgressPercent);
    }

    [Fact]
    public void ProgressPercent_FallsBackToPosition()
    {
        var snapshot = new PlaybackProgressSnapshot(null, -1, -1, 0.42f, DateTimeOffset.UtcNow);

        Assert.Equal(42, snapshot.ProgressPercent);
    }
}
