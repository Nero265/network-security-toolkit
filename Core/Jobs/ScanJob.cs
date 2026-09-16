namespace Core.Jobs;

public enum ScanJobStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

public sealed record ScanJob
{
    public required Guid Id { get; init; }
    public required string Host { get; init; }
    public required IReadOnlyList<int> Ports { get; init; }
    public ScanJobStatus Status { get; init; } = ScanJobStatus.Pending;
    public IReadOnlyList<PortScanResult>? Results { get; init; }
    public string? Error { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}