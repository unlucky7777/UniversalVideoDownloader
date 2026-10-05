#nullable enable
using System;
using System.Text.RegularExpressions;

namespace N_m3u8DL_RE_GUI.Core;

public sealed record SiteAdapterDescriptor(
    string Id,
    string MetadataHelper,
    string DownloadHelper,
    string DownloadMethod,
    string OutputFormat,
    string LoadingStageEn,
    string LoadingStageKo,
    string FailureStatusEn,
    string LogLabel);

/// <summary>
/// Central registry for site-specific exceptions. Generic HLS/DASH/direct-media
/// and generic website extraction must be attempted outside this registry.
/// Add a site here only when it requires a dedicated metadata/download contract.
/// </summary>
public static class SiteAdapterRegistry
{
    private static readonly SiteAdapterDescriptor Eporner = new(
        "eporner",
        "eporner-downloader.cjs",
        "eporner-downloader.cjs",
        "Site adapter HTTPS Range",
        "MP4 direct file",
        "Connecting to site video server",
        "사이트 영상 서버 연결 중",
        "Site-adapter download failed. Check Log for details.",
        "Site adapter: verified HTTPS ranged transfer");

    public static SiteAdapterDescriptor? ResolvePage(string? input)
    {
        if (!Uri.TryCreate(input?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            return null;

        if (IsEporner(uri))
            return Eporner;

        return null;
    }

    private static bool IsEporner(Uri uri)
    {
        if (!(uri.Host.Equals("eporner.com", StringComparison.OrdinalIgnoreCase) ||
              uri.Host.Equals("www.eporner.com", StringComparison.OrdinalIgnoreCase)))
            return false;

        return Regex.IsMatch(uri.AbsolutePath,
            @"^/(?:video-[A-Za-z0-9]+|embed/[A-Za-z0-9]+)(?:/|$)",
            RegexOptions.CultureInvariant);
    }
}
