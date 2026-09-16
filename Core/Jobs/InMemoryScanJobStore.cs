using System.Collections.Concurrent;

namespace Core.Jobs;

public sealed class InMemoryScanJobStore : IScanJobStore
{
    private readonly ConcurrentDictionary<Guid, ScanJob> _jobs = new();

    public ScanJob Create(string host, IReadOnlyList<int> ports)
    {
        var job = new ScanJob
        {
            Id = Guid.NewGuid(),
            Host = host,
            Ports = ports,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _jobs.TryAdd(job.Id, job);
        return job;
    }

    public ScanJob? Get(Guid id) => _jobs.GetValueOrDefault(id);

    public ScanJob MarkRunning(Guid id) =>
        Update(id, job => job with { Status = ScanJobStatus.Running });

    //The with expression - Nondestructive mutation creates a new object with modified properties {learn.microsoft.com}

    public ScanJob MarkCompleted(Guid id, IReadOnlyList<PortScanResult> results) =>
        Update(id, job => job with
        {
            Status = ScanJobStatus.Completed,
            Results = results,
            CompletedAt = DateTimeOffset.UtcNow
        });

    public ScanJob MarkFailed(Guid id, string error) =>
        Update(id, job => job with
        {
            Status = ScanJobStatus.Failed,
            Error = error,
            CompletedAt = DateTimeOffset.UtcNow
        });

    private ScanJob Update(Guid id, Func<ScanJob, ScanJob> transform) =>
        _jobs.AddOrUpdate(
            id,
            _ => throw new KeyNotFoundException($"Scan job '{id}' not found."),
            (_, current) => transform(current));


    // Explanation: id is unique identification for job we want to update
    // Func< ScanJob, ScanJob> transform is a delegate (function as parameter)
    // it takes current ScanJob, and by with expression makes its new copy;then return new ScanJob
    // In MarkRunning, we are forwarding function 'job => job with { ...}'

    //AddOrUpdate takes three arguments:
    //1. id (key we are looking for)
    //2.function if job with Id does not exist in dictionary
    //3.if job does exist
}