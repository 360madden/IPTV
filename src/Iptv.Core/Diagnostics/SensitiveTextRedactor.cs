using System.Text;
using System.Text.RegularExpressions;

namespace Iptv.Core.Diagnostics;

public static class SensitiveTextRedactor
{
    private static readonly HashSet<string> SensitiveQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "account",
        "auth",
        "device",
        "expires",
        "key",
        "mac",
        "pass",
        "password",
        "session",
        "sig",
        "signature",
        "stalker_portal",
        "token",
        "user",
        "username"
    };

    public static string RedactUri(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Path = RedactPath(uri.AbsolutePath),
            Query = RedactQuery(uri.Query)
        };

        return builder.Uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.PathAndQuery, UriFormat.UriEscaped);
    }

    public static string RedactText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string redacted = Regex.Replace(
            value,
            @"(?i)(https?://)[^/\s:@]+:[^/\s@]+@",
            "$1REDACTED:REDACTED@");

        foreach (string key in SensitiveQueryKeys)
        {
            redacted = Regex.Replace(
                redacted,
                $@"(?i)(\b{Regex.Escape(key)}=)[^&\s]+",
                "$1REDACTED");
        }

        return redacted;
    }

    private static string RedactPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return "/";
        }

        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return "/";
        }

        return segments.Length == 1 ? "/..." : $"/.../{segments[^1]}";
    }

    private static string RedactQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        string trimmed = query.TrimStart('?');
        var pairs = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var builder = new StringBuilder();

        foreach (string pair in pairs)
        {
            if (builder.Length > 0)
            {
                builder.Append('&');
            }

            int equalsIndex = pair.IndexOf('=');
            string key = equalsIndex >= 0 ? pair[..equalsIndex] : pair;
            builder.Append(Uri.EscapeDataString(Uri.UnescapeDataString(key)));
            builder.Append('=');
            builder.Append(SensitiveQueryKeys.Contains(Uri.UnescapeDataString(key)) ? "REDACTED" : "REDACTED");
        }

        return builder.ToString();
    }
}
