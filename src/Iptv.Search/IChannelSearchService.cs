using Iptv.Core.Channels;

namespace Iptv.Search;

public interface IChannelSearchService
{
    IReadOnlyList<Channel> Search(IEnumerable<Channel> channels, ChannelSearchQuery query);
}
