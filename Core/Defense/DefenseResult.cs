namespace Core.Defense;

public sealed record DefenseResult
{
    public static DefenseResult Allowed { get; } = new(true, null, null, null);

    public bool IsAllowed { get; }
    public string? Reason { get; }
    public string? BlockedBy { get; }
    public TimeSpan? RetryAfter { get; }

    private DefenseResult(bool isAllowed, string? reason, string? blockedBy, TimeSpan? retryAfter)
    {
        IsAllowed = isAllowed;
        Reason = reason;
        BlockedBy = blockedBy;
        RetryAfter = retryAfter;
    }

    public static DefenseResult Block(string reason, string blockedBy, TimeSpan? retryAfter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(blockedBy);
        return new DefenseResult(false, reason, blockedBy, retryAfter);
    }
}