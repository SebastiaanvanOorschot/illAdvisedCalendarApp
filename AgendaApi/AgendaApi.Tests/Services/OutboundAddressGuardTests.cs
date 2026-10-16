using System.Net;
using AgendaApi.Services;

namespace AgendaApi.Tests.Services;

public class OutboundAddressGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.255.0.9")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]   // cloud metadata
    [InlineData("100.64.0.1")]        // carrier-grade NAT
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("192.0.2.10")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fd12:3456::1")]      // Railway private network
    [InlineData("fc00::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:127.0.0.1")]  // IPv4-mapped loopback
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("64:ff9b::a00:1")]    // NAT64 for 10.0.0.1
    [InlineData("2001:db8::1")]
    public void NonPublicAddresses_AreNotPublic(string address)
    {
        Assert.False(OutboundAddressGuard.IsPublicAddress(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    [InlineData("172.32.0.1")]        // just outside 172.16.0.0/12
    [InlineData("100.128.0.1")]       // just outside 100.64.0.0/10
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    [InlineData("64:ff9b::808:808")]  // NAT64 for 8.8.8.8
    public void PublicAddresses_ArePublic(string address)
    {
        Assert.True(OutboundAddressGuard.IsPublicAddress(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("localhost", true)]
    [InlineData("LOCALHOST.", true)]
    [InlineData("api.localhost", true)]
    [InlineData("postgres.railway.internal", true)]
    [InlineData("printer.local", true)]
    [InlineData("nas.home.arpa", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("[::1]", true)]
    [InlineData("calendar.google.com", false)]
    [InlineData("localhost.example.com", false)]
    [InlineData("internal.example.com", false)]
    [InlineData("8.8.8.8", false)]
    public void IsBlockedHost(string host, bool expected)
    {
        Assert.Equal(expected, OutboundAddressGuard.IsBlockedHost(host));
    }

    [Fact]
    public void EnsureAllPublic_AcceptsOnlyPublicAddresses()
    {
        OutboundAddressGuard.EnsureAllPublic("example.test", new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("2606:4700:4700::1111") });
    }

    [Fact]
    public void EnsureAllPublic_RejectsANameWithOnePrivateRecord()
    {
        // one public and one private A record must not be enough to reach the private one
        Assert.Throws<BlockedAddressException>(() =>
            OutboundAddressGuard.EnsureAllPublic("rebind.example.test", new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("10.0.0.1") }));
    }

    [Fact]
    public void EnsureAllPublic_RejectsANameWithoutAddresses()
    {
        Assert.Throws<BlockedAddressException>(() => OutboundAddressGuard.EnsureAllPublic("nothing.example.test", Array.Empty<IPAddress>()));
    }

    [Theory]
    [InlineData("http://127.0.0.1:9/feed.ics")]
    [InlineData("http://[::1]:9/feed.ics")]
    [InlineData("http://localhost:9/feed.ics")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    public async Task Handler_RefusesToConnectToInternalAddresses(string url)
    {
        using var client = new HttpClient(OutboundAddressGuard.CreateHandler());

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(url));

        Assert.Contains(InnerExceptions(error), e => e is BlockedAddressException);
    }

    [Fact]
    public void Handler_LimitsRedirects()
    {
        var handler = OutboundAddressGuard.CreateHandler();

        Assert.True(handler.AllowAutoRedirect);
        Assert.Equal(5, handler.MaxAutomaticRedirections);
        Assert.NotNull(handler.ConnectCallback);
    }

    private static IEnumerable<Exception> InnerExceptions(Exception exception)
    {
        for (var e = exception.InnerException; e != null; e = e.InnerException)
            yield return e;
    }
}
