namespace Core;

public enum PortState
{
    Open,
    Closed,
    Filtered
}

public record PortScanResult(int Port, PortState State);