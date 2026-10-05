using N_m3u8DL_RE_GUI.Core;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.Core;

public class LinkSizeEstimatorTests
{
    [Fact]
    public void CombinedStreams_AreSummedForSelectedFormats()
    {
        var result = LinkSizeEstimator.FromYtDlpJson(
            """{"requested_formats":[{"filesize":104857600},{"filesize_approx":10485760}],"duration":120,"tbr":999}""");
        Assert.Equal(115343360L, result.Bytes);
        Assert.True(result.Approximate);
    }

    [Fact]
    public void ExactMainSize_TakesPriority()
    {
        var result = LinkSizeEstimator.FromYtDlpJson(
            """{"filesize":2048,"filesize_approx":4096,"duration":999,"tbr":999}""");
        Assert.Equal(2048L, result.Bytes);
        Assert.False(result.Approximate);
    }

    [Fact]
    public void DurationAndBitrate_AreMarkedAsEstimate()
    {
        var result = LinkSizeEstimator.FromYtDlpJson("""{"duration":80,"tbr":1000}""");
        Assert.Equal(10000000L, result.Bytes);
        Assert.True(result.Approximate);
    }

    [Fact]
    public void UnknownMetadata_DoesNotInventSize()
    {
        var result = LinkSizeEstimator.FromYtDlpJson("""{"title":"test","formats":[{}]}""");
        Assert.Null(result.Bytes);
    }

    [Fact]
    public void AllExactFormats_AreExact()
    {
        var result = LinkSizeEstimator.FromYtDlpJson(
            """{"requested_formats":[{"filesize":1000},{"filesize":300}]}""");
        Assert.Equal(1300L, result.Bytes);
        Assert.False(result.Approximate);
    }
}
