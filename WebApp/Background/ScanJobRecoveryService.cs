using Core.Jobs;
using Data;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Background;

public sealed class ScanJobRecoveryService(
    IDbContextFactory<AppDbContext> contextFactory,
    ILogger<ScanJobRecoveryService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        await db.Database.MigrateAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var recovered = await db.ScanJobs
            .Where(j => j.Status == ScanJobStatus.Pending || j.Status == ScanJobStatus.Running)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, ScanJobStatus.Failed)
                    .SetProperty(j => j.Error, "Interrupted by application restart")
                    .SetProperty(j => j.CompletedAt, now),
                cancellationToken);

        if (recovered > 0)
        {
            logger.LogWarning("Marked {RecoveredCount} interrupted scan job(s) as Failed on startup.", recovered);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}