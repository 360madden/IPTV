namespace Iptv.Persistence.Logos;

public sealed record LogoCacheStatistics(
    int FileCount,
    long TotalBytes)
{
    public string DisplayText => $"Logo cache: {FileCount:N0} files, {FormatBytes(TotalBytes)}.";

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L)
        {
            return $"{bytes / 1024d / 1024d / 1024d:N1} GB";
        }

        if (bytes >= 1024L * 1024L)
        {
            return $"{bytes / 1024d / 1024d:N1} MB";
        }

        if (bytes >= 1024L)
        {
            return $"{bytes / 1024d:N1} KB";
        }

        return $"{bytes:N0} bytes";
    }
}
