using Iptv.Core.PlaylistImport;

namespace Iptv.Epg.Tests;

public sealed class XmltvImportServiceTests
{
    [Fact]
    public async Task ImportFileAsync_ParsesChannelsAndPrograms()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <tv>
              <channel id="sample.news">
                <display-name>Sample News</display-name>
              </channel>
              <programme channel="sample.news" start="20260620120000 +0000" stop="20260620123000 +0000">
                <title>Midday News</title>
                <desc>Headlines.</desc>
              </programme>
            </tv>
            """;

        string path = Path.Combine(Path.GetTempPath(), $"xmltv-{Guid.NewGuid():N}.xml");
        try
        {
            await File.WriteAllTextAsync(path, xml, CancellationToken.None);
            var service = new XmltvImportService();

            var result = await service.ImportFileAsync(path, CancellationToken.None);

            Assert.Single(result.Channels);
            Assert.Single(result.Programs);
            Assert.Equal("sample.news", result.Channels[0].Id);
            Assert.Equal("Midday News", result.Programs[0].Title);
            Assert.DoesNotContain(result.Issues, issue => issue.Severity == ImportIssueSeverity.Error);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
