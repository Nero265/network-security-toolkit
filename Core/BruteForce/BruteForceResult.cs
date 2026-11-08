namespace Core.BruteForce;

public sealed record BruteForceResult(int Attempts, TimeSpan Elapsed, BruteForceOutcome Outcome);
