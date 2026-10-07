using N_m3u8DL_RE_GUI.Core.Capture;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.Core.Capture;

public class NativeHandoffRoutingPolicyTests
{
    [Fact]
    public void FileHandoff_KeepsExactMediaUrlPrimary_WhenPageFallbackHasSiteAdapter()
    {
        var media="https://vod.pstatic.net/cafe/video-1080.mp4?sig=temporary";
        var page="https://cafe.naver.com/f-e/cafes/30888761/articles/12269";

        var route=NativeHandoffRoutingPolicy.Select(
            "FILE",media,page,1080,null);

        Assert.Equal("FILE",route.Kind);
        Assert.Equal(media,route.Url);
        Assert.Equal(page,route.PageFallbackUrl);
        Assert.Equal(1080,route.PreferredHeight);
    }

    [Fact]
    public void MediaHandoff_PreservesSignedUrlByteForByte()
    {
        const string media="https://cdn.example/video%2F1080.mp4?Policy=A%2BB%3D&Signature=x%2Fy%3D";

        var route=NativeHandoffRoutingPolicy.Select(
            "FILE",media,"https://example.com/watch/1",1080,null);

        Assert.Equal(media,route.Url);
    }

    [Fact]
    public void PageHandoff_UsesPageUrlAsPrimary()
    {
        var page="https://cafe.naver.com/f-e/cafes/30888761/articles/12269";

        var route=NativeHandoffRoutingPolicy.Select(
            "PAGE",page,null,null,1080);

        Assert.Equal("PAGE",route.Kind);
        Assert.Equal(page,route.Url);
        Assert.Equal(1080,route.PreferredHeight);
        Assert.Null(route.PageFallbackUrl);
    }

    [Fact]
    public void SelectedHeight_TakesPriorityOverManifestCap()
    {
        var route=NativeHandoffRoutingPolicy.Select(
            "HLS","https://cdn.example/master.m3u8",
            "https://example.com/watch/1",720,1080);

        Assert.Equal(720,route.PreferredHeight);
    }
}
