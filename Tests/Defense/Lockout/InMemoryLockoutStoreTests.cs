using System.Net;
using Core.Defense.Lockout;
using Microsoft.Extensions.Time.Testing;

namespace Tests.Defense.Lockout;

public sealed class InMemoryLockoutStoreTests : LockoutStoreContractTests
{
    private readonly FakeTimeProvider _clock = new();

    protected override ILockoutStore CreateStore(LockoutOptions options, TimeProvider timeProvider) =>
        new InMemoryLockoutStore(options, timeProvider);

    private InMemoryLockoutStore Store(int maxKeys, int maxAttempts = 3) =>
        new(new LockoutOptions
        {
            MaxTrackedKeys = maxKeys,
            MaxFailedAttempts = maxAttempts,
            LockoutDuration = TimeSpan.FromMinutes(15),
            FailureWindow = TimeSpan.FromMinutes(10)
        }, _clock);

    private static LockoutKey Key(string user) => LockoutKey.Create(user, IPAddress.Parse("10.0.0.5"));

    [Fact]
    public async Task RegisterFailure_NeverTracksMoreThanMaxTrackedKeys()
    {
        var store = Store(maxKeys: 10);

        for (var i = 0; i < 50; i++)
        {
            await store.RegisterFailureAsync(Key($"user{i}"));
        }

        Assert.True(store.TrackedKeyCount <= 10);
    }

    [Fact]
    public async Task WhenFull_ExpiredEntriesAreRemovedFirst()
    {
        var store = Store(maxKeys: 10);
        for (var i = 0; i < 10; i++)
        {
            await store.RegisterFailureAsync(Key($"user{i}"));
        }

        _clock.Advance(TimeSpan.FromMinutes(10)); // all entries expire
        var status = await store.RegisterFailureAsync(Key("new"));

        Assert.Equal(1, status.FailedAttempts);
        Assert.Equal(1, store.TrackedKeyCount);
        Assert.Equal(0, store.DroppedFailureCount);
    }

    [Fact]
    public async Task WhenFull_OldestUnlockedEntryIsEvicted()
    {
        var store = Store(maxKeys: 10);
        for (var i = 0; i < 10; i++)
        {
            await store.RegisterFailureAsync(Key($"user{i}"));
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        await store.RegisterFailureAsync(Key("new"));

        Assert.Equal(0, (await store.GetStatusAsync(Key("user0"))).FailedAttempts); // oldest evicted
        Assert.Equal(1, (await store.GetStatusAsync(Key("user1"))).FailedAttempts); // others kept
        Assert.Equal(1, (await store.GetStatusAsync(Key("new"))).FailedAttempts);
        Assert.Equal(10, store.TrackedKeyCount);
    }

    [Fact]
    public async Task WhenFullOfLockedEntries_NewKeysFailOpen_AndLockedEntriesSurvive()
    {
        var store = Store(maxKeys: 10, maxAttempts: 1); // one failure locks
        for (var i = 0; i < 10; i++)
        {
            await store.RegisterFailureAsync(Key($"user{i}"));
        }

        var first = await store.RegisterFailureAsync(Key("new1"));
        var second = await store.RegisterFailureAsync(Key("new2")); // eviction back-off path

        Assert.False(first.IsLocked);
        Assert.False(second.IsLocked);
        Assert.Equal(2, store.DroppedFailureCount);
        Assert.Equal(10, store.TrackedKeyCount);
        for (var i = 0; i < 10; i++)
        {
            Assert.True((await store.GetStatusAsync(Key($"user{i}"))).IsLocked);
        }
    }

    [Fact]
    public async Task RemoveExpired_RemovesOnlyExpiredEntries()
    {
        var store = Store(maxKeys: 10);
        await store.RegisterFailureAsync(Key("old"));
        _clock.Advance(TimeSpan.FromMinutes(10));
        await store.RegisterFailureAsync(Key("fresh"));

        var removed = store.RemoveExpired();

        Assert.Equal(1, removed);
        Assert.Equal(1, store.TrackedKeyCount);
    }

    [Fact]
    public async Task ConcurrentFailures_ForSameKey_AreNotLost()
    {
        var store = Store(maxKeys: 100, maxAttempts: 1000);

        await Parallel.ForEachAsync(Enumerable.Range(0, 500), async (_, ct) =>
            await store.RegisterFailureAsync(Key("alice"), ct));

        Assert.Equal(500, (await store.GetStatusAsync(Key("alice"))).FailedAttempts);
    }
}