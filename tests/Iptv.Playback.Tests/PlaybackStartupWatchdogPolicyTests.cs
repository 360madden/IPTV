using Iptv.Core.Playback;

namespace Iptv.Playback.Tests;

public sealed class PlaybackStartupWatchdogPolicyTests
{
    [Theory]
    [InlineData(PlaybackStatus.Loading)]
    [InlineData(PlaybackStatus.Buffering)]
    [InlineData(PlaybackStatus.TimedOut)]
    public void ShouldPromoteAlivePlayback_ForStartupAndTimeoutStates(PlaybackStatus status)
    {
        Assert.True(PlaybackStartupWatchdogPolicy.ShouldPromoteAlivePlayback(status, isPlaybackAlive: true));
        Assert.False(PlaybackStartupWatchdogPolicy.ShouldPromoteAlivePlayback(status, isPlaybackAlive: false));
    }

    [Theory]
    [InlineData(PlaybackStatus.Loading)]
    [InlineData(PlaybackStatus.Buffering)]
    public void ShouldTimeout_OnlyWhenStartupStateHasNoAlivePlayback(PlaybackStatus status)
    {
        Assert.True(PlaybackStartupWatchdogPolicy.ShouldTimeout(status, isPlaybackAlive: false));
        Assert.False(PlaybackStartupWatchdogPolicy.ShouldTimeout(status, isPlaybackAlive: true));
    }

    [Fact]
    public void ShouldClearActivePlayback_DoesNotClearForTimeoutWarning()
    {
        Assert.False(PlaybackStartupWatchdogPolicy.ShouldClearActivePlayback(PlaybackStatus.TimedOut));
        Assert.True(PlaybackStartupWatchdogPolicy.ShouldClearActivePlayback(PlaybackStatus.Stopped));
        Assert.True(PlaybackStartupWatchdogPolicy.ShouldClearActivePlayback(PlaybackStatus.Failed));
        Assert.True(PlaybackStartupWatchdogPolicy.ShouldClearActivePlayback(PlaybackStatus.Unsupported));
    }
}
