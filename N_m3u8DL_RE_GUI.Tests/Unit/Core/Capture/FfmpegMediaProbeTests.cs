using N_m3u8DL_RE_GUI.Core.Capture;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.Core.Capture;

public class FfmpegMediaProbeTests
{
    [Fact]
    public void ParseProbeOutput_ExtractsMp4DurationResolutionFpsCodecsAndBitrate()
    {
        const string stderr = """
Input #0, mov,mp4,m4a,3gp,3g2,mj2, from 'https://cdn.example/video.mp4':
  Metadata:
    major_brand     : isom
  Duration: 00:37:18.12, start: 0.000000, bitrate: 791 kb/s
  Stream #0:0: Video: h264 (High), yuv420p(progressive), 1920x1080, 650 kb/s, 29.97 fps, 29.97 tbr
  Stream #0:1: Audio: aac (LC), 48000 Hz, stereo, fltp, 128 kb/s
At least one output file must be specified
""";

        var result = FfmpegMediaProbe.ParseProbeOutput(stderr, "/video.mp4");

        Assert.True(result.Success);
        Assert.Equal(2238.12, result.Duration!.Value, 2);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
        Assert.Equal(29.97, result.Fps!.Value, 2);
        Assert.Equal(791000, result.Bitrate);
        Assert.Equal("MP4 / MOV", result.Format);
        Assert.Equal("H.264", result.VideoCodec);
        Assert.Equal("AAC", result.AudioCodec);
    }

    [Fact]
    public void ParseProbeOutput_HandlesWebmVp9Opus()
    {
        const string stderr = """
Input #0, matroska,webm, from 'https://cdn.example/video.webm':
  Duration: 00:02:03.45, start: 0.000000, bitrate: 2048 kb/s
  Stream #0:0: Video: vp9 (Profile 0), yuv420p, 1280x720, 60 fps
  Stream #0:1: Audio: opus, 48000 Hz, stereo, fltp
""";

        var result = FfmpegMediaProbe.ParseProbeOutput(stderr, "/video.webm");

        Assert.True(result.Success);
        Assert.Equal(123.45, result.Duration!.Value, 2);
        Assert.Equal(1280, result.Width);
        Assert.Equal(720, result.Height);
        Assert.Equal(60, result.Fps);
        Assert.Equal("WebM / Matroska", result.Format);
        Assert.Equal("VP9", result.VideoCodec);
        Assert.Equal("Opus", result.AudioCodec);
    }

    [Fact]
    public void ParseProbeOutput_HandlesWindowsCrLfVideoStreamLines()
    {
        const string stderr =
            "Input #0, mov,mp4,m4a,3gp,3g2,mj2, from 'https://media.example/video.mp4':\r\n" +
            "  Duration: 00:00:52.21, start: 0.000000, bitrate: 669 kb/s\r\n" +
            "  Stream #0:0[0x1](und): Video: h264 (High) (avc1 / 0x31637661), yuv420p(progressive), 854x480, 537 kb/s, 24 fps, 24 tbr, 24 tbn (default)\r\n" +
            "  Stream #0:1[0x2](und): Audio: aac (LC), 48000 Hz, stereo, fltp, 126 kb/s (default)\r\n";

        var result = FfmpegMediaProbe.ParseProbeOutput(stderr, "/video.mp4");

        Assert.True(result.Success);
        Assert.Equal(52.21, result.Duration!.Value, 2);
        Assert.Equal(854, result.Width);
        Assert.Equal(480, result.Height);
        Assert.Equal(24, result.Fps);
        Assert.Equal("H.264", result.VideoCodec);
        Assert.Equal("AAC", result.AudioCodec);
    }

    [Fact]
    public void ParseProbeOutput_UsesExtensionWhenContainerLineIsMissing()
    {
        var result = FfmpegMediaProbe.ParseProbeOutput(
            "  Duration: 00:00:10.00\n  Stream #0:0: Video: av1, 854x480, 24 fps",
            "/asset.mp4");

        Assert.True(result.Success);
        Assert.Equal("MP4", result.Format);
        Assert.Equal("AV1", result.VideoCodec);
        Assert.Equal(480, result.Height);
    }
}
