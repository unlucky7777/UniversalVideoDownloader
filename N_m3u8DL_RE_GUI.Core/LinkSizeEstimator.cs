#nullable enable
using System;
using System.Globalization;
using System.Text.Json;

namespace N_m3u8DL_RE_GUI.Core;

/// <summary>
/// Read download-size estimates from yt-dlp's metadata-only JSON, without saving media.
/// Selected requested_formats are preferred over the entire site's format inventory.
/// </summary>
public static class LinkSizeEstimator
{
    public readonly record struct Result(long? Bytes, bool Approximate);

    public static Result FromYtDlpJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return default;

        // yt-dlp normally places format-selection results here when -f is supplied.
        if (root.TryGetProperty("requested_formats", out var formats) &&
            formats.ValueKind == JsonValueKind.Array && formats.GetArrayLength() > 0)
        {
            long sum = 0;
            bool approximate = false;
            foreach (var item in formats.EnumerateArray())
            {
                var size = ReadSize(item);
                if (size.Bytes is not > 0) return FromRoot(root);
                if (sum > long.MaxValue - size.Bytes.Value) return default;
                sum += size.Bytes.Value;
                approximate |= size.Approximate;
            }
            return new Result(sum, approximate);
        }
        return FromRoot(root);
    }

    private static Result FromRoot(JsonElement root)
    {
        var size = ReadSize(root);
        if (size.Bytes is > 0) return size;

        // Estimated compressed size = duration × total bitrate. Explicitly approximate.
        double duration = Number(root, "duration");
        double bitrate = Number(root, "tbr");
        if (duration > 0 && bitrate > 0)
        {
            var estimate = duration * bitrate * 1000d / 8d;
            if (double.IsFinite(estimate) && estimate <= long.MaxValue)
                return new Result((long)estimate, true);
        }
        return default;
    }

    private static Result ReadSize(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return default;
        var exact = Number(value, "filesize");
        if (exact > 0 && exact <= long.MaxValue) return new Result((long)exact, false);
        var approx = Number(value, "filesize_approx");
        if (approx > 0 && approx <= long.MaxValue) return new Result((long)approx, true);
        return default;
    }

    private static double Number(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value)) return 0;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var number) &&
                                      double.IsFinite(number) => number,
            _ => 0
        };
    }
}
