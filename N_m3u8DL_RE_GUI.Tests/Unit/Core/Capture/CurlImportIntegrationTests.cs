#nullable enable
using N_m3u8DL_RE_GUI.Core;
using N_m3u8DL_RE_GUI.Core.Capture;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.Core.Capture;

public class CurlImportIntegrationTests
{
    [Fact]
    public void BrowserCurl_UrlAndHeaders_RoundTripIntoStreamEngineArguments()
    {
        const string command = """
            curl.exe -H ^"Referer: https://luluvdo.com//^" ^
              -H ^"Origin: https://luluvdo.com^" ^
              -H ^"User-Agent: Mozilla/5.0^" ^
              --url ^"https://cdn.example.com/master.m3u8?t=abc^&s=123^"
            """;
        var request = CurlCommandParser.Parse(command);
        Assert.NotNull(request);
        Assert.Equal("https://cdn.example.com/master.m3u8?t=abc&s=123", request!.Url);
        var headerText = request.ToHeaderLines();
        Assert.Contains("Referer: https://luluvdo.com//", headerText);
        Assert.Contains("Origin: https://luluvdo.com", headerText);

        var args = ArgsBuilder.Build(new DownloadOptions
        {
            Input = request.Url,
            Headers = headerText,
            ThreadCount = 128
        });
        Assert.StartsWith("\"https://cdn.example.com/master.m3u8?t=abc&s=123\"", args);
        Assert.Contains("-H \"Referer: https://luluvdo.com//\"", args);
        Assert.Contains("-H \"Origin: https://luluvdo.com\"", args);
        Assert.Contains("-H \"User-Agent: Mozilla/5.0\"", args);
    }

    [Fact]
    public void SingleCapturedCookie_WithPipe_IsPreservedInEngineArguments()
    {
        var request = CurlCommandParser.Parse(
            "curl 'https://cdn.example.com/master.m3u8' -H 'Cookie: session=a|b'");
        Assert.NotNull(request);
        var args = ArgsBuilder.Build(new DownloadOptions
        {
            Input = request!.Url,
            Headers = request.ToHeaderLines()
        });
        Assert.Contains("-H \"Cookie: session=a|b\"", args);
    }

    [Fact]
    public void HeaderOnlyCurl_NeverFallsBackToRefererAsTarget()
    {
        const string command = "curl.exe -H 'Referer: https://player.example.com/'";
        Assert.Null(CurlCommandParser.Parse(command));
        Assert.Equal(string.Empty, InputValidation.ExtractFirstUrl(command));
    }
}
