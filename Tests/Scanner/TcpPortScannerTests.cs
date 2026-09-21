using System.Net;
using System.Net.Sockets;
using Core;

namespace Tests.Scanner;

public sealed class TcpPortScannerTests
{
    [Fact]
    public async Task ScanAsync_UnresolvableHost_ThrowsSocketException()
    {
        //Arrange
        var scanner = new TcpPortScanner();
        var ports = new[] { 80, 443 };

        //Act & Assert
        await Assert.ThrowsAsync<SocketException>(async () =>
            await scanner.ScanAsync("this-host-does-not-exist.invalid", ports));
    }

    [Fact]
    public async Task ScanAsync_ReturnsResultsSortedByPort()
    {
        var scanner = new TcpPortScanner(maxConcurrency: 5, timeout: TimeSpan.FromMilliseconds(100));
        var results = await scanner.ScanAsync("127.0.0.1", [9, 5, 7, 1, 3]);

        Assert.Equal([1, 3, 5, 7, 9], results.Select(r => r.Port).ToArray());
    }

    [Fact]
    public async Task ScanAsync_WithOpenAndClosedPorts_UsingRawSocket()
    {
        //Arrange
        using var listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        //bind localhost and port 0 (let OS choose)
        listeningSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listeningSocket.Listen(1); //state listening

        int openPort = ((IPEndPoint)listeningSocket.LocalEndPoint!).Port;
        int closedPort;
        {
            using var tempSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tempSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            closedPort = ((IPEndPoint)tempSocket.LocalEndPoint!).Port;
            //here, tempSocket is getting closed, port is closed
        }

        var scanner = new TcpPortScanner(maxConcurrency: 2, timeout: TimeSpan.FromSeconds(1));

        //Act
        var results = await scanner.ScanAsync("127.0.0.1", [openPort, closedPort]);

        //Assert
        var openResult = results.FirstOrDefault(r => r.Port == openPort);
        var closedResult = results.FirstOrDefault(r => r.Port == closedPort);

        Assert.NotNull(openResult);
        Assert.Equal(PortState.Open, openResult.State);

        Assert.NotNull(closedResult);
        Assert.True(
            closedResult.State == PortState.Closed || closedResult.State == PortState.Filtered,
            $"Expected Closed or Filtered, but got {closedResult.State}"
        );
    }

    [Fact]
    public async Task ScanAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var scanner = new TcpPortScanner();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await scanner.ScanAsync("127.0.0.1", [80], cts.Token));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ScanAsync_PortTimeout_ReturnFilteredState()
    {
        // Arrange: We use ultra short timeout and address that does not correspond
        var scanner = new TcpPortScanner(maxConcurrency: 1, timeout: TimeSpan.FromMilliseconds(50));
        
        // Act
        // Address 192.0.2.1 is TEST-NET-1 reserved address [RFC 5737]
        var results = await scanner.ScanAsync("192.0.2.1", [80]);
        
        // Assert
        var result = results.FirstOrDefault();
        Assert.NotNull(result);
        Assert.Equal(PortState.Filtered, result.State);
    }
    
    [Fact]
    public async Task ScanAsync_EmptyPortsList_ReturnsEmptyResult()
    {
        // Arrange
        var scanner = new TcpPortScanner();

        // Act
        var results = await scanner.ScanAsync("127.0.0.1", Enumerable.Empty<int>());

        // Assert
        Assert.Empty(results);
    }
    
    [Fact]
    public async Task ScanAsync_WhenHostResolvesToIPv6_DoesNotThrowAddressFamilyMismatch()
    {
        var scanner = new TcpPortScanner(timeout: TimeSpan.FromMilliseconds(200));
    
        // "::1" resolves directly to IPv6 loopback, no DNS lookup needed
        var results = await scanner.ScanAsync("::1", new[] { 65000 });
    
        // Before the fix, this would throw SocketException (address family mismatch)
        // instead of returning a Filtered/Closed result
        Assert.Single(results);
        Assert.NotEqual(default, results[0]);
    }
}