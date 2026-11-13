using System.ComponentModel.DataAnnotations;

namespace Core.Defense.Lockout;

public sealed class LockoutOptions
{
    public const string SectionName = "Lockout";

    [Range(1, 1000)] public int MaxFailedAttempts { get; init; } = 5;

    [Range(typeof(TimeSpan), "00:00:01", "7.00:00:00")]
    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);

    [Range(1, 10_000_000)] public int MaxTrackedKeys { get; init; } = 100_000;
    
    [Range(typeof(TimeSpan), "00:00:01", "7.00:00:00")]
    public TimeSpan FailureWindow { get; init; } = TimeSpan.FromMinutes(15);
}