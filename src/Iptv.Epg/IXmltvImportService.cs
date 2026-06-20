using Iptv.Core.Epg;

namespace Iptv.Epg;

public interface IXmltvImportService
{
    Task<EpgImportResult> ImportFileAsync(string path, CancellationToken cancellationToken);
}
