#nullable enable
using N_m3u8DL_RE_GUI;
using N_m3u8DL_RE_GUI.Core.Capture;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.UI;

public sealed class StaleCaptureRecoveryTests
{
    [Theory]
    [InlineData(200, true, false)]
    [InlineData(206, true, false)]
    [InlineData(302, false, false)]
    [InlineData(403, false, false)]
    [InlineData(404, false, true)]
    [InlineData(410, false, true)]
    [InlineData(500, false, false)]
    public void CandidateMustHaveSuccessfulNetworkResponse(int status, bool record, bool missing)
    {
        Assert.Equal(record, MediaRequestSelector.IsSuccessfulMediaStatus(status));
        Assert.Equal(missing, MediaRequestSelector.IsPermanentMissingStatus(status));
    }

    [Theory]
    [InlineData("https://yako49.com/korea/190892", true)]
    [InlineData("https://www.yako49.com/korea/190892?query=1", true)]
    [InlineData("https://other.example.com/korea/190892", false)]
    [InlineData("https://yako49.com.evil.example/korea/190892", false)]
    [InlineData("file:///korea/190892", false)]
    [InlineData("https://user:secret@yako49.com/korea/190892", false)]
    [InlineData("https://yako49.com/other/190892", false)]
    public void UnsupportedEmbedPageRoutesBackToExistingChrome(string url, bool expected)
    {
        Assert.Equal(expected, BrowserSessionRecoveryPolicy.RequiresChromeRescan(url));
    }

    [Theory]
    [InlineData("404", true)]
    [InlineData("410", true)]
    [InlineData(" 404\n", true)]
    [InlineData("403", false)]
    [InlineData("000", false)]
    [InlineData("200", false)]
    [InlineData("", false)]
    public void OnlyDefinitivelyMissingStatusSkipsNormalDownload(string status, bool missing)
    {
        Assert.Equal(missing, BrowserSessionRecoveryPolicy.IsDefinitivelyMissingStatus(status));
    }
}
