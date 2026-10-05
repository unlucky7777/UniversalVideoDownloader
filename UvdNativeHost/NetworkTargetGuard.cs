using System.Net;
using System.Net.Sockets;

internal static class NetworkTargetGuard
{
    internal static bool IsAllowedHttpUrl(string? value, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 8192 ||
            !Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            parsed.Scheme is not ("https" or "http") ||
            !string.IsNullOrEmpty(parsed.UserInfo) ||
            string.IsNullOrWhiteSpace(parsed.Host))
            return false;

        if (parsed.IsLoopback ||
            parsed.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            parsed.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            parsed.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            parsed.Host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            parsed.Host.EndsWith(".home.arpa", StringComparison.OrdinalIgnoreCase) ||
            !ResolvesOnlyToPublicAddresses(parsed.DnsSafeHost))
            return false;

        uri = parsed;
        return true;
    }

    private static bool ResolvesOnlyToPublicAddresses(string host)
    {
        if (IPAddress.TryParse(host, out var literal))
            return !IsPrivateOrSpecial(literal);

        try
        {
            var lookup = Dns.GetHostAddressesAsync(host);
            if (!lookup.Wait(TimeSpan.FromSeconds(2)))
                return false;

            var addresses = lookup.Result;
            return addresses.Length > 0 && addresses.All(ip => !IsPrivateOrSpecial(ip));
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsPrivateOrSpecial(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] is 0 or 10 or 127 ||
                   (b[0] == 100 && b[1] is >= 64 and <= 127) ||
                   (b[0] == 169 && b[1] == 254) ||
                   (b[0] == 172 && b[1] is >= 16 and <= 31) ||
                   (b[0] == 192 && b[1] == 0 && b[2] == 0) ||
                   (b[0] == 192 && b[1] == 168) ||
                   (b[0] == 198 && b[1] is 18 or 19) ||
                   b[0] >= 224;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = ip.GetAddressBytes();
            if (ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.IPv6None) ||
                ip.Equals(IPAddress.IPv6Loopback))
                return true;
            // fc00::/7 unique-local, fe80::/10 link-local, ff00::/8 multicast.
            return (b[0] & 0xfe) == 0xfc ||
                   (b[0] == 0xfe && (b[1] & 0xc0) == 0x80) ||
                   b[0] == 0xff;
        }

        return true;
    }
}
