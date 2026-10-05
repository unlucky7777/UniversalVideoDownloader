using N_m3u8DL_RE_GUI.Core;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.Core;

public class DownloadTelemetryParserTests
{
    [Fact]
    public void EpornerTotal_IsReadFromExactByteCount()
    {
        var sample = DownloadTelemetryParser.Parse("[size] 105335413");
        Assert.Equal(105335413L, sample.TotalBytes);
        Assert.Null(sample.Percent);
    }

    [Fact]
    public void EpornerProgress_DoesNotInventSize()
    {
        var sample = DownloadTelemetryParser.Parse("[download] 55.7% (7/13)");
        Assert.Equal(55.7, sample.Percent);
        Assert.Null(sample.TotalBytes);
    }

    [Fact]
    public void YtDlpProgress_ParsesApproximateTotalSpeedAndEta()
    {
        var sample = DownloadTelemetryParser.Parse(
            "[download]  25.0% of ~100.0MiB at 5.0MiB/s ETA 00:15");
        Assert.Equal(25d, sample.Percent);
        Assert.Equal(104857600L, sample.TotalBytes);
        Assert.Equal(5242880d, sample.BytesPerSecond);
        Assert.Equal(System.TimeSpan.FromSeconds(15), sample.Eta);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[download] unknown")]
    [InlineData("[warning] playlist skipped")]
    public void UnknownValuesRemainUnknown(string line)
    {
        var sample = DownloadTelemetryParser.Parse(line);
        Assert.Null(sample.TotalBytes);
        Assert.Null(sample.Eta);
        Assert.Null(sample.Percent);
    }
}
