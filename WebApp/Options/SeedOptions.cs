namespace WebApp.Options;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public Dictionary<string, SeedAccount> Accounts { get; init; } = new();
}

public sealed class SeedAccount
{
    public string? UserName { get; init; }
    public string? Password { get; init; }
}