using System.Diagnostics;
using Iptv.Core.PlaylistImport;

namespace Iptv.App.ViewModels;

public delegate Task<PlaylistImportResult> PlaylistImportOperation(
    CancellationToken cancellationToken,
    IProgress<PlaylistImportProgress>? progress);

public sealed class PlaylistImportCoordinator
{
    public async Task<PlaylistImportExecution> RunAsync(
        PlaylistImportOperation operation,
        CancellationToken cancellationToken,
        IProgress<PlaylistImportProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(operation);

        Stopwatch stopwatch = Stopwatch.StartNew();
        PlaylistImportResult result = await operation(cancellationToken, progress).ConfigureAwait(false);
        stopwatch.Stop();

        string? fatalError = null;
        if (result.Summary.ErrorCount > 0 && result.Channels.Count == 0)
        {
            fatalError = result.Issues.FirstOrDefault(issue => issue.Severity == ImportIssueSeverity.Error)?.Message
                ?? "Playlist import failed.";
        }

        return new PlaylistImportExecution(result, stopwatch.Elapsed, fatalError);
    }
}

public sealed record PlaylistImportExecution(
    PlaylistImportResult Result,
    TimeSpan Duration,
    string? FatalError);
