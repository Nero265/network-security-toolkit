using Core;
using Core.Jobs;
using Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Data;

public sealed class SqliteScanJobStore(IDbContextFactory<AppDbContext> contextFactory) : IScanJobStore
{
    public ScanJob Create(string host, IReadOnlyList<int> ports, ScanType type = ScanType.Tcp)
    {
        var job = new ScanJob
        {
            Id = Guid.NewGuid(),
            Host = host,
            Ports = ports,
            Type = type,
            CreatedAt = DateTimeOffset.UtcNow
        };

        using var db = contextFactory.CreateDbContext();
        db.ScanJobs.Add(job.ToEntity());
        db.SaveChanges();
        return job;
    }
    
    public ScanJob? Get(Guid id)
    {
        using var db = contextFactory.CreateDbContext();
        return db.ScanJobs.AsNoTracking()
            .FirstOrDefault(j => j.Id == id)
            ?.ToDomain();
    }
    
    public ScanJob MarkRunning(Guid id) =>
        Update(id, e => e.Status = ScanJobStatus.Running);
    
    public ScanJob MarkCompleted(Guid id, IReadOnlyList<PortScanResult> results) =>
        Update(id, e =>
        {
            e.Status = ScanJobStatus.Completed;
            e.ResultsJson = ScanJobMapper.SerializeResults(results);
            e.CompletedAt = DateTimeOffset.UtcNow;
        });
    
    public ScanJob MarkFailed(Guid id, string error) =>
        Update(id, e =>
        {
            e.Status = ScanJobStatus.Failed;
            e.Error = error;
            e.CompletedAt = DateTimeOffset.UtcNow;
        });
    
    public int CountActive()
    {
        using var db = contextFactory.CreateDbContext();
        return db.ScanJobs.Count(j =>
            j.Status == ScanJobStatus.Pending || j.Status == ScanJobStatus.Running);
    }
    
    private ScanJob Update(Guid id, Action<ScanJobEntity> change)
    {
        using var db = contextFactory.CreateDbContext();
        var entity = db.ScanJobs.Find(id)
                     ?? throw new KeyNotFoundException($"Scan job '{id}' not found.");
        change(entity);
        db.SaveChanges();
        return entity.ToDomain();
    }
}
