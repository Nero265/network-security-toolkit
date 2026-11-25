using System.Net;
using Core.Defense.Lockout;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using WebApp.Background;

namespace Tests.Defense.Lockout;

public sealed class LockoutSweeperTests
{
    private static LockoutKey Key(string user) => LockoutKey.Create(user, IPAddress.Parse("10.0.0.5"));

    [Fact]
    public async Task SweepOnce_RemovesExpiredEntries_AndKeepsFreshOnes()
    {
        var clock = new FakeTimeProvider();
        var options = new LockoutOptions();
        var store = new InMemoryLockoutStore(options, clock);
        var sweeper = new LockoutSweeper(store, options, clock, NullLogger<LockoutSweeper>.Instance);

        await store.RegisterFailureAsync(Key("old"));
        clock.Advance(options.FailureWindow);
        await store.RegisterFailureAsync(Key("fresh"));

        var removed = sweeper.SweepOnce();

        Assert.Equal(1, removed);
        Assert.Equal(1, store.TrackedKeyCount);
        Assert.Equal(0, sweeper.SweepOnce()); // nothing left to remove
    }
}