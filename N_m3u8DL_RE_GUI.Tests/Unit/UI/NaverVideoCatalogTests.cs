#nullable enable
using N_m3u8DL_RE_GUI;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.UI;

public sealed class NaverVideoCatalogTests
{
    [Fact]
    public void MultipleRealVariants_AreSortedAndRetainExactSignedUrls()
    {
        const string payload = """
          {"videos":{"list":[
            {"source":"https://video.pstatic.net/a/360.mp4?token=a1","encodingOption":{"height":360},"size":10485760},
            {"source":"https://video.pstatic.net/a/1080.mp4?token=b2","encodingOption":{"height":1080},"size":"73400320"},
            {"source":"https://video.pstatic.net/a/720.mp4?token=c3","encodingOption":{"name":"720p"},"size":31457280}
          ]}}
          """;
        var variants = NaverVideoCatalog.Parse(payload);
        Assert.Equal(3, variants.Count);
        Assert.Equal(new[] {1080,720,360}, variants.Select(v => v.Height));
        Assert.Equal("https://video.pstatic.net/a/1080.mp4?token=b2", variants[0].Url);
        Assert.Equal(73400320L, variants[0].Bytes);
    }

    [Fact]
    public void UntrustedOrMissingMediaSources_AreNotSynthesized()
    {
        const string payload = """
          {"videos":{"list":[
            {"source":"http://video.pstatic.net/a/1080.mp4","encodingOption":{"height":1080}},
            {"source":"https://127.0.0.1/private.mp4","encodingOption":{"height":2160}},
            {"source":"https://attacker.example/vid.mp4","encodingOption":{"height":720}},
            {"source":"https://video.pstatic.net/a/live.m3u8","encodingOption":{"height":480}}
          ]}}
          """;
        Assert.Empty(NaverVideoCatalog.Parse(payload));
        Assert.Empty(NaverVideoCatalog.Parse("{}"));
        Assert.Empty(NaverVideoCatalog.Parse("{bad"));
    }

    [Fact]
    public void DuplicateFormatUrl_DoesNotDuplicateMedia()
    {
        const string payload = """
          {"videos":{"list":[
            {"source":"https://video.pstatic.net/v/a.mp4?x=1","encodingOption":{"height":720}},
            {"source":"https://video.pstatic.net/v/a.mp4?x=1","encodingOption":{"height":720}}
          ]}}
          """;
        Assert.Single(NaverVideoCatalog.Parse(payload));
    }

    [Fact]
    public void DiscoveryScript_IsBundled_AndScopesItselfToNaver()
    {
        using var script = typeof(BrowserCaptureWindow).Assembly
            .GetManifestResourceStream("UniversalVideoDownloader.BrowserNaver");
        Assert.NotNull(script);
        using var reader = new System.IO.StreamReader(script!);
        var text = reader.ReadToEnd();
        Assert.Contains("data-module", text);
        Assert.Contains("uvd-naver-video:", text);
        Assert.Contains("location.hostname", text);
        Assert.DoesNotContain("document.cookie", text);
        Assert.DoesNotContain("window.location =", text);
    }

    [Fact]
    public void BrowserAutoplay_DoesNotStopAtFirstPlayingVideo()
    {
        using var script = typeof(BrowserCaptureWindow).Assembly
            .GetManifestResourceStream("UniversalVideoDownloader.BrowserAutoplay");
        using var reader = new System.IO.StreamReader(script!);
        var text = reader.ReadToEnd();
        Assert.DoesNotContain("if (played || Date.now() >= deadline)", text);
        Assert.DoesNotContain("feedback('playing'); stop();", text);
        Assert.Contains("for (const video of root.querySelectorAll('video'))", text);
    }
}
