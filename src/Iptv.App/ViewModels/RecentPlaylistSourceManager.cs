using Iptv.Core;
using Iptv.Persistence;

namespace Iptv.App.ViewModels;

public sealed class RecentPlaylistSourceManager
{
    private readonly int maximumSources;

    public RecentPlaylistSourceManager(int maximumSources)
    {
        this.maximumSources = Math.Max(1, maximumSources);
    }

    public IReadOnlyList<RecentPlaylistSourceViewModel> Normalize(IEnumerable<RecentPlaylistSourceViewModel> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        return sources
            .Where(IsUsable)
            .GroupBy(source => $"{source.Kind}|{source.Value}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(source => source.IsPinned)
                .ThenByDescending(source => source.LastUsedAt)
                .First())
            .OrderByDescending(source => source.IsPinned)
            .ThenByDescending(source => source.LastUsedAt)
            .Take(maximumSources)
            .ToArray();
    }

    public IReadOnlyList<RecentPlaylistSourceViewModel> FromPreferences(IEnumerable<RecentPlaylistSourcePreference> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return Normalize(sources.Select(RecentPlaylistSourceViewModel.FromPreference));
    }

    public RecentPlaylistSourceViewModel Remember(
        IEnumerable<RecentPlaylistSourceViewModel> existingSources,
        RecentPlaylistSourceViewModel source,
        DateTimeOffset now,
        out IReadOnlyList<RecentPlaylistSourceViewModel> updatedSources)
    {
        ArgumentNullException.ThrowIfNull(existingSources);

        if (!IsUsable(source))
        {
            updatedSources = Normalize(existingSources);
            return source;
        }

        RecentPlaylistSourceViewModel? existing = existingSources.FirstOrDefault(candidate => IsSame(candidate, source));
        RecentPlaylistSourceViewModel merged = source with
        {
            DisplayName = existing?.DisplayName ?? source.DisplayName,
            IsPinned = existing?.IsPinned ?? source.IsPinned,
            LastUsedAt = now
        };

        updatedSources = Normalize(existingSources.Where(candidate => !IsSame(candidate, source)).Append(merged));
        return merged;
    }

    public IReadOnlyList<RecentPlaylistSourceViewModel> Rename(
        IEnumerable<RecentPlaylistSourceViewModel> sources,
        RecentPlaylistSourceViewModel selected,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(selected);

        return Normalize(sources.Select(source => IsSame(source, selected) ? selected with { DisplayName = displayName } : source));
    }

    public IReadOnlyList<RecentPlaylistSourceViewModel> TogglePin(
        IEnumerable<RecentPlaylistSourceViewModel> sources,
        RecentPlaylistSourceViewModel selected)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(selected);

        return Normalize(sources.Select(source => IsSame(source, selected) ? selected with { IsPinned = !selected.IsPinned } : source));
    }

    public IReadOnlyList<RecentPlaylistSourceViewModel> Remove(
        IEnumerable<RecentPlaylistSourceViewModel> sources,
        RecentPlaylistSourceViewModel selected)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(selected);

        return Normalize(sources.Where(source => !IsSame(source, selected)));
    }

    public static bool IsSame(RecentPlaylistSourceViewModel left, RecentPlaylistSourceViewModel right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return left.Kind == right.Kind &&
            left.Value.Equals(right.Value, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUsable(RecentPlaylistSourceViewModel source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(source.Value) || !Enum.IsDefined(source.Kind))
        {
            return false;
        }

        return source.Kind switch
        {
            RecentPlaylistSourceKind.LocalFile => true,
            RecentPlaylistSourceKind.RemoteUrl => SensitiveUri.TryCreate(source.Value, out _, out _),
            _ => false
        };
    }
}
