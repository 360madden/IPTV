namespace Iptv.Persistence.SmartGroups;

public sealed record SmartGroupRulePreset
{
    public required string Name { get; init; }

    public required string MatchText { get; init; }

    public required string DestinationGroup { get; init; }

    public SmartRuleMatchMode MatchMode { get; init; } = SmartRuleMatchMode.ContainsAny;

    public bool PreserveExistingGroups { get; init; } = true;
}
