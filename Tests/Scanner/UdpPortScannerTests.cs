using System.Net;
using System.Net.Sockets;
using Core;

namespace Tests.Scanner;

public class UdpPortScannerTests
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
    public async Task ScanAsync_ClosedPort_ReturnsClosedState()
    {
        // Arrange
        int closedPort;
        {
            using var tempSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            tempSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            closedPort = ((IPEndPoint)tempSocket.LocalEndPoint!).Port;
        } // tempSocket is disposed here -> nobody listens on closedPort

        var scanner = new UdpPortScanner(maxConcurrency: 1, timeout: TimeSpan.FromSeconds(1));

        // Act
        var results = await scanner.ScanAsync("127.0.0.1", [closedPort]);

        // Assert
        var result = Assert.Single(results);
        Assert.Equal(PortState.Closed, result.State);
    }
    
    [Fact]
    public async Task ScanAsync_SilentBoundPort_ReturnsOpenFilteredState()
    {
        // Arrange
        using var silentSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        silentSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        int silentPort = ((IPEndPoint)silentSocket.LocalEndPoint!).Port;

        var scanner = new UdpPortScanner(maxConcurrency: 1, timeout: TimeSpan.FromMilliseconds(300));

        // Act
        var results = await scanner.ScanAsync("127.0.0.1", [silentPort]);

        // Assert
        var result = Assert.Single(results);
        Assert.Equal(PortState.OpenFiltered, result.State);
    }
    
    [Fact]
    public async Task ScanAsync_RespondingPort_ReturnsOpenState()
    {
        // Arrange
        using var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        int serverPort = ((IPEndPoint)serverSocket.LocalEndPoint!).Port;
    
        // safety net: server can never hang the test longer than 5 s
        using var serverCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    
        var serverTask = Task.Run(async () =>
        {
            var buffer = new byte[64];
            var anySender = new IPEndPoint(IPAddress.Any, 0);
    
            var received = await serverSocket.ReceiveFromAsync(buffer, SocketFlags.None, anySender, serverCts.Token);
            await serverSocket.SendToAsync(new byte[] { 1 }, SocketFlags.None, received.RemoteEndPoint, serverCts.Token);
        });
    
        var scanner = new UdpPortScanner(maxConcurrency: 1, timeout: TimeSpan.FromSeconds(1));
        try
        {
            var results = await scanner.ScanAsync("127.0.0.1", [serverPort]);

            var result = Assert.Single(results);
            Assert.Equal(PortState.Open, result.State);
        }
        finally
        {
            await serverTask; // always awaited, even if the Act/Assert above throws
        }
    }
}