#nullable enable
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace N_m3u8DL_RE_GUI.Core;

/// <summary>Structured, best-effort telemetry from downloader stdout. Unknown stays unknown.</summary>
public static class DownloadTelemetryParser
{
    private static readonly Regex TotalBytes = new(
        @"^\[size\]\s+(?<bytes>\d+)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EpornerTotal = new(
        @"^\[media\].*?,\s*(?<value>\d+(?:\.\d+)?)\s*(?<unit>[KMGT]?i?B)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex YtTotal = new(
        @"\bof\s+~?(?<value>\d+(?:\.\d+)?)\s*(?<unit>[KMGT]?i?B)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Progress = new(
        @"\[download\]\s+(?<pct>\d{1,3}(?:\.\d+)?)%",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Speed = new(
        @"\bat\s+(?<value>\d+(?:\.\d+)?)\s*(?<unit>[KMGT]?i?B)/s",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Eta = new(
        @"\bETA\s+(?<clock>\d{1,3}:\d{2}(?::\d{2})?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static DownloadTelemetrySample Parse(string? rawLine)
    {
        var line = ConsoleOutputParser.Clean(rawLine);
        long? total = null;
        var exact = TotalBytes.Match(line);
        if (exact.Success && long.TryParse(exact.Groups["bytes"].Value, out var value))
            total = value;
        else
        {
            var match = YtTotal.Match(line);
            if (!match.Success) match = EpornerTotal.Match(line);
            if (match.Success) total = ToBytes(match);
        }

        double? pct = null;
        var progress = Progress.Match(line);
        if (progress.Success &&
            double.TryParse(progress.Groups["pct"].Value, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var p) && p >= 0 && p <= 100)
            pct = p;

        double? speed = null;
        var rate = Speed.Match(line);
        if (rate.Success) speed = ToBytes(rate);

        TimeSpan? eta = null;
        var mEta = Eta.Match(line);
        if (mEta.Success)
        {
            var parts = mEta.Groups["clock"].Value.Split(':');
            if (parts.Length >= 2 && int.TryParse(parts[^1], out var sec) &&
                int.TryParse(parts[^2], out var min))
            {
                var hour = parts.Length == 3 && int.TryParse(parts[0], out var h) ? h : 0;
                eta = new TimeSpan(hour, min, sec);
            }
        }
        return new DownloadTelemetrySample(pct, total, speed, eta);
    }

    private static long? ToBytes(Match match)
    {
        if (!double.TryParse(match.Groups["value"].Value, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var value)) return null;
        var unit = match.Groups["unit"].Value.ToUpperInvariant();
        var scale = unit switch
        {
            "B" => 1d,
            "KB" => 1e3, "MB" => 1e6, "GB" => 1e9, "TB" => 1e12,
            "KIB" => 1024d, "MIB" => 1048576d,
            "GIB" => 1073741824d, "TIB" => 1099511627776d,
            _ => 0d
        };
        var bytes = value * scale;
        return double.IsFinite(bytes) && bytes >= 0 && bytes <= long.MaxValue
            ? (long)Math.Round(bytes) : null;
    }
}
public readonly record struct DownloadTelemetrySample(
    double? Percent, long? TotalBytes, double? BytesPerSecond, TimeSpan? Eta);
