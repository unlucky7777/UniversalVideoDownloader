using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

static class Program
{
    private static readonly string[] AllowedHeaders =
        ["Referer", "Origin", "User-Agent", "Accept", "Accept-Language"];

    private static string InstallRoot()
    {
        var here = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (File.Exists(Path.Combine(here, "UniversalVideoDownloader.exe")))
            return here;
        var parent = Directory.GetParent(here)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent) &&
            File.Exists(Path.Combine(parent, "UniversalVideoDownloader.exe")))
            return parent;
        return here;
    }

    static int Main()
    {
        try
        {
            using var input = Console.OpenStandardInput();
            Span<byte> prefix = stackalloc byte[4];
            input.ReadExactly(prefix);
            var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
            if (length is < 1 or > 262144) throw new InvalidDataException("Invalid message length.");
            var buffer = new byte[length];
            input.ReadExactly(buffer);
            using var doc = JsonDocument.Parse(buffer);
            var root = doc.RootElement;
            if (Get(root, "type", 24) == "METADATA")
            {
                var target=Get(root,"url",8192);
                Reply(PageMetadata.Probe(target,AppContext.BaseDirectory));
                return 0;
            }
            if (Get(root, "type", 24) == "STREAM_METADATA")
            {
                var target=Get(root,"url",8192);
                if(!NetworkTargetGuard.IsAllowedHttpUrl(target,out _))
                    throw new InvalidDataException("Invalid or non-public stream URL.");
                var streamKind=Get(root,"kind",10);
                if(streamKind!="HLS")throw new InvalidDataException("Unsupported stream metadata kind.");
                var streamHeaders=new System.Collections.Generic.List<StreamMetadata.HeaderValue>();
                if(root.TryGetProperty("headers",out var streamEntries) &&
                   streamEntries.ValueKind==JsonValueKind.Array)
                {
                    if(streamEntries.GetArrayLength()>12)
                        throw new InvalidDataException("Too many headers.");
                    foreach(var entry in streamEntries.EnumerateArray())
                    {
                        var name=Get(entry,"name",40);
                        var value=Get(entry,"value",2048);
                        if(!AllowedHeaders.Contains(name,StringComparer.OrdinalIgnoreCase) ||
                           value.IndexOfAny(['\r','\n','\0'])>=0)
                            throw new InvalidDataException("Unsupported header.");
                        streamHeaders.Add(new StreamMetadata.HeaderValue(name,value));
                    }
                }
                Reply(StreamMetadata.ProbeHls(target,streamHeaders));
                return 0;
            }
            if (Get(root, "type", 24) == "MEDIA_METADATA")
            {
                var target=Get(root,"url",8192);
                if(!NetworkTargetGuard.IsAllowedHttpUrl(target,out _))
                    throw new InvalidDataException("Invalid or non-public media URL.");
                var mediaKind=Get(root,"kind",10);
                if(mediaKind is not ("FILE" or "HLS" or "DASH" or "MSS"))
                    throw new InvalidDataException("Unsupported media metadata kind.");
                var mediaHeaders=new System.Collections.Generic.List<StreamMetadata.HeaderValue>();
                if(root.TryGetProperty("headers",out var mediaEntries) &&
                   mediaEntries.ValueKind==JsonValueKind.Array)
                {
                    if(mediaEntries.GetArrayLength()>12)
                        throw new InvalidDataException("Too many headers.");
                    foreach(var entry in mediaEntries.EnumerateArray())
                    {
                        var name=Get(entry,"name",40);
                        var value=Get(entry,"value",2048);
                        if(!AllowedHeaders.Contains(name,StringComparer.OrdinalIgnoreCase) ||
                           value.IndexOfAny(['\r','\n','\0'])>=0)
                            throw new InvalidDataException("Unsupported header.");
                        mediaHeaders.Add(new StreamMetadata.HeaderValue(name,value));
                    }
                }
                Reply(mediaKind=="HLS"
                    ?StreamMetadata.ProbeHls(target,mediaHeaders)
                    :MediaMetadata.Probe(target,mediaKind,mediaHeaders,AppContext.BaseDirectory));
                return 0;
            }
            if (Get(root, "type", 24) == "PING")
            {
                var installedExe=Path.Combine(InstallRoot(),
                    "UniversalVideoDownloader.exe");
                var folder=Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "UniversalVideoDownloader", "NativeInboxV2");
                bool inboxWritable=false;
                try
                {
                    Directory.CreateDirectory(folder);
                    var probe=Path.Combine(folder,".uvd-connectivity-"+Guid.NewGuid().ToString("N"));
                    File.WriteAllText(probe,"ok");
                    File.Delete(probe);
                    inboxWritable=true;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                Reply(new { ok = true, nativeHost = "com.uvd.bridge", version = 11,
                    appPresent=File.Exists(installedExe), inboxWritable, pageSupported=true,
                    qualitySelectionSupported=true, pageMetadataSupported=true,
                    streamMetadataSupported=true, mediaMetadataSupported=true,
                    reviewBeforeDownload=true, renditionHandoffSupported=true });
                return 0;
            }
            if (Get(root, "type", 24) != "DOWNLOAD" || root.GetProperty("version").GetInt32() != 1)
                throw new InvalidDataException("Unsupported command.");
            var url = Get(root, "url", 8192);
            if (!NetworkTargetGuard.IsAllowedHttpUrl(url, out _))
                throw new InvalidDataException("Expected a public HTTP(S) media URL.");
            var kind = Get(root, "kind", 10);
            if (kind is not ("HLS" or "DASH" or "MSS" or "FILE" or "PAGE"))
                throw new InvalidDataException("Unsupported media kind.");
            var quality=root.TryGetProperty("maxQuality",out var q)?Get(root,"maxQuality",4):"best";
            if (quality is not ("best" or "4320" or "2880" or "2160" or "1440" or "1080" or "720" or "480" or "360" or "240"))
                throw new InvalidDataException("Unsupported quality.");
            int? selectedHeight=null;
            if(root.TryGetProperty("selectedHeight",out var selectedHeightNode) &&
               selectedHeightNode.ValueKind==JsonValueKind.Number &&
               selectedHeightNode.TryGetInt32(out var parsedSelectedHeight))
            {
                if(parsedSelectedHeight is <144 or >4320)
                    throw new InvalidDataException("Invalid selected height.");
                selectedHeight=parsedSelectedHeight;
            }
            if (kind=="MSS" && quality!="best")
                throw new InvalidDataException("MSS requires best/exact URL.");
            // FILE may carry the extension's selected-height hint for review UI.
            // The URL itself remains the exact rendition and is never rewritten.
            string? suggestedTitle=null;
            if(root.TryGetProperty("title",out var titleElement) &&
               titleElement.ValueKind==JsonValueKind.String)
            {
                suggestedTitle=(titleElement.GetString()??string.Empty)
                    .Replace('\r',' ').Replace('\n',' ').Trim();
                if(suggestedTitle.Length>180)suggestedTitle=suggestedTitle[..180];
                if(suggestedTitle.IndexOf('\0')>=0)
                    throw new InvalidDataException("Invalid title.");
            }
            string? pageUrl=null;
            if(root.TryGetProperty("pageUrl",out var pageElement) &&
               pageElement.ValueKind==JsonValueKind.String)
            {
                pageUrl=pageElement.GetString();
                if(!NetworkTargetGuard.IsAllowedHttpUrl(pageUrl,out _))
                    throw new InvalidDataException("Invalid or non-public page fallback URL.");
            }
            var headers = new System.Collections.Generic.List<Header>();
            if (root.TryGetProperty("headers", out var entries) && entries.ValueKind == JsonValueKind.Array)
            {
                if (entries.GetArrayLength() > 12) throw new InvalidDataException("Too many headers.");
                foreach (var entry in entries.EnumerateArray())
                {
                    var name = Get(entry, "name", 40);
                    var value = Get(entry, "value", 2048);
                    if (!AllowedHeaders.Contains(name, StringComparer.OrdinalIgnoreCase) ||
                        value.IndexOfAny(['\r','\n','\0']) >= 0)
                        throw new InvalidDataException("Unsupported header.");
                    headers.Add(new Header(name, value));
                }
            }
            var renditions = new System.Collections.Generic.List<Rendition>();
            if(root.TryGetProperty("renditions",out var renditionEntries) &&
               renditionEntries.ValueKind==JsonValueKind.Array)
            {
                if(renditionEntries.GetArrayLength()>12)
                    throw new InvalidDataException("Too many renditions.");
                foreach(var renditionEntry in renditionEntries.EnumerateArray())
                {
                    if(!renditionEntry.TryGetProperty("height",out var hNode) ||
                       !hNode.TryGetInt32(out var height) ||
                       height is <144 or >4320)
                        throw new InvalidDataException("Invalid rendition height.");
                    var renditionUrl=Get(renditionEntry,"url",8192);
                    if(!NetworkTargetGuard.IsAllowedHttpUrl(renditionUrl,out _))
                        throw new InvalidDataException("Invalid or non-public rendition URL.");
                    var renditionKind=Get(renditionEntry,"kind",10);
                    if(renditionKind is not ("HLS" or "DASH" or "FILE"))
                        throw new InvalidDataException("Invalid rendition kind.");
                    var renditionHeaders=new System.Collections.Generic.List<Header>();
                    if(renditionEntry.TryGetProperty("headers",out var renditionHeaderEntries) &&
                       renditionHeaderEntries.ValueKind==JsonValueKind.Array)
                    {
                        if(renditionHeaderEntries.GetArrayLength()>12)
                            throw new InvalidDataException("Too many rendition headers.");
                        foreach(var headerEntry in renditionHeaderEntries.EnumerateArray())
                        {
                            var name=Get(headerEntry,"name",40);
                            var value=Get(headerEntry,"value",2048);
                            if(!AllowedHeaders.Contains(name,StringComparer.OrdinalIgnoreCase) ||
                               value.IndexOfAny(['\r','\n','\0'])>=0)
                                throw new InvalidDataException("Unsupported rendition header.");
                            renditionHeaders.Add(new Header(name,value));
                        }
                    }
                    if(renditions.Any(x=>x.height==height))
                        continue;
                    renditions.Add(new Rendition(height,renditionUrl,renditionKind,
                        renditionHeaders.ToArray()));
                }
            }

            var installRoot = InstallRoot();
            var exe = Path.Combine(installRoot, "UniversalVideoDownloader.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("UVD executable is not installed next to native host.");
            var inbox = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UniversalVideoDownloader", "NativeInboxV2");
            Directory.CreateDirectory(inbox);
            var id = Guid.NewGuid().ToString("N");
            object? handoffMetadata=null;
            if(kind!="PAGE")
            {
                try
                {
                    var metadataHeaders=headers
                        .Select(h=>new StreamMetadata.HeaderValue(h.name,h.value)).ToArray();
                    handoffMetadata=kind=="HLS"
                        ?StreamMetadata.ProbeHls(url,metadataHeaders)
                        :MediaMetadata.Probe(url,kind,metadataHeaders,AppContext.BaseDirectory);
                }
                catch
                {
                    // Handoff remains valid even when metadata probing is unavailable.
                }
            }
            var data = JsonSerializer.Serialize(new DownloadRequest(
                1, url, kind, headers.ToArray(), quality, selectedHeight,
                pageUrl, suggestedTitle,
                renditions.OrderByDescending(x=>x.height).ToArray(),
                handoffMetadata));
            var tmp = Path.Combine(inbox, id + ".tmp");
            File.WriteAllText(tmp, data, new UTF8Encoding(false));
            File.Move(tmp, Path.Combine(inbox, id + ".json"));
            try { File.Delete(exe+".locked-old"); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            if (!IsCurrentAppRunning())
            {
                try
                {
                    // Launch the GUI detached from Native Messaging stdio.
                    // With UseShellExecute=false the GUI inherits Chrome's native
                    // stdin/stdout handles, so sendNativeMessage can receive a frame
                    // but never observe the one-shot host connection closing.
                    Process.Start(new ProcessStartInfo(exe)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = installRoot
                    });
                }
                catch
                {
                    // The queue remains available for a later manual UVD launch.
                    Reply(new { ok = true, queued = true, launched = false,
                        reviewRequired = true,
                        note = "Queued for review; please open UVD manually." });
                    return 0;
                }
            }
            Reply(new { ok = true, queued = true, launched = true,
                reviewRequired = true });
            return 0;
        }
        catch (Exception ex)
        {
            // Error feedback deliberately contains no URL or header values.
            Reply(new { ok = false, error = ex is FileNotFoundException
                ? "Install UVD Native Host inside the UVD Runtime folder."
                : "Local handoff failed: " + ex.GetType().Name });
            // Native Messaging transport completed successfully; operation-level
            // failure is encoded in JSON. Exit 0 so Chrome does not discard the
            // framed error and replace it with a generic "host has exited".
            return 0;
        }
    }
    const string NativeInboxInstanceMarkerName = @"Local\UVD.NativeInboxV2.App";

    static bool IsCurrentAppRunning()
    {
        try
        {
            using var marker = Mutex.OpenExisting(NativeInboxInstanceMarkerName);
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // The marker exists but is not openable by this process.
            return true;
        }
    }

    static string Get(JsonElement parent, string key, int max)
    {
        if (!parent.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Missing field.");
        var s = value.GetString() ?? "";
        if (s.Length < 1 || s.Length > max) throw new InvalidDataException("Invalid field length.");
        return s;
    }
    static void Reply(object value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        Span<byte> prefix = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, bytes.Length);
        using var output = Console.OpenStandardOutput();
        output.Write(prefix);
        output.Write(bytes);
        output.Flush();
    }
    record Header(string name, string value);
    record Rendition(int height, string url, string kind, Header[] headers);
    record DownloadRequest(int version, string url, string kind, Header[] headers,
        string maxQuality, int? selectedHeight, string? pageUrl, string? title,
        Rendition[] renditions, object? metadata);
}
