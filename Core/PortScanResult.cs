namespace Core;

public enum PortState
{
    Open,
    Closed,
    Filtered,
    OpenFiltered
}

public sealed record PortScanResult(int Port, PortState State);