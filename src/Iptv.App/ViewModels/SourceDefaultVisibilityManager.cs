using Iptv.Core.Channels;

namespace Iptv.App.ViewModels;

public sealed class SourceDefaultVisibilityManager
{
    public Dictionary<string, string[]> NormalizeRules(IDictionary<string, string[]>? rules)
    {
        var normalized = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (rules is null)
        {
            return normalized;
        }

        foreach ((string sourceId, string[] groups) in rules)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                continue;
            }

            string[] normalizedGroups = NormalizeGroups(groups);
            if (normalizedGroups.Length > 0)
            {
                normalized[sourceId.Trim()] = normalizedGroups;
            }
        }

        return normalized;
    }

    public bool IsHiddenByDefault(Channel channel, IReadOnlyDictionary<string, string[]> rules)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(rules);

        return rules.TryGetValue(channel.SourceId.ToString(), out string[]? hiddenGroups) &&
            hiddenGroups.Contains(channel.GroupTitle, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetGroupOptions(IEnumerable<Channel> channels, string? sourceId, string allGroupsOption)
    {
        ArgumentNullException.ThrowIfNull(channels);

        var groups = new List<string> { allGroupsOption };
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return groups;
        }

        groups.AddRange(channels
            .Where(channel => channel.SourceId.ToString().Equals(sourceId, StringComparison.OrdinalIgnoreCase))
            .Select(channel => channel.GroupTitle)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase));
        return groups;
    }

    public string BuildSummary(
        SourceProfileViewModel? selectedProfile,
        string selectedGroup,
        IReadOnlyDictionary<string, string[]> rules,
        string allGroupsOption)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (selectedProfile is null)
        {
            return "Default source visibility rules appear after importing a playlist.";
        }

        rules.TryGetValue(selectedProfile.SourceId, out string[]? hiddenGroups);
        string rulesText = hiddenGroups is { Length: > 0 }
            ? $"{hiddenGroups.Length:N0} default hidden group(s): {string.Join(", ", hiddenGroups.Take(4))}{(hiddenGroups.Length > 4 ? "..." : string.Empty)}"
            : "No default hidden groups configured.";
        string selectedText = string.Equals(selectedGroup, allGroupsOption, StringComparison.OrdinalIgnoreCase)
            ? "Select a source group to hide or show it by default for future imports."
            : $"Selected group '{selectedGroup}' can be toggled as a default visibility rule.";
        return $"{selectedProfile.DisplayName}: {rulesText} {selectedText}";
    }

    public SourceDefaultVisibilityChange SetRule(
        IDictionary<string, string[]> rules,
        string sourceId,
        string groupName,
        bool hidden)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Source ID is required.", nameof(sourceId));
        }

        string normalizedGroup = NormalizeGroup(groupName) ??
            throw new ArgumentException("Group name is required.", nameof(groupName));

        var groups = rules.TryGetValue(sourceId, out string[]? existing)
            ? existing.ToList()
            : new List<string>();
        bool changed;
        if (hidden)
        {
            changed = !groups.Contains(normalizedGroup, StringComparer.OrdinalIgnoreCase);
            if (changed)
            {
                groups.Add(normalizedGroup);
            }
        }
        else
        {
            changed = groups.RemoveAll(group => group.Equals(normalizedGroup, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        string[] normalizedGroups = NormalizeGroups(groups);
        if (normalizedGroups.Length == 0)
        {
            rules.Remove(sourceId);
        }
        else
        {
            rules[sourceId] = normalizedGroups;
        }

        return new SourceDefaultVisibilityChange(sourceId, normalizedGroup, hidden, changed, normalizedGroups);
    }

    public static string[] NormalizeGroups(IEnumerable<string>? groups)
    {
        return (groups ?? [])
            .Select(NormalizeGroup)
            .Where(group => group is not null)
            .Select(group => group!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string? NormalizeGroup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length == 0 ? null : normalized;
    }
}

public sealed record SourceDefaultVisibilityChange(
    string SourceId,
    string GroupName,
    bool Hidden,
    bool Changed,
    string[] CurrentHiddenGroups);
