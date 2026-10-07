using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// Strict read-only page metadata probe. Returns only sanitized fields and
// resolution-level aggregates. Site-specific exceptions are delegated to
// SiteMetadataAdapters; generic pages use yt-dlp.
internal static class PageMetadata
{
    static string? Str(JsonElement v,string key,int max=110)
    {
        return v.TryGetProperty(key,out var x) && x.ValueKind==JsonValueKind.String
            ? (x.GetString()??"")[..Math.Min(max,(x.GetString()??"").Length)] : null;
    }
    static long? Long(JsonElement v,string key)
    {
        if(!v.TryGetProperty(key,out var x) || x.ValueKind!=JsonValueKind.Number)return null;
        return x.TryGetInt64(out var n)&&n>0&&n<100_000_000_000?n:null;
    }
    static double? Num(JsonElement v,string key)
    {
        if(!v.TryGetProperty(key,out var x)||x.ValueKind!=JsonValueKind.Number)return null;
        var n=x.GetDouble();
        return double.IsFinite(n)&&n>0&&n<100_000_000?n:null;
    }
    internal static object Probe(string url,string folder)
    {
        if(!NetworkTargetGuard.IsAllowedHttpUrl(url,out _))
            throw new InvalidDataException("Unsafe or non-public metadata URL.");
        if(SiteMetadataAdapters.TryProbe(url,folder,out var siteAdapterResult))
            return siteAdapterResult;
        var executable=Path.Combine(folder,"yt-dlp.exe");
        if(!File.Exists(executable))return new {ok=false,error="Bundled page metadata extractor unavailable."} as object;
        var info=new ProcessStartInfo(executable)
        {
            UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardOutput=true,RedirectStandardError=true,
            WorkingDirectory=folder
        };
        foreach(var part in new[]{"--ignore-config","--no-playlist","--skip-download",
          "--dump-single-json","--no-warnings",
          "--socket-timeout","8","--retries","1","--extractor-retries","1","--"})
            info.ArgumentList.Add(part);
        info.ArgumentList.Add(url);
        using var proc=new Process{StartInfo=info};
        try
        {
            if(!proc.Start())throw new InvalidOperationException("Metadata process did not start.");
            using var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(22));
            // Drain BOTH pipes to avoid deadlock. Read only the JSON result;
            // never include stderr because it may contain the input signed URL.
            var output=proc.StandardOutput.ReadToEndAsync(cancel.Token);
            var errors=proc.StandardError.ReadToEndAsync(cancel.Token);
            try{proc.WaitForExitAsync(cancel.Token).GetAwaiter().GetResult();}
            catch(OperationCanceledException)
            {
                try{proc.Kill(entireProcessTree:true);}catch{}
                return new {ok=false,error="Metadata request timed out."};
            }
            var raw=output.GetAwaiter().GetResult();
            _=errors.GetAwaiter().GetResult();
            if(proc.ExitCode!=0||raw.Length<2||raw.Length>5_000_000)
                return new {ok=false,error="The website did not provide accessible metadata."};
            using var doc=JsonDocument.Parse(raw);
            var root=doc.RootElement;
            var duration=Num(root,"duration");
            var title=Str(root,"title",180);
            var videos=new List<(int height,int? fps,string ext,string codec,long? exact,long? estimate,double rate)>();
            long? audioExact=null,audioEstimate=null;
            double audioScore=-1;
            if(root.TryGetProperty("formats",out var rows)&&rows.ValueKind==JsonValueKind.Array)
            foreach(var format in rows.EnumerateArray())
            {
                if(format.ValueKind!=JsonValueKind.Object)continue;
                var codec=Str(format,"vcodec",52)??"none";
                var acodec=Str(format,"acodec",52)??"none";
                var bytes=Long(format,"filesize");
                var estimated=Long(format,"filesize_approx");
                var rate=Num(format,"tbr")??0;
                if(codec!="none")
                {
                    var h=Long(format,"height");
                    if(h is not >0 or >8640)continue;
                    var fps=Num(format,"fps");
                    videos.Add(((int)h, fps is >0 and <241?(int?)Math.Round(fps.Value):null,
                      Str(format,"ext",12)??"unknown",codec,bytes,estimated,rate));
                }
                else if(acodec!="none")
                {
                    var score=Num(format,"abr")??rate;
                    if(score>=audioScore)
                    {
                        audioScore=score;
                        audioExact=bytes;
                        audioEstimate=estimated??(duration.HasValue&&score>0
                          ?(long?)(duration.Value*score*1000/8):null);
                    }
                }
            }
            // Keep one best available VIDEO representation per actual height.
            var list=videos.GroupBy(v=>v.height)
                .Select(g=>g.OrderByDescending(v=>v.exact.HasValue)
                    .ThenByDescending(v=>v.fps??0).ThenByDescending(v=>v.rate).First())
                .OrderByDescending(v=>v.height).Take(20)
                .Select(v=>new {
                   height=v.height,fps=v.fps,ext=v.ext,videoCodec=v.codec,
                   sizeBytes=v.exact.HasValue&&audioExact.HasValue?v.exact+audioExact:null,
                   estimatedBytes=v.exact.HasValue&&audioExact.HasValue?(long?)null:
                       (v.exact??v.estimate??(duration.HasValue&&v.rate>0
                         ?(long?)(duration.Value*v.rate*1000/8):null)) is long videoBytes
                           ?(long?)(videoBytes+(audioExact??audioEstimate??0)):null,
                   sizeIsExact=v.exact.HasValue&&audioExact.HasValue
                }).ToArray();
            return new {
              ok=true,title,duration,
              formats=list,
              container="MP4 (merge requested; source codec/container may vary)",
              audioAvailable=audioScore>=0,
              note=list.Length==0?
                "Page metadata is available, but individual video formats were not exposed.":
                "Filesize may be estimated. Listed heights are formats returned by the site."
            };
        }
        catch(Exception ex) when(ex is not OutOfMemoryException)
        {
            try{if(!proc.HasExited)proc.Kill(entireProcessTree:true);}catch{}
            return new {ok=false,error="Page metadata is unavailable ("+ex.GetType().Name+")."};
        }
    }
}

