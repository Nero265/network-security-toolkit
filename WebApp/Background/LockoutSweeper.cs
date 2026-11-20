using Core.Defense.Lockout;

namespace WebApp.Background;

public sealed class LockoutSweeper(
    InMemoryLockoutStore store,
    LockoutOptions options,
    TimeProvider timeProvider,
    ILogger<LockoutSweeper> logger) : BackgroundService
{
    private long _lastDroppedFailures;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.SweepInterval, timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    SweepOnce();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Lockout sweep failed.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            
        }
    }

    public int SweepOnce()
    {
        var removed = store.RemoveExpired();

        var dropped = store.DroppedFailureCount;
        var newlyDropped = dropped - _lastDroppedFailures;
        _lastDroppedFailures = dropped;

        if (removed > 0)
        {
            logger.LogDebug("Lockout sweep removed {RemovedCount} expired entries.", removed);
        }

        if (newlyDropped > 0)
        {
            logger.LogWarning(
                "{DroppedCount} failed login attempt(s) were not tracked since the last sweep because the lockout store is full ({TrackedKeys} keys).",
                newlyDropped, store.TrackedKeyCount);
        }

        return removed;
    }
}