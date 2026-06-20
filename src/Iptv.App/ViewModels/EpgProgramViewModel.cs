using Iptv.Core.Epg;

namespace Iptv.App.ViewModels;

public sealed record EpgProgramViewModel(string TimeText, string Title, string Description)
{
    public string DisplayText => string.IsNullOrWhiteSpace(Description)
        ? $"{TimeText} — {Title}"
        : $"{TimeText} — {Title}: {Description}";

    public static EpgProgramViewModel FromProgram(EpgProgram program)
    {
        string timeText = program.Start is null
            ? "Time unavailable"
            : program.Stop is null
                ? program.Start.Value.ToLocalTime().ToString("g")
                : $"{program.Start.Value.ToLocalTime():g} - {program.Stop.Value.ToLocalTime():t}";

        return new EpgProgramViewModel(timeText, program.Title, program.Description ?? string.Empty);
    }
}
