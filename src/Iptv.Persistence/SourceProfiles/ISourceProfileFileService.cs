namespace Iptv.Persistence.SourceProfiles;

public interface ISourceProfileFileService
{
    Task<SourceProfileExport> ImportAsync(string path, CancellationToken cancellationToken);

    Task ExportAsync(string path, SourceProfileExport export, CancellationToken cancellationToken);
}
