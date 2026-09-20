namespace WebApp.Options;

public sealed class ScanApiOptions
{
    public const string SectionName = "ScanApi";
    
    public int MaxPortsPerScan { get; init; } = 1000;
}