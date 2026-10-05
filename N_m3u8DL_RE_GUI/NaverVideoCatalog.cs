#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace N_m3u8DL_RE_GUI;

/// <summary>Parses Naver's signed playback metadata without fabricating missing qualities.</summary>
public static class NaverVideoCatalog
{
    public sealed record Variant(string Url, int Height, long? Bytes);

    public static IReadOnlyList<Variant> Parse(string json)
    {
        if (string.IsNullOrEmpty(json) || json.Length > 1048576)
            return Array.Empty<Variant>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("videos", out var videos) ||
                !videos.TryGetProperty("list", out var list) ||
                list.ValueKind != JsonValueKind.Array)
                return Array.Empty<Variant>();
            var result = new List<Variant>();
            foreach (var item in list.EnumerateArray().Take(32))
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("source", out var source) ||
                    source.ValueKind != JsonValueKind.String)
                    continue;
                var url = WebUtility.HtmlDecode(source.GetString() ?? "").Replace("∈", "&");
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    uri.Scheme != Uri.UriSchemeHttps ||
                    !string.IsNullOrEmpty(uri.UserInfo) ||
                    uri.AbsoluteUri.Length > 8192 ||
                    !(uri.Host.Equals("pstatic.net", StringComparison.OrdinalIgnoreCase) ||
                      uri.Host.EndsWith(".pstatic.net", StringComparison.OrdinalIgnoreCase)) ||
                    !uri.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                    continue;
                int height = 0;
                if (item.TryGetProperty("encodingOption", out var opt) &&
                    opt.ValueKind == JsonValueKind.Object)
                {
                    if (opt.TryGetProperty("height", out var h))
                        height = GetNumber(h) is long n && n is >= 144 and <= 4320 ? (int)n : 0;
                    if (height == 0 && opt.TryGetProperty("name", out var name) &&
                        name.ValueKind == JsonValueKind.String)
                    {
                        var m = Regex.Match(name.GetString() ?? "", @"(?<!\d)(2160|1440|1080|720|540|480|360|240)p?\b",
                            RegexOptions.IgnoreCase);
                        if (m.Success) height = int.Parse(m.Groups[1].Value);
                    }
                }
                long? bytes = item.TryGetProperty("size", out var sz) ? GetNumber(sz) : null;
                if (bytes is <= 0 or > 34359738368L) bytes = null;
                if (result.All(r => r.Url != url))
                    result.Add(new Variant(url, height, bytes));
            }
            return result.OrderByDescending(r => r.Height).ThenByDescending(r => r.Bytes ?? 0).ToArray();
        }
        catch (JsonException) { return Array.Empty<Variant>(); }
    }

    private static long? GetNumber(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var value)) return value;
        if (element.ValueKind == JsonValueKind.String && long.TryParse(element.GetString(), out var text)) return text;
        return null;
    }
}
