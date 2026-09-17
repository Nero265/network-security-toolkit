namespace Core;

public enum PortState
{
    Open,
    Closed,
    Filtered
}

public sealed record PortScanResult(int Port, PortState State);