#nullable enable
using N_m3u8DL_RE_GUI.Core;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.Core;

public class StreamUrlDiagnosticsTests
{
    [Theory]
    [InlineData("https://luluvdo.com//", "SITE_ROOT_NOT_STREAM")]
    [InlineData("https://ripnsfw.com/model/sample/91542711/", "RIPNSFW_PAGE_NOT_STREAM")]
    [InlineData("https://3duxixhjxwkg.tnmr.org/hls2/03/04360/x/master.m3u8?t=secret&s=123", null)]
    [InlineData("https://example.com/manifest.mpd?token=a%26b", null)]
    [InlineData("C:\\work\\local.m3u8", null)]
    public void ValidatesStreamInputWithoutRewritingSignedQuery(string input, string? expected)
        => Assert.Equal(expected, StreamUrlDiagnostics.CheckStreamInput(input));

    [Theory]
    [InlineData("WARN : Response status code does not indicate success: 403 (Forbidden). (1/10)", false, "MANIFEST_ACCESS_DENIED")]
    [InlineData("WARN : Response status code does not indicate success: 403 (Forbidden). (1/10)", true, "SEGMENT_ACCESS_DENIED")]
    [InlineData("WARN : Response status code does not indicate success: 404 (Not Found). (1/10)", false, "MANIFEST_NOT_FOUND")]
    [InlineData("WARN : Response status code does not indicate success: 404 (Not Found). (1/10)", true, "SEGMENT_NOT_FOUND")]
    [InlineData("WARN : Response status code does not indicate success: 410 (Gone). (1/10)", false, "MANIFEST_GONE")]
    [InlineData("WARN : Response status code does not indicate success: 410 (Gone). (1/10)", true, "SEGMENT_GONE")]
    [InlineData("INFO : Content Matched: HTTP Live Streaming", false, null)]
    public void DistinguishesManifestVersusMediaFailures(string line, bool manifestRead, string? expected)
        => Assert.Equal(expected, StreamUrlDiagnostics.ClassifyEngineOutput(line, manifestRead));

    [Fact]
    public void UserFacingErrorsDoNotRepeatSecretUrls()
    {
        foreach (var code in new[] { "MANIFEST_ACCESS_DENIED", "MANIFEST_NOT_FOUND", "SEGMENT_ACCESS_DENIED",
                                     "MANIFEST_GONE", "SEGMENT_GONE" })
        {
            var message = StreamUrlDiagnostics.Message(code, korean: true);
            Assert.DoesNotContain("https://", message, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", message, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
