#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace N_m3u8DL_RE_GUI;

internal sealed record YtDlpFormatMetadata(
    int Height,
    int? Width,
    int? Fps,
    string Extension,
    string VideoCodec,
    long? ExactBytes,
    long? EstimatedBytes);

internal sealed record YtDlpPageMetadata(
    bool Success,
    string? Title,
    double? Duration,
    IReadOnlyList<YtDlpFormatMetadata> Formats,
    string Container,
    bool AudioAvailable,
    string Note,
    string? Error = null)
{
    internal YtDlpFormatMetadata? SelectForMaximumHeight(int maximumHeight)
    {
        if (Formats.Count == 0) return null;
        if (maximumHeight <= 0) return Formats.OrderByDescending(x => x.Height).FirstOrDefault();
        return Formats.Where(x => x.Height <= maximumHeight)
            .OrderByDescending(x => x.Height).FirstOrDefault();
    }

    internal IReadOnlyList<int> Heights => Formats
        .Select(x => x.Height).Where(x => x > 0).Distinct()
        .OrderByDescending(x => x).ToArray();
}

internal static class YtDlpMetadataProbe
{
    internal static string FindBundledTool(string file)
    {
        var nested = Path.Combine(AppContext.BaseDirectory, "Runtime", file);
        return File.Exists(nested) ? nested : Path.Combine(AppContext.BaseDirectory, file);
    }

    internal static async Task<YtDlpPageMetadata> ProbeAsync(
        string url, CancellationToken token)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(uri.UserInfo))
            return Failed("지원되는 HTTP(S) 주소가 아닙니다.");

        var tool = FindBundledTool("yt-dlp.exe");
        if (!File.Exists(tool))
            return Failed("번들 yt-dlp를 찾을 수 없습니다.");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = tool,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            }
        };
        foreach (var arg in new[]
        {
            "--ignore-config", "--no-playlist", "--dump-single-json", "--skip-download",
            "--no-warnings", "--socket-timeout", "10", "--retries", "1",
            "--extractor-retries", "1", "--", url
        })
            process.StartInfo.ArgumentList.Add(arg);

        var started = false;
        try
        {
            started = process.Start();
            if (!started) return Failed("yt-dlp를 시작하지 못했습니다.");

            var outputTask = process.StandardOutput.ReadToEndAsync(token);
            var errorTask = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);
            var raw = await outputTask;
            _ = await errorTask;

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(raw))
                return Failed("사이트에서 메타데이터를 가져오지 못했습니다.");
            if (raw.Length > 8_000_000)
                return Failed("메타데이터 응답이 너무 큽니다.");

            return Parse(raw);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
        {
            return Failed("메타데이터 조회 실패 (" + ex.GetType().Name + ").");
        }
        finally
        {
            if (started && !process.HasExited)
                try { process.Kill(entireProcessTree: true); } catch { }
        }
    }

    internal static YtDlpPageMetadata Parse(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var title = ReadString(root, "title", 220);
        var duration = ReadNumber(root, "duration");
        var rows = new List<(int Height, int? Width, int? Fps, string Ext,
            string Codec, long? Exact, long? Estimate, double Rate)>();

        long? audioExact = null;
        long? audioEstimate = null;
        double audioScore = -1;

        if (root.TryGetProperty("formats", out var formats) &&
            formats.ValueKind == JsonValueKind.Array)
        {
            foreach (var format in formats.EnumerateArray())
            {
                if (format.ValueKind != JsonValueKind.Object) continue;
                var vcodec = ReadString(format, "vcodec", 80) ?? "none";
                var acodec = ReadString(format, "acodec", 80) ?? "none";
                var exact = ReadLong(format, "filesize");
                var estimate = ReadLong(format, "filesize_approx");
                var tbr = ReadNumber(format, "tbr") ?? 0;

                if (!vcodec.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    var height = ReadLong(format, "height");
                    if (height is not > 0 or > 8640) continue;
                    var width = ReadLong(format, "width");
                    var fps = ReadNumber(format, "fps");
                    rows.Add((
                        (int)height.Value,
                        width is > 0 and <= 16384 ? (int?)width.Value : null,
                        fps is > 0 and <= 240 ? (int?)Math.Round(fps.Value) : null,
                        ReadString(format, "ext", 20) ?? "unknown",
                        vcodec,
                        exact,
                        estimate,
                        tbr));
                }
                else if (!acodec.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    var score = ReadNumber(format, "abr") ?? tbr;
                    if (score >= audioScore)
                    {
                        audioScore = score;
                        audioExact = exact;
                        audioEstimate = estimate ??
                            (duration.HasValue && score > 0
                                ? (long?)(duration.Value * score * 1000d / 8d)
                                : null);
                    }
                }
            }
        }

        var best = rows.GroupBy(x => x.Height)
            .Select(g => g.OrderByDescending(x => x.Exact.HasValue)
                .ThenByDescending(x => x.Fps ?? 0)
                .ThenByDescending(x => x.Rate)
                .First())
            .OrderByDescending(x => x.Height)
            .Take(30)
            .Select(x =>
            {
                var videoEstimate = x.Exact ?? x.Estimate ??
                    (duration.HasValue && x.Rate > 0
                        ? (long?)(duration.Value * x.Rate * 1000d / 8d)
                        : null);
                var combinedExact = x.Exact.HasValue && audioExact.HasValue
                    ? x.Exact + audioExact : null;
                var combinedEstimate = combinedExact.HasValue ? null :
                    videoEstimate.HasValue
                        ? videoEstimate + (audioExact ?? audioEstimate ?? 0)
                        : null;
                return new YtDlpFormatMetadata(
                    x.Height, x.Width, x.Fps, x.Ext, x.Codec,
                    combinedExact, combinedEstimate);
            })
            .ToArray();

        var container = ReadString(root, "ext", 24) ?? "사이트 원본";
        var note = best.Length == 0
            ? "페이지 정보는 확인했지만 개별 영상 화질 목록은 제공되지 않았습니다."
            : "사이트가 제공한 실제 화질 목록입니다. 영상과 음성 트랙은 다운로드 시 자동 병합됩니다.";

        return new YtDlpPageMetadata(
            true, title, duration, best, container,
            audioScore >= 0, note);
    }

    private static YtDlpPageMetadata Failed(string error) =>
        new(false, null, null, Array.Empty<YtDlpFormatMetadata>(),
            "확인 불가", false, error, error);

    private static string? ReadString(JsonElement node, string key, int max)
    {
        if (!node.TryGetProperty(key, out var value) ||
            value.ValueKind != JsonValueKind.String) return null;
        var text = (value.GetString() ?? string.Empty)
            .Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length == 0 ? null : text[..Math.Min(max, text.Length)];
    }

    private static long? ReadLong(JsonElement node, string key)
    {
        if (!node.TryGetProperty(key, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out var number) ||
            number <= 0 || number > 500_000_000_000L) return null;
        return number;
    }

    private static double? ReadNumber(JsonElement node, string key)
    {
        if (!node.TryGetProperty(key, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out var number) ||
            !double.IsFinite(number) || number <= 0 || number > 1_000_000_000d)
            return null;
        return number;
    }
}
