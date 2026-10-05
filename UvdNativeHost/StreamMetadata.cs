using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

internal static class StreamMetadata
{
    internal sealed record HeaderValue(string Name,string Value);
    internal sealed record Variant(int? Width,int? Height,long? Bandwidth,double? Fps,string Uri);

    internal static object ProbeHls(string url,IReadOnlyList<HeaderValue> headers)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var original) ||
           original.Scheme is not ("http" or "https"))
            return new { ok=false,error="Invalid stream URL." };

        var tempRoot=Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UniversalVideoDownloader","MetadataProbe",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var first=Fetch(url,headers,Path.Combine(tempRoot,"master.m3u8"));
            if(!first.Ok)
                return new { ok=false,error="Stream metadata unavailable.",httpStatus=first.Status };
            if(!first.Text.TrimStart().StartsWith("#EXTM3U",StringComparison.Ordinal))
                return new { ok=false,error="The response is not an HLS playlist." };

            var variants=ParseMaster(first.Text);
            string mediaUrl=url;
            long? selectedBandwidth=null;
            int? selectedWidth=null,selectedHeight=null;
            double? selectedFps=null;
            if(variants.Count>0)
            {
                var best=variants
                    .OrderByDescending(v=>v.Height??0)
                    .ThenByDescending(v=>v.Bandwidth??0)
                    .First();
                mediaUrl=new Uri(original,best.Uri).AbsoluteUri;
                selectedBandwidth=best.Bandwidth;
                selectedWidth=best.Width;
                selectedHeight=best.Height;
                selectedFps=best.Fps;
            }

            var media=variants.Count>0
                ?Fetch(mediaUrl,headers,Path.Combine(tempRoot,"media.m3u8"))
                :first;
            double? duration=null;
            if(media.Ok)
            {
                var total=0d;var count=0;
                foreach(Match m in Regex.Matches(media.Text,@"(?m)^#EXTINF:([0-9]+(?:\.[0-9]+)?)"))
                    if(double.TryParse(m.Groups[1].Value,NumberStyles.Float,
                        CultureInfo.InvariantCulture,out var sec))
                    { total+=sec;count++; }
                if(count>0 && total>0)duration=total;
            }

            if(selectedHeight is null)
            {
                var h=Regex.Match(original.AbsolutePath,
                    @"(?:^|[/_.-])(2160|1440|1080|720|540|480|360|240)P?(?=[/_.-]|$)",
                    RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
                if(h.Success)selectedHeight=int.Parse(h.Groups[1].Value,CultureInfo.InvariantCulture);
            }

            long? estimate=null;
            if(duration is >0 && selectedBandwidth is >0)
            {
                var value=selectedBandwidth.Value*duration.Value/8d;
                if(value>0 && value<long.MaxValue)estimate=(long)Math.Round(value);
            }

            var formats=variants
                .Where(v=>v.Height is >0)
                .GroupBy(v=>v.Height)
                .Select(g=>g.OrderByDescending(v=>v.Bandwidth??0).First())
                .OrderByDescending(v=>v.Height)
                .Select(v=>new {
                    width=v.Width,height=v.Height,bandwidth=v.Bandwidth,
                    fps=v.Fps is >0?v.Fps:null
                }).ToArray();

            return new {
                ok=true,kind="HLS",verifiedBy="windows-curl",
                height=selectedHeight,width=selectedWidth,fps=selectedFps,
                duration,estimatedBytes=estimate,formats,
                note="HLS playlist verified through Windows curl."
            };
        }
        catch
        {
            return new { ok=false,error="Stream metadata probe failed." };
        }
        finally
        {
            try { Directory.Delete(tempRoot,true); } catch { }
        }
    }

    private static List<Variant> ParseMaster(string text)
    {
        var lines=Regex.Split(text,"\r?\n");
        var result=new List<Variant>();
        for(var i=0;i<lines.Length;i++)
        {
            var line=lines[i].Trim();
            if(!line.StartsWith("#EXT-X-STREAM-INF:",StringComparison.OrdinalIgnoreCase))
                continue;
            var attrs=line[18..];
            int? width=null,height=null;long? bandwidth=null;double? fps=null;
            var res=Regex.Match(attrs,@"(?:^|,)RESOLUTION=(\d+)x(\d+)",
                RegexOptions.IgnoreCase);
            if(res.Success)
            {
                width=int.Parse(res.Groups[1].Value,CultureInfo.InvariantCulture);
                height=int.Parse(res.Groups[2].Value,CultureInfo.InvariantCulture);
            }
            var bw=Regex.Match(attrs,@"(?:^|,)(?:AVERAGE-)?BANDWIDTH=(\d+)",
                RegexOptions.IgnoreCase);
            if(bw.Success)bandwidth=long.Parse(bw.Groups[1].Value,CultureInfo.InvariantCulture);
            var fr=Regex.Match(attrs,@"(?:^|,)FRAME-RATE=([0-9.]+)",
                RegexOptions.IgnoreCase);
            if(fr.Success && double.TryParse(fr.Groups[1].Value,NumberStyles.Float,
                CultureInfo.InvariantCulture,out var parsedFps))fps=parsedFps;
            string? uri=null;
            for(var j=i+1;j<lines.Length;j++)
            {
                var next=lines[j].Trim();
                if(next.Length==0)continue;
                if(next.StartsWith('#'))break;
                uri=next;break;
            }
            if(uri is not null)result.Add(new Variant(width,height,bandwidth,fps,uri));
        }
        return result;
    }

    private static (bool Ok,int Status,string Text) Fetch(
        string url,IReadOnlyList<HeaderValue> headers,string output)
    {
        var psi=new ProcessStartInfo
        {
            FileName="curl.exe",UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardOutput=true,RedirectStandardError=true
        };
        foreach(var arg in new[]{"-sS","--location","--connect-timeout","10",
                 "--max-time","20","--max-filesize","2097152","--output",output,
                 "--write-out","%{http_code}"})
            psi.ArgumentList.Add(arg);
        foreach(var h in headers)
        {
            psi.ArgumentList.Add("-H");
            psi.ArgumentList.Add(h.Name+": "+h.Value);
        }
        psi.ArgumentList.Add(url);
        using var process=new Process{StartInfo=psi};
        process.Start();
        var stdout=process.StandardOutput.ReadToEnd();
        _=process.StandardError.ReadToEnd();
        if(!process.WaitForExit(25000))
        {
            try { process.Kill(true); } catch { }
            return (false,0,string.Empty);
        }
        var status=int.TryParse(stdout.Trim(),out var parsed)?parsed:0;
        if(process.ExitCode!=0 || status is <200 or >=400 || !File.Exists(output))
            return (false,status,string.Empty);
        var info=new FileInfo(output);
        if(info.Length is <=0 or >2097152)return (false,status,string.Empty);
        return (true,status,File.ReadAllText(output));
    }
}
