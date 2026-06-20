using Iptv.Persistence.SmartGroups;

namespace Iptv.App.ViewModels;

public sealed record SmartGroupRulePresetViewModel(
    string Name,
    string MatchText,
    string DestinationGroup,
    SmartRuleMatchMode MatchMode = SmartRuleMatchMode.ContainsAny)
{
    public string DisplayText => $"{Name}: {FormatMode(MatchMode)} '{MatchText}' → {DestinationGroup}";

    public SmartGroupRulePreset ToPreset()
    {
        return new SmartGroupRulePreset
        {
            Name = Name,
            MatchText = MatchText,
            DestinationGroup = DestinationGroup,
            MatchMode = MatchMode
        };
    }

    public static SmartGroupRulePresetViewModel FromPreset(SmartGroupRulePreset preset)
    {
        return new SmartGroupRulePresetViewModel(preset.Name, preset.MatchText, preset.DestinationGroup, preset.MatchMode);
    }

    private static string FormatMode(SmartRuleMatchMode mode)
    {
        return mode switch
        {
            SmartRuleMatchMode.NameStartsWith => "starts with",
            SmartRuleMatchMode.Regex => "regex",
            SmartRuleMatchMode.GroupEquals => "group equals",
            SmartRuleMatchMode.CategoryEquals => "category equals",
            _ => "contains"
        };
    }
}
