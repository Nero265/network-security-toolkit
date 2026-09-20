using Core;

namespace Tests;

public sealed class TcpPortScannerIntegrationTests
{
    private const string KaliVmIp = "192.168.1.103";
    //on kali vm use sudo iptables -A INPUT -p tcp --dport 8888 -j DROP
    //and when finished sudo iptables -D INPUT -p tcp --dport 8888 -j DROP
    private const int BlockedPort = 8888;

    [Fact(Skip = "This integration test require Kali VM and executes only locally.")]
    [Trait("Category", "Integration")]
    public async Task ScanAsync_AgainstKaliVm_FilteredPort_ReturnsFilteredState()
    {
        // Arrange - give it 1 sec timeout 
        var scanner = new TcpPortScanner(maxConcurrency: 1, timeout: TimeSpan.FromSeconds(1));
        
        // Act
        var results = await scanner.ScanAsync(KaliVmIp, [BlockedPort]);
        
        // Assert
        var result = results.FirstOrDefault();
        Assert.NotNull(result);
        
        Assert.Equal(PortState.Filtered, result.State);
    }
}