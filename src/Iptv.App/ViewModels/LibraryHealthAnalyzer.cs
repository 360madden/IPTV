using Iptv.Core;
using Iptv.Core.Channels;
using Iptv.Core.PlaylistImport;

namespace Iptv.App.ViewModels;

public static class LibraryHealthAnalyzer
{
    public static IReadOnlyList<LibraryHealthMetricViewModel> BuildMetrics(
        IReadOnlyCollection<Channel> channels,
        IReadOnlyCollection<Guid> savedStateIds,
        IReadOnlyDictionary<string, string[]> sourceDefaultHiddenGroups,
        int epgProgramCount,
        TimeSpan? lastImportDuration,
        PlaylistImportSummary? importSummary,
        LibraryHealthResourceMetrics? resourceMetrics)
    {
        ArgumentNullException.ThrowIfNull(channels);
        ArgumentNullException.ThrowIfNull(savedStateIds);
        ArgumentNullException.ThrowIfNull(sourceDefaultHiddenGroups);

        var metrics = new List<LibraryHealthMetricViewModel>();
        int total = channels.Count;
        if (total == 0)
        {
            metrics.Add(new LibraryHealthMetricViewModel("Library", "Empty", "Import a user-provided playlist to populate health metrics."));
            return metrics;
        }

        int visible = channels.Count(channel => !channel.IsHidden);
        int hidden = total - visible;
        int favorites = channels.Count(channel => channel.IsFavorite);
        int customGrouped = channels.Count(channel => !string.IsNullOrWhiteSpace(channel.CustomGroup));
        int sources = channels.Select(channel => channel.SourceId).Distinct().Count();
        int groups = channels.Select(channel => channel.EffectiveGroupTitle).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        int vodAndSeries = channels.Count(channel => channel.ContentKind is ContentKind.Vod or ContentKind.Series);
        int logoUrls = channels.Count(channel => !string.IsNullOrWhiteSpace(channel.TvgLogo));
        int matchedStates = channels.Count(channel => savedStateIds.Contains(channel.Id));
        int defaultHiddenRules = sourceDefaultHiddenGroups.Values.Sum(groups => groups.Length);

        metrics.Add(new LibraryHealthMetricViewModel("Channels", total.ToString("N0"), $"{visible:N0} visible, {hidden:N0} hidden"));
        metrics.Add(new LibraryHealthMetricViewModel("Sources", sources.ToString("N0"), $"{groups:N0} effective groups"));
        metrics.Add(new LibraryHealthMetricViewModel("Organization", $"{favorites:N0} favorites", $"{customGrouped:N0} custom-grouped, {matchedStates:N0} saved states matched"));
        metrics.Add(new LibraryHealthMetricViewModel("VOD/Series", vodAndSeries.ToString("N0"), "Detected from playlist metadata and names."));
        metrics.Add(new LibraryHealthMetricViewModel("Logos", logoUrls.ToString("N0"), "Playlist entries with logo/poster URLs."));
        metrics.Add(new LibraryHealthMetricViewModel("Default visibility rules", defaultHiddenRules.ToString("N0"), $"Across {sourceDefaultHiddenGroups.Count:N0} sources."));
        metrics.Add(new LibraryHealthMetricViewModel("EPG programs", epgProgramCount.ToString("N0"), "Imported XMLTV guide entries currently loaded."));

        if (lastImportDuration is not null)
        {
            metrics.Add(new LibraryHealthMetricViewModel("Last import time", FormatDuration(lastImportDuration.Value), "Includes parsing and service-level import work."));
        }

        if (resourceMetrics is not null)
        {
            metrics.Add(new LibraryHealthMetricViewModel(
                "Import memory",
                FormatBytes(resourceMetrics.ManagedMemoryAfterBytes),
                $"delta {FormatSignedBytes(resourceMetrics.ManagedMemoryDeltaBytes)}, GC {resourceMetrics.Gen0Collections:N0}/{resourceMetrics.Gen1Collections:N0}/{resourceMetrics.Gen2Collections:N0}"));
        }

        if (importSummary is not null)
        {
            metrics.Add(new LibraryHealthMetricViewModel(
                "Last import quality",
                $"{importSummary.ValidCount:N0} valid",
                $"{importSummary.WarningCount:N0} warnings, {importSummary.ErrorCount:N0} errors, {importSummary.DuplicateCount:N0} duplicates"));
        }

        return metrics;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalSeconds >= 1
            ? $"{duration.TotalSeconds:N1}s"
            : $"{duration.TotalMilliseconds:N0}ms";
    }

    private static string FormatSignedBytes(long bytes)
    {
        return bytes >= 0 ? $"+{FormatBytes(bytes)}" : $"-{FormatBytes(Math.Abs(bytes))}";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L)
        {
            return $"{bytes / 1024d / 1024d / 1024d:N1} GB";
        }

        if (bytes >= 1024L * 1024L)
        {
            return $"{bytes / 1024d / 1024d:N1} MB";
        }

        if (bytes >= 1024L)
        {
            return $"{bytes / 1024d:N1} KB";
        }

        return $"{bytes:N0} bytes";
    }
}
