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

    // IPv4 ranges that are not reachable on (or not meant for) the public internet
    private static readonly (IPAddress Network, int PrefixLength)[] BlockedIPv4Ranges =
    {
        (IPAddress.Parse("0.0.0.0"), 8),        // "this network"
        (IPAddress.Parse("10.0.0.0"), 8),       // private
        (IPAddress.Parse("100.64.0.0"), 10),    // carrier-grade NAT
        (IPAddress.Parse("127.0.0.0"), 8),      // loopback
        (IPAddress.Parse("169.254.0.0"), 16),   // link-local, cloud metadata
        (IPAddress.Parse("172.16.0.0"), 12),    // private
        (IPAddress.Parse("192.0.0.0"), 24),     // IETF protocol assignments
        (IPAddress.Parse("192.0.2.0"), 24),     // documentation
        (IPAddress.Parse("192.168.0.0"), 16),   // private
        (IPAddress.Parse("198.18.0.0"), 15),    // benchmarking
        (IPAddress.Parse("198.51.100.0"), 24),  // documentation
        (IPAddress.Parse("203.0.113.0"), 24),   // documentation
        (IPAddress.Parse("224.0.0.0"), 4),      // multicast
        (IPAddress.Parse("240.0.0.0"), 4),      // reserved, broadcast
    };

    private static readonly (IPAddress Network, int PrefixLength)[] BlockedIPv6Ranges =
    {
        (IPAddress.Parse("fc00::"), 7),         // unique local, incl. Railway's private network
        (IPAddress.Parse("fe80::"), 10),        // link-local
        (IPAddress.Parse("fec0::"), 10),        // site-local (deprecated)
        (IPAddress.Parse("ff00::"), 8),         // multicast
        (IPAddress.Parse("2001:db8::"), 32),    // documentation
    };

    private static readonly (IPAddress Network, int PrefixLength) Nat64Range = (IPAddress.Parse("64:ff9b::"), 96);

    /// True when the address is a normal public unicast address.
    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
            return !BlockedIPv4Ranges.Any(r => IsInRange(address, r.Network, r.PrefixLength));

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return false;

        if (address.Equals(IPAddress.IPv6Loopback) || address.Equals(IPAddress.IPv6None))
            return false;

        // NAT64 embeds an IPv4 address in the last 32 bits; judge that address instead
        if (IsInRange(address, Nat64Range.Network, Nat64Range.PrefixLength))
            return IsPublicAddress(new IPAddress(address.GetAddressBytes()[12..]));

        return !BlockedIPv6Ranges.Any(r => IsInRange(address, r.Network, r.PrefixLength));
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

    private static bool IsInRange(IPAddress address, IPAddress network, int prefixLength)
    {
        if (address.AddressFamily != network.AddressFamily)
            return false;

        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (var i = 0; i < fullBytes; i++)
            if (addressBytes[i] != networkBytes[i])
                return false;

        if (remainingBits == 0)
            return true;

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (addressBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
    }
}

/// Thrown when a user-supplied URL would make the server connect to a non-public address.
public sealed class BlockedAddressException : IOException
{
    public BlockedAddressException(string message) : base(message) { }
}
