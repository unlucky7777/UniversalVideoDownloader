using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

internal sealed record NativeSiteMetadataAdapter(
    string Id,
    string HelperFile,
    Func<Uri, bool> Match,
    string Container,
    string Note);

/// <summary>
/// Site-specific metadata exceptions live here. Helpers must expose the common
/// CLI contract:
///   helper <page-url> <work-dir> <max-height> <threads> --probe
/// and emit one [probe-json] line with title, duration, bytes, selectedHeight,
/// heights[] and optional ext. Generic pages never enter this registry.
/// </summary>
internal static class SiteMetadataAdapters
{
    private static readonly NativeSiteMetadataAdapter[] Adapters =
    {
        new(
            "eporner",
            "eporner-downloader.cjs",
            uri =>
                (uri.Host.Equals("eporner.com", StringComparison.OrdinalIgnoreCase) ||
                 uri.Host.Equals("www.eporner.com", StringComparison.OrdinalIgnoreCase)) &&
                Regex.IsMatch(uri.AbsolutePath,
                    @"^/(?:video-[A-Za-z0-9]+|embed/[A-Za-z0-9]+)(?:/|$)",
                    RegexOptions.CultureInvariant),
            "MP4 direct file",
            "Listed heights are actual renditions returned by the site adapter.")
    };

    internal static bool TryProbe(string url, string folder, out object result)
    {
        result = null!;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            return false;

        var adapter = Adapters.FirstOrDefault(x => x.Match(uri));
        if (adapter is null)
            return false;

        result = Probe(adapter, url, folder);
        return true;
    }

    private static string NodeExecutable(string folder)
    {
        var bundled = Path.Combine(folder, "node.exe");
        if (File.Exists(bundled)) return bundled;
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var installed = Path.Combine(programFiles, "nodejs", "node.exe");
        return File.Exists(installed) ? installed : "node.exe";
    }

    private static object Probe(NativeSiteMetadataAdapter adapter, string url, string folder)
    {
        var script = Path.Combine(folder, adapter.HelperFile);
        if (!File.Exists(script))
            return new { ok = false, error = "Site-adapter metadata helper unavailable." };

        var info = new ProcessStartInfo(NodeExecutable(folder))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = folder
        };
        foreach (var arg in new[] { script, url, folder, "0", "1", "--probe" })
            info.ArgumentList.Add(arg);

        using var proc = new Process { StartInfo = info };
        try
        {
            if (!proc.Start())
                throw new InvalidOperationException("Site-adapter metadata process did not start.");

            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(22));
            var output = proc.StandardOutput.ReadToEndAsync(cancel.Token);
            var errors = proc.StandardError.ReadToEndAsync(cancel.Token);
            try { proc.WaitForExitAsync(cancel.Token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return new { ok = false, error = "Site-adapter metadata request timed out." };
            }

            var raw = output.GetAwaiter().GetResult();
            _ = errors.GetAwaiter().GetResult();
            if (proc.ExitCode != 0 || raw.Length < 2 || raw.Length > 1_000_000)
                return new { ok = false, error = "The site adapter did not provide accessible metadata." };

            var match = Regex.Match(raw, @"(?m)^\[probe-json\]\s+(.+)$",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                return new { ok = false, error = "Site-adapter metadata response was invalid." };

            using var doc = JsonDocument.Parse(match.Groups[1].Value.Trim());
            var root = doc.RootElement;
            var title = ReadString(root, "title", 180);
            var duration = ReadDouble(root, "duration");
            var bytes = ReadLong(root, "bytes");
            var selectedHeight = root.TryGetProperty("selectedHeight", out var selectedNode) &&
                                 selectedNode.TryGetInt32(out var sh) ? sh : 0;
            var extension = ReadString(root, "ext", 12) ?? "mp4";

            var heights = root.TryGetProperty("heights", out var heightNode) &&
                          heightNode.ValueKind == JsonValueKind.Array
                ? heightNode.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.Number &&
                                x.TryGetInt32(out var h) && h > 0 && h <= 8640)
                    .Select(x => x.GetInt32())
                    .Distinct()
                    .OrderByDescending(x => x)
                    .Take(20)
                    .ToArray()
                : Array.Empty<int>();

            var formats = heights.Select(h => new
            {
                height = h,
                fps = (int?)null,
                ext = extension,
                videoCodec = "site-adapter",
                sizeBytes = h == selectedHeight ? bytes : null,
                estimatedBytes = (long?)null,
                sizeIsExact = h == selectedHeight && bytes.HasValue
            }).ToArray();

            return new
            {
                ok = true,
                title,
                duration,
                formats,
                container = adapter.Container,
                audioAvailable = true,
                note = adapter.Note
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            return new
            {
                ok = false,
                error = "Site-adapter metadata is unavailable (" + ex.GetType().Name + ")."
            };
        }
    }

    private static string? ReadString(JsonElement node, string key, int max)
    {
        if (!node.TryGetProperty(key, out var value) ||
            value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString() ?? string.Empty;
        return text[..Math.Min(max, text.Length)];
    }

    private static long? ReadLong(JsonElement node, string key)
    {
        if (!node.TryGetProperty(key, out var value) ||
            value.ValueKind != JsonValueKind.Number)
            return null;
        return value.TryGetInt64(out var n) && n > 0 && n < 100_000_000_000 ? n : null;
    }

    private static double? ReadDouble(JsonElement node, string key)
    {
        if (!node.TryGetProperty(key, out var value) ||
            value.ValueKind != JsonValueKind.Number)
            return null;
        var n = value.GetDouble();
        return double.IsFinite(n) && n > 0 && n < 100_000_000 ? n : null;
    }
}
