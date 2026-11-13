namespace Core.Defense.Lockout;

public readonly record struct LockoutStatus(bool IsLocked, int FailedAttempts, TimeSpan? RetryAfter)
{
    public static LockoutStatus None => default;
}