namespace Core.Jobs;

public interface IScanJobStore
{
    ScanJob Create(string host, IReadOnlyList<int> ports);
    ScanJob? Get(Guid id);
    ScanJob MarkRunning(Guid id);
    ScanJob MarkCompleted(Guid id, IReadOnlyList<PortScanResult> results);
    ScanJob MarkFailed(Guid id, string error);
}