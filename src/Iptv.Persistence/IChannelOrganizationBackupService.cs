namespace Iptv.Persistence;

public interface IChannelOrganizationBackupService
{
    Task ExportAsync(string path, ChannelOrganizationBackup backup, CancellationToken cancellationToken);

    Task<ChannelOrganizationBackup> ImportAsync(string path, CancellationToken cancellationToken);
}
