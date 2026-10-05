#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace N_m3u8DL_RE_GUI.Core;

public enum UniversalEngine { Stream, Website }

/// <summary>Route page URLs to yt-dlp; keep HLS/DASH/MSS URLs on N_m3u8DL-RE.</summary>
public static class UniversalDownloadRouter
{
    /// <summary>Direct main-video MP4 from the site's player quality table.
    /// Unlike a page URL, this contains the specifically chosen rendition.</summary>
    public static bool IsPornvovDirectMp4(string? input)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) ||
            !(uri.Host.Equals("pornvov.net", StringComparison.OrdinalIgnoreCase) ||
              uri.Host.EndsWith(".pornvov.net", StringComparison.OrdinalIgnoreCase)))
            return false;
        return System.Text.RegularExpressions.Regex.IsMatch(uri.AbsolutePath,
            @"^/(?:[^?#]*/)?[0-9]{1,15}-(?:240|360|480|720|1080|1440|2160)\.mp4$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public static UniversalEngine Select(string? input, int selection = 0)
    {
        if (selection == 1) return UniversalEngine.Stream;
        if (selection == 2) return UniversalEngine.Website;
        if (!Uri.TryCreate(input?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return UniversalEngine.Stream;

        var path = uri.AbsolutePath;
        if (path.Contains(".ism/Manifest", StringComparison.OrdinalIgnoreCase))
            return UniversalEngine.Stream;
        foreach (var suffix in new[] { ".m3u8", ".mpd", ".ism", ".ismc" })
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return UniversalEngine.Stream;
        return UniversalEngine.Website;
    }
}

/// <summary>
/// Build safe, tokenized yt-dlp arguments. Never concatenate an untrusted URL into a shell command.
/// Native stream-specific headers are deliberately not inherited by website mode.
/// </summary>
public static class WebsiteArgsBuilder
{
    public static IReadOnlyList<string> Build(
        string url, string? saveDir, string? saveName, int fragmentThreads = 128,
        int maximumHeight = 2160)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host))
            throw new ArgumentException("An absolute HTTP(S) page URL is required.", nameof(url));

        // The review UI is metadata-driven, so real sites may advertise non-standard
        // heights such as 2026p or 4050p. Accept any sane positive metadata height.
        if (maximumHeight < 0 || maximumHeight > 8640)
            throw new ArgumentOutOfRangeException(nameof(maximumHeight));

        var format = maximumHeight == 0
            ? "bv*+ba/b"
            : $"bv*[height<={maximumHeight}]+ba/b[height<={maximumHeight}]";

        var args = new List<string>
        {
            "--ignore-config", "--no-playlist", "--newline",
            "-N", Math.Clamp(fragmentThreads, 1, 256).ToString(),
            "-f", format,
            "--merge-output-format", "mp4"
        };
        if (!string.IsNullOrWhiteSpace(saveDir))
        {
            args.Add("-P");
            args.Add(saveDir.Trim());
        }

        // Display title can contain shell metacharacters; the process receives separate args.
        // Escape yt-dlp output-template percent expansion for a user-provided literal name.
        var output = string.IsNullOrWhiteSpace(saveName)
            ? "%(title).180B [%(id)s].%(ext)s"
            : SafeFileName(saveName) + ".%(ext)s";
        args.Add("-o");
        args.Add(output);
        // Preserve the exact signed URL; Uri.AbsoluteUri may normalize escaped token bytes.
        args.Add(url.Trim());
        return args;
    }

    private static string SafeFileName(string value)
    {
        var name = value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        foreach (var invalid in "<>:\"/\\|?*")
            name = name.Replace(invalid, '_');
        name = name.TrimEnd(' ', '.').Replace("%", "%%");
        return string.IsNullOrWhiteSpace(name) ? "video" : name;
    }
}
