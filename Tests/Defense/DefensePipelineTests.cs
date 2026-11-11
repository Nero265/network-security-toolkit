using System.Net;
using Core.Defense;

namespace Tests.Defense;

public sealed class DefensePipeline
{
    private static readonly DefenseContext Context = new("alice", IPAddress.Loopback);

    [Fact]
    public async Task EvaluateAsync_WithNoHandlers_ReturnsAllowed()
    {
        var pipeline = new DefensePipelineBuilder().Build();
        var result = await pipeline.EvaluateAsync(Context);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task EvaluateAsync_RunsHandlersInTheOrderTheyWereAdded()
    {
        var calls = new List<string>();
        var pipeline = new DefensePipelineBuilder()
            .Add(new PassThroughHandler("lockout", calls))
            .Add(new PassThroughHandler("rateLimit", calls))
            .Add(new PassThroughHandler("captcha", calls))
            .Build();

        var result = await pipeline.EvaluateAsync(Context);

        Assert.True(result.IsAllowed);
        Assert.Equal(["lockout", "rateLimit", "captcha"], calls);
    }
    
    [Fact]
    public async Task EvaluateAsync_WhenHandlerBlocks_SkipsLaterHandlers()
    {
        var calls = new List<string>();
        var pipeline = new DefensePipelineBuilder()
            .Add(new PassThroughHandler("lockout", calls))
            .Add(new BlockingHandler("rateLimit", calls, DefenseResult.Block("Too many attempts.", "RateLimit")))
            .Add(new PassThroughHandler("captcha", calls))
            .Build();

        var result = await pipeline.EvaluateAsync(Context);

        Assert.False(result.IsAllowed);
        Assert.Equal(["lockout", "rateLimit"], calls); // captcha never ran
    }

    [Fact]
    public async Task EvaluateAsync_WhenHandlerBlocks_ReturnsItsResultUnchanged()
    {
        var block = DefenseResult.Block("Account locked.", "Lockout");
        var pipeline = new DefensePipelineBuilder()
            .Add(new BlockingHandler("lockout", [], block))
            .Build();

        var result = await pipeline.EvaluateAsync(Context);

        Assert.Same(block, result);
        Assert.Equal("Account locked.", result.Reason);
        Assert.Equal("Lockout", result.BlockedBy);
    }

    [Fact]
    public async Task EvaluateAsync_OuterHandlerSeesResultOfInnerHandler()
    {
        var seen = new List<DefenseResult>();
        var block = DefenseResult.Block("Captcha required.", "Captcha");
        var pipeline = new DefensePipelineBuilder()
            .Add(new ObservingHandler(seen))
            .Add(new BlockingHandler("captcha", [], block))
            .Build();

        await pipeline.EvaluateAsync(Context);

        Assert.Single(seen);
        Assert.Same(block, seen[0]);
    }

    [Fact]
    public async Task EvaluateAsync_PassesContextAndCancellationTokenToHandlers()
    {
        using var cts = new CancellationTokenSource();
        var capturing = new CapturingHandler();
        var pipeline = new DefensePipelineBuilder()
            .Add(new PassThroughHandler("first", []))
            .Add(capturing)
            .Build();

        await pipeline.EvaluateAsync(Context, cts.Token);

        Assert.Same(Context, capturing.Context);
        Assert.Equal(cts.Token, capturing.Token);
    }

    [Fact]
    public async Task Build_IsNotAffectedByHandlersAddedAfterwards()
    {
        var calls = new List<string>();
        var builder = new DefensePipelineBuilder().Add(new PassThroughHandler("first", calls));
        var pipeline = builder.Build();

        builder.Add(new PassThroughHandler("late", calls));
        await pipeline.EvaluateAsync(Context);

        Assert.Equal(["first"], calls);
    }

    [Fact]
    public void Add_WithNullHandler_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DefensePipelineBuilder().Add(null!));
    }

    // ---- fakes ----

    private sealed class PassThroughHandler(string name, List<string> calls) : IDefenseHandler
    {
        public ValueTask<DefenseResult> HandleAsync(
            DefenseContext context, DefenseDelegate next, CancellationToken cancellationToken = default)
        {
            calls.Add(name);
            return next(context, cancellationToken);
        }
    }
    
    private sealed class BlockingHandler(string name, List<string> calls, DefenseResult result) : IDefenseHandler
    {
        public ValueTask<DefenseResult> HandleAsync(
            DefenseContext context, DefenseDelegate next, CancellationToken cancellationToken = default)
        {
            calls.Add(name);
            return ValueTask.FromResult(result); // does not call next
        }
    }

    private sealed class ObservingHandler(List<DefenseResult> seen) : IDefenseHandler
    {
        public async ValueTask<DefenseResult> HandleAsync(
            DefenseContext context, DefenseDelegate next, CancellationToken cancellationToken = default)
        {
            var result = await next(context, cancellationToken);
            seen.Add(result);
            return result;
        }
    }

    private sealed class CapturingHandler : IDefenseHandler
    {
        public DefenseContext? Context { get; private set; }
        public CancellationToken Token { get; private set; }

        public ValueTask<DefenseResult> HandleAsync(
            DefenseContext context, DefenseDelegate next, CancellationToken cancellationToken = default)
        {
            Context = context;
            Token = cancellationToken;
            return next(context, cancellationToken);
        }
    }
}