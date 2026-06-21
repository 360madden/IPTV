namespace Iptv.App.ViewModels;

public sealed record LibraryHealthResourceMetrics(
    long ManagedMemoryBeforeBytes,
    long ManagedMemoryAfterBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections)
{
    public long ManagedMemoryDeltaBytes => ManagedMemoryAfterBytes - ManagedMemoryBeforeBytes;
}
