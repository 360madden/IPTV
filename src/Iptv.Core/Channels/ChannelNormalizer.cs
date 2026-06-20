using System.Globalization;
using System.Text;

namespace Iptv.Core.Channels;

public static class ChannelNormalizer
{
    private static readonly (string Category, string[] Terms)[] CategoryRules =
    [
        ("Sports", ["sport", "espn", "nba", "nfl", "mlb", "nhl", "soccer", "football", "ufc"]),
        ("News", ["news", "cnn", "bbc", "msnbc", "fox news", "sky news", "al jazeera"]),
        ("Movies", ["movie", "cinema", "film", "hbo", "showtime", "starz"]),
        ("Kids", ["kids", "cartoon", "nick", "disney", "junior"]),
        ("Music", ["music", "mtv", "vh1", "radio"]),
        ("Documentary", ["documentary", "history", "discovery", "nat geo", "science"]),
        ("Local", ["local", "abc", "cbs", "nbc", "pbs"])
    ];

    public static string CleanDisplayName(string? value)
    {
        string cleaned = string.IsNullOrWhiteSpace(value) ? "Unnamed Channel" : value.Trim();

        while (cleaned.Contains("  ", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
        }

        return cleaned;
    }

    public static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (char c in normalized)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string NormalizeGroup(string? groupTitle)
    {
        return string.IsNullOrWhiteSpace(groupTitle) ? "Ungrouped" : CleanDisplayName(groupTitle);
    }

    public static string InferCategory(string? groupTitle, string? displayName)
    {
        string haystack = NormalizeForSearch($"{groupTitle} {displayName}");
        foreach ((string category, string[] terms) in CategoryRules)
        {
            if (terms.Any(term => haystack.Contains(term, StringComparison.Ordinal)))
            {
                return category;
            }
        }

        return "Other";
    }
}
