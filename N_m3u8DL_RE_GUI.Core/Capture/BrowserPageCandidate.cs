#nullable enable
using System;
using System.Collections.Generic;

namespace N_m3u8DL_RE_GUI.Core.Capture;

/// <summary>
/// Creates an explicit page-level candidate for sites where direct media network
/// requests are fragmented, short-lived, or split into separate audio/video tracks.
/// </summary>
public static class BrowserPageCandidate
{
    public static bool IsYouTubeVideoPage(string? input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return false;

        var host = uri.Host.TrimEnd('.').ToLowerInvariant();
        var path = uri.AbsolutePath.TrimEnd('/');
        var youtube = host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or
            "music.youtube.com" or "youtube-nocookie.com" or "www.youtube-nocookie.com";
        if (host == "youtu.be")
            return path.Length > 1;

        if (!youtube) return false;
        if (path.Equals("/watch", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var part in uri.Query.TrimStart('?')
                         .Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length == 2 &&
                    Uri.UnescapeDataString(pair[0]).Equals("v", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(Uri.UnescapeDataString(pair[1])))
                    return true;
            }
            return false;
        }

        return path.StartsWith("/shorts/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/live/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase);
    }

    public static CapturedRequest? Create(string? input)
    {
        if (!IsYouTubeVideoPage(input)) return null;
        var directives = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["engine"] = "website",
            ["source"] = "page"
        };
        return new CapturedRequest(input!, Array.Empty<CapturedHeader>(),
            CapturedStreamKind.Page, directives);
    }
}
