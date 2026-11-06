using Data;
using Microsoft.EntityFrameworkCore;

namespace WebApp.Background;

public sealed class DatabaseMigrationService(
    IDbContextFactory<AppDbContext> contextFactory,
    ILogger<DatabaseMigrationService> logger): IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying {Count} pending database migration(s).", pending.Count);
        }
        
        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}