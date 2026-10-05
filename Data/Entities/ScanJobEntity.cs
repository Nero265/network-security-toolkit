using Core.Jobs;

namespace Data.Entities;

public sealed class ScanJobEntity
{
    public Guid Id { get; set; }
    public string Host { get; set; } = string.Empty;
    public string PortsJson { get; set; } = "[]";
    public ScanType Type { get; set; }
    public ScanJobStatus Status { get; set; }
    public string? ResultsJson { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}