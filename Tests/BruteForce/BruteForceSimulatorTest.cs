using System.Net;
using Core.BruteForce;

namespace Tests.BruteForce;

public sealed class BruteForceSimulatorTests
{
    private sealed class StubHttpMessageHandler(Func<int, HttpStatusCode> statusForAttempt) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(statusForAttempt(Bodies.Count));
        }
    }

    private static BruteForceOptions Options(params string[] passwords) => new()
    {
        TargetUrl = new Uri("http://127.0.0.1/api/auth/login"),
        Username = "weak",
        Passwords = passwords,
        DelayBetweenAttempts = TimeSpan.Zero
    };

    private static (BruteForceSimulator Simulator, StubHttpMessageHandler Handler) Create(
        Func<int, HttpStatusCode> statusForAttempt,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolveHost = null)
    {
        var handler = new StubHttpMessageHandler(statusForAttempt);
        return (new BruteForceSimulator(new HttpClient(handler), resolveHost), handler);
    }
    
    private static Func<string, CancellationToken, Task<IPAddress[]>> ResolvesTo(params string[] ips) =>
        (_, _) => Task.FromResult(ips.Select(IPAddress.Parse).ToArray());

    [Fact]
    public async Task RunAsync_WhenSecondPasswordWorks_ReturnsFoundAfterTwoAttempts()
    {
        var (simulator, _) = Create(n => n == 2 ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);

        var result = await simulator.RunAsync(Options("a", "b", "c"));

        Assert.Equal(BruteForceOutcome.Found, result.Outcome);
        Assert.Equal(2, result.Attempts);
    }

    [Fact]
    public async Task RunAsync_WhenNoPasswordWorks_ReturnsExhausted()
    {
        var (simulator, _) = Create(_ => HttpStatusCode.Unauthorized);

        var result = await simulator.RunAsync(Options("a", "b", "c"));

        Assert.Equal(BruteForceOutcome.Exhausted, result.Outcome);
        Assert.Equal(3, result.Attempts);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Locked)]
    public async Task RunAsync_WhenDefenseRespondsWithBlockedStatus_StopsAndReturnsBlocked(HttpStatusCode blockedStatus)
    {
        var (simulator, handler) = Create(n => n == 3 ? blockedStatus : HttpStatusCode.Unauthorized);

        var result = await simulator.RunAsync(Options("a", "b", "c", "d", "e"));

        Assert.Equal(BruteForceOutcome.Blocked, result.Outcome);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, handler.Bodies.Count);
    }

    [Fact]
    public async Task RunAsync_MaxAttemptsLowerThanPasswordCount_CapsAttempts()
    {
        var (simulator, handler) = Create(_ => HttpStatusCode.Unauthorized);

        var result = await simulator.RunAsync(Options("a", "b", "c", "d", "e") with { MaxAttempts = 3 });

        Assert.Equal(BruteForceOutcome.Exhausted, result.Outcome);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, handler.Bodies.Count);
    }

    [Fact]
    public async Task RunAsync_SendsConfiguredUsernameAndPasswordAsJson()
    {
        var (simulator, handler) = Create(_ => HttpStatusCode.Unauthorized);

        await simulator.RunAsync(Options("hunter2"));

        var body = Assert.Single(handler.Bodies);
        Assert.Contains("\"username\":\"weak\"", body);
        Assert.Contains("\"password\":\"hunter2\"", body);
    }

    [Theory]
    [InlineData("http://8.8.8.8/api/auth/login")]
    [InlineData("https://1.1.1.1/api/auth/login")]
    [InlineData("http://[2001:4860:4860::8888]/api/auth/login")]
    public async Task RunAsync_WhenTargetIsPublic_ThrowsWithoutSendingAnyRequest(string url)
    {
        var (simulator, handler) = Create(_ => HttpStatusCode.OK);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            simulator.RunAsync(Options("a") with { TargetUrl = new Uri(url) }));

        Assert.Empty(handler.Bodies);
    }

    [Fact]
    public async Task RunAsync_WhenSchemeIsNotHttp_Throws()
    {
        var (simulator, _) = Create(_ => HttpStatusCode.OK);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            simulator.RunAsync(Options("a") with { TargetUrl = new Uri("ftp://127.0.0.1/login") }));
    }

    [Fact]
    public async Task RunAsync_WhenStatusIsUnexpected_ThrowsInsteadOfReportingExhausted()
    {
        var (simulator, _) = Create(_ => HttpStatusCode.InternalServerError);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => simulator.RunAsync(Options("a", "b")));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [Fact]
    public async Task RunAsync_WhenAlreadyCancelled_ThrowsOperationCanceled()
    {
        var (simulator, handler) = Create(_ => HttpStatusCode.Unauthorized);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            simulator.RunAsync(Options("a", "b"), cts.Token));

        Assert.Empty(handler.Bodies);
    }

    [Fact]
    public async Task RunAsync_WithEmptyPasswordList_Throws()
    {
        var (simulator, _) = Create(_ => HttpStatusCode.OK);

        await Assert.ThrowsAsync<ArgumentException>(() => simulator.RunAsync(Options()));
    }
    
    [Fact]
    public async Task RunAsync_WhenCancelledDuringDelay_ThrowsPromptly()
    {
        var (simulator, handler) = Create(_ => HttpStatusCode.Unauthorized);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var run = simulator.RunAsync(
            Options("a", "b", "c") with { DelayBetweenAttempts = TimeSpan.FromHours(1) },
            cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => run.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Single(handler.Bodies);
    }
    
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("127.0.0.1,8.8.8.8")] // mixed: one allowed + one public must still be rejected
    public async Task RunAsync_WhenHostnameResolvesToAnyPublicAddress_ThrowsWithoutSendingAnyRequest(string resolved)
    {
        var (simulator, handler) = Create(_ => HttpStatusCode.OK, ResolvesTo(resolved.Split(',')));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            simulator.RunAsync(Options("a") with
            {
                TargetUrl = new Uri("http://login.example.test/api/auth/login")
            }));

        Assert.Empty(handler.Bodies);
    }

    [Fact]
    public async Task RunAsync_WhenHostnameResolvesToPrivateAddress_ResolvesHostAndProceeds()
    {
        string? resolvedHost = null;
        var (simulator, _) = Create(_ => HttpStatusCode.OK, (host, _) =>
        {
            resolvedHost = host;
            return Task.FromResult(new[] { IPAddress.Parse("10.0.0.5") });
        });

        var result = await simulator.RunAsync(Options("a") with
        {
            TargetUrl = new Uri("http://login.example.test/api/auth/login")
        });

        Assert.Equal(BruteForceOutcome.Found, result.Outcome);
        Assert.Equal("login.example.test", resolvedHost);
    }
}