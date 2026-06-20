using Iptv.Core.Channels;

namespace Iptv.Core.Tests;

public sealed class ChannelMetadataExtractorTests
{
    [Theory]
    [InlineData("Movie Title (1999)", 1999)]
    [InlineData("Series S01E01 2024", 2024)]
    [InlineData("No year here", null)]
    public void TryInferReleaseYear_FindsPlausibleYears(string value, int? expected)
    {
        Assert.Equal(expected, ChannelMetadataExtractor.TryInferReleaseYear(value));
    }
}
