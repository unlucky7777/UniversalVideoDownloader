#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace N_m3u8DL_RE_GUI.Core.Capture;

/// <summary>
/// Browser network observations, scored separately from generic page/document requests.
/// Credentials are never copied automatically: WebView2 may request third-party segments,
/// and N_m3u8DL-RE propagates imported headers to follow-up media requests.
/// </summary>
public static class MediaRequestSelector
{
    private static readonly HashSet<string> SafeHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Referer", "Origin", "User-Agent", "Accept", "Accept-Language"
    };

    public static CapturedRequest? Create(
        string? url,
        IEnumerable<KeyValuePair<string, string>>? requestHeaders,
        string? contentType = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return null;
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.IsLoopback || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            IsPrivateAddress(uri.Host))
            return null;

        var type = contentType?.Split(';')[0].Trim().ToLowerInvariant() ?? "";
        var kind = Classify(uri.AbsolutePath, type);
        if (kind == CapturedStreamKind.Unknown) return null;
        var headers = (requestHeaders ?? Array.Empty<KeyValuePair<string, string>>())
            .Where(h => SafeHeaders.Contains(h.Key) && HeaderPolicy.ShouldForward(h.Key)
                     && !string.IsNullOrWhiteSpace(h.Value)
                     && !h.Value.Contains('\r') && !h.Value.Contains('\n'))
            .GroupBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CapturedHeader(g.First().Key, g.Last().Value))
            .ToArray();

        // Preserve the original raw signed query; Uri.AbsoluteUri can normalize escape bytes.
        return new CapturedRequest(url!, headers, kind);
    }

    private static bool IsPrivateAddress(string host)
    {
        if (!IPAddress.TryParse(host, out var ip)) return false;
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 0 ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                   (bytes[0] == 169 && bytes[1] == 254);
        }
        var v6 = ip.GetAddressBytes();
        return (v6[0] & 0xfe) == 0xfc ||
               (v6[0] == 0xfe && (v6[1] & 0xc0) == 0x80);
    }

    public static CapturedStreamKind Classify(string path, string mimeType)
    {
        var p = path.TrimEnd('/').ToLowerInvariant();
        var mime = mimeType.ToLowerInvariant();
        if (p.EndsWith(".ts") || p.EndsWith(".m4s") || p.EndsWith(".aac") ||
            p.EndsWith(".vtt") || p.EndsWith(".key") || p.EndsWith("/init.mp4"))
            return CapturedStreamKind.Unknown;
        if (p.EndsWith(".m3u8") || p.EndsWith(".m3u") ||
            mime.Contains("mpegurl")) return CapturedStreamKind.Hls;
        if (p.EndsWith(".mpd") || mime.Contains("dash+xml"))
            return CapturedStreamKind.Dash;
        if (p.EndsWith(".ism/manifest") || p.EndsWith(".isml/manifest") ||
            mime.Contains("ms-sstr"))
            return CapturedStreamKind.Mss; // N_m3u8DL-RE automatically detects MSS.
        if (p.EndsWith(".mp4") || p.EndsWith(".webm") ||
            p.EndsWith(".mkv") || p.EndsWith(".mov") ||
            mime.StartsWith("video/"))
            return CapturedStreamKind.Media;
        return CapturedStreamKind.Unknown;
    }

    // HTTP 404/410 is not a downloadable playlist: never promote a request
    // merely because its URL ends in .m3u8. 403 is a separate auth problem.
    public static bool IsPermanentMissingStatus(int statusCode) => statusCode is 404 or 410;

    public static bool IsSuccessfulMediaStatus(int statusCode) => statusCode is 200 or 206;

    public static int Score(CapturedRequest item)
    {
        var uri = new Uri(item.Url);
        var score = item.Kind switch
        {
            CapturedStreamKind.Hls => 100,
            CapturedStreamKind.Dash => 95,
            CapturedStreamKind.Mss => 95,
            CapturedStreamKind.Page => 120,
            CapturedStreamKind.Media => 45,
            _ => 0
        };
        var path = uri.AbsolutePath.ToLowerInvariant();
        if (path.EndsWith("/master.m3u8") || path.EndsWith("/master.mpd")) score += 40;
        if (path.Contains("playlist") || path.Contains("manifest")) score += 8;
        if (path.Contains("/ads/") || path.Contains("/ad/") ||
            uri.Host.Contains("doubleclick") || uri.Host.Contains("googleads"))
            score -= 100;
        return score;
    }

    public static bool IsHighConfidenceMaster(CapturedRequest request) =>
        Score(request) >= 130;

    public static CapturedRequest? Best(IEnumerable<CapturedRequest> requests) =>
        requests.OrderByDescending(Score).FirstOrDefault();
}
