using System.IO;
using Iptv.Core;
using Iptv.Persistence;

namespace Iptv.App.ViewModels;

public sealed record RecentPlaylistSourceViewModel(
    RecentPlaylistSourceKind Kind,
    string DisplayName,
    string Value,
    DateTimeOffset LastUsedAt)
{
    public string DisplayText => $"{DisplayName} · {KindLabel} · {LastUsedAt.LocalDateTime:g}";

    public string KindLabel => Kind == RecentPlaylistSourceKind.RemoteUrl ? "URL" : "File";

    public RecentPlaylistSourcePreference ToPreference()
    {
        return new RecentPlaylistSourcePreference
        {
            Kind = Kind,
            DisplayName = DisplayName,
            Value = Value,
            LastUsedAt = LastUsedAt
        };
    }

    public static RecentPlaylistSourceViewModel FromPreference(RecentPlaylistSourcePreference preference)
    {
        string value = preference.Value?.Trim() ?? string.Empty;
        string displayName = string.IsNullOrWhiteSpace(preference.DisplayName)
            ? CreateDisplayName(preference.Kind, value)
            : preference.DisplayName.Trim();
        return new RecentPlaylistSourceViewModel(
            preference.Kind,
            displayName,
            value,
            preference.LastUsedAt == default ? DateTimeOffset.UtcNow : preference.LastUsedAt);
    }

    public static string CreateDisplayName(RecentPlaylistSourceKind kind, string value)
    {
        if (kind == RecentPlaylistSourceKind.LocalFile)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "Playlist file"
                : Path.GetFileName(value);
        }

        return SensitiveUri.TryCreate(value, out SensitiveUri? uri, out _)
            ? uri?.ToString() ?? "Playlist URL"
            : "Playlist URL";
    }
}
