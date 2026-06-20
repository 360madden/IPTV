using Iptv.Core.PlaylistImport;
using System.IO.Compression;

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

    [Fact]
    public async Task ImportFileAsync_ParsesGzipXmltv()
    {
        string path = Path.Combine(Path.GetTempPath(), $"xmltv-{Guid.NewGuid():N}.xml.gz");
        try
        {
            await using (FileStream file = File.Create(path))
            await using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
            await using (var writer = new StreamWriter(gzip))
            {
                await writer.WriteAsync(CreateXmltv("Gzip News"));
            }

            var service = new XmltvImportService();

            var result = await service.ImportFileAsync(path, CancellationToken.None);

            Assert.Single(result.Channels);
            Assert.Equal("Gzip News", result.Channels[0].DisplayName);
            Assert.DoesNotContain(result.Issues, issue => issue.Severity == ImportIssueSeverity.Error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportFileAsync_ParsesZipXmltv()
    {
        string path = Path.Combine(Path.GetTempPath(), $"xmltv-{Guid.NewGuid():N}.zip");
        try
        {
            using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("guide.xml");
                await using Stream stream = entry.Open();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(CreateXmltv("Zip News"));
            }

            var service = new XmltvImportService();

            var result = await service.ImportFileAsync(path, CancellationToken.None);

            Assert.Single(result.Channels);
            Assert.Equal("Zip News", result.Channels[0].DisplayName);
            Assert.DoesNotContain(result.Issues, issue => issue.Severity == ImportIssueSeverity.Error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateXmltv(string displayName)
    {
        return $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <tv>
              <channel id="sample.news">
                <display-name>{{displayName}}</display-name>
              </channel>
              <programme channel="sample.news" start="20260620120000 +0000" stop="20260620123000 +0000">
                <title>Midday News</title>
                <desc>Headlines.</desc>
              </programme>
            </tv>
            """;
    }
}
