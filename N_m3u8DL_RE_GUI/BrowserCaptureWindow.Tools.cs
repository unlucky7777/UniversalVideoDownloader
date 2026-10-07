#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using Microsoft.Web.WebView2.Core;
using N_m3u8DL_RE_GUI.Core;
using N_m3u8DL_RE_GUI.Core.Capture;

namespace N_m3u8DL_RE_GUI;

public sealed partial class BrowserCaptureWindow
{
    private readonly CapturedRequest? _pageCandidate;
    private bool _capturePaused;
    private bool _rightClickEnabled;

    private Expander BuildAdditionalTools()
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 7, 0, 2) };

        Button Add(string text, string tooltip)
        {
            var button = new Button
            {
                Content = text,
                ToolTip = tooltip,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(9, 4, 9, 4)
            };
            panel.Children.Add(button);
            return button;
        }

        var uvd = Add("UVD 연결 확인", "UVD 본체와 번들 다운로드 도구 상태를 확인합니다.");
        uvd.Click += (_, _) => CheckUvdTools();

        var rightClick = Add("영상 우클릭 허용", "사이트가 영상 우클릭을 막을 때 현재 Auto Detect 브라우저에서만 허용합니다.");
        rightClick.Click += async (_, _) =>
        {
            _rightClickEnabled = !_rightClickEnabled;
            await ApplyVideoContextMenuAsync(_rightClickEnabled);
            rightClick.Content = _rightClickEnabled ? "영상 우클릭 허용 중 · 끄기" : "영상 우클릭 허용";
            _status.Text = _rightClickEnabled ? "이 Auto Detect 창에서 영상 우클릭을 허용했습니다." : "영상 우클릭 허용을 껐습니다.";
        };

        var copyRequest = Add("URL + 헤더 복사", "선택한 소스의 URL과 안전한 요청 헤더를 cURL 형식으로 복사합니다.");
        copyRequest.Click += (_, _) => CopySelectedRequest();

        var copyUrl = Add("URL 복사", "선택한 소스 주소만 복사합니다.");
        copyUrl.Click += (_, _) => CopySelectedUrl();

        var diagnostics = Add("진단 정보 복사", "URL과 헤더 값을 제외한 Auto Detect 진단 정보를 복사합니다.");
        diagnostics.Click += (_, _) => CopyDiagnostics();

        var clear = Add("감지 기록 지우기", "현재 창에서 수집한 네트워크 미디어 후보를 지웁니다. YouTube 페이지 후보는 유지됩니다.");
        clear.Click += (_, _) => ClearDetectedSources();

        var pause = Add("감지 중지", "새 네트워크 미디어 후보 수집을 일시 중지하거나 재개합니다.");
        pause.Click += async (_, _) =>
        {
            _capturePaused = !_capturePaused;
            pause.Content = _capturePaused ? "감지 재개" : "감지 중지";
            _status.Text = _capturePaused ? "네트워크 감지를 일시 중지했습니다." : "네트워크 감지를 재개했습니다.";
            if (!_capturePaused) await ScanOtherVideosAsync();
        };

        return new Expander
        {
            Header = "추가 도구",
            Content = panel,
            IsExpanded = false,
            Margin = new Thickness(0, 3, 0, 0)
        };
    }

    private CapturedRequest? SelectedCapture() => (_list.SelectedItem as CaptureRow)?.Request;

    private void CheckUvdTools()
    {
        static bool Exists(string file)
        {
            var nested = Path.Combine(AppContext.BaseDirectory, "Runtime", file);
            return File.Exists(nested) || File.Exists(Path.Combine(AppContext.BaseDirectory, file));
        }

        var yt = Exists("yt-dlp.exe");
        var stream = Exists("N_m3u8DL-RE.exe");
        _status.Text = yt && stream
            ? "UVD 연결 정상 · yt-dlp / N_m3u8DL-RE 확인"
            : "UVD는 실행 중이지만 일부 번들 도구가 없습니다: " +
              (!yt ? "yt-dlp " : "") + (!stream ? "N_m3u8DL-RE" : "");
    }

    private static string CurlQuote(string value)
    {
        if (value.IndexOfAny(new[] { '\r', '\n', '\0', '"', '$', '\\' }) >= 0)
            throw new InvalidOperationException("URL 또는 헤더에 안전하게 복사할 수 없는 문자가 있습니다.");
        return "\"" + value + "\"";
    }

    private void CopySelectedRequest()
    {
        var request = SelectedCapture();
        if (request is null)
        {
            _status.Text = "복사할 소스를 먼저 선택하세요.";
            return;
        }

        try
        {
            var command = new StringBuilder("curl --url ").Append(CurlQuote(request.Url));
            foreach (var header in request.Headers)
                command.Append(" -H ").Append(CurlQuote(header.Name + ": " + header.Value));

            if (request.Kind == CapturedStreamKind.Page)
                command.AppendLine().Append("# nre-engine: website");
            else if (request.Kind is CapturedStreamKind.Hls or CapturedStreamKind.Dash or CapturedStreamKind.Mss)
                command.AppendLine().Append("# nre-engine: stream");

            Clipboard.SetText(command.ToString());
            _status.Text = "URL + 안전한 요청 헤더를 복사했습니다.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            _status.Text = "클립보드 복사 실패: " + ex.Message;
        }
    }

    private void CopySelectedUrl()
    {
        var request = SelectedCapture();
        if (request is null)
        {
            _status.Text = "복사할 소스를 먼저 선택하세요.";
            return;
        }
        try
        {
            Clipboard.SetText(request.Url);
            _status.Text = "선택한 URL을 복사했습니다.";
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            _status.Text = "클립보드 복사 실패: " + ex.Message;
        }
    }

    private void CopyDiagnostics()
    {
        try
        {
            var selected = SelectedCapture();
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?";
            var diagnostic = new
            {
                version,
                pageCandidate = _pageCandidate is not null,
                capturePaused = _capturePaused,
                detectedCandidates = _found.Count,
                frameCount = _frames.Count,
                selectedKind = selected?.Kind.ToString(),
                selectedHeight = SelectedHeight,
                metadataAvailable = _lastMetadata is not null,
                networkKinds = _found.Values
                    .GroupBy(x => x.Kind.ToString())
                    .ToDictionary(g => g.Key, g => g.Count())
            };
            Clipboard.SetText(JsonSerializer.Serialize(diagnostic, new JsonSerializerOptions { WriteIndented = true }));
            _status.Text = "URL과 헤더를 제외한 진단 정보를 복사했습니다.";
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            _status.Text = "진단 정보 복사 실패: " + ex.Message;
        }
    }

    private void ClearDetectedSources()
    {
        _captureProbeCts?.Cancel();
        _firstObservedUtc.Clear();
        _found.Clear();
        _naverRequested.Clear();
        _naverGroups.Clear();
        _naverByUrl.Clear();
        _naverBestUrls.Clear();
        if (_pageCandidate is not null)
            _found[_pageCandidate.Url] = _pageCandidate;
        RefreshCandidates();
        _status.Text = _pageCandidate is not null
            ? "네트워크 감지 기록을 지웠습니다. YouTube 페이지 후보는 유지됩니다."
            : "감지 기록을 지웠습니다.";
    }

    private async Task ApplyVideoContextMenuAsync(bool enabled)
    {
        if (_browser.CoreWebView2 is not { } core) return;
        var enabledJs = enabled ? "true" : "false";
        var script =
            "(() => {const key='__uvdVideoContextOptIn';" +
            "const targets=e=>{try{return (typeof e.composedPath==='function'?e.composedPath():[e.target]).some(n=>n&&n.tagName==='VIDEO');}catch(_){return false;}};" +
            "if(!window[key]){const original=Event.prototype.preventDefault;window[key]={enabled:false};" +
            "Event.prototype.preventDefault=function(){if(window[key]&&window[key].enabled&&this.type==='contextmenu'&&targets(this))return;return Reflect.apply(original,this,arguments);};" +
            "window.addEventListener('contextmenu',e=>{if(window[key]&&window[key].enabled&&targets(e))e.stopImmediatePropagation();},true);}" +
            "window[key].enabled=" + enabledJs + ";return window[key].enabled;})();";

        try { await core.ExecuteScriptAsync(script); }
        catch (System.Runtime.InteropServices.COMException) { }

        foreach (var frame in _frames.ToArray())
            try { await frame.ExecuteScriptAsync(script); }
            catch (System.Runtime.InteropServices.COMException) { }
            catch (InvalidOperationException) { }
    }

    private void UpdateSelectedQualityMetadata()
    {
        if (_list.SelectedItem is CaptureRow row &&
            row.Request.Kind == CapturedStreamKind.Page)
        {
            UpdatePageQualityMetadata();
            return;
        }
        UpdateNaverQualityMetadata();
    }

    private void UpdatePageQualityMetadata()
    {
        if (_lastPageMetadata is not { Success: true } metadata) return;
        var choice = _qualities.SelectedItem as QualityChoice;
        var format = choice?.Height is > 0
            ? metadata.Formats.FirstOrDefault(x => x.Height == choice.Height)
            : metadata.Formats.OrderByDescending(x => x.Height).FirstOrDefault();
        var size = format?.ExactBytes ?? format?.EstimatedBytes;
        var sizeText = size is > 0
            ? (format?.ExactBytes is > 0 ? "" : "약 ") +
              (size.Value / 1048576d).ToString("N1") + " MiB"
            : "사이트 미제공";
        var durationText = metadata.Duration.HasValue
            ? TimeSpan.FromSeconds(metadata.Duration.Value).ToString(@"hh\:mm\:ss")
            : "사이트 미제공";
        var qualities = metadata.Heights.Count > 0
            ? string.Join(" · ", metadata.Heights.Select(h => h + "p"))
            : "원본 해상도 · 사이트 미표기";
        var selectedQuality = format is null ? "자동" : format.Height + "p";
        var codec = format?.VideoCodec ?? "사이트 코덱 미표기";
        var container = format?.Extension ?? metadata.Container;
        var fps = format?.Fps is > 0 ? format.Fps + " FPS" : "FPS 미표기";
        var resolution = format?.Width is > 0 && format.Height > 0
            ? format.Width + "×" + format.Height
            : selectedQuality;

        _metadata.Text =
            "영상: " + (metadata.Title ?? "페이지 영상") +
            "\n선택: " + selectedQuality +
            "  ·  해상도: " + resolution +
            "  ·  길이: " + durationText +
            "  ·  용량: " + sizeText +
            "\n형식: " + container +
            "  ·  코덱: " + codec +
            "  ·  FPS: " + fps +
            "\n제공 화질: " + qualities;
    }

    private async Task ProbePageSelectionAsync(
        CapturedRequest selected, int sequence, CancellationTokenSource cts)
    {
        _qualities.Items.Clear();
        _qualities.Items.Add(new QualityChoice(0, "원본 최고 화질"));
        _qualities.SelectedIndex = 0;
        _metadata.Text = "페이지 메타데이터 확인 중… 실제 영상은 다운로드하지 않습니다.";

        try
        {
            var metadata = await YtDlpMetadataProbe.ProbeAsync(selected.Url, cts.Token);
            if (_completed || cts.IsCancellationRequested || sequence != _probeSequence) return;

            _lastPageMetadata = metadata;
            if (!metadata.Success)
            {
                _metadata.Text = "페이지 영상은 확인됐지만 메타데이터 조회에 실패했습니다. " +
                                 (metadata.Error ?? "정보를 가져오지 못했습니다.");
                _status.Text = "페이지 후보는 그대로 사용할 수 있습니다. UVD에서 다시 분석할 수 있습니다.";
                return;
            }

            SelectedTitle = metadata.Title;
            foreach (var format in metadata.Formats.OrderByDescending(x => x.Height))
                _qualities.Items.Add(new QualityChoice(format.Height, format.Height + "p"));
            _qualities.SelectedIndex = 0;
            UpdatePageQualityMetadata();
            _status.Text = metadata.Heights.Count > 0
                ? "메타데이터 확인 완료 · 제공 화질 " +
                  string.Join(" · ", metadata.Heights.Select(h => h + "p"))
                : "메타데이터 확인 완료 · 사이트가 개별 화질 목록을 제공하지 않았습니다.";
        }
        catch (OperationCanceledException)
        {
            if (sequence == _probeSequence && !_completed)
                _metadata.Text = "메타데이터 조회 시간이 초과됐습니다. 페이지 후보는 그대로 사용할 수 있습니다.";
        }
    }
}
