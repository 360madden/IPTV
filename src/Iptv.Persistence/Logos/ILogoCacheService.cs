namespace Iptv.Persistence.Logos;

public interface ILogoCacheService
{
    string? TryGetCachedLogoPath(string? logoUrl);

    Task<LogoCacheResult> CacheLogoAsync(string? logoUrl, HttpClient httpClient, CancellationToken cancellationToken);

    LogoCacheStatistics GetStatistics();

    Task<int> TrimAsync(long maxBytes, CancellationToken cancellationToken);

    Task<int> ClearAsync(CancellationToken cancellationToken);
}
