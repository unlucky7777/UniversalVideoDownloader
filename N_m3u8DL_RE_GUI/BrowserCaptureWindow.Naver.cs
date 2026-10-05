#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using N_m3u8DL_RE_GUI.Core.Capture;

namespace N_m3u8DL_RE_GUI;

public sealed partial class BrowserCaptureWindow
{
    private string _naverScript = string.Empty;
    private readonly HashSet<string> _naverRequested = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<NaverVideoCatalog.Variant>> _naverGroups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NaverChoice> _naverByUrl = new(StringComparer.Ordinal);
    private readonly HashSet<string> _naverBestUrls = new(StringComparer.Ordinal);
    private sealed record NaverChoice(int Number, string Vid, NaverVideoCatalog.Variant Variant);

    private void HandleBrowserMessage(string? feedback, string? source)
    {
        if (_completed || string.IsNullOrWhiteSpace(feedback)) return;
        const string prefix = "uvd-naver-video:";
        if (!feedback.StartsWith(prefix, StringComparison.Ordinal))
        {
            HandlePlayerFeedback(feedback);
            return;
        }
        if (!Uri.TryCreate(source, UriKind.Absolute, out var from) ||
            !new[] { "cafe.naver.com", "m.cafe.naver.com", "blog.naver.com", "m.blog.naver.com" }
                .Contains(from.Host, StringComparer.OrdinalIgnoreCase) ||
            !Uri.TryCreate(_pageUrl, UriKind.Absolute, out var page) ||
            !page.Host.EndsWith(".naver.com", StringComparison.OrdinalIgnoreCase))
            return;
        _ = AddNaverModuleAsync(feedback[prefix.Length..]);
    }

    private async Task AddNaverModuleAsync(string json)
    {
        if (json.Length > 1024) return;
        string vid, key;
        try
        {
            using var document = JsonDocument.Parse(json);
            vid = document.RootElement.GetProperty("vid").GetString() ?? "";
            key = document.RootElement.GetProperty("key").GetString() ?? "";
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        { return; }
        if (!Regex.IsMatch(vid, @"^[A-Za-z0-9_-]{8,96}$") ||
            !Regex.IsMatch(key, @"^[A-Za-z0-9_-]{8,512}$") ||
            _naverRequested.Count >= 10)
            return;
        var identity = vid + ":" + key;
        if (!_naverRequested.Add(identity)) return;
        try
        {
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(13) };
            var api = "https://apis.naver.com/rmcnmv/rmcnmv/vod/play/v2.0/" +
                Uri.EscapeDataString(vid) + "?key=" + Uri.EscapeDataString(key);
            using var request = new HttpRequestMessage(HttpMethod.Get, api);
            request.Headers.Referrer = new Uri("https://cafe.naver.com/");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(13));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 1048576)
            {
                _naverRequested.Remove(identity);
                return;
            }
            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
            var buffer = new byte[1048577];
            var count = 0;
            while (count < buffer.Length)
            {
                var read = await input.ReadAsync(buffer.AsMemory(count), timeout.Token);
                if (read == 0) break;
                count += read;
            }
            if (count > 1048576) { _naverRequested.Remove(identity); return; }
            var variants = NaverVideoCatalog.Parse(Encoding.UTF8.GetString(buffer, 0, count));
            if (_completed || variants.Count == 0) { _naverRequested.Remove(identity); return; }
            var number = _naverGroups.Count + 1;
            if (!_naverGroups.ContainsKey(vid)) _naverGroups[vid] = variants.ToList();
            else return;
            foreach (var variant in variants)
                _naverByUrl[variant.Url] = new NaverChoice(number, vid, variant);
            var top = variants[0];
            var captured = MediaRequestSelector.Create(top.Url,
                new[] { new KeyValuePair<string, string>("Referer", _pageUrl) }, "video/mp4");
            if (captured is null) return;
            _naverBestUrls.Add(top.Url);
            _found[top.Url] = captured;
            RefreshCandidates();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.IO.IOException)
        {
            // Signed keys can expire. Allow an explicit Scan again; never log them.
            _naverRequested.Remove(identity);
        }
    }

    private void UpdateNaverQualityMetadata()
    {
        if (_list.SelectedItem is not CaptureRow row ||
            !_naverByUrl.TryGetValue(row.Request.Url, out var selected)) return;
        var chosen = _qualities.SelectedItem as QualityChoice;
        var info = chosen?.Url is { } chosenUrl && _naverByUrl.TryGetValue(chosenUrl, out var option)
            && option.Vid == selected.Vid ? option : selected;
        var size = info.Variant.Bytes is > 0
            ? (info.Variant.Bytes.Value / 1048576d).ToString("N1") + " MiB (Naver metadata)"
            : "Unavailable";
        _metadata.Text = "Naver video #" + info.Number +
            "  ·  Quality: " + (info.Variant.Height > 0 ? info.Variant.Height + "p" : "not advertised") +
            "  ·  Total size: " + size +
            "  ·  Current speed: measured after Start.\\n" +
            "Choose the quality above; each option uses its own exact signed MP4 URL.";
    }

    private async Task ScanOtherVideosAsync()
    {
        if (_completed || _browser.CoreWebView2 is not { } core) return;
        const string scan = @"(() => {
            window.__uvdNaverScan?.();
            const nodes = [...document.querySelectorAll('.se-component.se-video, .se-module-video, video, iframe[loading=""lazy""]')].slice(0, 10);
            nodes.forEach((n,i) => setTimeout(() => {
                try { n.scrollIntoView({block:'center', behavior:'instant'}); } catch(_) {}
                window.__uvdNaverScan?.();
            }, i * 350));
            window.__uvdAutoplayReset?.();
        })();";
        try
        {
            if (!string.IsNullOrEmpty(_autoplayScript)) await core.ExecuteScriptAsync(_autoplayScript);
            if (!string.IsNullOrEmpty(_qualityScript)) await core.ExecuteScriptAsync(_qualityScript);
            if (!string.IsNullOrEmpty(_naverScript)) await core.ExecuteScriptAsync(_naverScript);
            await core.ExecuteScriptAsync(scan);
        }
        catch (System.Runtime.InteropServices.COMException) { }
        foreach (var frame in _frames.ToArray())
            try
            {
                if (!string.IsNullOrEmpty(_autoplayScript)) await frame.ExecuteScriptAsync(_autoplayScript);
                if (!string.IsNullOrEmpty(_qualityScript)) await frame.ExecuteScriptAsync(_qualityScript);
                if (!string.IsNullOrEmpty(_naverScript)) await frame.ExecuteScriptAsync(_naverScript);
                await frame.ExecuteScriptAsync(scan);
            }
            catch (System.Runtime.InteropServices.COMException) { }
            catch (InvalidOperationException) { }
        RefreshCandidates();
        _status.Text = "현재 페이지와 포함된 프레임의 영상 후보를 다시 탐색했습니다.";
    }
}
