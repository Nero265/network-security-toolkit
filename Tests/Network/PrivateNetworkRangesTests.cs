using System.Net;
using System.Runtime.InteropServices;
using Core;

namespace Tests.Network;

public sealed class PrivateNetworkRangesTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.255.255.254")]
    [InlineData("::1")]
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.254")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.0.1")]
    [InlineData("192.168.255.254")]
    [InlineData("169.254.1.1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    public void IsAllowed_PrivateOrLoopbackOrLinkLocal_ReturnsTrue(string ip)
    {
        var address = IPAddress.Parse(ip);
        
        Assert.True(PrivateNetworkRanges.IsAllowed(address));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("192.0.2.1")]
    [InlineData("172.32.0.1")]
    [InlineData("172.15.255.255")]
    [InlineData("2001:4860:4860::8888")] //google public dns, ipv6
    public void IsAllowed_PublicAddress_ReturnsFalse(string ip)
    {
        var address = IPAddress.Parse(ip);
        
        Assert.False(PrivateNetworkRanges.IsAllowed(address));
    }
}