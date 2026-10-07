#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ComboBox = System.Windows.Controls.ComboBox;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ListBox = System.Windows.Controls.ListBox;
using Orientation = System.Windows.Controls.Orientation;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using N_m3u8DL_RE_GUI.Core.Capture;

namespace N_m3u8DL_RE_GUI;

/// <summary>
/// Embedded browser for websites without a supported yt-dlp extractor. It listens to
/// the browser's actual media requests, not arbitrary Referer-looking URLs.
/// Nothing is downloaded automatically until a high-confidence master manifest
/// is observed or the user explicitly chooses a candidate.
/// </summary>
public sealed partial class BrowserCaptureWindow : Window
{
    private readonly WebView2 _browser = new();
    private readonly ListBox _list = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _autoplayStatus = new() { Text = "Autoplay: waiting for the player…" };
    private readonly List<CoreWebView2Frame> _frames = new();
    private string _autoplayScript = string.Empty;
    private string _qualityScript = string.Empty;
    private bool _reportedPlayback;
    private readonly DateTime _openedUtc = DateTime.UtcNow;
    private DateTime _qualityChangedUtc = DateTime.MinValue;
    private int? _selectedQualityHeight;
    private readonly Dictionary<string, DateTime> _firstObservedUtc = new(StringComparer.Ordinal);
    private readonly TextBlock _metadata = new()
    {
        Text = "Choose a detected media source to inspect its quality and size.",
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0,5,0,5)
    };
    private readonly ComboBox _qualities = new() { Width=205, Margin=new Thickness(6,0,6,0) };
    private int _probeSequence;
    private CancellationTokenSource? _captureProbeCts;
    private CaptureMediaProbe.Result? _lastMetadata;
    private YtDlpPageMetadata? _lastPageMetadata;
    public int? SelectedHeight { get; private set; }
    public string? SelectedTitle { get; private set; }
    private readonly Dictionary<string, CapturedRequest> _found = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _selectionTimer;
    private readonly string _pageUrl;
    private bool _completed;
    public CapturedRequest? SelectedRequest { get; private set; }

    public BrowserCaptureWindow(string pageUrl)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("Browser capture needs an HTTP(S) page URL.", nameof(pageUrl));

        _pageUrl = pageUrl;
        _pageCandidate = BrowserPageCandidate.Create(pageUrl);
        if (_pageCandidate is not null)
            _found[_pageCandidate.Url] = _pageCandidate;
        Title = "Automatic media detection · Browser";
        Width = 1100; Height = 820; MinWidth = 800; MinHeight = 570;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(10) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(310) });
        var hint = new TextBlock
        {
            Text = "Video autoplay starts muted when the page opens (including supported embedded players). " +
                   "If the site requires a click or login, use the player below. HLS / DASH / MP4 requests " +
                   "are captured locally; Cookie and Authorization are not auto-forwarded.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,8)
        };
        root.Children.Add(hint);
        Grid.SetRow(_browser, 1); root.Children.Add(_browser);
        var bottom = new DockPanel { Margin = new Thickness(0,8,0,0) };
        Grid.SetRow(bottom, 2); root.Children.Add(bottom);
        var controls = new WrapPanel { Orientation = Orientation.Horizontal,
                                        Margin = new Thickness(0,6,0,0) };
        DockPanel.SetDock(controls, Dock.Bottom);
        bottom.Children.Add(controls);
        controls.Children.Add(new TextBlock { Text="화질:", VerticalAlignment=VerticalAlignment.Center });
        _qualities.Items.Add(new QualityChoice(0, "원본 / 검증된 최고 화질"));
        _qualities.SelectedIndex=0;
        controls.Children.Add(_qualities);
        var retry = new Button { Content = "재생 다시 시도", Margin = new Thickness(8,0,4,0),
                                 Padding = new Thickness(10,5,10,5),
                                 ToolTip = "현재 페이지와 iframe에서 음소거 재생을 다시 시도합니다." };
        retry.Click += async (_, _) => await RetryPlaybackAsync();
        controls.Children.Add(retry);
        var scan = new Button { Content = "다시 탐색", Margin = new Thickness(4,0,4,0),
                                Padding = new Thickness(10,5,10,5),
                                ToolTip = "현재 페이지와 포함된 프레임에서 영상 후보를 다시 탐색합니다." };
        scan.Click += async (_, _) => await ScanOtherVideosAsync();
        controls.Children.Add(scan);
        var recheck = new Button { Content = "정보 다시 확인", Margin = new Thickness(4,0,4,0),
                                   Padding = new Thickness(10,5,10,5),
                                   ToolTip = "선택한 영상의 화질과 메타데이터를 다시 확인합니다." };
        recheck.Click += async (_, _) => await ProbeSelectionAsync();
        controls.Children.Add(recheck);
        var use = new Button { Content = "선택한 영상 UVD로 가져오기", Margin = new Thickness(12,0,4,0),
                               Padding = new Thickness(14,5,14,5) };
        use.Click += (_, _) => AcceptSelected();
        controls.Children.Add(use);
        var cancel = new Button { Content = "취소",
                                  Margin = new Thickness(4,0,0,0), Padding = new Thickness(14,5,14,5),
                                  ToolTip = "Auto Detect 창을 닫습니다." };
        cancel.Click += (_, _) => { _completed = true; DialogResult = false; };
        controls.Children.Add(cancel);
        var additionalTools = BuildAdditionalTools();
        DockPanel.SetDock(additionalTools, Dock.Bottom);
        bottom.Children.Add(additionalTools);
        DockPanel.SetDock(_status, Dock.Top);
        _status.Text = "Opening webpage…";
        _status.Margin = new Thickness(0,0,0,4);
        bottom.Children.Add(_status);
        DockPanel.SetDock(_autoplayStatus, Dock.Top);
        _autoplayStatus.Margin = new Thickness(0,0,0,4);
        bottom.Children.Add(_autoplayStatus);
        _list.DisplayMemberPath = "Label";
        _list.SelectionChanged += (_, _) =>
        {
            _selectionTimer?.Stop();
            _ = ProbeSelectionAsync();
        };
        _qualities.SelectionChanged += (_, _) => UpdateSelectedQualityMetadata();
        DockPanel.SetDock(_metadata,Dock.Bottom);
        bottom.Children.Add(_metadata);
        bottom.Children.Add(_list);
        Content = root;

        _selectionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.7) };
        // Capturing is not authorization to download. Always wait for user choice.
        _selectionTimer.Tick += (_, _) =>
        {
            _selectionTimer.Stop();
            if (!_completed)
                _status.Text = "Media detected. Review quality and metadata, then confirm your selection.";
        };
        if (_pageCandidate is not null)
            RefreshCandidates();
        Loaded += async (_, _) => await InitializeBrowserAsync();
        Closed += (_, _) =>
        {
            _completed = true;
            _captureProbeCts?.Cancel();
            _selectionTimer.Stop();
            _frames.Clear();
            _browser.Dispose();
            _found.Clear(); // Expiring signed URLs are not persisted after handoff.
        };
    }

    private async System.Threading.Tasks.Task InitializeBrowserAsync()
    {
        try
        {
            // Keep browser state outside the distributable application directory.
            // It remains isolated from the user's Chrome/Edge profiles.
            var profileDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "UniversalVideoDownloader", "WebView2");
            Directory.CreateDirectory(profileDir);
            var options = new CoreWebView2EnvironmentOptions
            {
                // This environment is created only after the user explicitly presses GO
                // or Auto detect. Playback is always muted to avoid surprise audio.
                AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required"
            };
            var environment = await CoreWebView2Environment.CreateAsync(null, profileDir, options);
            await _browser.EnsureCoreWebView2Async(environment);
            var core = _browser.CoreWebView2;
            core.IsMuted = true;
            core.Settings.IsWebMessageEnabled = true;
            core.WebMessageReceived += (_, e) =>
            {
                try { HandleBrowserMessage(e.TryGetWebMessageAsString(), e.Source); }
                catch (ArgumentException) { }
            };
            core.FrameCreated += (_, e) => AttachFrame(e.Frame);
            // Register before navigation: document-start injection also covers dynamically
            // navigated video iframes supported by WebView2. A bounded observer handles
            // late-mounted HTML5 video and known player play overlays.
            using (var resource = typeof(BrowserCaptureWindow).Assembly
                .GetManifestResourceStream("UniversalVideoDownloader.BrowserAutoplay")
                ?? throw new InvalidOperationException("Embedded autoplay script is missing."))
            using (var reader = new StreamReader(resource))
                _autoplayScript = await reader.ReadToEndAsync();
            using (var resource = typeof(BrowserCaptureWindow).Assembly
                .GetManifestResourceStream("UniversalVideoDownloader.BrowserQuality")
                ?? throw new InvalidOperationException("Embedded quality script is missing."))
            using (var reader = new StreamReader(resource))
                _qualityScript = await reader.ReadToEndAsync();
            using (var resource = typeof(BrowserCaptureWindow).Assembly
                .GetManifestResourceStream("UniversalVideoDownloader.BrowserNaver")
                ?? throw new InvalidOperationException("Naver video discovery script is missing."))
            using (var reader = new StreamReader(resource))
                _naverScript = await reader.ReadToEndAsync();
            await core.AddScriptToExecuteOnDocumentCreatedAsync(_autoplayScript);
            await core.AddScriptToExecuteOnDocumentCreatedAsync(_qualityScript);
            await core.AddScriptToExecuteOnDocumentCreatedAsync(_naverScript);
            // Only a successful network response can become a download option.
            // Previously WebResourceRequested added every URL before the response:
            // an HTTP 404 playlist was displayed and handed off as if playable.
            core.WebResourceResponseReceived += (_, e) =>
            {
                var status = e.Response.StatusCode;
                if (MediaRequestSelector.IsPermanentMissingStatus(status))
                {
                    RejectCandidate(e.Request.Uri, status);
                    return;
                }
                if (status == 403 && _found.Count == 0 &&
                    Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var blocked) &&
                    blocked.AbsolutePath.Contains("/embed/", StringComparison.OrdinalIgnoreCase))
                {
                    _status.Text = "Embedded player returned HTTP 403 in this separate browser profile. " +
                        "Open the original page in your existing Chrome session, play it, then use UVD Source Capture > Scan.";
                }
                if (!MediaRequestSelector.IsSuccessfulMediaStatus(status)) return;
                string? type = null;
                try { type = e.Response.Headers.GetHeader("Content-Type"); }
                catch (ArgumentException) { }
                catch (System.Runtime.InteropServices.COMException) { }
                Observe(e.Request.Uri, e.Request.Headers, type);
            };
            core.NavigationCompleted += async (_, e) =>
            {
                if (!e.IsSuccess && _found.Count == 0)
                {
                    _status.Text = "Page navigation failed. Try the Chrome extension if this page needs your existing browser session.";
                    return;
                }
                // The document-start hook may run before a site's player initializes.
                // Kick it again when the main frame has finished navigating.
                if (e.IsSuccess && !_completed)
                    try
                    {
                        await core.ExecuteScriptAsync(_autoplayScript);
                        await core.ExecuteScriptAsync(_qualityScript);
                        await core.ExecuteScriptAsync(_naverScript);
                    }
                    catch (System.Runtime.InteropServices.COMException) { }
            };
            core.Navigate(_pageUrl);
            _status.Text = "Autoplay (muted) started. Watching HLS / DASH / MP4; use Play manually only if the site blocks autoplay.";
        }
        catch (Exception ex)
        {
            _status.Text = "Embedded browser unavailable: " + ex.GetType().Name +
                           ". Install the Microsoft Edge WebView2 Runtime or use the existing Chrome extension.";
        }
    }

    private void AttachFrame(CoreWebView2Frame frame)
    {
        _frames.Add(frame);
        if (!_reportedPlayback)
            _autoplayStatus.Text = $"Autoplay: scanning embedded players ({_frames.Count} frame(s))…";
        frame.WebMessageReceived += (_, e) =>
        {
            try { HandleBrowserMessage(e.TryGetWebMessageAsString(), e.Source); }
            catch (ArgumentException) { }
        };
        frame.FrameCreated += (_, e) => AttachFrame(e.Frame);
        frame.Destroyed += (_, _) => _frames.Remove(frame);
        frame.NavigationCompleted += async (_, e) =>
        {
            if (!e.IsSuccess || _completed || string.IsNullOrEmpty(_autoplayScript)) return;
            // Frame-specific ExecuteScriptAsync reaches cross-origin iframe documents
            // that the main document cannot access via window.frames.
            try
            {
                await frame.ExecuteScriptAsync(_autoplayScript);
                await frame.ExecuteScriptAsync(_qualityScript);
                await frame.ExecuteScriptAsync(_naverScript);
            }
            catch (System.Runtime.InteropServices.COMException) { }
            catch (InvalidOperationException) { }
        };
    }

    private void ScheduleSelection(TimeSpan delay)
    {
        _selectionTimer.Stop();
        _selectionTimer.Interval = delay;
        _selectionTimer.Start();
    }

    private void HandlePlayerFeedback(string? feedback)
    {
        if (_completed || string.IsNullOrWhiteSpace(feedback)) return;
        if (feedback.Equals("uvd-quality:unavailable", StringComparison.Ordinal))
        {
            ScheduleSelection(TimeSpan.FromSeconds(1));
            return;
        }
        if (feedback.StartsWith("uvd-quality:selected:", StringComparison.Ordinal))
        {
            var raw = feedback["uvd-quality:selected:".Length..];
            if (int.TryParse(raw, out var height) && height is >= 240 and <= 4320 &&
                (!_selectedQualityHeight.HasValue || height > _selectedQualityHeight.Value))
            {
                _selectedQualityHeight = height;
                    _qualityChangedUtc = DateTime.UtcNow;
                _status.Text = $"Switching player to {height}p; waiting for new stream…";
                ScheduleSelection(TimeSpan.FromSeconds(3));
            }
            return;
        }
        ShowAutoplayFeedback(feedback);
    }

    private void ShowAutoplayFeedback(string? feedback)
    {
        if (_completed || string.IsNullOrEmpty(feedback) ||
            !feedback.StartsWith("uvd-autoplay:", StringComparison.Ordinal)) return;
        var code = feedback["uvd-autoplay:".Length..];
        if (code.StartsWith("play-failed:", StringComparison.Ordinal))
        {
            if (!_reportedPlayback)
                _autoplayStatus.Text = "Player refused automatic playback (" +
                    code["play-failed:".Length..] + "). Stream detection may still work.";
            return;
        }
        switch (code)
        {
            case "playing":
                _reportedPlayback = true;
                _autoplayStatus.Text = "Autoplay: video is playing (muted).";
                break;
            case "blocked":
                if (!_reportedPlayback)
                    _autoplayStatus.Text = "Autoplay blocked by player/browser. Click Play on the video, or Retry Play.";
                break;
            case "attempt":
            case "player-control":
                if (!_reportedPlayback)
                    _autoplayStatus.Text = "Autoplay: trying the player, including embedded frames…";
                break;
            case "play-failed":
                if (!_reportedPlayback)
                    _autoplayStatus.Text = "Player refused automatic playback. Stream detection can still work; use manual Play or Retry Play.";
                break;
            case "no-video":
                if (!_reportedPlayback && _frames.Count == 0)
                    _autoplayStatus.Text = "No HTML5 video yet. Waiting for embedded player or network capture…";
                break;
        }
    }

    private async System.Threading.Tasks.Task RetryPlaybackAsync()
    {
        if (_completed || string.IsNullOrWhiteSpace(_autoplayScript) ||
            _browser.CoreWebView2 is not { } core) return;
        _reportedPlayback = false;
        _autoplayStatus.Text = "Autoplay: retrying muted playback in the page and embedded frames…";
        const string retryScript = "window.__uvdAutoplayReset?.();";
        try { await core.ExecuteScriptAsync(_autoplayScript); await core.ExecuteScriptAsync(retryScript); }
        catch (System.Runtime.InteropServices.COMException) { }
        foreach (var frame in _frames.ToArray())
            try
            {
                await frame.ExecuteScriptAsync(_autoplayScript);
                await frame.ExecuteScriptAsync(retryScript);
            }
            catch (System.Runtime.InteropServices.COMException) { }
            catch (InvalidOperationException) { }
    }

    private void RejectCandidate(string url, int httpStatus)
    {
        if (_completed) return;
        // Drop exactly this failed rendition. Unrelated sibling videos remain.
        _firstObservedUtc.Remove(url);
        if (_found.Remove(url)) RefreshCandidates();
        if (MediaRequestSelector.Create(url, null) is null) return;
        _metadata.Text = "Source unavailable (HTTP " + httpStatus + "). It was removed from the selectable list.";
        _status.Text = "An observed media URL returned HTTP " + httpStatus +
            ". Reload the original page in Chrome and use UVD Source Capture > Scan to obtain a fresh source.";
    }

    private void Observe(string url, CoreWebView2HttpRequestHeaders originalHeaders, string? contentType)
    {
        if (_completed || _capturePaused) return;
        CapturedRequest? captured;
        try
        {
            // Only safe, non-credential headers survive MediaRequestSelector.Create.
            var headers = new List<KeyValuePair<string, string>>();
            foreach (var h in originalHeaders)
                headers.Add(h);
            captured = MediaRequestSelector.Create(url, headers, contentType);
        }
        catch { return; }
        if (captured is null || MediaRequestSelector.Score(captured) <= 0) return;

        if (_found.TryGetValue(captured.Url, out var previous))
        {
            if (captured.Headers.Count <= previous.Headers.Count) return;
        }
        if (!_found.ContainsKey(captured.Url))
            _firstObservedUtc[captured.Url] = DateTime.UtcNow;
        _found[captured.Url] = captured;
        RefreshCandidates();
        if (captured.Kind is CapturedStreamKind.Hls or CapturedStreamKind.Dash or CapturedStreamKind.Mss &&
            true)
        {
            ScheduleSelection(MediaRequestSelector.IsHighConfidenceMaster(captured)
                ? TimeSpan.FromSeconds(1.7) : TimeSpan.FromSeconds(3));
        }
    }

    private void RefreshCandidates()
    {
        var selected = (_list.SelectedItem as CaptureRow)?.Request.Url;
        var ordered = _found.Values
            .Where(r => !_naverByUrl.ContainsKey(r.Url) || _naverBestUrls.Contains(r.Url))
            .OrderBy(r => _naverByUrl.TryGetValue(r.Url, out var entry) ? entry.Number : int.MaxValue)
            .ThenByDescending(MediaRequestSelector.Score).Take(45)
            .Select((r,index) =>
            {
                var uri = new Uri(r.Url);
                // Two qualities can have the same CDN path but distinct signed
                // queries. Number rows without exposing their query tokens.
                var advertised=CaptureMediaProbe.HeightFromPath(uri.AbsolutePath);
                var quality=advertised.HasValue ? $" · site label {advertised}p" : " · quality unverified";
                if (r.Kind == CapturedStreamKind.Page)
                    return new CaptureRow(r, "YouTube 페이지 영상 · UVD 페이지 추출 · 영상+음성");
                if (_naverByUrl.TryGetValue(r.Url, out var video))
                    return new CaptureRow(r, $"Naver video #{video.Number} · " +
                        (video.Variant.Height > 0 ? video.Variant.Height + "p" : "source") +
                        " · choose quality below");
                return new CaptureRow(r,$"#{index+1}  {r.Kind,-5}{quality} · {uri.Host}{uri.AbsolutePath}");
            }).ToList();
        _list.ItemsSource = ordered;
        _list.SelectedItem = ordered.FirstOrDefault(r => r.Request.Url == selected) ??
                             ordered.FirstOrDefault();
        _status.Text = ordered.Count == 0
            ? "아직 영상 후보가 없습니다. 영상을 재생하거나 다시 탐색하세요."
            : $"{ordered.Count}개 영상 후보를 확인했습니다. 화질과 정보를 확인한 뒤 선택하세요.";
        if (!_reportedPlayback && _found.Values.Any(x => x.Kind is
            CapturedStreamKind.Hls or CapturedStreamKind.Dash or CapturedStreamKind.Mss))
            _autoplayStatus.Text = "Stream detected: downloading can start even if the preview is paused. Retry Play to watch it.";
    }

    private void AcceptSelected()
    {
        if (_list.SelectedItem is CaptureRow row)
        {
            var choice = _qualities.SelectedItem as QualityChoice;
            SelectedHeight = choice?.Height is > 0 ? choice.Height : null;
            var request = row.Request;
            if (choice?.Url is { } variantUrl &&
                _naverByUrl.TryGetValue(row.Request.Url, out var selectedVideo) &&
                _naverByUrl.TryGetValue(variantUrl, out var variant) && variant.Vid == selectedVideo.Vid)
                request = MediaRequestSelector.Create(variantUrl,
                    row.Request.Headers.Select(h => new KeyValuePair<string,string>(h.Name, h.Value)), "video/mp4") ?? row.Request;
            Accept(request);
        }
        else _status.Text = "영상 후보가 없습니다. 영상을 재생하거나 다시 탐색하세요.";
    }

    private sealed record QualityChoice(int Height,string Label,string? Url=null)
    {
        public override string ToString()=>Label;
    }

    private async Task ProbeSelectionAsync()
    {
        _captureProbeCts?.Cancel();
        _captureProbeCts?.Dispose();
        var cts=new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(27));
        _captureProbeCts=cts;
        var sequence=++_probeSequence;
        var selected=(_list.SelectedItem as CaptureRow)?.Request;
        _lastMetadata=null;
        _lastPageMetadata=null;
        _qualities.Items.Clear();
        _qualities.Items.Add(new QualityChoice(0,"Original / highest available"));
        _qualities.SelectedIndex=0;
        _metadata.Text="Checking media metadata (no download is started)…";
        if(selected is null)return;
        if (selected.Kind == CapturedStreamKind.Page)
        {
            await ProbePageSelectionAsync(selected, sequence, cts);
            return;
        }
        if (_naverByUrl.TryGetValue(selected.Url, out var naver) &&
            _naverGroups.TryGetValue(naver.Vid, out var variants))
        {
            _qualities.Items.Clear();
            _qualities.Items.Add(new QualityChoice(0, "Highest available", selected.Url));
            foreach (var variant in variants)
                _qualities.Items.Add(new QualityChoice(variant.Height,
                    (variant.Height > 0 ? variant.Height + "p" : "Unspecified") +
                    (variant.Bytes is > 0 ? " · " + (variant.Bytes.Value / 1048576d).ToString("N1") + " MiB" : ""),
                    variant.Url));
            _qualities.SelectedIndex = 0;
            try
            {
                _lastMetadata = await CaptureMediaProbe.InspectAsync(selected, cts.Token);
            }
            catch (OperationCanceledException) when (!cts.IsCancellationRequested)
            {
            }
            UpdateNaverQualityMetadata();
            _status.Text = "Naver video #" + naver.Number + ": choose its quality, then confirm. No automatic download.";
            return;
        }
        try
        {
            var result=await CaptureMediaProbe.InspectAsync(selected,cts.Token);
            if(_completed||cts.IsCancellationRequested||sequence!=_probeSequence)return;
            _lastMetadata=result;
            foreach(var h in result.Heights.Where(h=>h>0).Distinct().OrderByDescending(h=>h))
                _qualities.Items.Add(new QualityChoice(h,h+"p (observed / playlist)"));
            var bytes=result.Bytes.HasValue
                ?(result.Approximate?"≈ ":"")+ (result.Bytes.Value/1048576d).ToString("N1")+" MiB"
                :"서버 미제공";
            var duration=result.Duration.HasValue
                ?TimeSpan.FromSeconds(result.Duration.Value).ToString(@"hh\:mm\:ss"):"컨테이너 미제공";
            var quality=result.Height.HasValue?result.Height+"p":"원본 해상도 · 미표기";
            var resolution=result.Width is >0 && result.Height is >0
                ?result.Width+"×"+result.Height:quality;
            var format=string.IsNullOrWhiteSpace(result.Format)?"Media stream":result.Format;
            var codec=string.IsNullOrWhiteSpace(result.Codec)?"코덱 미표기":result.Codec;
            var fps=result.Fps is >0?result.Fps.Value.ToString("0.##")+" FPS":"FPS 미표기";
            _metadata.Text="Quality: "+quality+
                "  ·  Resolution: "+resolution+
                "  ·  Duration: "+duration+
                "  ·  Total size: "+bytes+
                "\nFormat: "+format+"  ·  Codec: "+codec+"  ·  FPS: "+fps+
                "\n"+result.Note;
            _status.Text="Review the detected source and click Use selected media. Download never starts automatically.";
        }
        catch(OperationCanceledException)
        {
            if(sequence==_probeSequence && !_completed)
                _metadata.Text="Metadata lookup timed out; original source remains selectable.";
        }
        catch(Exception){if(sequence==_probeSequence)_metadata.Text="Metadata unavailable; select the source manually.";}
    }
    private void Accept(CapturedRequest request)
    {
        if (_completed) return;
        _completed = true; _selectionTimer.Stop();
        SelectedRequest = request;
        DialogResult = true;
    }

    private sealed record CaptureRow(CapturedRequest Request, string Label);
}
