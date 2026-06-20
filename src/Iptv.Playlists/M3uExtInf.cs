namespace Iptv.Playlists;

internal sealed record M3uExtInf(
    string DisplayName,
    IReadOnlyDictionary<string, string> Attributes,
    int LineNumber);
