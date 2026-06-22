using Iptv.App.ViewModels;
using Iptv.Core.Playback;

namespace Iptv.App.Tests;

public sealed class StreamHealthViewModelTests
{
    [Fact]
    public void DisplayText_RecommendsPoorNetworkBufferAndHardwareDecodeToggleAfterTimeout()
    {
        var row = new StreamHealthViewModel(
            Guid.CreateVersion7(),
            "Example",
            "example.test",
            PlaybackStatus.TimedOut,
            SuccessCount: 0,
            FailureCount: 1,
            SlowEventCount: 0,
            DateTimeOffset.UtcNow,
            "Still waiting");

        Assert.Contains("PoorNetwork buffer", row.DisplayText, StringComparison.Ordinal);
        Assert.Contains("disable hardware decoding", row.DisplayText, StringComparison.Ordinal);
    }
}
