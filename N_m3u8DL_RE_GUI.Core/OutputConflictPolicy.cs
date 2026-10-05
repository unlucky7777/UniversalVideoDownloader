#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace N_m3u8DL_RE_GUI.Core;

public static class OutputConflictPolicy
{
    private static readonly HashSet<string> MediaExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".webm", ".mov", ".m4v",
            ".ts", ".m2ts", ".flv", ".avi"
        };

    public static string NormalizeBaseName(string? value)
    {
        var name = (value ?? string.Empty).Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        var extension = Path.GetExtension(name);
        if (MediaExtensions.Contains(extension))
            name = Path.GetFileNameWithoutExtension(name);

        name = name.Trim().TrimEnd('.', ' ');
        return name.Length > 180 ? name[..180].TrimEnd() : name;
    }

    public static IReadOnlyList<string> FindConflicts(string saveDir, string? desiredName)
    {
        var baseName = NormalizeBaseName(desiredName);
        if (string.IsNullOrWhiteSpace(saveDir) || string.IsNullOrWhiteSpace(baseName) ||
            !Directory.Exists(saveDir))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(saveDir, "*", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                var fileName = Path.GetFileName(path);
                if (fileName.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".resume.json", StringComparison.OrdinalIgnoreCase))
                    return false;

                var stem = Path.GetFileNameWithoutExtension(fileName);
                return string.Equals(stem, baseName, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(fileName, baseName, StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();
    }

    public static string FindUniqueBaseName(string saveDir, string? desiredName)
    {
        var baseName = NormalizeBaseName(desiredName);
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "video";

        for (var suffix = 2; suffix < 10000; suffix++)
        {
            var candidate = $"{baseName} ({suffix})";
            if (FindConflicts(saveDir, candidate).Count == 0)
                return candidate;
        }

        return $"{baseName} ({DateTime.Now:yyyyMMdd-HHmmss})";
    }
}
