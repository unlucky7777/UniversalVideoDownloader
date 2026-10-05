using System;
using System.Linq;
using N_m3u8DL_RE_GUI.Core;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.Core;

public class UniversalDownloadRouterTests
{
    [Theory]
    [InlineData("https://www.pornvov.net/get_file/10273-2160.mp4?token=x", true)]
    [InlineData("https://www.pornvov.net/get_file/10273-360.mp4", true)]
    [InlineData("https://pornvov.net/get_file/10273-1080.mp4", true)]
    [InlineData("https://www.pornvov.net/video/watch-page/", false)]
    [InlineData("https://example.com/get_file/10273-2160.mp4", false)]
    [InlineData("https://fakepornvov.net/get_file/10273-2160.mp4", false)]
    [InlineData("http://www.pornvov.net/get_file/10273-2160.mp4", false)]
    public void DirectMainMp4Router_RequiresActualSiteAndRendition(
        string url, bool expected)
        => Assert.Equal(expected, UniversalDownloadRouter.IsPornvovDirectMp4(url));

    [Theory]
    [InlineData("https://www.eporner.com/video-ONV8nqkI36y/some-title/", true)]
    [InlineData("https://www.eporner.com/embed/ONV8nqkI36y/", true)]
    [InlineData("https://noteporner.com/embed/ONV8nqkI36y/", false)]
    [InlineData("http://www.eporner.com/embed/ONV8nqkI36y/", false)]
    [InlineData("https://example.com/video-ONV8nqkI36y", false)]
    public void SiteAdapterRegistry_RestrictsSpecialPagesToRegisteredHttpsPatterns(
        string input, bool expected)
        => Assert.Equal(expected, SiteAdapterRegistry.ResolvePage(input) is not null);

    [Fact]
    public void SiteAdapterRegistry_ExposesCommonHelperContract()
    {
        var adapter=SiteAdapterRegistry.ResolvePage(
            "https://www.eporner.com/video-ONV8nqkI36y/some-title/");
        Assert.NotNull(adapter);
        Assert.False(string.IsNullOrWhiteSpace(adapter!.MetadataHelper));
        Assert.False(string.IsNullOrWhiteSpace(adapter.DownloadHelper));
        Assert.False(string.IsNullOrWhiteSpace(adapter.DownloadMethod));
        Assert.False(string.IsNullOrWhiteSpace(adapter.OutputFormat));
    }

    [Theory]
    [InlineData("https://example.org/watch?id=1", UniversalEngine.Website)]
    [InlineData("https://www.youtube.com/watch?v=abc", UniversalEngine.Website)]
    [InlineData("https://cdn.example.org/master.m3u8?t=token", UniversalEngine.Stream)]
    [InlineData("https://cdn.example.org/MANIFEST.MPD", UniversalEngine.Stream)]
    [InlineData("https://cdn.example.org/video.ism/Manifest", UniversalEngine.Stream)]
    [InlineData("C:\\list.txt", UniversalEngine.Stream)]
    public void AutoRoute_UsesCorrectEngine(string input, UniversalEngine expected)
        => Assert.Equal(expected, UniversalDownloadRouter.Select(input));

    [Fact]
    public void ExplicitOverride_AlwaysWins()
    {
        Assert.Equal(UniversalEngine.Stream,
            UniversalDownloadRouter.Select("https://example.org/watch", 1));
        Assert.Equal(UniversalEngine.Website,
            UniversalDownloadRouter.Select("https://cdn.example.org/master.m3u8", 2));
    }

    [Fact]
    public void WebsiteArguments_AreSeparateTokensAndKeepOriginalUrl()
    {
        var url = "https://example.org/watch?id=7&sig=%2fABC%2F";
        var args = WebsiteArgsBuilder.Build(url, "D:\\Video Downloads",
            "A&B % title", 128, 2160).ToArray();
        Assert.Contains("--no-playlist", args);
        Assert.Equal("128", args[Array.IndexOf(args, "-N") + 1]);
        Assert.Contains("[height<=2160]", args[Array.IndexOf(args, "-f") + 1]);
        Assert.Equal("D:\\Video Downloads", args[Array.IndexOf(args, "-P") + 1]);
        Assert.Equal(url, args[^1]);
        Assert.Equal("A&B %% title.%(ext)s", args[Array.IndexOf(args, "-o") + 1]);
    }

    [Theory]
    [InlineData(0, "1")]
    [InlineData(-1, "1")]
    [InlineData(256, "256")]
    [InlineData(9999, "256")]
    public void ThreadCount_IsBounded(int requested, string expected)
    {
        var args = WebsiteArgsBuilder.Build("https://example.org/watch", null, null,
            requested).ToArray();
        Assert.Equal(expected, args[Array.IndexOf(args, "-N") + 1]);
    }

    [Theory]
    [InlineData(136)]
    [InlineData(240)]
    [InlineData(360)]
    [InlineData(480)]
    [InlineData(720)]
    [InlineData(1080)]
    [InlineData(1350)]
    [InlineData(1440)]
    [InlineData(2026)]
    [InlineData(2160)]
    [InlineData(2880)]
    [InlineData(4050)]
    [InlineData(4320)]
    public void WebsiteMaximumQuality_IsPassedToYtDlp(int quality)
    {
        var args=WebsiteArgsBuilder.Build("https://www.youtube.com/watch?v=jNQXAC9IVRw",
            null,null,8,maximumHeight:quality).ToArray();
        var format=args[Array.IndexOf(args,"-f")+1];
        Assert.Contains($"[height<={quality}]",format);
        Assert.Contains("bv*",format);
        Assert.Contains("ba",format);
    }

    [Fact]
    public void QualityZero_IsUnrestricted()
    {
        var args = WebsiteArgsBuilder.Build("https://example.org/watch", null, null,
            maximumHeight: 0).ToArray();
        Assert.Equal("bv*+ba/b", args[Array.IndexOf(args, "-f") + 1]);
    }

    [Theory]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("javascript:alert(1)")]
    [InlineData("bad-url")]
    public void RejectsNonHttpInput(string url)
        => Assert.Throws<ArgumentException>(() => WebsiteArgsBuilder.Build(url, null, null));
}
