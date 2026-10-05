#nullable enable
using System;
using System.Collections.Generic;
using N_m3u8DL_RE_GUI.Core.Capture;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.Core.Capture;
public class MediaRequestSelectorTests
{
    [Theory]
    [InlineData("https://cdn.test/hls/master.m3u8?t=a%2Fb&s=12", CapturedStreamKind.Hls)]
    [InlineData("https://cdn.test/manifest.mpd?token=x", CapturedStreamKind.Dash)]
    [InlineData("https://cdn.test/smooth.ism/Manifest", CapturedStreamKind.Mss)]
    [InlineData("https://cdn.test/video.mp4?token=ab", CapturedStreamKind.Media)]
    public void ClassifiesDownloadableMedia(string url, CapturedStreamKind expected)
    {
        var candidate = MediaRequestSelector.Create(url, Array.Empty<KeyValuePair<string,string>>());
        Assert.NotNull(candidate);
        Assert.Equal(expected, candidate.Kind);
        Assert.Equal(url, candidate.Url); // raw signed URL, no re-encoding
    }

    [Theory]
    [InlineData("https://cdn.test/segment001.ts", "video/mp2t")]
    [InlineData("https://cdn.test/seg001.m4s", "video/mp4")]
    [InlineData("https://cdn.test/init.mp4", "video/mp4")]
    [InlineData("https://cdn.test/index.html", "text/html")]
    [InlineData("http://127.0.0.1/secret/master.m3u8", "application/vnd.apple.mpegurl")]
    [InlineData("http://192.168.1.1/private/master.m3u8", "application/vnd.apple.mpegurl")]
    [InlineData("http://10.1.2.3/secret/master.m3u8", "application/vnd.apple.mpegurl")]
    public void IgnoresFragmentsDocumentsAndLoopback(string url, string type)
        => Assert.Null(MediaRequestSelector.Create(url, null, type));

    [Fact]
    public void PreservesOnlyNonCredentialHeaders()
    {
        var source = new[]
        {
            new KeyValuePair<string,string>("Referer", "https://site.example/video"),
            new KeyValuePair<string,string>("Origin", "https://site.example"),
            new KeyValuePair<string,string>("User-Agent", "Browser/1.0"),
            new KeyValuePair<string,string>("Cookie", "session=SECRET"),
            new KeyValuePair<string,string>("Authorization", "Bearer SECRET"),
            new KeyValuePair<string,string>("Host", "cdn.test"),
            new KeyValuePair<string,string>("X-Key", "SECRET")
        };
        var result = MediaRequestSelector.Create("https://cdn.test/master.m3u8", source)!;
        Assert.Equal(3, result.Headers.Count);
        Assert.DoesNotContain("SECRET", result.ToHeaderLines());
        Assert.Contains("Referer:", result.ToHeaderLines());
    }

    [Fact]
    public void PrefersRealMasterOverProgressiveVideo()
    {
        var master = MediaRequestSelector.Create("https://cdn.test/hls/master.m3u8?t=secret", null)!;
        var clip = MediaRequestSelector.Create("https://cdn.test/clip.mp4", null)!;
        Assert.True(MediaRequestSelector.IsHighConfidenceMaster(master));
        Assert.Equal(master, MediaRequestSelector.Best(new[] { clip, master }));
    }

    [Fact]
    public void CapturesExtensionlessVideoByResponseMime()
    {
        var result = MediaRequestSelector.Create("https://cdn.test/media?id=42", null, "video/mp4; charset=binary");
        Assert.NotNull(result);
        Assert.Equal(CapturedStreamKind.Media, result.Kind);
    }
}
