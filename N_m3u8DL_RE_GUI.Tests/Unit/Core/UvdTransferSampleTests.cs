using Xunit;
using N_m3u8DL_RE_GUI.Core;
namespace N_m3u8DL_RE_GUI.Tests.Unit.Core;
public sealed class UvdTransferSampleTests
{
    [Fact] public void ParsesExactNaverRange()
    {
        Assert.True(UvdTransferSample.TryParse(
            "[UVD_METRICS] 25.00|96272867|385091469|2790985|104|0",out var row));
        Assert.Equal(25,row.Percent);
        Assert.Equal(96272867,row.Bytes);
        Assert.Equal(385091469,row.Total);
        Assert.Equal(2790985,row.SpeedBytesPerSecond);
        Assert.False(row.EstimatedTotal);
    }
    [Fact] public void ParsesApproximateHlsResume()
    {
        Assert.True(UvdTransferSample.TryParse(
            "[UVD_METRICS] 76|2780000000|3280000000|11200000|45|1",out var row));
        Assert.True(row.EstimatedTotal);
        Assert.Equal(45,row.EtaSeconds);
    }
    [Theory]
    [InlineData("[UVD_METRICS] NaN|1|2|3|4|0")]
    [InlineData("[UVD_METRICS] 20|-1|2|3|4|0")]
    [InlineData("[UVD_METRICS] 120|1|2|3|4|0")]
    [InlineData("[UVD_METRICS] 20|1|0|3|4|0")]
    [InlineData("[UVD_METRICS] 20|1|2|3|4|no")]
    [InlineData("another log")]
    public void RejectsInvalidSamples(string line) =>
        Assert.False(UvdTransferSample.TryParse(line,out _));
}
