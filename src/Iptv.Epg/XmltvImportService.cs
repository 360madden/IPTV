using System.Globalization;
using System.Xml;
using Iptv.Core.Epg;
using Iptv.Core.PlaylistImport;

namespace Iptv.Epg;

public sealed class XmltvImportService : IXmltvImportService
{
    private readonly XmltvImportOptions options;

    public XmltvImportService(XmltvImportOptions? options = null)
    {
        this.options = options ?? new XmltvImportOptions();
    }

    public async Task<EpgImportResult> ImportFileAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Failure("invalid-path", "XMLTV path is empty.");
        }

        var file = new FileInfo(path);
        if (!file.Exists)
        {
            return Failure("file-not-found", "XMLTV file was not found.");
        }

        if (file.Length > options.MaxXmltvBytes)
        {
            return Failure("xmltv-too-large", $"XMLTV file exceeds the configured {options.MaxXmltvBytes:N0} byte limit.");
        }

        var channels = new List<EpgChannel>();
        var programs = new List<EpgProgram>();
        var issues = new List<PlaylistImportIssue>();

        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreWhitespace = true
        };

        await using FileStream stream = file.OpenRead();
        using XmlReader reader = XmlReader.Create(stream, settings);

        try
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (reader.Name.Equals("channel", StringComparison.OrdinalIgnoreCase))
                {
                    EpgChannel? channel = await ReadChannelAsync(reader, cancellationToken).ConfigureAwait(false);
                    if (channel is not null)
                    {
                        channels.Add(channel);
                    }
                }
                else if (reader.Name.Equals("programme", StringComparison.OrdinalIgnoreCase))
                {
                    EpgProgram? program = await ReadProgramAsync(reader, issues, cancellationToken).ConfigureAwait(false);
                    if (program is not null)
                    {
                        programs.Add(program);
                    }
                }
            }
        }
        catch (XmlException ex)
        {
            issues.Add(new PlaylistImportIssue(ImportIssueSeverity.Error, "invalid-xmltv", ex.Message, ex.LineNumber));
        }

        if (channels.Count == 0)
        {
            issues.Add(new PlaylistImportIssue(ImportIssueSeverity.Warning, "no-epg-channels", "No XMLTV channels were found."));
        }

        return new EpgImportResult(channels, programs, issues);
    }

    private static async Task<EpgChannel?> ReadChannelAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        string? id = reader.GetAttribute("id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        string displayName = id;
        using XmlReader subtree = reader.ReadSubtree();
        while (await subtree.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (subtree.NodeType == XmlNodeType.Element &&
                subtree.Name.Equals("display-name", StringComparison.OrdinalIgnoreCase))
            {
                string? value = await subtree.ReadElementContentAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    displayName = value.Trim();
                    break;
                }
            }
        }

        return new EpgChannel(id.Trim(), displayName);
    }

    private static async Task<EpgProgram?> ReadProgramAsync(
        XmlReader reader,
        ICollection<PlaylistImportIssue> issues,
        CancellationToken cancellationToken)
    {
        string? channelId = reader.GetAttribute("channel");
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return null;
        }

        DateTimeOffset? start = ParseXmltvTime(reader.GetAttribute("start"), "start", issues);
        DateTimeOffset? stop = ParseXmltvTime(reader.GetAttribute("stop"), "stop", issues);
        string title = "Untitled";
        string? description = null;

        using XmlReader subtree = reader.ReadSubtree();
        while (await subtree.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (subtree.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (subtree.Name.Equals("title", StringComparison.OrdinalIgnoreCase))
            {
                string? value = await subtree.ReadElementContentAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    title = value.Trim();
                }
            }
            else if (subtree.Name.Equals("desc", StringComparison.OrdinalIgnoreCase))
            {
                string? value = await subtree.ReadElementContentAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    description = value.Trim();
                }
            }
        }

        return new EpgProgram(channelId.Trim(), title, start, stop, description);
    }

    private static DateTimeOffset? ParseXmltvTime(
        string? value,
        string fieldName,
        ICollection<PlaylistImportIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = NormalizeXmltvTimeZone(value.Trim());
        string[] formats =
        [
            "yyyyMMddHHmmss zzz",
            "yyyyMMddHHmmss K",
            "yyyyMMddHHmmss",
            "yyyyMMddHHmm zzz",
            "yyyyMMddHHmm"
        ];

        if (DateTimeOffset.TryParseExact(
                trimmed,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out DateTimeOffset parsed))
        {
            return parsed;
        }

        issues.Add(new PlaylistImportIssue(
            ImportIssueSeverity.Warning,
            "invalid-xmltv-time",
            $"Could not parse XMLTV {fieldName} time '{trimmed}'."));
        return null;
    }

    private static string NormalizeXmltvTimeZone(string value)
    {
        if (value.Length >= 6 &&
            value[^5] is '+' or '-' &&
            char.IsDigit(value[^4]) &&
            char.IsDigit(value[^3]) &&
            char.IsDigit(value[^2]) &&
            char.IsDigit(value[^1]))
        {
            return $"{value[..^2]}:{value[^2..]}";
        }

        return value;
    }

    private static EpgImportResult Failure(string code, string message)
    {
        return new EpgImportResult(
            [],
            [],
            [new PlaylistImportIssue(ImportIssueSeverity.Error, code, message)]);
    }
}
