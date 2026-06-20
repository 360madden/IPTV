using System.Text.RegularExpressions;
using Iptv.Core.Channels;
using Iptv.Persistence.SmartGroups;

namespace Iptv.App.ViewModels;

internal static class SmartGroupRuleMatcher
{
    private static readonly TimeSpan RegexRuleTimeout = TimeSpan.FromMilliseconds(150);

    public static string? NormalizeTerm(string? value, SmartRuleMatchMode mode)
    {
        string? normalized = NormalizeText(value);
        if (normalized is null)
        {
            return null;
        }

        return mode == SmartRuleMatchMode.Regex ? normalized : ChannelNormalizer.NormalizeForSearch(normalized);
    }

    public static bool Matches(Channel channel, string matchText, SmartRuleMatchMode mode)
    {
        return mode switch
        {
            SmartRuleMatchMode.NameStartsWith => channel.NormalizedName.StartsWith(matchText, StringComparison.Ordinal),
            SmartRuleMatchMode.Regex => RegexMatchesChannel(channel, matchText),
            SmartRuleMatchMode.GroupEquals =>
                ChannelNormalizer.NormalizeForSearch(channel.EffectiveGroupTitle).Equals(matchText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.GroupTitle).Equals(matchText, StringComparison.Ordinal),
            SmartRuleMatchMode.CategoryEquals => ChannelNormalizer.NormalizeForSearch(channel.Category).Equals(matchText, StringComparison.Ordinal),
            _ => channel.NormalizedName.Contains(matchText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.EffectiveGroupTitle).Contains(matchText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.GroupTitle).Contains(matchText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.Category).Contains(matchText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.TvgName).Contains(matchText, StringComparison.Ordinal) ||
                ChannelNormalizer.NormalizeForSearch(channel.TvgId).Contains(matchText, StringComparison.Ordinal)
        };
    }

    public static bool ValidatePattern(string matchText, SmartRuleMatchMode mode)
    {
        if (mode != SmartRuleMatchMode.Regex)
        {
            return true;
        }

        try
        {
            _ = Regex.IsMatch(string.Empty, matchText, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexRuleTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    public static string FormatMode(SmartRuleMatchMode mode)
    {
        return mode switch
        {
            SmartRuleMatchMode.NameStartsWith => "name starts with",
            SmartRuleMatchMode.Regex => "regex",
            SmartRuleMatchMode.GroupEquals => "group equals",
            SmartRuleMatchMode.CategoryEquals => "category equals",
            _ => "contains"
        };
    }

    private static bool RegexMatchesChannel(Channel channel, string pattern)
    {
        string[] fields =
        [
            channel.DisplayName,
            channel.EffectiveGroupTitle,
            channel.GroupTitle,
            channel.Category,
            channel.TvgName ?? string.Empty,
            channel.TvgId ?? string.Empty
        ];

        try
        {
            return fields.Any(field => Regex.IsMatch(
                field,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                RegexRuleTimeout));
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length == 0 ? null : normalized;
    }
}
