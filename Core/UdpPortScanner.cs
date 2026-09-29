using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Core;

public sealed class UdpPortScanner(int maxConcurrency = 100, TimeSpan? timeout = null) : IPortScanner
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMilliseconds(500);
    public async Task<IReadOnlyList<PortScanResult>> ScanAsync(string host, IEnumerable<int> ports, CancellationToken cancellationToken = default)
    {
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);

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

    private async Task<PortScanResult> ScanPortAsync(IPAddress ipAddress, int port, CancellationToken cancellationToken)
    {
        using var socket = new Socket(ipAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await socket.ConnectAsync(new IPEndPoint(ipAddress, port), linkedCts.Token);

            await socket.SendAsync(Array.Empty<byte>(), SocketFlags.None, linkedCts.Token);

            var buffer = ArrayPool<byte>.Shared.Rent(64);
            try
            {
                _ = await socket.ReceiveAsync(buffer.AsMemory(0, 64), SocketFlags.None, linkedCts.Token);
                return new PortScanResult(port, PortState.Open);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested &&
                                                 !cancellationToken.IsCancellationRequested)
        {
            return new PortScanResult(port, PortState.OpenFiltered);
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionRefused)
        {
            return new PortScanResult(port, PortState.Closed);
        }
        catch (SocketException)
        {
            //example: ICMP Destination Unreachable
            return new PortScanResult(port, PortState.OpenFiltered);
        }
    }
}