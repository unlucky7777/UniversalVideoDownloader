using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using N_m3u8DL_RE_GUI.Core.Capture;

internal static class MediaMetadata
{
    internal static object Probe(string url,string kind,
        IReadOnlyList<StreamMetadata.HeaderValue> headers,string folder)
    {
        if(!NetworkTargetGuard.IsAllowedHttpUrl(url,out _))
            return new {ok=false,error="Invalid or non-public media URL."};

        var ffmpeg=Path.Combine(folder,"ffmpeg.exe");
        var pairs=headers.Select(h=>new KeyValuePair<string,string>(h.Name,h.Value)).ToArray();
        FfmpegMediaMetadata metadata;
        try
        {
            metadata=FfmpegMediaProbe.ProbeAsync(ffmpeg,url,pairs,CancellationToken.None,
                TimeSpan.FromSeconds(14)).GetAwaiter().GetResult();
        }
        catch
        {
            metadata=new FfmpegMediaMetadata(false,null,null,null,null,null,
                string.Empty,string.Empty,string.Empty,"FFmpeg metadata probe failed.");
        }

        long? exactBytes=null;
        if(kind=="FILE")
            exactBytes=ProbeExactBytes(url,headers);

        long? estimatedBytes=exactBytes;
        var approximate=false;
        if(!estimatedBytes.HasValue && metadata.Duration is >0 && metadata.Bitrate is >0)
        {
            var estimate=metadata.Duration.Value*metadata.Bitrate.Value/8d;
            if(estimate>0 && estimate<long.MaxValue)
            {
                estimatedBytes=(long)Math.Round(estimate);
                approximate=true;
            }
        }

        var ok=metadata.Success || exactBytes is >0;
        var formats=metadata.Height is >0
            ?new[]{new {
                width=metadata.Width,height=metadata.Height,bandwidth=metadata.Bitrate,
                fps=metadata.Fps
            }}
            :Array.Empty<object>();

        return new {
            ok,
            kind,
            verifiedBy="ffmpeg-input-probe",
            height=metadata.Height,
            width=metadata.Width,
            fps=metadata.Fps,
            duration=metadata.Duration,
            exactBytes,
            estimatedBytes,
            approximate,
            format=metadata.Format,
            videoCodec=metadata.VideoCodec,
            audioCodec=metadata.AudioCodec,
            bitrate=metadata.Bitrate,
            formats,
            note=metadata.Note
        };
    }

    private static long? ProbeExactBytes(string url,IReadOnlyList<StreamMetadata.HeaderValue> headers)
    {
        try
        {
            var psi=new ProcessStartInfo
            {
                FileName="curl.exe",UseShellExecute=false,CreateNoWindow=true,
                RedirectStandardOutput=true,RedirectStandardError=true
            };
            foreach(var arg in new[]{"-sS","--location","--range","0-0",
                "--connect-timeout","8","--max-time","15","--max-filesize","1048576",
                "--output","NUL",
                "--write-out","%{http_code}|%header{content-range}|%header{content-length}"})
                psi.ArgumentList.Add(arg);
            foreach(var h in headers)
            {
                psi.ArgumentList.Add("-H");
                psi.ArgumentList.Add(h.Name+": "+h.Value);
            }
            psi.ArgumentList.Add(url);
            using var process=new Process{StartInfo=psi};
            if(!process.Start())return null;
            var output=process.StandardOutput.ReadToEnd();
            _=process.StandardError.ReadToEnd();
            if(!process.WaitForExit(20000))
            {
                try{process.Kill(true);}catch{}
                return null;
            }
            var parts=output.Trim().Split('|');
            var status=parts.Length>0&&int.TryParse(parts[0],out var s)?s:0;
            if(status is not (200 or 206))return null;
            if(parts.Length>1)
            {
                var m=Regex.Match(parts[1],@"bytes\s+\d+-\d+/(\d+)",
                    RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
                if(m.Success&&long.TryParse(m.Groups[1].Value,NumberStyles.Integer,
                    CultureInfo.InvariantCulture,out var total)&&total>0)
                    return total;
            }
            if(status==200&&parts.Length>2&&long.TryParse(parts[2],NumberStyles.Integer,
                CultureInfo.InvariantCulture,out var length)&&length>0)
                return length;
        }
        catch{}
        return null;
    }
}
