using N_m3u8DL_RE_GUI.Core.Capture;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.Core.Capture;

public sealed class BrowserPageCandidateTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=jNQXAC9IVRw")]
    [InlineData("https://m.youtube.com/watch?v=jNQXAC9IVRw")]
    [InlineData("https://youtu.be/jNQXAC9IVRw")]
    [InlineData("https://www.youtube.com/shorts/jNQXAC9IVRw")]
    [InlineData("https://www.youtube.com/live/jNQXAC9IVRw")]
    [InlineData("https://www.youtube-nocookie.com/embed/jNQXAC9IVRw")]
    public void YouTubeVideoPages_AreAccepted(string url)
        => Assert.True(BrowserPageCandidate.IsYouTubeVideoPage(url));

    [Theory]
    [InlineData("https://www.youtube.com/")]
    [InlineData("https://www.youtube.com/watch")]
    [InlineData("https://www.youtube.com/results?search_query=test")]
    [InlineData("https://example.com/watch?v=jNQXAC9IVRw")]
    [InlineData("javascript:alert(1)")]
    public void NonVideoPages_AreRejected(string url)
        => Assert.False(BrowserPageCandidate.IsYouTubeVideoPage(url));

    [Fact]
    public void CreatedCandidate_ForcesWebsiteRouteWithoutHeaders()
    {
        var candidate = BrowserPageCandidate.Create("https://www.youtube.com/watch?v=jNQXAC9IVRw");
        Assert.NotNull(candidate);
        Assert.Equal(CapturedStreamKind.Page, candidate!.Kind);
        Assert.Empty(candidate.Headers);
        Assert.Equal("website", candidate.Directives["engine"]);
        Assert.Equal("page", candidate.Directives["source"]);
        Assert.Equal(120, MediaRequestSelector.Score(candidate));
    }
}
