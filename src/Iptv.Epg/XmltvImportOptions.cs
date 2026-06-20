namespace Iptv.Epg;

public sealed record XmltvImportOptions
{
    public long MaxXmltvBytes { get; init; } = 50 * 1024 * 1024;
}
