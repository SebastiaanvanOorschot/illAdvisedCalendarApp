using System.Net;
using System.Net.Sockets;

namespace AgendaApi.Services;

/// <summary>
/// Keeps requests to user-supplied URLs (iCal subscriptions) on the public internet, so a URL
/// can't be used to reach this server, Railway's private network (*.railway.internal, fd00::/8)
/// or cloud metadata endpoints.
/// </summary>
/// <remarks>
/// The real check runs in <see cref="CreateHandler"/>'s ConnectCallback: it resolves the host
/// itself and refuses to connect to a non-public address. That covers DNS names that resolve
/// to private addresses, DNS rebinding and redirects, because every connection, including one
/// made after a redirect, goes through it. <see cref="IsBlockedHost"/> is only an early check
/// that gives the user a clear error for URLs that are obviously internal.
/// </remarks>
public static class OutboundAddressGuard
{
    private const int MaxRedirects = 5;

    // Names that only ever resolve inside a private network
    private static readonly string[] InternalNameSuffixes = { ".localhost", ".internal", ".local", ".home.arpa" };

    // Ranges that are not reachable on (or not meant for) the public internet
    private static readonly IPNetwork[] BlockedRanges = new[]
    {
        "0.0.0.0/8",        // "this network"
        "10.0.0.0/8",       // private
        "100.64.0.0/10",    // carrier-grade NAT
        "127.0.0.0/8",      // loopback
        "169.254.0.0/16",   // link-local, cloud metadata
        "172.16.0.0/12",    // private
        "192.0.0.0/24",     // IETF protocol assignments
        "192.0.2.0/24",     // documentation
        "192.168.0.0/16",   // private
        "198.18.0.0/15",    // benchmarking
        "198.51.100.0/24",  // documentation
        "203.0.113.0/24",   // documentation
        "224.0.0.0/4",      // multicast
        "240.0.0.0/4",      // reserved, broadcast
        "::/128",           // unspecified
        "::1/128",          // loopback
        "fc00::/7",         // unique local, incl. Railway's private network
        "fe80::/10",        // link-local
        "fec0::/10",        // site-local (deprecated)
        "ff00::/8",         // multicast
        "2001:db8::/32",    // documentation
    }.Select(IPNetwork.Parse).ToArray();

    // NAT64 embeds an IPv4 address in the last 32 bits
    private static readonly IPNetwork Nat64Range = IPNetwork.Parse("64:ff9b::/96");

    /// True when the address is a normal public unicast address.
    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily != AddressFamily.InterNetwork && address.AddressFamily != AddressFamily.InterNetworkV6)
            return false;

        // For NAT64, judge the embedded IPv4 address instead
        if (Nat64Range.Contains(address))
            return IsPublicAddress(new IPAddress(address.GetAddressBytes()[12..]));

        return !BlockedRanges.Any(range => range.Contains(address));
    }

    /// True for hosts that are internal by their name or are a non-public IP literal.
    /// Other names are checked when the connection is made.
    public static bool IsBlockedHost(string host)
    {
        var name = host.Trim('[', ']').TrimEnd('.').ToLowerInvariant();

        if (IPAddress.TryParse(name, out var literal))
            return !IsPublicAddress(literal);

        return name == "localhost" || InternalNameSuffixes.Any(name.EndsWith);
    }

    /// Throws unless every address the host resolved to is public. All of them, not just one:
    /// otherwise a name with one public and one private record could still reach the private one.
    public static void EnsureAllPublic(string host, IReadOnlyCollection<IPAddress> addresses)
    {
        if (addresses.Count == 0)
            throw new BlockedAddressException($"'{host}' did not resolve to any address.");

        if (addresses.Any(a => !IsPublicAddress(a)))
            throw new BlockedAddressException($"'{host}' resolves to a private or internal address.");
    }

    /// The HTTP handler for fetching user-supplied URLs: it only ever connects to public addresses.
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = MaxRedirects,
        ConnectCallback = ConnectToPublicAddressAsync
    };

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        if (IsBlockedHost(host))
            throw new BlockedAddressException($"'{host}' is a private or internal host.");

        var addresses = IPAddress.TryParse(host.Trim('[', ']'), out var literal)
            ? new[] { literal }
            : await Dns.GetHostAddressesAsync(host, cancellationToken);
        EnsureAllPublic(host, addresses);

        // Connect to exactly the addresses that were checked, so a second DNS lookup can't swap them
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

/// Thrown when a user-supplied URL would make the server connect to a non-public address.
public sealed class BlockedAddressException : IOException
{
    public BlockedAddressException(string message) : base(message) { }
}
