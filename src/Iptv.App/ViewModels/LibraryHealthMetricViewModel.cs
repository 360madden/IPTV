namespace Iptv.App.ViewModels;

public sealed record LibraryHealthMetricViewModel(string Name, string Value, string Detail)
{
    public string DisplayText => string.IsNullOrWhiteSpace(Detail)
        ? $"{Name}: {Value}"
        : $"{Name}: {Value} · {Detail}";
}
