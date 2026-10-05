#nullable enable
using System.IO;
using N_m3u8DL_RE_GUI;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.UI;

public class BrowserAutoplayPackagingTests
{
    [Fact]
    public void EmbeddedAutoplay_IsPackagedIntoApplicationAssembly()
    {
        using var script = typeof(BrowserCaptureWindow).Assembly
            .GetManifestResourceStream("UniversalVideoDownloader.BrowserAutoplay");
        Assert.NotNull(script);
        using var reader = new StreamReader(script!);
        var text = reader.ReadToEnd();
        Assert.Contains("video.play()", text);
        Assert.Contains("video.muted = true", text);
        Assert.Contains("MutationObserver", text);
        Assert.Contains("45000", text);
        Assert.DoesNotContain("window.location =", text);
    }

    [Fact]
    public void HighestQualitySelector_IsPackagedAndLimitedToPlayerControls()
    {
        using var script = typeof(BrowserCaptureWindow).Assembly
            .GetManifestResourceStream("UniversalVideoDownloader.BrowserQuality");
        Assert.NotNull(script);
        using var reader = new StreamReader(script!);
        var text = reader.ReadToEnd();
        Assert.Contains("mgp_qualitiesMenu", text);
        Assert.Contains("uvd-quality:", text);
        Assert.Contains("choices.sort((a,b)=>b.h-a.h)", text);
        Assert.DoesNotContain("window.location =", text);
    }
}
