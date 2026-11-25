using System.Net;
using Core.Defense;
using Core.Defense.Lockout;
using Microsoft.Extensions.Time.Testing;

namespace Tests.Defense.Lockout;

public sealed class LockoutHandlerTests
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);

    private readonly FakeTimeProvider _time = new();
    private readonly LockoutHandler _handler;
    private readonly LockoutOutcomeSink _sink;

    public LockoutHandlerTests()
    {
        var store = new InMemoryLockoutStore(
            new LockoutOptions { MaxFailedAttempts = MaxAttempts, LockoutDuration = Duration },
            _time);

        _handler = new LockoutHandler(store);
        _sink = new LockoutOutcomeSink(store);
    }

    private static DefenseContext Ctx(string user = "alice", string ip = "10.0.0.5") =>
        new(user, IPAddress.Parse(ip));

    private async Task Fail(DefenseContext ctx, int times)
    {
        for (var i = 0; i < times; i++)
        {
            await _sink.RecordFailureAsync(ctx);
        }
    }

    private async Task<(DefenseResult Result, bool NextCalled)> Run(DefenseContext ctx)
    {
        var called = false;
        DefenseDelegate next = (_, _) =>
        {
            called = true;
            return ValueTask.FromResult(DefenseResult.Allowed);
        };

        var result = await _handler.HandleAsync(ctx, next, CancellationToken.None);
        return (result, called);
    }

    [Fact]
    public async Task NotLocked_CallsNext_AndReturnsItsResult()
    {
        var (result, nextCalled) = await Run(Ctx());

        Assert.True(result.IsAllowed);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Locked_Blocks_WithRetryAfter_AndDoesNotCallNext()
    {
        await Fail(Ctx(), MaxAttempts);

        var (result, nextCalled) = await Run(Ctx());

        Assert.False(result.IsAllowed);
        Assert.Equal(LockoutHandler.Name, result.BlockedBy);
        Assert.Equal(Duration, result.RetryAfter);
        Assert.False(nextCalled);
    }

    [Theory]
    [InlineData(MaxAttempts - 1, true)]  // N-1: still allowed
    [InlineData(MaxAttempts, false)]     // N: locked
    [InlineData(MaxAttempts + 1, false)] // N+1: stays locked
    public async Task Boundary_AfterFailures_IsAllowedOnlyBelowLimit(int failures, bool expectedAllowed)
    {
        await Fail(Ctx(), failures);

        var (result, _) = await Run(Ctx());

        Assert.Equal(expectedAllowed, result.IsAllowed);
    }

    [Fact]
    public async Task Lockout_Expires_AfterDuration()
    {
        await Fail(Ctx(), MaxAttempts);

        _time.Advance(Duration);
        var (result, nextCalled) = await Run(Ctx());

        Assert.True(result.IsAllowed);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Success_ResetsCounter()
    {
        await Fail(Ctx(), MaxAttempts - 1);

        await _sink.RecordSuccessAsync(Ctx());
        await Fail(Ctx(), MaxAttempts - 1); // would lock if the counter was not reset

        var (result, _) = await Run(Ctx());
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task Lockout_DoesNotAffectOtherIpOrUser()
    {
        await Fail(Ctx("alice", "10.0.0.5"), MaxAttempts);

        Assert.False((await Run(Ctx("alice", "10.0.0.5"))).Result.IsAllowed);
        Assert.True((await Run(Ctx("alice", "10.0.0.6"))).Result.IsAllowed);
        Assert.True((await Run(Ctx("bob", "10.0.0.5"))).Result.IsAllowed);
    }
}