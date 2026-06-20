using Iptv.Core.Channels;

namespace Iptv.Core.Tests;

public sealed class ChannelNormalizerTests
{
    [Fact]
    public void NormalizeForSearch_RemovesCaseAndAccents()
    {
        string normalized = ChannelNormalizer.NormalizeForSearch("  Café SPORTS  ");

        Assert.Equal("cafe sports", normalized);
    }

    [Fact]
    public void InferCategory_UsesGroupAndNameSignals()
    {
        string category = ChannelNormalizer.InferCategory("US Sports", "Example HD");

        Assert.Equal("Sports", category);
    }
}
