namespace Iptv.Core.PlaylistImport;

public sealed record PlaylistImportProgress(
    string Stage,
    long? ProcessedBytes = null,
    long? TotalBytes = null,
    int ParsedChannels = 0,
    int IssueCount = 0,
    int? LineNumber = null)
{
    public string DisplayText
    {
        get
        {
            string countText = ParsedChannels > 0
                ? $" {ParsedChannels:N0} entries"
                : string.Empty;
            string lineText = LineNumber is int line
                ? $" line {line:N0}"
                : string.Empty;
            string byteText = (ProcessedBytes, TotalBytes) switch
            {
                (long processed, long total) when total > 0 => $" {processed:N0}/{total:N0} bytes",
                (long processed, _) when processed > 0 => $" {processed:N0} bytes",
                _ => string.Empty
            };

            return $"{Stage}{countText}{lineText}{byteText}".Trim();
        }
    }
}
