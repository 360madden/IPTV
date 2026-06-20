namespace Iptv.Persistence.SmartGroups;

public sealed record SmartGroupPresetExport
{
    public int Version { get; init; } = 1;

    public DateTimeOffset ExportedAt { get; init; } = DateTimeOffset.UtcNow;

    public SmartGroupRulePreset[] Presets { get; init; } = [];
}
