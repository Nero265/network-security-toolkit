using System.Net;
using Core.Defense.Lockout;

namespace Tests.Defense.Lockout;

public sealed class LockoutKeyTests
{
    private static readonly IPAddress Ip = IPAddress.Parse("10.0.0.5");

    [Fact]
    public void Create_DifferentCasing_ProducesSameKey()
    {
        Assert.Equal(LockoutKey.Create("Admin", Ip), LockoutKey.Create("aDMIN", Ip));
    }

    [Fact]
    public void Create_DifferentUsers_SameIp_ProducesDifferentKeys()
    {
        Assert.NotEqual(LockoutKey.Create("alice", Ip), LockoutKey.Create("bob", Ip));
    }

    [Fact]
    public void Create_SameUser_DifferentIps_ProducesDifferentKeys()
    {
        Assert.NotEqual(
            LockoutKey.Create("alice", IPAddress.Parse("10.0.0.5")),
            LockoutKey.Create("alice", IPAddress.Parse("10.0.0.6")));
    }

    [Fact]
    public void Create_NullUsername_IsTreatedAsEmpty()
    {
        Assert.Equal(LockoutKey.Create("", Ip), LockoutKey.Create(null, Ip));
    }

    [Fact]
    public void Create_UsernameLongerThanLimit_IsTruncated()
    {
        var key = LockoutKey.Create(new string('a', 1000), Ip);

        Assert.Equal(LockoutKey.MaxUsernameLenght, key.Username.Length);
    }

    [Fact]
    public void Create_NullIp_UsesSharedBucketDifferentFromRealIps()
    {
        Assert.Equal(LockoutKey.Create("alice", null), LockoutKey.Create("alice", null));
        Assert.NotEqual(LockoutKey.Create("alice", null), LockoutKey.Create("alice", Ip));
    }

    [Fact]
    public void Create_IPv4MappedIPv6_EqualsPlainIPv4()
    {
        Assert.Equal(
            LockoutKey.Create("alice", IPAddress.Parse("1.2.3.4")),
            LockoutKey.Create("alice", IPAddress.Parse("::ffff:1.2.3.4")));
    }

    [Fact]
    public void Create_IPv6InSame64Prefix_ProducesSameKey()
    {
        Assert.Equal(
            LockoutKey.Create("alice", IPAddress.Parse("2001:db8:1:2:aaaa:bbbb:cccc:dddd")),
            LockoutKey.Create("alice", IPAddress.Parse("2001:db8:1:2::1")));
    }

    [Fact]
    public void Create_IPv6InDifferent64Prefix_ProducesDifferentKeys()
    {
        Assert.NotEqual(
            LockoutKey.Create("alice", IPAddress.Parse("2001:db8:1:2::1")),
            LockoutKey.Create("alice", IPAddress.Parse("2001:db8:1:3::1")));
    }
}