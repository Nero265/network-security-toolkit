using System.Net;

namespace Core;

public static class PrivateNetworkRanges
{
    private static readonly IPNetwork[] AllowedRanges =
    [
        IPNetwork.Parse("127.0.0.0/8"), //loopback ipv4
        IPNetwork.Parse("::1/128"), //loopback ipv6
        IPNetwork.Parse("10.0.0.0/8"), //private ipv4
        IPNetwork.Parse("172.16.0.0/12"), //private ipv4
        IPNetwork.Parse("192.168.0.0/16"), //private ipv4
        IPNetwork.Parse("fc00::/7"), //unique local ipv6
        IPNetwork.Parse("169.254.0.0/16"), //link-local ipv4
        IPNetwork.Parse("fe80::/10"), //link-local ipv6
    ];
    
    public static bool IsAllowed(IPAddress address) =>
        AllowedRanges.Any(range => range.Contains(address));
}