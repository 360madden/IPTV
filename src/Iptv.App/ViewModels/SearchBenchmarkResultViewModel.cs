namespace Iptv.App.ViewModels;

public sealed record SearchBenchmarkResultViewModel(
    string Scenario,
    int ChannelCount,
    int ResultCount,
    long ElapsedMilliseconds)
{
    public string DisplayText => $"{Scenario}: {ResultCount:N0}/{ChannelCount:N0} in {ElapsedMilliseconds:N0} ms";
}
