using Iptv.Core.Diagnostics;

namespace Iptv.Core;

public sealed record SensitiveUri
{
    private static readonly HashSet<string> SupportedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
        "rtmp",
        "rtsp",
        "udp"
    };

    private SensitiveUri(Uri uri)
    {
        Uri = uri;
    }

    public Uri Uri { get; }

    public string Host => Uri.Host;

    public string Redacted => SensitiveTextRedactor.RedactUri(Uri);

    public static bool TryCreate(string? value, out SensitiveUri? sensitiveUri, out string? error)
    {
        sensitiveUri = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "URL is empty.";
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri))
        {
            error = "URL is not an absolute URI.";
            return false;
        }

        if (!SupportedSchemes.Contains(uri.Scheme))
        {
            error = $"Unsupported URL scheme '{uri.Scheme}'.";
            return false;
        }

        sensitiveUri = new SensitiveUri(uri);
        error = null;
        return true;
    }

    public override string ToString()
    {
        return Redacted;
    }
}
