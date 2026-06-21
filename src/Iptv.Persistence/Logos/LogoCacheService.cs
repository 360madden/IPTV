using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using Iptv.Core.Diagnostics;

namespace Iptv.Persistence.Logos;

public sealed class LogoCacheService : ILogoCacheService
{
    public const int DefaultMaxLogoBytes = 512 * 1024;

    private static readonly HashSet<string> KnownImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gif",
        ".jpeg",
        ".jpg",
        ".png",
        ".svg",
        ".webp"
    };

    private readonly string cacheDirectory;
    private readonly int maxLogoBytes;

    public LogoCacheService(string? cacheDirectory = null, int maxLogoBytes = DefaultMaxLogoBytes)
    {
        if (maxLogoBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLogoBytes), "Logo byte limit must be positive.");
        }

        this.cacheDirectory = string.IsNullOrWhiteSpace(cacheDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IptvViewer", "logos")
            : cacheDirectory;
        this.maxLogoBytes = maxLogoBytes;
    }

    public string? TryGetCachedLogoPath(string? logoUrl)
    {
        if (!TryCreateLogoUri(logoUrl, out Uri? uri) || !Directory.Exists(cacheDirectory))
        {
            return null;
        }

        Uri logoUri = uri!;
        string prefix = ComputeCacheKey(logoUri);
        return Directory
            .EnumerateFiles(cacheDirectory, $"{prefix}.*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();
    }

    public async Task<LogoCacheResult> CacheLogoAsync(string? logoUrl, HttpClient httpClient, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (!TryCreateLogoUri(logoUrl, out Uri? uri))
        {
            return new LogoCacheResult(false, null, "Logo skipped: URL is empty or is not HTTP/HTTPS.");
        }

        Uri logoUri = uri!;
        string? existing = TryGetCachedLogoPath(logoUri.AbsoluteUri);
        if (existing is not null)
        {
            return new LogoCacheResult(true, existing, "Logo loaded from cache.");
        }

        try
        {
            using HttpResponseMessage response = await httpClient
                .GetAsync(logoUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return new LogoCacheResult(false, null, "Logo skipped: provider returned no new content.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new LogoCacheResult(false, null, $"Logo skipped: HTTP {(int)response.StatusCode}.");
            }

            if (response.Content.Headers.ContentLength is long length && length > maxLogoBytes)
            {
                return new LogoCacheResult(false, null, $"Logo skipped: file is larger than {maxLogoBytes:N0} bytes.");
            }

            Directory.CreateDirectory(cacheDirectory);
            string extension = GetExtension(response.Content.Headers.ContentType?.MediaType, logoUri);
            string cachePath = Path.Combine(cacheDirectory, $"{ComputeCacheKey(logoUri)}{extension}");
            string tempPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";

            try
            {
                await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await CopyWithLimitAsync(source, tempPath, maxLogoBytes, cancellationToken).ConfigureAwait(false);
                File.Move(tempPath, cachePath, overwrite: true);
                return new LogoCacheResult(true, cachePath, "Logo cached.");
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new LogoCacheResult(false, null, $"Logo skipped: {SensitiveTextRedactor.RedactText(ex.Message)}");
        }
    }

    public LogoCacheStatistics GetStatistics()
    {
        if (!Directory.Exists(cacheDirectory))
        {
            return new LogoCacheStatistics(0, 0);
        }

        FileInfo[] files = GetCacheFiles();
        return new LogoCacheStatistics(files.Length, files.Sum(file => file.Length));
    }

    public Task<int> TrimAsync(long maxBytes, CancellationToken cancellationToken)
    {
        if (maxBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "Logo cache size limit cannot be negative.");
        }

        if (!Directory.Exists(cacheDirectory))
        {
            return Task.FromResult(0);
        }

        return Task.Run(() =>
        {
            FileInfo[] files = GetCacheFiles()
                .OrderBy(file => file.LastAccessTimeUtc)
                .ThenBy(file => file.LastWriteTimeUtc)
                .ToArray();
            long totalBytes = files.Sum(file => file.Length);
            int removed = 0;

            foreach (FileInfo file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (totalBytes <= maxBytes)
                {
                    break;
                }

                long length = file.Length;
                try
                {
                    file.Delete();
                    totalBytes -= length;
                    removed++;
                }
                catch (IOException)
                {
                    // Best-effort cleanup; a logo currently in use can be retried later.
                }
                catch (UnauthorizedAccessException)
                {
                    // Best-effort cleanup; keep the app responsive if the cache is not writable.
                }
            }

            return removed;
        }, cancellationToken);
    }

    public Task<int> ClearAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(cacheDirectory))
        {
            return Task.FromResult(0);
        }

        return Task.Run(() =>
        {
            int removed = 0;
            foreach (FileInfo file in GetCacheFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    file.Delete();
                    removed++;
                }
                catch (IOException)
                {
                    // Best-effort cleanup; a logo currently in use can be retried later.
                }
                catch (UnauthorizedAccessException)
                {
                    // Best-effort cleanup; keep the app responsive if the cache is not writable.
                }
            }

            return removed;
        }, cancellationToken);
    }

    private FileInfo[] GetCacheFiles()
    {
        return new DirectoryInfo(cacheDirectory)
            .EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
            .Where(file => KnownImageExtensions.Contains(file.Extension) || file.Extension.Equals(".img", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static async Task CopyWithLimitAsync(Stream source, string tempPath, int maxBytes, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        int totalBytes = 0;

        try
        {
            await using var destination = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                buffer.Length,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            while (true)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                totalBytes += read;
                if (totalBytes > maxBytes)
                {
                    throw new InvalidDataException($"Logo exceeded {maxBytes:N0} byte limit.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool TryCreateLogoUri(string? logoUrl, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(logoUrl) ||
            !Uri.TryCreate(logoUrl, UriKind.Absolute, out Uri? parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private static string ComputeCacheKey(Uri uri)
    {
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(uri.AbsoluteUri));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetExtension(string? mediaType, Uri uri)
    {
        return mediaType?.ToLowerInvariant() switch
        {
            "image/gif" => ".gif",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/svg+xml" => ".svg",
            "image/webp" => ".webp",
            _ => GetSafeUriExtension(uri)
        };
    }

    private static string GetSafeUriExtension(Uri uri)
    {
        string extension = Path.GetExtension(uri.AbsolutePath);
        return KnownImageExtensions.Contains(extension)
            ? extension.ToLowerInvariant()
            : ".img";
    }
}
