using System.Net;
using System.Net.Sockets;

namespace Core.Defense.Lockout;

public sealed record LockoutKey
{
    public const int MaxUsernameLenght = 256;
    private const string UnknownIp = "unknown";
    
    public string Username { get; }
    public string Ip {get; }

    private LockoutKey(string username, string ip)
    {
        Username = username;
        Ip = ip;
    }

    public static LockoutKey Create(string? username, IPAddress? ip)
    {
        username ??= string.Empty;

        if (username.Length > MaxUsernameLenght)
        {
            username = username[..MaxUsernameLenght];
        }

        return new LockoutKey(username.ToUpperInvariant(), NormalizeIp(ip));
    }

    private static string NormalizeIp(IPAddress? ip)
    {
        if (ip is null)
        {
            return UnknownIp;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            Span<byte> bytes = stackalloc byte[16]; //keep only the /64 prefix
            ip.TryWriteBytes(bytes, out _);
            bytes[8..].Clear();
            ip = new IPAddress(bytes);
        }

        return ip.ToString();
    }
}