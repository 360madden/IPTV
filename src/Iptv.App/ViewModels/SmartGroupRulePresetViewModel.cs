using Iptv.Persistence.SmartGroups;

namespace Iptv.App.ViewModels;

public sealed record SmartGroupRulePresetViewModel(string Name, string MatchText, string DestinationGroup)
{
    public string DisplayText => $"{Name}: '{MatchText}' → {DestinationGroup}";

    public SmartGroupRulePreset ToPreset()
    {
        return new SmartGroupRulePreset
        {
            Name = Name,
            MatchText = MatchText,
            DestinationGroup = DestinationGroup
        };
    }

    public static SmartGroupRulePresetViewModel FromPreset(SmartGroupRulePreset preset)
    {
        return new SmartGroupRulePresetViewModel(preset.Name, preset.MatchText, preset.DestinationGroup);
    }
}
