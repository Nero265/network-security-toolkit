namespace Core.Defense;

public sealed record DefenseResult
{
    public static DefenseResult Allowed { get; } = new(true, null, null);

    public bool IsAllowed { get; }
    public string? Reason { get; }
    public string? BlockedBy { get; }

    private DefenseResult(bool isAllowed, string? reason, string? blockedBy)
    {
        IsAllowed = isAllowed;
        Reason = reason;
        BlockedBy = blockedBy;
    }

    public static DefenseResult Block(string reason, string blockedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(blockedBy);
        return new DefenseResult(false, reason, blockedBy);
    }

}