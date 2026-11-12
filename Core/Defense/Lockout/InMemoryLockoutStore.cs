using System.Collections.Concurrent;

namespace Core.Defense.Lockout;

public sealed class InMemoryLockoutStore(LockoutOptions options, TimeProvider timeProvider) : ILockoutStore
{
    private sealed record Entry(int FailedAttempts, DateTimeOffset? LockedUntil);

    private readonly ConcurrentDictionary<LockoutKey, Entry> _entries = new();

    public ValueTask<LockoutStatus> GetStatusAsync(LockoutKey key, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var status = _entries.TryGetValue(key, out var entry)
            ? ToStatus(entry, now)
            : LockoutStatus.None;

        return ValueTask.FromResult(status);
    }

    public ValueTask<LockoutStatus> RegisterFailureAsync(LockoutKey key, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        // AddOrUpdate may run the delegates more than once under contention,
        // so Next must stay pure (no side effects, result depends only on its arguments).
        var updated = _entries.AddOrUpdate(
            key,
            _ => Next(null, now),
            (_, current) => Next(current, now));

        return ValueTask.FromResult(ToStatus(updated, now));
    }

    public ValueTask ResetAsync(LockoutKey key, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        _entries.Clear();
        return ValueTask.CompletedTask;
    }

    private Entry Next(Entry? current, DateTimeOffset now)
    {
        // failure does not extend the lockout
        if (current?.LockedUntil > now)
        {
            return current;
        }

        var previous = current is null || current.LockedUntil is not null
            ? 0
            : current.FailedAttempts;

        var count = previous + 1;

        return count >= options.MaxFailedAttempts
            ? new Entry(count, now + options.LockoutDuration)
            : new Entry(count, null);
    }

    private static LockoutStatus ToStatus(Entry entry, DateTimeOffset now)
    {
        if (entry.LockedUntil is { } until)
        {
            return until > now
                ? new LockoutStatus(true, entry.FailedAttempts, until - now)
                : LockoutStatus.None;
        }

        return new LockoutStatus(false, entry.FailedAttempts, null);
    }
}