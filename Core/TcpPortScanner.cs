using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Core;

public sealed class TcpPortScanner(int maxConcurrency = 100, TimeSpan? timeout = null) : IPortScanner
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMilliseconds(500);

    public async Task<IReadOnlyList<PortScanResult>> ScanAsync(string host, IEnumerable<int> ports,
        CancellationToken cancellationToken = default)
    {
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        if (addresses.Length == 0)
        {
            throw new ArgumentException("Not possible to resolve given host.", nameof(host));
        }

        IPAddress ipAddress = addresses[0];

        var results = new ConcurrentBag<PortScanResult>();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(ports, parallelOptions, async (port, ct) =>
        {
            var result = await ScanPortAsync(ipAddress, port, ct);
            results.Add(result);
        });

        return results.OrderBy(r => r.Port).ToList();
    }

    private async Task<PortScanResult> ScanPortAsync(IPAddress ipAddress, int port, CancellationToken outerToken)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.NoDelay = true;

        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(outerToken, timeoutCts.Token);

        try
        {
            await socket.ConnectAsync(ipAddress, port, linkedCts.Token);
            socket.Close(0);
            return new PortScanResult(port, PortState.Open);
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode == SocketError.ConnectionRefused)
            {
                return new PortScanResult(port, PortState.Closed);
            }

            return new PortScanResult(port, PortState.Filtered);
        }
        catch (OperationCanceledException) when (!outerToken.IsCancellationRequested)
        {
            return new PortScanResult(port, PortState.Filtered);
        }
    }
}