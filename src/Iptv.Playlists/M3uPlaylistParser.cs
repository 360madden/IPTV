using System.Text;
using Iptv.Core;
using Iptv.Core.Channels;
using Iptv.Core.PlaylistImport;

namespace Iptv.Playlists;

public sealed class M3uPlaylistParser : IPlaylistParser
{
    public async Task<PlaylistImportResult> ParseAsync(
        Stream content,
        PlaylistSource source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(source);

        var channels = new List<Channel>();
        var issues = new List<PlaylistImportIssue>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        M3uExtInf? pendingMetadata = null;
        bool sawHeader = false;
        int lineNumber = 0;

        using var reader = new StreamReader(
            content,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 64 * 1024,
            leaveOpen: true);

        string? rawLine;
        while ((rawLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;

            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (lineNumber == 1 && line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
            {
                sawHeader = true;
                continue;
            }

            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                pendingMetadata = ParseExtInf(line, lineNumber, issues);
                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            M3uExtInf metadata = pendingMetadata ?? new M3uExtInf(
                DeriveDisplayName(line),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                lineNumber);

            pendingMetadata = null;
            AddChannel(source, line, metadata, channels, issues, seen, lineNumber, channels.Count);
        }

        if (!sawHeader)
        {
            issues.Add(new PlaylistImportIssue(
                ImportIssueSeverity.Warning,
                "missing-extm3u-header",
                "Playlist does not start with #EXTM3U; import continued defensively.",
                1));
        }

        if (pendingMetadata is not null)
        {
            issues.Add(new PlaylistImportIssue(
                ImportIssueSeverity.Warning,
                "metadata-without-url",
                "Found #EXTINF metadata without a following stream URL.",
                pendingMetadata.LineNumber));
        }

        if (channels.Count == 0)
        {
            issues.Add(new PlaylistImportIssue(
                ImportIssueSeverity.Error,
                "empty-playlist",
                "No playable channel entries were found."));
        }

        return new PlaylistImportResult(channels, issues);
    }

    private static M3uExtInf ParseExtInf(
        string line,
        int lineNumber,
        ICollection<PlaylistImportIssue> issues)
    {
        int colonIndex = line.IndexOf(':');
        string metadata = colonIndex >= 0 ? line[(colonIndex + 1)..] : string.Empty;
        int commaIndex = FindCommaOutsideQuotes(metadata);
        string attributesPart = commaIndex >= 0 ? metadata[..commaIndex] : metadata;
        string displayName = commaIndex >= 0 ? metadata[(commaIndex + 1)..].Trim() : "Unnamed Channel";

        if (commaIndex < 0)
        {
            issues.Add(new PlaylistImportIssue(
                ImportIssueSeverity.Warning,
                "malformed-extinf",
                "EXTINF line is missing a display-name comma.",
                lineNumber));
        }

        return new M3uExtInf(
            ChannelNormalizer.CleanDisplayName(displayName),
            ParseAttributes(attributesPart),
            lineNumber);
    }

    private static Dictionary<string, string> ParseAttributes(string value)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int index = 0;

        while (index < value.Length)
        {
            while (index < value.Length && !IsAttributeNameStart(value[index]))
            {
                index++;
            }

            int keyStart = index;
            while (index < value.Length && (char.IsLetterOrDigit(value[index]) || value[index] is '-' or '_'))
            {
                index++;
            }

            if (keyStart == index || index >= value.Length || value[index] != '=')
            {
                index++;
                continue;
            }

            string key = value[keyStart..index];
            index++;

            string attributeValue;
            if (index < value.Length && value[index] == '"')
            {
                index++;
                int valueStart = index;
                while (index < value.Length && value[index] != '"')
                {
                    index++;
                }

                attributeValue = value[valueStart..Math.Min(index, value.Length)];
                if (index < value.Length && value[index] == '"')
                {
                    index++;
                }
            }
            else
            {
                int valueStart = index;
                while (index < value.Length && !char.IsWhiteSpace(value[index]))
                {
                    index++;
                }

                attributeValue = value[valueStart..index];
            }

            attributes[key] = attributeValue.Trim();
        }

        return attributes;
    }

    private static void AddChannel(
        PlaylistSource source,
        string streamUrl,
        M3uExtInf metadata,
        ICollection<Channel> channels,
        ICollection<PlaylistImportIssue> issues,
        ISet<string> seen,
        int lineNumber,
        int importIndex)
    {
        if (!SensitiveUri.TryCreate(streamUrl, out SensitiveUri? sensitiveUri, out string? error))
        {
            issues.Add(new PlaylistImportIssue(
                ImportIssueSeverity.Warning,
                "invalid-stream-url",
                $"Skipped channel '{metadata.DisplayName}' because the stream URL is invalid: {error}",
                lineNumber));
            return;
        }

        SensitiveUri stream = sensitiveUri ?? throw new InvalidOperationException("Sensitive URI unexpectedly missing after successful validation.");

        string displayName = metadata.Attributes.TryGetValue("tvg-name", out string? tvgName) && !string.IsNullOrWhiteSpace(tvgName)
            ? ChannelNormalizer.CleanDisplayName(tvgName)
            : ChannelNormalizer.CleanDisplayName(metadata.DisplayName);
        string group = metadata.Attributes.TryGetValue("group-title", out string? groupTitle)
            ? ChannelNormalizer.NormalizeGroup(groupTitle)
            : "Ungrouped";
        string normalizedName = ChannelNormalizer.NormalizeForSearch(displayName);
        string duplicateKey = $"{normalizedName}|{stream.Uri.AbsoluteUri}";

        if (!seen.Add(duplicateKey))
        {
            issues.Add(new PlaylistImportIssue(
                ImportIssueSeverity.Warning,
                "duplicate-channel",
                $"Duplicate channel '{displayName}' was imported as an alternate entry.",
                lineNumber));
        }

        channels.Add(new Channel
        {
            Id = StableId.Create(source.DisplayName, displayName, stream.Uri.AbsoluteUri),
            SourceId = source.Id,
            RawName = metadata.DisplayName,
            DisplayName = displayName,
            NormalizedName = normalizedName,
            StreamUrl = stream,
            ImportIndex = importIndex,
            GroupTitle = group,
            Category = ChannelNormalizer.InferCategory(group, displayName),
            TvgId = metadata.Attributes.GetValueOrDefault("tvg-id"),
            TvgName = metadata.Attributes.GetValueOrDefault("tvg-name"),
            TvgLogo = metadata.Attributes.GetValueOrDefault("tvg-logo"),
            ContentKind = InferContentKind(group, displayName)
        });
    }

    private static ContentKind InferContentKind(string group, string displayName)
    {
        string text = ChannelNormalizer.NormalizeForSearch($"{group} {displayName}");
        if (text.Contains("radio", StringComparison.Ordinal))
        {
            return ContentKind.Radio;
        }

        if (text.Contains("vod", StringComparison.Ordinal) ||
            text.Contains("movie", StringComparison.Ordinal) ||
            text.Contains("cinema", StringComparison.Ordinal))
        {
            return ContentKind.Vod;
        }

        if (text.Contains("series", StringComparison.Ordinal))
        {
            return ContentKind.Series;
        }

        return ContentKind.LiveTv;
    }

    private static int FindCommaOutsideQuotes(string value)
    {
        bool inQuotes = false;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (value[i] == ',' && !inQuotes)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsAttributeNameStart(char c)
    {
        return char.IsLetter(c);
    }

    private static string DeriveDisplayName(string streamUrl)
    {
        if (Uri.TryCreate(streamUrl, UriKind.Absolute, out Uri? uri))
        {
            string segment = uri.Segments.LastOrDefault()?.Trim('/') ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(segment))
            {
                return ChannelNormalizer.CleanDisplayName(Uri.UnescapeDataString(segment));
            }
        }

        return "Unnamed Channel";
    }
}
