using System;
using System.IO;
using N_m3u8DL_RE_GUI.Core;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.Core;

public sealed class OutputConflictPolicyTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "uvd-conflict-" + Guid.NewGuid().ToString("N"));

    public OutputConflictPolicyTests()
    {
        Directory.CreateDirectory(_dir);
    }

    [Fact]
    public void FindsExistingFinalFileWithSameBaseName()
    {
        var existing = Path.Combine(_dir, "Example Video.mp4");
        File.WriteAllText(existing, "x");

        var conflicts = OutputConflictPolicy.FindConflicts(_dir, "Example Video");

        Assert.Single(conflicts);
        Assert.Equal(existing, conflicts[0]);
    }

    [Fact]
    public void IgnoresPartAndResumeFiles()
    {
        File.WriteAllText(Path.Combine(_dir, "Example Video.part"), "x");
        File.WriteAllText(Path.Combine(_dir, "Example Video.resume.json"), "{}");

        Assert.Empty(OutputConflictPolicy.FindConflicts(_dir, "Example Video"));
    }

    [Fact]
    public void RenameFindsNextUnusedNumber()
    {
        File.WriteAllText(Path.Combine(_dir, "Example Video.mp4"), "x");
        File.WriteAllText(Path.Combine(_dir, "Example Video (2).mkv"), "x");

        Assert.Equal("Example Video (3)",
            OutputConflictPolicy.FindUniqueBaseName(_dir, "Example Video"));
    }

    [Fact]
    public void NormalizationStripsKnownMediaExtension()
    {
        Assert.Equal("Example Video",
            OutputConflictPolicy.NormalizeBaseName("Example Video.mp4"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }
        catch
        {
        }
    }
}
