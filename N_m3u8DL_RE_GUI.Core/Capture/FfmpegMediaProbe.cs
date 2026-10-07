#nullable enable
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace N_m3u8DL_RE_GUI.Core.Capture;

public sealed record FfmpegMediaMetadata(
    bool Success,
    double? Duration,
    int? Width,
    int? Height,
    double? Fps,
    long? Bitrate,
    string Format,
    string VideoCodec,
    string AudioCodec,
    string Note);

public static class FfmpegMediaProbe
{
    private static readonly HashSet<string> SafeHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Referer", "Origin", "User-Agent", "Accept", "Accept-Language"
    };

    public static async Task<FfmpegMediaMetadata> ProbeAsync(
        string ffmpegPath,
        string url,
        IEnumerable<KeyValuePair<string, string>>? headers,
        CancellationToken token,
        TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
            return Empty("FFmpeg probe unavailable.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            return Empty("Unsupported media URL.");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
        linked.CancelAfter(timeout ?? TimeSpan.FromSeconds(14));

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in new[]
        {
            "-hide_banner", "-nostdin", "-loglevel", "info",
            "-probesize", "8388608", "-analyzeduration", "8000000"
        })
            psi.ArgumentList.Add(arg);

        var forwarded = (headers ?? Array.Empty<KeyValuePair<string, string>>())
            .Where(h => SafeHeaders.Contains(h.Key) &&
                        !string.IsNullOrWhiteSpace(h.Value) &&
                        h.Value.IndexOfAny(['\r','\n','\0']) < 0)
            .Select(h => h.Key.Trim() + ": " + h.Value.Trim())
            .ToArray();
        if (forwarded.Length > 0)
        {
            psi.ArgumentList.Add("-headers");
            psi.ArgumentList.Add(string.Join("\r\n", forwarded) + "\r\n");
        }

        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(url);

        using var process = new Process { StartInfo = psi };
        try
        {
            if (!process.Start()) return Empty("FFmpeg probe did not start.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync(linked.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linked.Token);
            try
            {
                await process.WaitForExitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                if (token.IsCancellationRequested) throw;
                return Empty("FFmpeg metadata probe timed out.");
            }

            _ = await stdoutTask;
            var stderr = await stderrTask;
            if (stderr.Length > 2_000_000) stderr = stderr[..2_000_000];

            return ParseProbeOutput(stderr, uri.AbsolutePath);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return Empty("FFmpeg metadata probe failed.");
        }
        finally
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }
            catch { }
        }
    }

    private static FfmpegMediaMetadata Empty(string note) =>
        new(false, null, null, null, null, null, string.Empty, string.Empty, string.Empty, note);

    public static FfmpegMediaMetadata ParseProbeOutput(string text,string path)
    {
        text ??= string.Empty;
        path ??= string.Empty;
        var duration = ParseDuration(text);
        var (width, height, fps, videoCodec) = ParseVideo(text);
        var audioCodec = ParseAudio(text);
        var bitrate = ParseBitrate(text);
        var format = ParseFormat(text);
        var success = duration is > 0 || height is > 0 ||
                      !string.IsNullOrWhiteSpace(videoCodec) ||
                      !string.IsNullOrWhiteSpace(format);
        return new FfmpegMediaMetadata(
            success,duration,width,height,fps,bitrate,
            NormalizeFormat(format,path),
            NormalizeCodec(videoCodec),NormalizeCodec(audioCodec),
            success
                ? "FFmpeg input probe confirmed container/stream metadata without writing media output."
                : "FFmpeg could not expose container metadata for this source.");
    }

    private static double? ParseDuration(string text)
    {
        var m = Regex.Match(text,
            @"Duration:\s*(\d{1,3}):(\d{2}):(\d{2}(?:\.\d+)?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success) return null;
        if (!double.TryParse(m.Groups[3].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var sec)) return null;
        var total = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 3600d +
                    int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) * 60d + sec;
        return total is > 0 and < 31_536_000 ? total : null;
    }

    private static (int? Width, int? Height, double? Fps, string Codec) ParseVideo(string text)
    {
        var line = Regex.Match(text, @"(?m)^\s*Stream #.*?Video:\s*([^,\s]+)([^\r\n]*)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!line.Success) return (null, null, null, string.Empty);

        var codec = line.Groups[1].Value;
        var tail = line.Groups[2].Value;
        var size = Regex.Match(tail, @"(?<!\d)(\d{2,5})x(\d{2,5})(?!\d)");
        int? width = size.Success ? int.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture) : null;
        int? height = size.Success ? int.Parse(size.Groups[2].Value, CultureInfo.InvariantCulture) : null;

        var fpsMatch = Regex.Match(tail, @"(?<!\d)(\d+(?:\.\d+)?)\s*fps\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        double? fps = null;
        if (fpsMatch.Success &&
            double.TryParse(fpsMatch.Groups[1].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var parsed) &&
            parsed is > 0 and <= 1000)
            fps = parsed;

        return (width, height, fps, codec);
    }

    private static string ParseAudio(string text)
    {
        var m = Regex.Match(text, @"(?m)^\s*Stream #.*?Audio:\s*([^,\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    private static long? ParseBitrate(string text)
    {
        var m = Regex.Match(text, @"bitrate:\s*(\d+)\s*kb/s",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return m.Success && long.TryParse(m.Groups[1].Value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var kbps) && kbps > 0
            ? kbps * 1000L : null;
    }

    private static string ParseFormat(string text)
    {
        var m = Regex.Match(text, @"(?m)^Input #0,\s*(.+?),\s*from\s+['""]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
    }

    private static string NormalizeFormat(string raw, string path)
    {
        var value = raw.ToLowerInvariant();
        if (value.Contains("hls")) return "HLS / M3U8";
        if (value.Contains("dash")) return "MPEG-DASH / MPD";
        if (value.Contains("smoothstreaming") || value.Contains("ism")) return "MSS / ISM";
        if (value.Contains("matroska") || value.Contains("webm")) return "WebM / Matroska";
        if (value.Contains("mov") || value.Contains("mp4") || value.Contains("m4a") || value.Contains("3gp"))
            return "MP4 / MOV";
        if (value.Contains("mpegts")) return "MPEG-TS";
        if (!string.IsNullOrWhiteSpace(raw)) return raw;

        var ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        return string.IsNullOrWhiteSpace(ext) ? "Media stream" : ext;
    }

    private static string NormalizeCodec(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return raw.ToLowerInvariant() switch
        {
            "h264" => "H.264",
            "hevc" or "h265" => "H.265 / HEVC",
            "av1" => "AV1",
            "vp9" => "VP9",
            "vp8" => "VP8",
            "aac" => "AAC",
            "opus" => "Opus",
            "mp3" => "MP3",
            "vorbis" => "Vorbis",
            _ => raw
        };
    }
}
