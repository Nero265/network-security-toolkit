using System.Collections.Concurrent;

namespace Core.Defense.Lockout;

public sealed class InMemoryLockoutStore(LockoutOptions options, TimeProvider timeProvider) : ILockoutStore
{
    private static readonly TimeSpan EvictionBackoff = TimeSpan.FromSeconds(1);

    private sealed record Entry(int FailedAttempts, DateTimeOffset? LockedUntil, DateTimeOffset LastFailureAt);

    private readonly ConcurrentDictionary<LockoutKey, Entry> _entries = new();

    // ConcurrentDictionary.Count takes all internal locks, so we keep our own counter.
    private int _count;
    private long _droppedFailures;
    private long _nextEvictionTicks; // eviction back-off, UTC ticks
    private int _evicting;           // single-flight guard (0/1)

    public int TrackedKeyCount => Volatile.Read(ref _count);
    public long DroppedFailureCount => Interlocked.Read(ref _droppedFailures);

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

        while (true)
        {
            if (_entries.TryGetValue(key, out var current))
            {
                var next = Next(current, now);

                if (ReferenceEquals(next, current) || _entries.TryUpdate(key, next, current))
                {
                    return ValueTask.FromResult(ToStatus(next, now));
                }

                continue; // lost a race with another writer, retry with fresh state
            }

            if (!TryReserveSlot(now))
            {
                // Saturated: fail open for tracking. Failing closed would let an attacker
                // fill the table and block every new login (the defense becoming the DoS).
                Interlocked.Increment(ref _droppedFailures);
                return ValueTask.FromResult(new LockoutStatus(false, 1, null));
            }

            var created = Next(null, now);
            if (_entries.TryAdd(key, created))
            {
                return ValueTask.FromResult(ToStatus(created, now));
            }

            Interlocked.Decrement(ref _count); // another thread added this key first, retry
        }
    }

    public ValueTask ResetAsync(LockoutKey key, CancellationToken cancellationToken = default)
    {
        if (_entries.TryRemove(key, out _))
        {
            Interlocked.Decrement(ref _count);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        foreach (var key in _entries.Keys)
        {
            if (_entries.TryRemove(key, out _))
            {
                Interlocked.Decrement(ref _count);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Removes expired entries. Called by the periodic sweeper (step 3b).</summary>
    public int RemoveExpired() => RemoveExpired(timeProvider.GetUtcNow());

    private bool TryReserveSlot(DateTimeOffset now)
    {
        if (TryIncrementBelowLimit())
        {
            return true;
        }

        return MakeRoom(now) && TryIncrementBelowLimit();
    }

    private bool TryIncrementBelowLimit()
    {
        if (Interlocked.Increment(ref _count) <= options.MaxTrackedKeys)
        {
            return true;
        }

        Interlocked.Decrement(ref _count);
        return false;
    }

    private bool MakeRoom(DateTimeOffset now)
    {
        // Back-off: an earlier attempt freed nothing, so do not scan the table again yet.
        if (now.UtcTicks < Interlocked.Read(ref _nextEvictionTicks))
        {
            return false;
        }

        // Single-flight: one thread scans, the others fail open immediately.
        if (Interlocked.CompareExchange(ref _evicting, 1, 0) != 0)
        {
            return false;
        }

        try
        {
            // Free ~10% of capacity per scan so the O(n) cost is amortized over many inserts.
            var batch = Math.Max(1, options.MaxTrackedKeys / 10);

            var freed = RemoveExpired(now);
            if (freed < batch)
            {
                freed += EvictOldestUnlocked(now, batch - freed);
            }

            if (freed == 0)
            {
                Interlocked.Exchange(ref _nextEvictionTicks, (now + EvictionBackoff).UtcTicks);
            }

            return freed > 0;
        }
        finally
        {
            Volatile.Write(ref _evicting, 0);
        }
    }

    private int RemoveExpired(DateTimeOffset now)
    {
        var removed = 0;

        foreach (var pair in _entries)
        {
            if (IsExpired(pair.Value, now) && TryRemoveExact(pair))
            {
                removed++;
            }
        }

        return removed;
    }

    private int EvictOldestUnlocked(DateTimeOffset now, int count)
    {
        var victims = _entries
            .Where(pair => !IsLocked(pair.Value, now))
            .OrderBy(pair => pair.Value.LastFailureAt)
            .Take(count)
            .ToList();

        var removed = 0;

        foreach (var pair in victims)
        {
            if (TryRemoveExact(pair))
            {
                removed++;
            }
        }

        return removed;
    }

    // Removes only if the value is still the one we inspected, so a fresher entry is never deleted.
    private bool TryRemoveExact(KeyValuePair<LockoutKey, Entry> pair)
    {
        if (!_entries.TryRemove(pair))
        {
            return false;
        }

        Interlocked.Decrement(ref _count);
        return true;
    }

    // Must stay pure: AddOrUpdate-style retry loops may call it more than once.
    private Entry Next(Entry? current, DateTimeOffset now)
    {
        // Still locked: a failure does not extend the lockout.
        if (current?.LockedUntil > now)
        {
            return current;
        }

        // Missing or expired (lockout over / failure window elapsed): start a fresh count.
        var previous = current is null || IsExpired(current, now) ? 0 : current.FailedAttempts;
        var count = previous + 1;

        return count >= options.MaxFailedAttempts
            ? new Entry(count, now + options.LockoutDuration, now)
            : new Entry(count, null, now);
    }

    private static bool IsLocked(Entry entry, DateTimeOffset now) => entry.LockedUntil > now;

    private bool IsExpired(Entry entry, DateTimeOffset now) =>
        entry.LockedUntil is { } until
            ? until <= now
            : entry.LastFailureAt + options.FailureWindow <= now;

    private LockoutStatus ToStatus(Entry entry, DateTimeOffset now)
    {
        if (IsExpired(entry, now))
        {
            return LockoutStatus.None;
        }

        return entry.LockedUntil is { } until
            ? new LockoutStatus(true, entry.FailedAttempts, until - now)
            : new LockoutStatus(false, entry.FailedAttempts, null);
    }
}