namespace Iptv.Persistence.Logos;

public sealed record LogoCacheResult(
    bool Success,
    string? FilePath,
    string Message);
