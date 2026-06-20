using System.Globalization;
using System.IO.Compression;
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

        try
        {
            using XmltvReadInput input = OpenXmltvInput(file, options.MaxXmltvBytes);
            using XmlReader reader = XmlReader.Create(input.Stream, settings);
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
        catch (InvalidDataException ex)
        {
            issues.Add(new PlaylistImportIssue(ImportIssueSeverity.Error, "invalid-xmltv-archive", ex.Message));
        }
        catch (IOException ex)
        {
            issues.Add(new PlaylistImportIssue(ImportIssueSeverity.Error, "xmltv-read-failed", ex.Message));
        }

        if (channels.Count == 0)
        {
            issues.Add(new PlaylistImportIssue(ImportIssueSeverity.Warning, "no-epg-channels", "No XMLTV channels were found."));
        }

        return new EpgImportResult(channels, programs, issues);
    }

    private static XmltvReadInput OpenXmltvInput(FileInfo file, long maxXmltvBytes)
    {
        FileStream fileStream = file.OpenRead();
        string extension = file.Extension.ToLowerInvariant();
        if (extension is ".gz" or ".gzip")
        {
            var gzip = new GZipStream(fileStream, CompressionMode.Decompress);
            return new XmltvReadInput(new LimitedReadStream(gzip, maxXmltvBytes));
        }

        if (extension == ".zip")
        {
            var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
            ZipArchiveEntry? entry = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .OrderByDescending(entry => Path.GetExtension(entry.Name).Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(entry.Name).Equals(".xmltv", StringComparison.OrdinalIgnoreCase))
                .ThenBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (entry is null)
            {
                archive.Dispose();
                throw new InvalidDataException("ZIP archive does not contain an XMLTV file.");
            }

            if (entry.Length > maxXmltvBytes)
            {
                archive.Dispose();
                throw new InvalidDataException($"XMLTV entry exceeds the configured {maxXmltvBytes:N0} byte limit.");
            }

            Stream entryStream = entry.Open();
            return new XmltvReadInput(new LimitedReadStream(entryStream, maxXmltvBytes), archive);
        }

        return new XmltvReadInput(new LimitedReadStream(fileStream, maxXmltvBytes));
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

    private sealed class XmltvReadInput : IDisposable
    {
        private readonly IDisposable[] disposables;

        public XmltvReadInput(Stream stream, params IDisposable[] disposables)
        {
            Stream = stream;
            this.disposables = disposables;
        }

        public Stream Stream { get; }

        public void Dispose()
        {
            Stream.Dispose();
            foreach (IDisposable disposable in disposables.Reverse())
            {
                disposable.Dispose();
            }
        }
    }

    private sealed class LimitedReadStream : Stream
    {
        private readonly Stream inner;
        private readonly long maxBytes;
        private long totalRead;

        public LimitedReadStream(Stream inner, long maxBytes)
        {
            this.inner = inner;
            this.maxBytes = maxBytes;
        }

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => totalRead;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = inner.Read(buffer, offset, count);
            Track(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            Track(read);
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void Track(int read)
        {
            totalRead += read;
            if (totalRead > maxBytes)
            {
                throw new InvalidDataException($"XMLTV file exceeds the configured {maxBytes:N0} byte limit.");
            }
        }
    }

    private static EpgImportResult Failure(string code, string message)
    {
        return new EpgImportResult(
            [],
            [],
            [new PlaylistImportIssue(ImportIssueSeverity.Error, code, message)]);
    }
}
