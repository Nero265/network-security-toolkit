namespace Core;

public interface IPortScanner
{
    Task<IReadOnlyList<PortScanResult>> ScanAsync(
        string host,
        IEnumerable<int> ports,
        CancellationToken cancellationToken = default);
}