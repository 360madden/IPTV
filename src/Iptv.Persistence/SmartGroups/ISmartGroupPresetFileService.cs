namespace Iptv.Persistence.SmartGroups;

public interface ISmartGroupPresetFileService
{
    Task ExportAsync(string path, IEnumerable<SmartGroupRulePreset> presets, CancellationToken cancellationToken);

    Task<IReadOnlyList<SmartGroupRulePreset>> ImportAsync(string path, CancellationToken cancellationToken);
}
