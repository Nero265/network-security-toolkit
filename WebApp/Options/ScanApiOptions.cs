namespace WebApp.Options;

public sealed class ScanApiOptions
{
    public const string SectionName = "ScanApi";
    
    public int MaxPortsPerScan { get; init; } = 1000;
    public int MaxActiveJobs { get; init; } = 10;
    public bool AllowPublicTargets { get; init; } = false;
}