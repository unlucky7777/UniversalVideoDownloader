#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using N_m3u8DL_RE_GUI.Core.Capture;

namespace N_m3u8DL_RE_GUI;

internal static class CaptureMediaProbe
{
    internal sealed record Variant(int Height,long Bandwidth,long? EstimatedBytes);
    internal sealed record Result(bool Accessible,long? Bytes,bool Approximate,
        double? Duration,int? Height,IReadOnlyList<int> Heights,
        IReadOnlyList<Variant> Variants,string Note,
        string Format="",string Codec="",int? Width=null,double? Fps=null,long? Bitrate=null);

    internal static async Task<Result> InspectAsync(CapturedRequest source,CancellationToken token)
    {
        var original=new Uri(source.Url);
        var temp=Path.Combine(Path.GetTempPath(),"uvd-probe-"+Guid.NewGuid().ToString("N")+".m3u8");
        try
        {
            var ffmpeg=await FfmpegMediaProbe.ProbeAsync(
                ResolveFfmpegPath(),source.Url,
                source.Headers.Select(h=>new KeyValuePair<string,string>(h.Name,h.Value)),
                token,TimeSpan.FromSeconds(14));

            if(source.Kind==CapturedStreamKind.Media)
            {
                var row=await CurlAsync(source,source.Url,null,true,token);
                var contentRange=Regex.Match(row.Meta,@"bytes\s+\d+-\d+/(\d+)",
                    RegexOptions.IgnoreCase);
                long? total=contentRange.Success && long.TryParse(contentRange.Groups[1].Value,out var n)
                    ?n:row.Status==200 && long.TryParse(row.Meta.Split('|').LastOrDefault(),out var length)
                    ?length:null;
                var pathHeight=HeightFromPath(original.AbsolutePath);
                var height=ffmpeg.Height??pathHeight;
                var mediaHeights=height.HasValue?new[]{height.Value}:Array.Empty<int>();
                var accessible=row.Status is 200 or 206 || ffmpeg.Success;
                var format=!string.IsNullOrWhiteSpace(ffmpeg.Format)
                    ?ffmpeg.Format:Path.GetExtension(original.AbsolutePath).TrimStart('.').ToUpperInvariant();
                return new Result(accessible,total,false,ffmpeg.Duration,height,mediaHeights,
                    Array.Empty<Variant>(),
                    accessible
                      ?"Direct media metadata combined from server byte-range and FFmpeg container probe."
                      :"Direct media metadata could not be verified.",
                    format,CombinedCodec(ffmpeg),ffmpeg.Width,ffmpeg.Fps,ffmpeg.Bitrate);
            }
            if(source.Kind is CapturedStreamKind.Dash or CapturedStreamKind.Mss)
            {
                long? dashEstimate=ffmpeg.Duration is >0 && ffmpeg.Bitrate is >0
                    ?(long?)Math.Round(ffmpeg.Duration.Value*ffmpeg.Bitrate.Value/8d):null;
                var dashHeights=ffmpeg.Height is >0?new[]{ffmpeg.Height.Value}:Array.Empty<int>();
                return new Result(ffmpeg.Success,dashEstimate,dashEstimate.HasValue,ffmpeg.Duration,
                    ffmpeg.Height,dashHeights,Array.Empty<Variant>(),
                    ffmpeg.Success
                      ?"Manifest/container metadata confirmed through FFmpeg input probe."
                      :"Stream metadata could not be confirmed by FFmpeg.",
                    ffmpeg.Format,CombinedCodec(ffmpeg),ffmpeg.Width,ffmpeg.Fps,ffmpeg.Bitrate);
            }
            if(source.Kind!=CapturedStreamKind.Hls)
                return new Result(ffmpeg.Success,null,false,ffmpeg.Duration,ffmpeg.Height,
                    ffmpeg.Height is >0?new[]{ffmpeg.Height.Value}:Array.Empty<int>(),
                    Array.Empty<Variant>(),ffmpeg.Note,ffmpeg.Format,CombinedCodec(ffmpeg),
                    ffmpeg.Width,ffmpeg.Fps,ffmpeg.Bitrate);
            var master=await CurlAsync(source,source.Url,temp,false,token);
            if(master.Status!=200 || !master.Body.StartsWith("#EXTM3U",StringComparison.Ordinal))
            {
                long? fallbackEstimate=ffmpeg.Duration is >0 && ffmpeg.Bitrate is >0
                    ?(long?)Math.Round(ffmpeg.Duration.Value*ffmpeg.Bitrate.Value/8d):null;
                return new Result(ffmpeg.Success,fallbackEstimate,fallbackEstimate.HasValue,
                    ffmpeg.Duration,ffmpeg.Height,
                    ffmpeg.Height is >0?new[]{ffmpeg.Height.Value}:Array.Empty<int>(),
                    Array.Empty<Variant>(),
                    ffmpeg.Success
                      ?"HLS manifest curl probe was limited, but FFmpeg confirmed stream metadata."
                      :"HLS metadata could not be inspected (HTTP "+master.Status+").",
                    string.IsNullOrWhiteSpace(ffmpeg.Format)?"HLS / M3U8":ffmpeg.Format,
                    CombinedCodec(ffmpeg),ffmpeg.Width,ffmpeg.Fps,ffmpeg.Bitrate);
            }

            var rows=new List<(int Height,long Bandwidth,string Uri)>();
            var lines=Regex.Split(master.Body,@"\r?\n");
            for(var i=0;i<lines.Length;i++)
            {
                if(!lines[i].StartsWith("#EXT-X-STREAM-INF:",StringComparison.OrdinalIgnoreCase))continue;
                var height=Regex.Match(lines[i],@"RESOLUTION=\d+x(\d+)");
                var bw=Regex.Match(lines[i],@"(?:AVERAGE-)?BANDWIDTH=(\d+)");
                var target=lines.Skip(i+1).FirstOrDefault(l=>!string.IsNullOrWhiteSpace(l)&&!l.StartsWith('#'));
                if(target is not null)
                    rows.Add((height.Success?int.Parse(height.Groups[1].Value):0,
                        bw.Success?long.Parse(bw.Groups[1].Value):0,target.Trim()));
            }
            var heights=rows.Where(r=>r.Height>0).Select(r=>r.Height).Distinct()
                .OrderByDescending(h=>h).ToArray();
            var chosen=rows.OrderByDescending(r=>r.Height).ThenByDescending(r=>r.Bandwidth)
                .FirstOrDefault();
            var media=master.Body;
            var mediaUri=original;
            if(rows.Count>0 && Uri.TryCreate(original,chosen.Uri,out var nested) &&
               nested.Scheme is "https" or "http")
            {
                var child=await CurlAsync(source,nested.AbsoluteUri,temp,false,token);
                if(child.Status==200){media=child.Body;mediaUri=nested;}
            }
            double duration=0;
            foreach(Match m in Regex.Matches(media,@"(?m)^#EXTINF:([0-9]+(?:\.[0-9]+)?)"))
                if(double.TryParse(m.Groups[1].Value,NumberStyles.Float,
                    CultureInfo.InvariantCulture,out var seconds))duration+=seconds;
            var inferred=HeightFromPath(original.AbsolutePath);
            var heightValue=chosen.Height>0?chosen.Height:(ffmpeg.Height??inferred);
            var effectiveDuration=duration>0?duration:ffmpeg.Duration??0d;
            long? estimated=null;
            if(effectiveDuration>0 && chosen.Bandwidth>0)
                estimated=(long)Math.Round(effectiveDuration*chosen.Bandwidth/8d);
            else if(effectiveDuration>0 && ffmpeg.Bitrate is >0)
                estimated=(long)Math.Round(effectiveDuration*ffmpeg.Bitrate.Value/8d);
            // Standalone HLS playlists may omit BANDWIDTH: estimate their size
            // from five byte-range segment lengths. Never label it exact.
            if(!estimated.HasValue && !media.Contains("#EXT-X-KEY:",StringComparison.Ordinal))
            {
                var segments=Regex.Split(media,@"\r?\n")
                    .Select(s=>s.Trim()).Where(s=>s.Length>0&&!s.StartsWith('#')).ToArray();
                if(segments.Length>0 && segments.Length<=20000)
                {
                    var positions=new[]{0,segments.Length/4,segments.Length/2,
                        3*segments.Length/4,segments.Length-1}.Distinct();
                    var sizes=new List<long>();
                    foreach(var index in positions)
                    {
                        if(!Uri.TryCreate(mediaUri,segments[index],out var segmentUri) ||
                           segmentUri.Host!=mediaUri.Host || segmentUri.Scheme is not ("http" or "https"))
                            continue;
                        var sample=await CurlAsync(source,segmentUri.AbsoluteUri,null,true,token);
                        var m=Regex.Match(sample.Meta,@"bytes\s+0-0/(\d+)",RegexOptions.IgnoreCase);
                        if(sample.Status==206 && m.Success &&
                           long.TryParse(m.Groups[1].Value,out var size) && size>0)
                            sizes.Add(size);
                    }
                    if(sizes.Count>=2)
                        estimated=(long)Math.Round(sizes.Average()*segments.Length);
                }
            }
            var variants=rows.Where(r=>r.Height>0)
                .GroupBy(r=>r.Height)
                .Select(g=>g.OrderByDescending(r=>r.Bandwidth).First())
                .OrderByDescending(r=>r.Height)
                .Select(r=>new Variant(r.Height,r.Bandwidth,
                    effectiveDuration>0&&r.Bandwidth>0
                        ?(long?)Math.Round(effectiveDuration*r.Bandwidth/8d):null))
                .ToArray();
            var mergedHeights=heights.Length>0?heights:
                heightValue.HasValue?new[]{heightValue.Value}:Array.Empty<int>();
            return new Result(true,estimated,estimated.HasValue,
                effectiveDuration>0?effectiveDuration:null,heightValue,
                mergedHeights,variants,
                estimated.HasValue
                    ?"HLS duration/quality confirmed; total size is estimated from playlist or stream bitrate."
                    :"HLS stream metadata confirmed; exact total size is not advertised.",
                string.IsNullOrWhiteSpace(ffmpeg.Format)?"HLS / M3U8":ffmpeg.Format,
                CombinedCodec(ffmpeg),ffmpeg.Width,ffmpeg.Fps,
                chosen.Bandwidth>0?chosen.Bandwidth:ffmpeg.Bitrate);
        }
        catch(OperationCanceledException){throw;}
        catch(Exception){return new Result(false,null,false,null,null,Array.Empty<int>(),
            Array.Empty<Variant>(),
            "Metadata unavailable; this does not by itself prove a download failure.");}
        finally{try{File.Delete(temp);}catch(IOException){}}
    }

    internal static int? HeightFromPath(string path)
    {
        var m=Regex.Match(path,@"(?:^|[/_.-])(4320|2880|2160|1440|1080|720|540|480|360|240)[pP](?:[/_.-]|$)");
        return m.Success?int.Parse(m.Groups[1].Value):null;
    }

    private static string ResolveFfmpegPath()
    {
        var runtime=Path.Combine(AppContext.BaseDirectory,"Runtime","ffmpeg.exe");
        if(File.Exists(runtime))return runtime;
        var local=Path.Combine(AppContext.BaseDirectory,"ffmpeg.exe");
        return local;
    }

    private static string CombinedCodec(FfmpegMediaMetadata metadata)
    {
        if(!string.IsNullOrWhiteSpace(metadata.VideoCodec) &&
           !string.IsNullOrWhiteSpace(metadata.AudioCodec))
            return metadata.VideoCodec+" + "+metadata.AudioCodec;
        if(!string.IsNullOrWhiteSpace(metadata.VideoCodec))return metadata.VideoCodec;
        if(!string.IsNullOrWhiteSpace(metadata.AudioCodec))return metadata.AudioCodec;
        return "Stream codec not advertised";
    }

    private static async Task<(int Status,string Meta,string Body)> CurlAsync(
        CapturedRequest source,string url,string? output,bool singleByte,CancellationToken token)
    {
        using var process=new Process{StartInfo=new ProcessStartInfo{
            FileName="curl.exe",UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardOutput=true,RedirectStandardError=true
        }};
        var args=process.StartInfo.ArgumentList;
        foreach(var a in new[]{"--silent","--show-error","--location",
            "--proto","=https,http","--proto-redir","=https,http",
            "--connect-timeout","7","--max-time","16"})args.Add(a);
        if(singleByte)
        {
            args.Add("--range");args.Add("0-0");
            args.Add("--max-filesize");args.Add("1048576");
        }
        else {args.Add("--max-filesize");args.Add("2097152");}
        args.Add("--output");args.Add(output??"NUL");
        args.Add("--write-out");args.Add("%{http_code}|%header{content-range}|%header{content-length}");
        foreach(var h in source.Headers)
        {
            if(!new[]{"Referer","Origin","User-Agent","Accept","Accept-Language"}
                .Contains(h.Name,StringComparer.OrdinalIgnoreCase) ||
                h.Value.IndexOfAny(['\r','\n','\0'])>=0)continue;
            args.Add("-H");args.Add(h.Name+": "+h.Value);
        }
        args.Add(url);
        process.Start();
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(18));
        try
        {
            var std=process.StandardOutput.ReadToEndAsync(timeout.Token);
            var err=process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var metadata=await std;_ = await err;
            var status=int.TryParse(metadata.Split('|')[0],out var code)?code:0;
            var body=!singleByte && status==200 && output is not null && File.Exists(output)
                ?await File.ReadAllTextAsync(output,token):string.Empty;
            return (status,metadata,body);
        }
        finally
        {
            if(!process.HasExited)try{process.Kill(entireProcessTree:true);}catch(InvalidOperationException){}
        }
    }
}
