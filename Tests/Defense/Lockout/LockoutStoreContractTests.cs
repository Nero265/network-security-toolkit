using System.Net;
using Core.Defense.Lockout;
using Microsoft.Extensions.Time.Testing;

namespace Tests.Defense.Lockout;

public abstract class LockoutStoreContractTests
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    // xUnit creates a new instance per test, so every test gets its own clock.
    private readonly FakeTimeProvider _time = new();

    protected abstract ILockoutStore CreateStore(LockoutOptions options, TimeProvider timeProvider);

    private ILockoutStore NewStore() => CreateStore(
        new LockoutOptions { MaxFailedAttempts = MaxAttempts, LockoutDuration = Duration, FailureWindow = Window },
        _time);

    private static LockoutKey Key(string user = "alice", string ip = "10.0.0.5") =>
        LockoutKey.Create(user, IPAddress.Parse(ip));

    private static async Task<LockoutStatus> Fail(ILockoutStore store, LockoutKey key, int times)
    {
        var status = LockoutStatus.None;
        for (var i = 0; i < times; i++)
        {
            status = await store.RegisterFailureAsync(key);
        }

        return status;
    }

    [Fact]
    public async Task GetStatus_UnknownKey_IsNotLocked()
    {
        var status = await NewStore().GetStatusAsync(Key());

        Assert.False(status.IsLocked);
        Assert.Equal(0, status.FailedAttempts);
        Assert.Null(status.RetryAfter);
    }

    [Fact]
    public async Task RegisterFailure_OneBelowLimit_DoesNotLock()
    {
        var status = await Fail(NewStore(), Key(), MaxAttempts - 1);

        Assert.False(status.IsLocked);
        Assert.Equal(MaxAttempts - 1, status.FailedAttempts);
    }

    [Fact]
    public async Task RegisterFailure_AtLimit_Locks()
    {
        var status = await Fail(NewStore(), Key(), MaxAttempts);

        Assert.True(status.IsLocked);
        Assert.Equal(Duration, status.RetryAfter);
    }

    [Fact]
    public async Task RegisterFailure_WhileLocked_DoesNotExtendLockout()
    {
        var store = NewStore();
        await Fail(store, Key(), MaxAttempts);

        _time.Advance(TimeSpan.FromMinutes(5));
        var status = await store.RegisterFailureAsync(Key());

        Assert.True(status.IsLocked);
        Assert.Equal(Duration - TimeSpan.FromMinutes(5), status.RetryAfter);
    }

    [Fact]
    public async Task Lockout_OneMillisecondBeforeExpiry_IsStillLocked()
    {
        var store = NewStore();
        await Fail(store, Key(), MaxAttempts);

        _time.Advance(Duration - TimeSpan.FromMilliseconds(1));

        Assert.True((await store.GetStatusAsync(Key())).IsLocked);
    }

    [Fact]
    public async Task Lockout_ExactlyAtExpiry_IsUnlockedAndCounterReset()
    {
        var store = NewStore();
        await Fail(store, Key(), MaxAttempts);

        _time.Advance(Duration);
        var status = await store.GetStatusAsync(Key());

        Assert.False(status.IsLocked);
        Assert.Equal(0, status.FailedAttempts);
    }

    [Fact]
    public async Task AfterExpiry_FullNumberOfFailuresIsNeededToLockAgain()
    {
        var store = NewStore();
        await Fail(store, Key(), MaxAttempts);
        _time.Advance(Duration);

        var belowLimit = await Fail(store, Key(), MaxAttempts - 1);
        var atLimit = await store.RegisterFailureAsync(Key());

        Assert.False(belowLimit.IsLocked);
        Assert.True(atLimit.IsLocked);
    }

    [Fact]
    public async Task Reset_ClearsFailureCounter()
    {
        var store = NewStore();
        await Fail(store, Key(), MaxAttempts - 1);

        await store.ResetAsync(Key());
        var status = await store.RegisterFailureAsync(Key());

        Assert.False(status.IsLocked);
        Assert.Equal(1, status.FailedAttempts);
    }

    [Fact]
    public async Task Reset_UnlocksLockedKey()
    {
        var store = NewStore();
        await Fail(store, Key(), MaxAttempts);

        await store.ResetAsync(Key());

        Assert.False((await store.GetStatusAsync(Key())).IsLocked);
    }

    [Fact]
    public async Task Keys_AreIndependent()
    {
        var store = NewStore();
        await Fail(store, Key("alice", "10.0.0.5"), MaxAttempts);

        Assert.True((await store.GetStatusAsync(Key("alice", "10.0.0.5"))).IsLocked);
        Assert.False((await store.GetStatusAsync(Key("bob", "10.0.0.5"))).IsLocked);
        Assert.False((await store.GetStatusAsync(Key("alice", "10.0.0.6"))).IsLocked);
    }

    [Fact]
    public async Task Clear_RemovesAllKeys()
    {
        var store = NewStore();
        await Fail(store, Key("alice"), MaxAttempts);
        await Fail(store, Key("bob"), MaxAttempts);

        await store.ClearAsync();

        Assert.False((await store.GetStatusAsync(Key("alice"))).IsLocked);
        Assert.False((await store.GetStatusAsync(Key("bob"))).IsLocked);
    }
    
    [Fact]
    public async Task Failures_OlderThanWindow_AreForgotten()
    {
        var store = NewStore();
        await Fail(store, Key(), MaxAttempts - 1);

        _time.Advance(Window);

        Assert.Equal(0, (await store.GetStatusAsync(Key())).FailedAttempts);
        Assert.Equal(1, (await store.RegisterFailureAsync(Key())).FailedAttempts);
    }

    [Fact]
    public async Task Failures_JustInsideWindow_StillCount()
    {
        var store = NewStore();
        await Fail(store, Key(), MaxAttempts - 1);

        _time.Advance(Window - TimeSpan.FromMilliseconds(1));
        var status = await store.RegisterFailureAsync(Key());

        Assert.True(status.IsLocked);
    }
}