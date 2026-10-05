#nullable enable
using System;
using System.Text.RegularExpressions;

namespace N_m3u8DL_RE_GUI;

/// <summary>
/// Site fallback routing after a browser-captured CDN URL has already expired.
/// Keep the original authenticated Chrome session when yt-dlp cannot extract
/// the site and a fresh embedded WebView2 profile gets HTTP 403 at its iframe.
/// </summary>
public static class BrowserSessionRecoveryPolicy
{
    public static bool IsDefinitivelyMissingStatus(string? statusOutput) =>
        statusOutput?.Trim() is "404" or "410";

    public static bool RequiresChromeRescan(string? pageUrl)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http") || uri.UserInfo.Length != 0)
            return false;
        return Regex.IsMatch(uri.Host, @"^(?:www\.)?yako\d+\.com$",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
               Regex.IsMatch(uri.AbsolutePath, @"^/korea/\d+/?$",
                   RegexOptions.CultureInvariant);
    }
}
