using System.Net;

namespace Core.BruteForce;

public sealed record BruteForceOptions
{
    private static readonly IReadOnlySet<HttpStatusCode> DefaultBlockedStatusCodes =
        new HashSet<HttpStatusCode> { HttpStatusCode.TooManyRequests, HttpStatusCode.Locked };
    
    public required Uri TargetUrl { get; init; }
    public required string Username { get; init; }
    public required IReadOnlyList<string> Passwords { get; init; }
    public int MaxAttempts { get; init; } = 100;
    public TimeSpan DelayBetweenAttempts { get; init; } = TimeSpan.FromMilliseconds(100);
    
    //defense stopped us
    public IReadOnlySet<HttpStatusCode> BlockedStatusCodes { get; init; } = DefaultBlockedStatusCodes;
}