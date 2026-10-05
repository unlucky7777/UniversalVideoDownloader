#nullable enable
// This file was reconstructed from the last verified binary after a local source-encoding accident.
// These diagnostics arise from decompiler-lost nullable annotations, not from new runtime behavior.
#pragma warning disable CS4014, CS8600, CS8602, CS8604, CS8629
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using N_m3u8DL_RE_GUI.Core;
using N_m3u8DL_RE_GUI.Core.Abyss;
using N_m3u8DL_RE_GUI.Core.Capture;
using N_m3u8DL_RE_GUI.Core.Resume;
using N_m3u8DL_RE_GUI.Services;
using N_m3u8DL_RE_GUI.ViewModels;
using N_m3u8DL_RE_GUI.Views;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using CheckBox = System.Windows.Controls.CheckBox;
using Clipboard = System.Windows.Clipboard;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using ComboBox = System.Windows.Controls.ComboBox;
using DataFormats = System.Windows.DataFormats;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using GroupBox = System.Windows.Controls.GroupBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;

namespace N_m3u8DL_RE_GUI;

public partial class MainWindow : Window
{
	private sealed record LinkPreviewMetadata(long? Bytes, bool Approximate, string? Title, double? Duration, IReadOnlyList<int> Heights, string Format, string Codec, string Note)
	{
		internal bool HasAny
		{
			get
			{
				long? bytes = Bytes;
				if ((!bytes.HasValue || bytes.GetValueOrDefault() <= 0) && string.IsNullOrWhiteSpace(Title))
				{
					double? duration = Duration;
					if ((!duration.HasValue || !(duration.GetValueOrDefault() > 0.0)) && Heights.Count <= 0)
					{
						return !string.IsNullOrWhiteSpace(Format);
					}
				}
				return true;
			}
		}

		[CompilerGenerated]
		private bool PrintMembers(StringBuilder builder)
		{
			RuntimeHelpers.EnsureSufficientExecutionStack();
			builder.Append("Bytes = ");
			builder.Append(Bytes.ToString());
			builder.Append(", Approximate = ");
			builder.Append(Approximate.ToString());
			builder.Append(", Title = ");
			builder.Append((object?)Title);
			builder.Append(", Duration = ");
			builder.Append(Duration.ToString());
			builder.Append(", Heights = ");
			builder.Append(Heights);
			builder.Append(", Format = ");
			builder.Append((object?)Format);
			builder.Append(", Codec = ");
			builder.Append((object?)Codec);
			builder.Append(", Note = ");
			builder.Append((object?)Note);
			return true;
		}
	}

	private sealed record NativeHeader(string name, string value);

	private sealed record NativeRendition(int height, string url, string kind, NativeHeader[] headers);

	private sealed record NativeJob(int version, string url, string kind, NativeHeader[] headers, string maxQuality, string? pageUrl, string? title, NativeRendition[]? renditions);

	private bool _userCancelledDownload;

	private bool _browserCaptureRunning;

	private static readonly HashSet<string> CurlHlsHeaderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Referer", "Origin", "User-Agent", "Accept", "Accept-Language" };

	private CancellationTokenSource? _previewProbeCts;

	private bool _previewActive;

	private bool _previewPending;

	private bool _previewUnavailable;

	private bool _previewApproximate;

	private string? _previewUrl;

	private long? _previewTotalBytes;

	private string? _cachedPageMetadataUrl;

	private YtDlpPageMetadata? _cachedPageMetadata;

	private bool _rebuildingWebsiteQuality;

	private int? _preferredWebsiteHeight;

	private bool _settingLinkedTitle;

	private bool _titleEditedForCurrentUrl;

	private string _titleLinkedUrl = string.Empty;

	private const double AssumedPreviewBytesPerSecond = 10485760.0;

	private bool _uiEnglish;

	private readonly Dictionary<DependencyObject, string> _uiOriginals = new Dictionary<DependencyObject, string>();

	private static readonly Dictionary<string, string> KoreanUi = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["N_m3u8DL-RE GUI"] = "유니버설 비디오 다운로더",
		["UI language"] = "표시 언어",
		["Input URL"] = "영상 주소",
		["Engine"] = "다운로드 방식",
		["Max quality"] = "최대 화질",
		["Threads"] = "동시 연결",
		["Save Dir"] = "저장 폴더",
		["Download Defaults"] = "다운로드 기본값",
		["Fallback"] = "수동 복구",
		["Browse"] = "찾아보기",
		["Save Name"] = "파일명",
		["Ready"] = "준비 완료",
		["Home"] = "홈",
		["Supported Sites"] = "지원 사이트",
		["Advanced Download"] = "고급 다운로드",
		["Network"] = "네트워크",
		["Security"] = "보안",
		["Media"] = "미디어",
		["Live"] = "실시간",
		["Advanced"] = "고급 설정",
		["Basic Paths"] = "기본 경로",
		["Download Flags"] = "다운로드 세부 옵션",
		["Request & Proxy"] = "HTTP 요청 및 프록시",
		["Performance & Limits"] = "성능 및 제한",
		["Executable"] = "다운로드 엔진",
		["Temp Dir"] = "임시 폴더",
		["Subtitle Format:"] = "자막 형식:",
		["Headers"] = "요청 헤더",
		["Base URL"] = "기본 URL",
		["Proxy"] = "프록시",
		["Retry"] = "재시도",
		["Timeout"] = "시간 제한",
		["Speed Limit"] = "속도 제한",
		["Fingerprint"] = "브라우저 지문",
		["Referer"] = "참조 주소",
		["CF Cookie"] = "CF 쿠키",
		["DL Language"] = "엔진 출력 언어",
		["Delete After Done"] = "완료 후 임시 파일 삭제",
		["No Date Info"] = "날짜 정보 제외",
		["No System Proxy"] = "시스템 프록시 미사용",
		["Skip Download"] = "분석만 실행",
		["Concurrent DL"] = "트랙 동시 다운로드",
		["Skip Merge"] = "병합 생략",
		["Binary Merge"] = "바이너리 병합",
		["Audio Only"] = "오디오만",
		["Skip Check"] = "분할 수 검사 생략",
		["Subtitle Only"] = "자막만",
		["Auto Sub Fix"] = "자막 자동 보정",
		["Auto Select"] = "최고 화질 자동 선택",
		["Always on Top"] = "항상 위에 표시",
		["Keys & Vectors"] = "암호 키 및 벡터",
		["Decryption Engine"] = "복호화 엔진",
		["Stream Selection (Regex Filters)"] = "스트림 선택 (정규식)",
		["Time Range & Muxing"] = "시간 범위 및 병합",
		["Live Recording Options"] = "실시간 녹화 설정",
		["Custom Paths & Variables"] = "사용자 지정 경로 및 변수",
		["Environment & System Flags"] = "환경 및 시스템 옵션",
		["Enable Cloudflare Bypass"] = "Cloudflare 호환 모드",
		["Keep segment files after merge"] = "병합 후 조각 유지",
		["Progress"] = "진행률",
		["Downloaded"] = "받은 용량",
		["Total size"] = "예상 용량",
		["Speed"] = "속도",
		["ETA"] = "남은 시간",
		["Log"] = "로그",
		["Open Folder"] = "폴더 열기",
		["Command:"] = "명령:",
		["\ud83d\udccb Copy"] = "복사",
		["\ud83d\udccb Paste from browser"] = "브라우저에서 붙여넣기",
		["▶ _GO"] = "▶ 다운로드",
		["⏹ S_top"] = "⏹ 중지",
		["Universal Video Downloader"] = "Universal Video Downloader",
		["1. Paste the video URL in the top field."] = "1. 상단 입력창에 영상 주소를 붙여 넣으세요.",
		["2. Choose the maximum quality and save folder."] = "2. 최대 화질과 저장 폴더만 선택하세요.",
		["3. Press Download. Advanced routing and performance settings are automatic by default."] = "3. 다운로드를 누르세요. 다운로드 방식과 성능 설정은 기본적으로 자동 처리됩니다.",
		["Live download status"] = "실시간 다운로드 현황",
		["Progress, expected size, speed and remaining time are always visible in the bottom bar."] = "진행률, 예상 용량, 속도, 남은 시간은 로그를 열지 않아도 아래에 계속 표시됩니다.",
		["For most videos, Auto routing and default performance settings are recommended. Use Advanced or Network only when a site needs special handling."] = "대부분의 영상은 자동 방식과 기본 성능 설정을 그대로 쓰는 것이 좋습니다. 특별한 사이트에서만 고급 설정이나 네트워크 탭을 사용하세요.",
		["Supported URLs and source types"] = "지원 URL 및 영상 유형",
		["Verified on this PC"] = "이 PC에서 실제 검증",
		["yt-dlp website extractor (site dependent)"] = "yt-dlp 사이트 추출기 (사이트별 상이)",
		["Direct streams / browser capture"] = "직접 스트림 / 브라우저 감지",
		["YouTube, Vimeo, Dailymotion, TikTok, Instagram, Facebook, X/Twitter, Twitch, Reddit, and other extractors provided by the installed yt-dlp version. Individual sites are not guaranteed or tested here."] = "YouTube, Vimeo, Dailymotion, TikTok, Instagram, Facebook, X/Twitter, Twitch, Reddit 및 설치된 yt-dlp의 기타 추출기. 개별 사이트는 아직 모두 검증된 것이 아니며 사이트 정책·네트워크에 따라 실패할 수 있습니다.",
		["HTTP(S) M3U8 (HLS), MPD (DASH), ISM/MSS, browser-captured stream requests, and compatible local batch lists via N_m3u8DL-RE."] = "HTTP(S) M3U8(HLS), MPD(DASH), ISM/MSS, 브라우저에서 감지한 직접 스트림 요청, 호환 가능한 로컬 목록 파일(N_m3u8DL-RE).",
		["A site's login, rights, DRM, changing URL, and local network may prevent downloading. For the exhaustive installed yt-dlp extractor names, see SUPPORTED-SITES.txt next to the EXE."] = "로그인·권리·DRM·URL 만료·네트워크 문제는 지원 여부와 별개입니다. 전체 yt-dlp 추출기 이름은 EXE 옆 SUPPORTED-SITES.txt를 확인하세요.",
		["\ud83d\udd04 GUI Auto-Update Engine"] = "GUI 자동 업데이트",
		["Auto Check GUI Updates on Startup"] = "시작 시 업데이트 확인",
		["Check Now"] = "지금 확인",
		["Ad Keyword"] = "광고 키워드",
		["Append URL Params"] = "URL 매개변수 추가",
		["Bin Path"] = "실행 파일 경로",
		["Drop Audio"] = "오디오 제외",
		["Drop Sub"] = "자막 제외",
		["Drop Video"] = "비디오 제외",
		["Enable Mux"] = "영상·음성 병합",
		["FFmpeg Concat"] = "FFmpeg 연결 병합",
		["FFmpeg Path"] = "FFmpeg 경로",
		["Fix VTT by Audio"] = "오디오 기준 VTT 보정",
		["Force ANSI Console"] = "ANSI 콘솔 강제",
		["Format"] = "출력 형식",
		["Get Downloader"] = "다운로드 엔진 받기",
		["HLS Key"] = "HLS 복호화 키",
		["HLS Method"] = "HLS 암호화 방식",
		["IV"] = "초기화 벡터",
		["Keep Files"] = "중간 파일 유지",
		["Keep Segments"] = "조각 파일 유지",
		["Key"] = "암호 키",
		["Key File"] = "키 파일",
		["Log File"] = "로그 파일",
		["Log Level"] = "로그 상세 수준",
		["MP4 Real-Time Decryption"] = "MP4 실시간 복호화",
		["Multi EXT-MAP"] = "다중 EXT-X-MAP 허용",
		["Mux Import"] = "병합 정보 가져오기",
		["Muxer"] = "병합 프로그램",
		["No ANSI Color"] = "ANSI 색상 끄기",
		["No Log"] = "로그 파일 생성 안 함",
		["No Update Check"] = "업데이트 확인 끄기",
		["Partial download found on disk."] = "저장된 미완료 다운로드가 있습니다.",
		["Perform as VOD"] = "주문형 비디오처럼 처리",
		["Pipe Mux"] = "파이프 병합",
		["Range"] = "다운로드 범위",
		["Realtime Merge"] = "실시간 병합",
		["Rec Limit"] = "녹화 시간 제한",
		["Save Pattern"] = "파일명 패턴",
		["Select Audio"] = "오디오 선택",
		["Select Sub"] = "자막 선택",
		["Select Video"] = "비디오 선택",
		["Skip Subtitles"] = "자막 제외",
		["Start At"] = "시작 시점",
		["Take"] = "선택 개수",
		["Threads: top bar"] = "동시 연결은 상단에서 변경",
		["URL Args"] = "URL 인자",
		["Use FFmpeg Concat Demuxer"] = "FFmpeg 연결 방식 사용",
		["Wait (s)"] = "대기 시간(초)",
		["Write Meta JSON"] = "메타데이터 JSON 저장",
		["\ud83d\udce6 Download"] = "다운로드",
		["Cancel active download process"] = "진행 중인 다운로드 중지",
		["Download"] = "다운로드",
		["Auto (page / stream)"] = "자동 (페이지 / 스트림)",
		["Stream: N_m3u8DL-RE"] = "스트림: N_m3u8DL-RE",
		["Website: yt-dlp"] = "사이트: yt-dlp",
		["Source best"] = "원본 최고 화질",
		["Save"] = "저장",
		["Resume"] = "이어받기",
		["Discard"] = "취소"
	};

	private static readonly HashSet<string> SimpleKoreanKeys = new HashSet<string>(StringComparer.Ordinal)
	{
		"Home", "Supported Sites", "Advanced Download", "Save Dir", "Browse", "Save Name", "Input URL", "Max quality", "UI language", "Download Defaults",
		"Fallback", "Ready", "Downloaded", "Total size", "Speed", "ETA", "Open Folder", "\ud83d\udccb Paste from browser", "▶ _GO", "⏹ S_top",
		"1. Paste the video URL in the top field.", "2. Choose the maximum quality and save folder.", "3. Press Download. Advanced routing and performance settings are automatic by default.", "Live download status", "Progress, expected size, speed and remaining time are always visible in the bottom bar.", "For most videos, Auto routing and default performance settings are recommended. Use Advanced or Network only when a site needs special handling.", "Supported URLs and source types", "Verified on this PC", "yt-dlp website extractor (site dependent)", "Direct streams / browser capture"
	};

	private readonly DispatcherTimer _nativeInboxTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(1L)
	};

	private bool _nativeInboxDraining;

	private string? _nativeFallbackPageUrl;

	private string? _nativeFallbackQuality;

	private readonly Dictionary<int, NativeRendition> _nativeRenditions = new Dictionary<int, NativeRendition>();

	private bool _switchingNativeRendition;

	private readonly IUtilityService _utilityService;

	private readonly IConfigService _configService;

	private readonly IBatchScriptService _batchScriptService;

	private readonly IDragDropService _dragDropService;

	private readonly IDownloadService _downloadService;

	private bool _suspendParameterRefresh;

	private CancellationTokenSource? _activeOperationCts;

	private ResumeJob? _activeResumeJob;

	private object? _downloadButtonLabel;

	private static readonly SolidColorBrush ErrorBorderBrush = CreateFrozenBrush(Color.FromRgb(231, 76, 60));

	public static readonly RoutedUICommand StartDownloadRoutedCommand = new RoutedUICommand("Start Download", "StartDownloadRoutedCommand", typeof(MainWindow));

	public static readonly RoutedUICommand StopDownloadRoutedCommand = new RoutedUICommand("Stop Download", "StopDownloadRoutedCommand", typeof(MainWindow));

	private readonly StringBuilder _logBuffer = new StringBuilder();

	private string? _lastOutputDirectory;

	private DateTime _downloadStartedUtc;

	private DateTime _sessionStartedUtc;

	private DateTime _lastSpeedReportUtc;

	private DateTime _lastTelemetryRenderUtc;

	private double _integratedTransferBytes;

	private double _completedTrackBytes;

	private double _highestTrackBytes;

	private bool _telemetryActive;

	private readonly DispatcherTimer _telemetryTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(1L)
	};

	private long? _expectedBytes;

	private bool _hasUvdTransferSample;

	private long _actualTransferredBytes;

	private double _displayPercent;

	private double? _reportedBytesPerSecond;

	private TimeSpan? _reportedEta;

	private string? _lastEngineErrorCode;

	private bool _streamManifestRead;

	private bool _awaitingFirstProgress;

	private string _activeDownloadMethod = "N_m3u8DL-RE";

	private string _activeDownloadFormat = "자동 감지";

	private int _lastUnifiedProgressLog = -1;

	private string _loadingStageEn = "Preparing download";

	private string _loadingStageKo = "다운로드 준비 중";

	private DateTime _loadingStartedUtc;

	private readonly DispatcherTimer _loadingTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(1L)
	};

	private static readonly SolidColorBrush DefaultStatusBrush = CreateFrozenBrush(Color.FromRgb(136, 136, 168));

	private static string NativeInboxPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalVideoDownloader", "NativeInbox");

	private static string UserDataDirectory
	{
		get
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalVideoDownloader");
			Directory.CreateDirectory(text);
			return text;
		}
	}

	private static string UserConfigPath
	{
		get
		{
			string text = Path.Combine(UserDataDirectory, "config.txt");
			string text2 = Path.Combine(AppContext.BaseDirectory, "config.txt");
			if (!File.Exists(text) && File.Exists(text2))
			{
				try
				{
					File.Copy(text2, text, overwrite: false);
				}
				catch (IOException)
				{
				}
				catch (UnauthorizedAccessException)
				{
				}
			}
			return text;
		}
	}

	private async void Button_CaptureBrowser_Click(object sender, RoutedEventArgs e)
	{
		if (_downloadService.IsDownloading || _browserCaptureRunning)
		{
			return;
		}
		string text = TextBox_URL.Text.Trim();
		if (!Uri.TryCreate(text, UriKind.Absolute, out Uri result) || (result.Scheme != Uri.UriSchemeHttps && result.Scheme != Uri.UriSchemeHttp))
		{
			SetStatus("Enter a website URL first.", isError: true);
			return;
		}
		Button_GO.IsEnabled = false;
		BeginLoading("Opening embedded browser", "내장 브라우저 여는 중");
		try
		{
			await TryBrowserCaptureAsync(text);
		}
		finally
		{
			FinishLoading();
			RenderDownloadTelemetry();
			Button_GO.IsEnabled = true;
		}
	}

	private Task<bool> TryBrowserCaptureAsync(string pageUrl)
	{
		if (_browserCaptureRunning)
		{
			return Task.FromResult(result: false);
		}
		_browserCaptureRunning = true;
		try
		{
			UpdateLoadingStage("Detecting media in embedded browser", "브라우저에서 영상 소스 감지 중");
			Button_Stop.Visibility = Visibility.Collapsed;
			BrowserCaptureWindow browserCaptureWindow = new BrowserCaptureWindow(pageUrl)
			{
				Owner = this
			};
			if (browserCaptureWindow.ShowDialog() == true)
			{
				CapturedRequest selectedRequest = browserCaptureWindow.SelectedRequest;
				if ((object)selectedRequest != null)
				{
					bool flag = (object)SiteAdapterRegistry.ResolvePage(pageUrl) != null;
					TextBox_URL.Text = (flag ? pageUrl : selectedRequest.Url);
					TextBox_Headers.Text = (flag ? string.Empty : selectedRequest.ToHeaderLines());
					SetLinkedFileName(string.IsNullOrWhiteSpace(browserCaptureWindow.SelectedTitle) ? string.Empty : browserCaptureWindow.SelectedTitle.Trim());
					string value;
					bool flag2 = flag || selectedRequest.Kind == CapturedStreamKind.Page || (selectedRequest.Directives.TryGetValue("engine", out value) && value.Equals("website", StringComparison.OrdinalIgnoreCase));
					ComboBox_DownloadEngine.SelectedIndex = ((!flag2 && selectedRequest.Kind != CapturedStreamKind.Media) ? 1 : 2);
					CheckBox_AutoSelect.IsChecked = true;
					int? selectedHeight;
					if (flag2)
					{
						TextBox_SelectVideo.Text = "best";
						ApplyWebsiteQualityFromCapture(browserCaptureWindow.SelectedHeight);
					}
					else
					{
						TextBox textBox_SelectVideo = TextBox_SelectVideo;
						selectedHeight = browserCaptureWindow.SelectedHeight;
						textBox_SelectVideo.Text = ((selectedHeight.HasValue && selectedHeight.GetValueOrDefault() > 0) ? NativeStreamSelection(browserCaptureWindow.SelectedHeight.Value.ToString()) : "best");
					}
					if (string.IsNullOrWhiteSpace(TextBox_WorkDir.Text))
					{
						TextBox_WorkDir.Text = Path.Combine(AppContext.BaseDirectory, "Downloads");
					}
					_previewProbeCts?.Cancel();
					ScheduleLinkPreview();
					AppendLog("Auto Detect: media selected for review; download NOT started.");
					selectedHeight = browserCaptureWindow.SelectedHeight;
					SetStatus((selectedHeight.HasValue && selectedHeight.GetValueOrDefault() > 0) ? ("Detected source reviewed (" + browserCaptureWindow.SelectedHeight + "p). Press Download to start.") : "Detected source reviewed. Check metadata and press Download to start.");
					return Task.FromResult(result: true);
				}
			}
			SetStatus("No media selected. Existing Chrome extension: play video and click Download or Copy.");
			return Task.FromResult(result: false);
		}
		catch (Exception ex)
		{
			SetStatus("Embedded capture error: " + ex.Message, isError: true);
			return Task.FromResult(result: false);
		}
		finally
		{
			_browserCaptureRunning = false;
			Button_Stop.Visibility = Visibility.Collapsed;
		}
	}

	private void ApplyWebsiteQualityFromCapture(int? height)
	{
		SetPreferredWebsiteHeight(height);
	}

	private static bool IsHlsUrl(string? input)
	{
		Uri result;
		bool flag = !Uri.TryCreate(input?.Trim(), UriKind.Absolute, out result);
		if (!flag)
		{
			string scheme = result.Scheme;
			bool flag2 = ((scheme == "http" || scheme == "https") ? true : false);
			flag = !flag2;
		}
		if (flag)
		{
			return false;
		}
		return result.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
	}

	private static bool PreferCurlHlsTransport(string? input)
	{
		if (!IsHlsUrl(input) || !Uri.TryCreate(input, UriKind.Absolute, out Uri result))
		{
			return false;
		}
		string text = result.Host.TrimEnd('.');
		if (!text.Equals("phncdn.com", StringComparison.OrdinalIgnoreCase) && !text.EndsWith(".phncdn.com", StringComparison.OrdinalIgnoreCase) && !text.Equals("surrit.com", StringComparison.OrdinalIgnoreCase))
		{
			return text.EndsWith(".surrit.com", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private string CurrentStreamQualityCap()
	{
		Match match = Regex.Match(TextBox_SelectVideo?.Text ?? string.Empty, "x(4320|2880|2160|1440|1080|720|480|360|240)\\$", RegexOptions.CultureInvariant);
		if (!match.Success)
		{
			return "best";
		}
		return match.Groups[1].Value;
	}

	private static object[] ParseCurlHlsHeaders(string? text)
	{
		List<object> list = new List<object>();
		string[] array = (text ?? string.Empty).Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text2 in array)
		{
			int num = text2.IndexOf(':');
			if (num > 0)
			{
				string text3 = text2.Substring(0, num).Trim();
				string text4 = text2;
				int num2 = num + 1;
				string text5 = text4.Substring(num2, text4.Length - num2).Trim();
				bool flag = !CurlHlsHeaderNames.Contains(text3);
				if (!flag)
				{
					num2 = text5.Length;
					bool flag2 = ((num2 < 1 || num2 > 2048) ? true : false);
					flag = flag2;
				}
				if (!flag && text5.IndexOfAny(new char[3] { '\r', '\n', '\0' }) < 0)
				{
					list.Add(new
					{
						name = text3,
						value = text5
					});
				}
			}
		}
		return list.ToArray();
	}

	private async Task<bool> StartCurlHlsTransportAsync(string reason)
	{
		string text = TextBox_URL.Text.Trim();
		if (!IsHlsUrl(text))
		{
			return false;
		}
		string script = BundledPath("curl-hls.ps1");
		string text2 = BundledPath("ffmpeg.exe");
		if (!File.Exists(script) || !File.Exists(text2))
		{
			AppendLog("Browser-compatible HLS transport is unavailable because its runtime files are missing.");
			return false;
		}
		DownloadOptions downloadOptions = BuildDownloadOptions();
		string text3 = (string.IsNullOrWhiteSpace(downloadOptions.SaveDir) ? Path.Combine(AppContext.BaseDirectory, "Downloads") : downloadOptions.SaveDir);
		Directory.CreateDirectory(text3);
		_lastOutputDirectory = text3;
		string text4 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalVideoDownloader", "CurlHlsJobs");
		Directory.CreateDirectory(text4);
		string jobPath = Path.Combine(text4, Guid.NewGuid().ToString("N") + ".json");
		var value = new
		{
			url = text,
			headers = ParseCurlHlsHeaders(TextBox_Headers.Text),
			maxQuality = CurrentStreamQualityCap(),
			saveDir = text3,
			saveName = (downloadOptions.SaveName ?? string.Empty),
			threads = Math.Clamp((downloadOptions.ThreadCount > 0) ? downloadOptions.ThreadCount : 128, 1, 128),
			ffmpeg = text2
		};
		await File.WriteAllTextAsync(jobPath, JsonSerializer.Serialize(value));
		SetDownloadMethod("Windows curl HLS", "HLS 조각 → MP4 (FFmpeg remux)");
		AppendLog("Switching to browser-compatible curl HLS transport: " + reason);
		AppendLog("Signed media URLs and header values are not printed.");
		UpdateLoadingStage("Using browser-compatible HLS transport", "브라우저 호환 HLS 전송으로 다운로드 중");
		Action<string> logCallback = delegate(string line)
		{
			base.Dispatcher.InvokeAsync(delegate
			{
				AppendLog(line);
			});
		};
		Progress<int> progressCallback = new Progress<int>(OnDownloadProgress);
		try
		{
			return await _downloadService.StartProcessAsync("powershell.exe", new string[7] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Job", jobPath }, logCallback, progressCallback);
		}
		finally
		{
			try
			{
				if (File.Exists(jobPath))
				{
					File.Delete(jobPath);
				}
			}
			catch (IOException)
			{
			}
		}
	}

	private static bool PreferCurlMp4Transport(string? source)
	{
		if (!Uri.TryCreate(source, UriKind.Absolute, out Uri result) || result.Scheme != "https" || !string.IsNullOrEmpty(result.UserInfo) || !result.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (!result.Host.Equals("pstatic.net", StringComparison.OrdinalIgnoreCase))
		{
			return result.Host.EndsWith(".pstatic.net", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private async Task<bool> StartCurlMp4TransportAsync()
	{
		string script = BundledPath("curl-mp4.ps1");
		if (!File.Exists(script))
		{
			SetStatus("Naver MP4 Range transport is not installed.", isError: true);
			return false;
		}
		DownloadOptions downloadOptions = BuildDownloadOptions();
		string text = (string.IsNullOrWhiteSpace(downloadOptions.SaveDir) ? Path.Combine(AppContext.BaseDirectory, "Downloads") : downloadOptions.SaveDir);
		Directory.CreateDirectory(text);
		_lastOutputDirectory = text;
		string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UniversalVideoDownloader", "CurlMp4Jobs");
		Directory.CreateDirectory(text2);
		string jobPath = Path.Combine(text2, Guid.NewGuid().ToString("N") + ".json");
		var value = new
		{
			url = TextBox_URL.Text.Trim(),
			headers = ParseCurlHlsHeaders(TextBox_Headers.Text),
			saveDir = text,
			saveName = (downloadOptions.SaveName ?? string.Empty),
			connections = Math.Clamp((downloadOptions.ThreadCount > 0) ? downloadOptions.ThreadCount : 24, 1, 24)
		};
		await File.WriteAllTextAsync(jobPath, JsonSerializer.Serialize(value));
		SetDownloadMethod("Windows curl Range", "MP4 직접 파일");
		UpdateLoadingStage("Connecting through verified HTTP Ranges", "검증된 MP4 구간 전송 시작 중");
		Action<string> logCallback = delegate(string line)
		{
			base.Dispatcher.InvokeAsync(delegate
			{
				AppendLog(line);
			});
		};
		Progress<int> progressCallback = new Progress<int>(OnDownloadProgress);
		try
		{
			return await _downloadService.StartProcessAsync("powershell.exe", new string[7] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Job", jobPath }, logCallback, progressCallback);
		}
		finally
		{
			try
			{
				if (File.Exists(jobPath))
				{
					File.Delete(jobPath);
				}
			}
			catch (IOException)
			{
			}
		}
	}

	private int SelectedWebsiteHeight()
	{
		if (!(ComboBox_WebsiteQuality?.SelectedItem is ComboBoxItem { Tag: var tag }))
		{
			return 0;
		}
		if (!int.TryParse(tag?.ToString(), out var result) || result <= 0)
		{
			return 0;
		}
		return result;
	}

	private void WebsiteQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_rebuildingWebsiteQuality)
		{
			return;
		}
		int num = SelectedWebsiteHeight();
		if (num > 0 && TryApplyNativeRendition(num))
		{
			if (base.IsLoaded && !_suspendParameterRefresh)
			{
				ScheduleLinkPreview();
			}
			return;
		}
		_preferredWebsiteHeight = null;
		if (base.IsLoaded && !_suspendParameterRefresh)
		{
			ScheduleLinkPreview();
		}
	}

	private void SetPreferredWebsiteHeight(int? height)
	{
		_preferredWebsiteHeight = ((height.HasValue && height.GetValueOrDefault() > 0) ? height : ((int?)null));
	}

	private void ResetLinkedTitleForUrl()
	{
		string text = TextBox_URL?.Text?.Trim() ?? string.Empty;
		if (!string.Equals(_titleLinkedUrl, text, StringComparison.Ordinal))
		{
			_titleLinkedUrl = text;
			_titleEditedForCurrentUrl = false;
			SetLinkedFileName(string.Empty);
		}
	}

	private void SetLinkedFileName(string? title)
	{
		_settingLinkedTitle = true;
		try
		{
			TextBox_Title.Text = (string.IsNullOrWhiteSpace(title) ? string.Empty : SanitizeSuggestedFileName(title));
		}
		finally
		{
			_settingLinkedTitle = false;
		}
	}

	private void MarkFileNameEditedByUser()
	{
		if (!_settingLinkedTitle)
		{
			_titleEditedForCurrentUrl = true;
		}
	}

	private void ScheduleLinkPreview()
	{
		_previewProbeCts?.Cancel();
		_previewActive = false;
		_previewPending = false;
		_previewUnavailable = false;
		_previewTotalBytes = null;
		_previewUrl = null;
		if (base.IsLoaded && !_suspendParameterRefresh && !_downloadService.IsDownloading)
		{
			string text = TextBox_URL.Text.Trim();
			if (!Uri.TryCreate(text, UriKind.Absolute, out Uri result) || (result.Scheme != Uri.UriSchemeHttps && result.Scheme != Uri.UriSchemeHttp))
			{
				_expectedBytes = null;
				Border_LinkMetadata.Visibility = Visibility.Collapsed;
				RebuildWebsiteQualityChoices((IReadOnlyCollection<int>)(object)Array.Empty<int>(), null, text);
				RenderDownloadTelemetry();
				return;
			}
			_previewActive = true;
			_previewPending = true;
			_previewUrl = text;
			_expectedBytes = null;
			_previewApproximate = false;
			_reportedBytesPerSecond = null;
			_reportedEta = null;
			Border_LinkMetadata.Visibility = Visibility.Visible;
			TextBlock_MetaTitle.Text = "분석 중…";
			TextBlock_MetaQuality.Text = "확인 중";
			TextBlock_MetaDuration.Text = "—";
			TextBlock_MetaSize.Text = "—";
			TextBlock_MetaFormat.Text = "—";
			TextBlock_MetaCodec.Text = "—";
			TextBlock_MetaNote.Text = "영상 메타데이터와 제공 화질을 확인 중입니다.";
			RenderDownloadTelemetry();
			TextBlock_Status.Text = (_uiEnglish ? "Analyzing link…" : "링크 분석 중…");
			CancellationTokenSource cts = (_previewProbeCts = new CancellationTokenSource());
			int quality = _preferredWebsiteHeight ?? SelectedWebsiteHeight();
			RunLinkPreviewAsync(text, quality, cts);
		}
	}

	private async Task RunLinkPreviewAsync(string input, int quality, CancellationTokenSource cts)
	{
		_ = 1;
		try
		{
			await Task.Delay(350, cts.Token);
			using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
			timeout.CancelAfter(TimeSpan.FromSeconds(28L));
			LinkPreviewMetadata linkPreviewMetadata = await ProbeLinkMetadataAsync(input, quality, timeout.Token);
			if (cts.IsCancellationRequested || _previewUrl != input || TextBox_URL.Text.Trim() != input || _downloadService.IsDownloading || !base.IsLoaded)
			{
				return;
			}
			_previewPending = false;
			_previewTotalBytes = linkPreviewMetadata.Bytes;
			_previewApproximate = linkPreviewMetadata.Approximate;
			_previewUnavailable = !linkPreviewMetadata.HasAny;
			_expectedBytes = linkPreviewMetadata.Bytes;
			RenderLinkMetadata(linkPreviewMetadata, input, quality);
			RenderDownloadTelemetry();
			TextBlock_Status.Text = ((!_previewUnavailable) ? (_uiEnglish ? "Metadata ready — review and press Download" : "영상 정보 확인 완료 · 설정을 검토한 뒤 다운로드하세요") : (_uiEnglish ? "Metadata unavailable from this source" : "이 링크의 메타데이터를 확인하지 못했습니다"));
		}
		catch (OperationCanceledException)
		{
			if (!cts.IsCancellationRequested && _previewUrl == input)
			{
				_previewPending = false;
				_previewUnavailable = true;
				RenderDownloadTelemetry();
				TextBlock_MetaNote.Text = "메타데이터 조회 시간이 초과됐습니다.";
				TextBlock_Status.Text = (_uiEnglish ? "Metadata lookup timed out" : "링크 분석 시간 초과");
			}
		}
		catch (Exception)
		{
			if (!cts.IsCancellationRequested && _previewUrl == input)
			{
				_previewPending = false;
				_previewUnavailable = true;
				RenderDownloadTelemetry();
				TextBlock_MetaNote.Text = "다운로드 전에 메타데이터를 확인하지 못했습니다.";
				TextBlock_Status.Text = (_uiEnglish ? "Unable to inspect before download" : "다운로드 전 정보 확인 불가");
			}
		}
		finally
		{
			if (_previewProbeCts == cts)
			{
				_previewProbeCts = null;
			}
			cts.Dispose();
		}
	}

	private void RenderLinkMetadata(LinkPreviewMetadata result, string input, int requestedHeight)
	{
		Border_LinkMetadata.Visibility = Visibility.Visible;
		string text = (string.IsNullOrWhiteSpace(result.Title) ? DisplayNameFromUrl(input) : result.Title.Trim());
		TextBlock_MetaTitle.Text = (string.IsNullOrWhiteSpace(text) ? "제목 확인 불가" : text);
		if ((!_titleEditedForCurrentUrl || string.IsNullOrWhiteSpace(TextBox_Title.Text)) && !string.IsNullOrWhiteSpace(result.Title))
		{
			SetLinkedFileName(result.Title);
		}
		int[] second = NativeRenditionHeightsFor(input);
		int[] array = (from h in (from h in result.Heights.Concat(second)
				where h > 0
				select h).Distinct()
			orderby h descending
			select h).ToArray();
		TextBlock_MetaQuality.Text = ((array.Length != 0) ? string.Join(" · ", array.Select((int h) => h + "p")) : "확인 불가");
		RebuildWebsiteQualityChoices((IReadOnlyCollection<int>)(object)array, (requestedHeight > 0) ? new int?(requestedHeight) : _preferredWebsiteHeight, input);
		TextBlock textBlock_MetaDuration = TextBlock_MetaDuration;
		double? duration = result.Duration;
		textBlock_MetaDuration.Text = ((duration.HasValue && duration.GetValueOrDefault() > 0.0) ? TimeSpan.FromSeconds(result.Duration.Value).ToString("hh\\:mm\\:ss") : "확인 불가");
		TextBlock textBlock_MetaSize = TextBlock_MetaSize;
		long? bytes = result.Bytes;
		textBlock_MetaSize.Text = ((bytes.HasValue && bytes.GetValueOrDefault() > 0) ? ((result.Approximate ? "약 " : "") + FormatByteSize(result.Bytes.Value)) : "확인 불가");
		TextBlock_MetaFormat.Text = (string.IsNullOrWhiteSpace(result.Format) ? "확인 불가" : result.Format);
		TextBlock_MetaCodec.Text = (string.IsNullOrWhiteSpace(result.Codec) ? "확인 불가" : result.Codec);
		TextBlock_MetaNote.Text = result.Note;
	}

	private void RebuildWebsiteQualityChoices(IReadOnlyCollection<int> heights, int? preferredHeight, string? input)
	{
		if (ComboBox_WebsiteQuality == null)
		{
			return;
		}
		int[] array = (from h in heights.Where((int h) => h > 0).Distinct()
			orderby h descending
			select h).ToArray();
		int num = SelectedWebsiteHeight();
		int value = ((preferredHeight.HasValue && preferredHeight.GetValueOrDefault() > 0) ? preferredHeight.Value : ((num > 0) ? num : 0));
		_rebuildingWebsiteQuality = true;
		try
		{
			ComboBox_WebsiteQuality.Items.Clear();
			if (array.Length == 0)
			{
				ComboBox_WebsiteQuality.Items.Add(new ComboBoxItem
				{
					Content = "Source best",
					Tag = "0"
				});
				ComboBox_WebsiteQuality.SelectedIndex = 0;
			}
			else
			{
				int[] array2 = array;
				for (int num2 = 0; num2 < array2.Length; num2++)
				{
					int height = array2[num2];
					ComboBox_WebsiteQuality.Items.Add(new ComboBoxItem
					{
						Content = WebsiteQualityLabel(height),
						Tag = height.ToString()
					});
				}
				int num3 = Array.IndexOf(array, value);
				ComboBox_WebsiteQuality.SelectedIndex = ((num3 >= 0) ? num3 : 0);
			}
			_preferredWebsiteHeight = null;
		}
		finally
		{
			_rebuildingWebsiteQuality = false;
		}
	}

	private static string WebsiteQualityLabel(int height)
	{
		return height switch
		{
			4320 => "8K (4320p)", 
			2880 => "5K (2880p)", 
			2160 => "4K (2160p)", 
			_ => height + "p", 
		};
	}

	private static string SanitizeSuggestedFileName(string value)
	{
		HashSet<char> invalid = Path.GetInvalidFileNameChars().ToHashSet();
		string text = new string(value.Where((char c) => !invalid.Contains(c) && !char.IsControl(c)).ToArray()).Trim().TrimEnd('.');
		if (text.Length <= 180)
		{
			return text;
		}
		return text.Substring(0, 180);
	}

	private static string DisplayNameFromUrl(string input)
	{
		if (!Uri.TryCreate(input, UriKind.Absolute, out Uri result))
		{
			return string.Empty;
		}
		string fileName = Path.GetFileName(Uri.UnescapeDataString(result.AbsolutePath));
		if (string.IsNullOrWhiteSpace(fileName))
		{
			return result.Host;
		}
		string extension = Path.GetExtension(fileName);
		if (extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) || extension.Equals(".webm", StringComparison.OrdinalIgnoreCase) || extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) || extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) || extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase) || extension.Equals(".m3u8", StringComparison.OrdinalIgnoreCase) || extension.Equals(".mpd", StringComparison.OrdinalIgnoreCase))
		{
			return Path.GetFileNameWithoutExtension(fileName);
		}
		return fileName;
	}

	private static async Task<(string Output, bool Success)> RunMetadataProcessAsync(string executable, IReadOnlyList<string> args, CancellationToken token)
	{
		using Process process = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = executable,
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			}
		};
		foreach (string arg in args)
		{
			process.StartInfo.ArgumentList.Add(arg);
		}
		bool started = false;
		try
		{
			started = process.Start();
			if (!started)
			{
				return (Output: string.Empty, Success: false);
			}
			Task<string> outputTask = process.StandardOutput.ReadToEndAsync(token);
			Task<string> errorTask = process.StandardError.ReadToEndAsync(token);
			await process.WaitForExitAsync(token);
			string output = await outputTask;
			await errorTask;
			return (Output: output, Success: process.ExitCode == 0);
		}
		finally
		{
			if (started && !process.HasExited)
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch (InvalidOperationException)
				{
				}
			}
		}
	}

	private async Task<LinkPreviewMetadata?> ProbeSiteAdapterMetadataAsync(string input, int maxHeight, CancellationToken token)
	{
		SiteAdapterDescriptor siteAdapterDescriptor = SiteAdapterRegistry.ResolvePage(input);
		if ((object)siteAdapterDescriptor == null)
		{
			return null;
		}
		string text = BundledPath(siteAdapterDescriptor.MetadataHelper);
		if (!File.Exists(text))
		{
			return new LinkPreviewMetadata(null, Approximate: false, null, null, Array.Empty<int>(), string.Empty, string.Empty, "사이트 어댑터 메타데이터 도구를 찾을 수 없습니다.");
		}
		(string, bool) tuple = await RunMetadataProcessAsync("node.exe", new string[6]
		{
			text,
			input,
			AppContext.BaseDirectory,
			maxHeight.ToString(CultureInfo.InvariantCulture),
			"1",
			"--probe"
		}, token);
		if (!tuple.Item2)
		{
			return new LinkPreviewMetadata(null, Approximate: false, null, null, Array.Empty<int>(), string.Empty, string.Empty, "사이트 어댑터 메타데이터 조회에 실패했습니다.");
		}
		Match match = Regex.Match(tuple.Item1, "(?m)^\\[probe-json\\]\\s+(.+)$", RegexOptions.CultureInvariant);
		if (!match.Success)
		{
			return new LinkPreviewMetadata(null, Approximate: false, null, null, Array.Empty<int>(), string.Empty, string.Empty, "사이트 어댑터 메타데이터 응답 형식을 확인하지 못했습니다.");
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(match.Groups[1].Value.Trim());
			JsonElement rootElement = jsonDocument.RootElement;
			JsonElement value;
			string title = ((rootElement.TryGetProperty("title", out value) && value.ValueKind == JsonValueKind.String) ? value.GetString() : null);
			JsonElement value2;
			double value3;
			double? duration = ((rootElement.TryGetProperty("duration", out value2) && value2.ValueKind == JsonValueKind.Number && value2.TryGetDouble(out value3) && value3 > 0.0) ? new double?(value3) : ((double?)null));
			JsonElement value4;
			long value5;
			long? bytes = ((rootElement.TryGetProperty("bytes", out value4) && value4.ValueKind == JsonValueKind.Number && value4.TryGetInt64(out value5) && value5 > 0) ? new long?(value5) : ((long?)null));
			JsonElement value6;
			int value7;
			int[] heights = ((rootElement.TryGetProperty("heights", out value6) && value6.ValueKind == JsonValueKind.Array) ? (from h in (from v in value6.EnumerateArray()
					where v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out value7) && value7 > 0
					select v.GetInt32()).Distinct()
				orderby h descending
				select h).ToArray() : Array.Empty<int>());
			return new LinkPreviewMetadata(bytes, Approximate: false, title, duration, heights, "mp4", "다운로드 후 확정", "사이트 어댑터가 반환한 실제 화질 목록입니다.");
		}
		catch (JsonException)
		{
			return new LinkPreviewMetadata(null, Approximate: false, null, null, Array.Empty<int>(), string.Empty, string.Empty, "사이트 어댑터 메타데이터 JSON을 해석하지 못했습니다.");
		}
	}

	private async Task<LinkPreviewMetadata> ProbeLinkMetadataAsync(string input, int maxHeight, CancellationToken token)
	{
		IEnumerable<KeyValuePair<string, string>> requestHeaders = from line in (TextBox_Headers.Text ?? string.Empty).Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
			select line.Split(':', 2) into parts
			where parts.Length == 2
			select new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim());
		CapturedRequest direct = MediaRequestSelector.Create(input, requestHeaders);
		bool flag;
		switch (direct?.Kind)
		{
		case CapturedStreamKind.Hls:
		case CapturedStreamKind.Media:
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		CaptureMediaProbe.Result result;
		CaptureMediaProbe.Variant variant;
		long? bytes;
		IReadOnlyList<int> heights;
		string text;
		string title;
		object obj;
		long? estimatedBytes;
		if (flag)
		{
			result = await CaptureMediaProbe.InspectAsync(direct, token);
			variant = (from v in result.Variants
				where maxHeight <= 0 || v.Height <= maxHeight
				orderby v.Height descending
				select v).FirstOrDefault();
			bytes = variant?.EstimatedBytes ?? result.Bytes;
			IReadOnlyList<int> readOnlyList2;
			if (result.Heights.Count <= 0)
			{
				IReadOnlyList<int> readOnlyList = (((object)variant == null) ? ((IReadOnlyList<int>)Array.Empty<int>()) : ((IReadOnlyList<int>)new int[1] { variant.Height }));
				readOnlyList2 = readOnlyList;
			}
			else
			{
				readOnlyList2 = result.Heights;
			}
			heights = readOnlyList2;
			text = ((direct.Kind == CapturedStreamKind.Hls) ? "HLS / M3U8" : Path.GetExtension(new Uri(input).AbsolutePath).TrimStart('.').ToUpperInvariant());
			if (string.IsNullOrWhiteSpace(text))
			{
				text = "직접 미디어";
			}
			title = DisplayNameFromUrl(input);
			if ((object)variant != null)
			{
				estimatedBytes = variant.EstimatedBytes;
				if (estimatedBytes.HasValue && estimatedBytes.GetValueOrDefault() > 0)
				{
					obj = "선택 가능한 HLS 화질과 해당 화질의 예상 용량을 재생목록에서 확인했습니다.";
					goto IL_02d2;
				}
			}
			obj = result.Note;
			goto IL_02d2;
		}
		LinkPreviewMetadata linkPreviewMetadata = await ProbeSiteAdapterMetadataAsync(input, maxHeight, token);
		if ((object)linkPreviewMetadata != null)
		{
			return linkPreviewMetadata;
		}
		if (_cachedPageMetadataUrl != input || (object)_cachedPageMetadata == null)
		{
			_cachedPageMetadata = await YtDlpMetadataProbe.ProbeAsync(input, token);
			_cachedPageMetadataUrl = input;
		}
		YtDlpPageMetadata cachedPageMetadata = _cachedPageMetadata;
		if ((object)cachedPageMetadata != null && cachedPageMetadata.Success)
		{
			YtDlpFormatMetadata ytDlpFormatMetadata = cachedPageMetadata.SelectForMaximumHeight(maxHeight);
			long? bytes2 = ytDlpFormatMetadata?.ExactBytes ?? ytDlpFormatMetadata?.EstimatedBytes;
			estimatedBytes = ytDlpFormatMetadata?.ExactBytes;
			int approximate;
			if (!estimatedBytes.HasValue || estimatedBytes.GetValueOrDefault() <= 0)
			{
				estimatedBytes = ytDlpFormatMetadata?.EstimatedBytes;
				approximate = ((estimatedBytes.HasValue && estimatedBytes.GetValueOrDefault() > 0) ? 1 : 0);
			}
			else
			{
				approximate = 0;
			}
			return new LinkPreviewMetadata(bytes2, (byte)approximate != 0, cachedPageMetadata.Title, cachedPageMetadata.Duration, cachedPageMetadata.Heights, ((object)ytDlpFormatMetadata == null) ? cachedPageMetadata.Container : (ytDlpFormatMetadata.Extension + (cachedPageMetadata.AudioAvailable ? " · 영상+음성 병합" : "")), ytDlpFormatMetadata?.VideoCodec ?? "확인 불가", cachedPageMetadata.Note);
		}
		return new LinkPreviewMetadata(null, Approximate: false, null, null, Array.Empty<int>(), string.Empty, string.Empty, cachedPageMetadata?.Error ?? "이 링크에서 메타데이터를 가져오지 못했습니다.");
		IL_02d2:
		string note = (string)obj;
		estimatedBytes = variant?.EstimatedBytes;
		return new LinkPreviewMetadata(bytes, (estimatedBytes.HasValue && estimatedBytes.GetValueOrDefault() > 0) || result.Approximate, title, result.Duration, heights, text, "다운로드 후 확정", note);
	}

	private static (string Korean, string English)? HelpFor(string key)
	{
		return key switch
		{
			"Save Dir" => ("파일이 저장되는 위치입니다. 비어 있으면 EXE가 있는 폴더가 기본값입니다.", "Output folder. If empty, defaults to the EXE folder."), 
			"Save Name" => ("저장할 파일 이름입니다. 빈칸이면 영상 제목을 사용합니다.", "Output filename. Empty means use the video title."), 
			"Input URL" => ("영상 페이지 또는 직접 미디어 주소입니다.", "Website page or direct media URL."), 
			"Engine" => ("자동: 일반 페이지는 추출기, M3U8/MPD는 스트림 엔진입니다.", "Automatic page/stream routing."), 
			"Max quality" => ("선택할 최대 원본 화질입니다. 업스케일하지 않습니다.", "Maximum source quality; no upscaling."), 
			"Threads" => ("동시 요청 수입니다. 기본 128, 1~256으로 변경할 수 있습니다.", "Concurrent requests; default 128, editable 1–256."), 
			"Headers" => ("Referer, Cookie, User-Agent 등 서버에 전달할 HTTP 헤더입니다.", "HTTP headers forwarded to the stream server."), 
			"Base URL" => ("상대 경로의 기준 주소입니다.", "Base URL for relative manifest paths."), 
			"Proxy" => ("HTTP 또는 SOCKS5 프록시 서버입니다.", "Optional HTTP/SOCKS5 proxy."), 
			"Retry" => ("실패한 요청을 다시 시도하는 횟수입니다.", "Retries when a request fails."), 
			"Timeout" => ("HTTP 응답을 기다리는 제한 시간(초)입니다.", "HTTP request timeout in seconds."), 
			"Speed Limit" => ("전체 다운로드 속도 제한입니다. 빈칸은 제한 없음입니다.", "Optional maximum download speed."), 
			"Executable" => ("직접 스트림을 받을 때 사용하는 N_m3u8DL-RE 실행 파일입니다.", "N_m3u8DL-RE stream engine executable."), 
			"Temp Dir" => ("다운로드 조각을 임시로 저장하는 위치입니다.", "Temporary segment directory."), 
			"Total size" => ("서버 메타데이터 또는 진행 로그에 표시된 예상 총 용량입니다.", "Estimated total size from server metadata."), 
			"Downloaded" => ("현재 다운로드된 예상 용량입니다.", "Estimated data downloaded so far."), 
			"Speed" => ("현재 수신 속도입니다.", "Current or average transfer rate."), 
			"ETA" => ("속도를 바탕으로 계산한 예상 남은 시간입니다.", "Estimated time remaining; may change."), 
			"Fingerprint" => ("서버 요청에 사용하는 브라우저 TLS 지문 설정입니다.", "Browser TLS impersonation profile."), 
			"Referer" => ("영상 서버가 요구할 수 있는 원래 페이지 주소입니다.", "Referring page address."), 
			"CF Cookie" => ("사용자가 직접 제공한 사이트 쿠키입니다. 공유하지 마세요.", "Optional site cookies. Keep private."), 
			"Delete After Done" => ("병합 성공 뒤 임시 분할 파일을 삭제합니다.", "Delete temporary segments after a successful merge."), 
			"No Date Info" => ("출력 미디어의 날짜 메타데이터를 쓰지 않습니다.", "Omit date metadata from the saved media."), 
			"No System Proxy" => ("Windows 시스템 프록시를 사용하지 않습니다.", "Do not use the Windows system proxy."), 
			"Skip Download" => ("영상 목록·메타정보만 분석하고 실제 파일은 받지 않습니다.", "Inspect formats without downloading media."), 
			"Concurrent DL" => ("영상·오디오·자막 트랙을 병렬로 내려받습니다.", "Fetch audio/video/subtitles concurrently."), 
			"Skip Merge" => ("세그먼트 병합을 생략하여 조각 파일만 보존합니다.", "Skip post-download segment merge."), 
			"Binary Merge" => ("FFmpeg 대신 바이너리 연결 병합을 사용합니다.", "Use binary concatenation when appropriate."), 
			"Audio Only" => ("비디오를 제외하고 음성만 받습니다.", "Download audio without video."), 
			"Skip Check" => ("세그먼트 개수 검증을 생략합니다.", "Skip segment count verification."), 
			"Subtitle Only" => ("오디오·비디오를 제외하고 자막만 받습니다.", "Download subtitles only."), 
			"Auto Sub Fix" => ("자막 타이밍을 자동으로 보정합니다.", "Automatically repair subtitle timing."), 
			"Auto Select" => ("가능한 최고 화질 스트림을 자동 선택합니다.", "Auto-select the best stream."), 
			"Always on Top" => ("다른 창보다 위에 GUI를 고정합니다.", "Keep this window above other windows."), 
			"Enable Cloudflare Bypass" => ("별도의 선택적 Python 네트워크 요청 방식을 사용합니다.", "Use the optional Python-based request method."), 
			"Keep segment files after merge" => ("병합 후 원본 조각 파일을 남깁니다.", "Retain downloaded segments after merging."), 
			"Append URL Params" => ("URL 매개변수를 후속 요청에도 추가합니다.", "Append URL query parameters to subrequests."), 
			"No Log" => ("별도의 엔진 로그 파일을 생성하지 않습니다.", "Do not create a separate engine log file."), 
			"Write Meta JSON" => ("영상 정보를 JSON 파일로 함께 저장합니다.", "Write metadata JSON beside the video."), 
			"FFmpeg Concat" => ("FFmpeg의 concat demuxer로 파일을 병합합니다.", "Use FFmpeg concat demuxer."), 
			"Multi EXT-MAP" => ("HLS에서 여러 EXT-X-MAP 태그를 허용합니다.", "Allow multiple HLS EXT-X-MAP entries."), 
			"No Update Check" => ("다운로드 엔진의 업데이트 확인을 끕니다.", "Disable downloader update checks."), 
			"Force ANSI Console" => ("ANSI 콘솔 출력 방식을 강제합니다.", "Force ANSI console format."), 
			"No ANSI Color" => ("콘솔 로그의 색상 코드를 끕니다.", "Disable ANSI log colors."), 
			"Enable Mux" => ("다운로드 후 영상·음성을 하나의 파일로 합칩니다.", "Mux video and audio into one file."), 
			"MP4 Real-Time Decryption" => ("MP4를 수신 중에 복호화합니다.", "Decrypt compatible MP4 while downloading."), 
			"Realtime Merge" => ("실시간 스트림 수신 중 병합도 수행합니다.", "Merge live segments during recording."), 
			"Pipe Mux" => ("파이프 방식으로 병합 도구에 데이터를 전달합니다.", "Pipe segments directly to the muxer."), 
			"Auto Check GUI Updates on Startup" => ("실행 시 GUI 새 버전 존재 여부를 확인합니다.", "Check for GUI updates on startup."), 
			_ => null, 
		};
	}

	private static (string Korean, string English) GroupHelpFor(string key)
	{
		if (key.Contains("Cloudflare Bypass", StringComparison.Ordinal))
		{
			return (Korean: "일부 요청이 일반 엔진에서 실패할 때 선택적으로 사용하는 별도 HTTP 방식입니다.", English: "Optional alternative HTTP transport for compatible streams.");
		}
		if (key.Contains("Auto-Update", StringComparison.Ordinal))
		{
			return (Korean: "프로그램 새 버전 존재 여부만 확인합니다.", English: "Checks for available application updates.");
		}
		return key switch
		{
			"Verified on this PC" => (Korean: "표시된 사이트는 이 PC에서 지정한 테스트 범위가 실제 통과했습니다.", English: "Source types verified within the stated local test scope."), 
			"yt-dlp website extractor (site dependent)" => (Korean: "설치된 yt-dlp의 추출기 목록이며 개별 사이트의 접속 성공까지 보장하지 않습니다.", English: "Extractor availability varies with each site, account and network."), 
			"Direct streams / browser capture" => (Korean: "브라우저가 발견한 미디어 원본 URL 또는 직접 스트림 주소를 입력하는 모드입니다.", English: "Use original manifest URLs or requests identified by browser capture."), 
			"Basic Paths" => (Korean: "사용할 다운로드 엔진과 임시 파일 경로를 지정합니다.", English: "Select the stream engine and temporary folder."), 
			"Download Flags" => (Korean: "병합·자막·트랙 선택 등 선택적 다운로드 동작입니다.", English: "Optional merge, subtitle and track settings."), 
			"Request & Proxy" => (Korean: "영상 서버에 보내는 헤더와 프록시를 설정합니다.", English: "Customize HTTP headers and proxy."), 
			"Performance & Limits" => (Korean: "재시도 횟수, 시간 제한, 속도 제한 설정입니다.", English: "Retries, timeout and speed controls."), 
			"Keys & Vectors" => (Korean: "사용 권한이 있는 스트림의 암호키와 초기화 벡터를 입력합니다.", English: "Keys and initialization vectors for authorized streams."), 
			"Decryption Engine" => (Korean: "호환되는 스트림의 복호화 실행기를 지정합니다.", English: "Choose a compatible decryption engine."), 
			"Stream Selection (Regex Filters)" => (Korean: "화질·언어·트랙을 필터링합니다.", English: "Filter video/audio/subtitle streams."), 
			"Time Range & Muxing" => (Korean: "다운로드할 구간과 출력 병합 형식을 설정합니다.", English: "Select a time range and output muxing settings."), 
			"Live Recording Options" => (Korean: "생방송 스트림 녹화와 병합 세부 설정입니다.", English: "Live stream recording and merging settings."), 
			"Custom Paths & Variables" => (Korean: "외부 도구 및 파일명 관련 경로 설정입니다.", English: "Custom tool paths and filename variables."), 
			"Environment & System Flags" => (Korean: "다운로드 엔진 환경과 출력 형식 설정입니다.", English: "Downloader environment and console settings."), 
			_ => (Korean: "마우스를 각 항목의 [?]에 올려 기능 설명을 확인하세요.", English: "Hover each option's [?] for details."), 
		};
	}

	private void AppLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (base.IsLoaded && !_suspendParameterRefresh)
		{
			ApplyInterfaceLanguage();
		}
	}

	private void ApplyInterfaceLanguage()
	{
		ComboBox comboBox_AppLanguage = ComboBox_AppLanguage;
		_uiEnglish = comboBox_AppLanguage != null && comboBox_AppLanguage.SelectedIndex == 1;
		Walk(this);
		Version version = Assembly.GetExecutingAssembly().GetName().Version;
		string text = ((version == null) ? string.Empty : $" v{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}");
		base.Title = "UVD — Universal Video Downloader" + text;
		_downloadButtonLabel = Button_GO.Content;
		string text2 = TextBlock_Status.Text;
		if ((text2 == "Ready" || text2 == "준비 완료") ? true : false)
		{
			TextBlock_Status.Text = (_uiEnglish ? "Ready" : "준비 완료");
		}
		RenderDownloadTelemetry();
		void Translate(DependencyObject node, string current, Action<string> apply)
		{
			if (!_uiOriginals.TryGetValue(node, out string value))
			{
				value = current;
				_uiOriginals.Add(node, value);
			}
			apply((_uiEnglish || !SimpleKoreanKeys.Contains(value)) ? value : KoreanUi.GetValueOrDefault(value, value));
		}
		void Walk(DependencyObject node)
		{
			TextBlock label = node as TextBlock;
			if (label != null && !label.Name.StartsWith("TextBlock_", StringComparison.Ordinal))
			{
				Translate(node, label.Text, delegate(string translated)
				{
					label.Text = translated;
				});
			}
			else
			{
				TabItem tab = node as TabItem;
				if (tab != null && tab.Header is string current)
				{
					Translate(node, current, delegate(string translated)
					{
						tab.Header = translated;
					});
				}
				else
				{
					GroupBox group = node as GroupBox;
					if (group != null && group.Header is string current2)
					{
						Translate(node, current2, delegate(string translated)
						{
							group.Header = translated;
						});
					}
					else
					{
						ComboBoxItem option = node as ComboBoxItem;
						if (option != null && option.Content is string text3 && KoreanUi.ContainsKey(text3))
						{
							Translate(node, text3, delegate(string translated)
							{
								option.Content = translated;
							});
						}
						else
						{
							CheckBox checkbox = node as CheckBox;
							if (checkbox != null && checkbox.Content is string current3)
							{
								Translate(node, current3, delegate(string translated)
								{
									checkbox.Content = translated;
								});
							}
							else
							{
								ContentControl content = node as ContentControl;
								if (content != null && content.Content is string current4)
								{
									Translate(node, current4, delegate(string translated)
									{
										content.Content = translated;
									});
								}
							}
						}
					}
				}
			}
			foreach (object child in LogicalTreeHelper.GetChildren(node))
			{
				if (child is DependencyObject node2)
				{
					Walk(node2);
				}
			}
		}
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetForegroundWindow(nint hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool ShowWindowAsync(nint hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, nint processId);

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentThreadId();

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool AttachThreadInput(uint attachThread, uint attachToThread, bool attach);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool BringWindowToTop(nint hWnd);

	[DllImport("user32.dll")]
	private static extern nint SetFocus(nint hWnd);

	private void BringNativeHandoffToFront()
	{
		if (base.WindowState == WindowState.Minimized)
		{
			base.WindowState = WindowState.Normal;
		}
		Show();
		nint handle = new WindowInteropHelper(this).Handle;
		if (handle == IntPtr.Zero)
		{
			return;
		}
		nint foregroundWindow = GetForegroundWindow();
		uint currentThreadId = GetCurrentThreadId();
		uint num = ((foregroundWindow != IntPtr.Zero) ? GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero) : 0u);
		bool flag = false;
		try
		{
			if (num != 0 && num != currentThreadId)
			{
				flag = AttachThreadInput(currentThreadId, num, attach: true);
			}
			ShowWindowAsync(handle, 9);
			BringWindowToTop(handle);
			SetForegroundWindow(handle);
			SetFocus(handle);
		}
		finally
		{
			if (flag)
			{
				AttachThreadInput(currentThreadId, num, attach: false);
			}
		}
		bool keepTopMost = base.Topmost || (CheckBox_TopMost?.IsChecked == true);
		base.Topmost = true;
		Activate();
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			BringWindowToTop(handle);
			SetForegroundWindow(handle);
			if (!keepTopMost)
			{
				base.Topmost = false;
			}
			Activate();
			Focus();
			Button_GO.Focus();
		}, DispatcherPriority.ApplicationIdle);
	}

	private static int? NativeWebsiteQualityHeight(string? quality)
	{
		if (quality == null)
		{
			goto IL_015a;
		}
		if (quality != null)
		{
			int length = quality.Length;
			if (length != 3)
			{
				if (length == 4)
				{
					switch (quality[1])
					{
					case 'e':
						break;
					case '3':
						if (!(quality == "4320"))
						{
							goto IL_01de;
						}
						return 4320;
					case '8':
						if (!(quality == "2880"))
						{
							goto IL_01de;
						}
						return 2880;
					case '1':
						if (!(quality == "2160"))
						{
							goto IL_01de;
						}
						return 2160;
					case '4':
						if (!(quality == "1440"))
						{
							goto IL_01de;
						}
						return 1440;
					case '0':
						if (!(quality == "1080"))
						{
							goto IL_01de;
						}
						return 1080;
					default:
						goto IL_01de;
					}
					if (quality == "best")
					{
						goto IL_015a;
					}
				}
			}
			else
			{
				switch (quality[0])
				{
				case '7':
					if (!(quality == "720"))
					{
						break;
					}
					return 720;
				case '4':
					if (!(quality == "480"))
					{
						break;
					}
					return 480;
				case '3':
					if (!(quality == "360"))
					{
						break;
					}
					return 360;
				case '2':
					if (!(quality == "240"))
					{
						break;
					}
					return 240;
				}
			}
		}
		goto IL_01de;
		IL_015a:
		return null;
		IL_01de:
		throw new InvalidDataException("Unsupported download quality.");
	}

	private static string NativeStreamSelection(string? quality)
	{
		NativeWebsiteQualityHeight(quality);
		if ((quality != null && !(quality == "best")) || 1 == 0)
		{
			return "res=x" + quality + "$:for=best";
		}
		return "best";
	}

	private static string NativeSuggestedTitle(string? title)
	{
		if (string.IsNullOrWhiteSpace(title))
		{
			return string.Empty;
		}
		HashSet<char> invalid = Path.GetInvalidFileNameChars().ToHashSet();
		string text = new string(title.Where((char c) => !invalid.Contains(c) && !char.IsControl(c)).ToArray()).Trim().TrimEnd('.');
		if (text.Length > 180)
		{
			text = text.Substring(0, 180);
		}
		return text;
	}

	private int[] NativeRenditionHeightsFor(string? input)
	{
		if (string.IsNullOrWhiteSpace(input) || _nativeRenditions.Count == 0)
		{
			return Array.Empty<int>();
		}
		if (!_nativeRenditions.Values.Any((NativeRendition x) => string.Equals(x.url, input, StringComparison.Ordinal)))
		{
			return Array.Empty<int>();
		}
		return _nativeRenditions.Keys.OrderByDescending((int x) => x).ToArray();
	}

	private bool TryApplyNativeRendition(int height)
	{
		if (_switchingNativeRendition || !_nativeRenditions.TryGetValue(height, out NativeRendition value))
		{
			return false;
		}
		Uri result;
		bool flag = !Uri.TryCreate(value.url, UriKind.Absolute, out result);
		if (!flag)
		{
			string scheme = result.Scheme;
			bool flag2 = ((scheme == "https" || scheme == "http") ? true : false);
			flag = !flag2;
		}
		if (flag || !string.IsNullOrEmpty(result.UserInfo))
		{
			return false;
		}
		_switchingNativeRendition = true;
		try
		{
			string text = TextBox_Title.Text;
			SetPreferredWebsiteHeight(height);
			TextBox_URL.Text = value.url;
			TextBox_Headers.Text = string.Join("\n", from h in value.headers ?? Array.Empty<NativeHeader>()
				where (object)h != null
				select h.name + ": " + h.value);
			ComboBox_DownloadEngine.SelectedIndex = ((!(value.kind == "FILE")) ? 1 : 2);
			TextBox_SelectVideo.Text = "best";
			SetLinkedFileName(text);
			_titleEditedForCurrentUrl = true;
		}
		finally
		{
			_switchingNativeRendition = false;
		}
		return true;
	}

	private bool TryTakeNativeFallback(out string pageUrl, out string quality)
	{
		pageUrl = _nativeFallbackPageUrl ?? string.Empty;
		quality = _nativeFallbackQuality ?? "best";
		_nativeFallbackPageUrl = null;
		_nativeFallbackQuality = null;
		Uri result;
		bool flag = Uri.TryCreate(pageUrl, UriKind.Absolute, out result);
		if (flag)
		{
			string scheme = result.Scheme;
			bool flag2 = ((scheme == "https" || scheme == "http") ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			return string.IsNullOrEmpty(result.UserInfo);
		}
		return false;
	}

	private async Task RecoverOriginalPageAsync(string pageUrl, string quality)
	{
		TextBox_URL.Text = pageUrl;
		TextBox_Headers.Text = string.Empty;
		ComboBox_DownloadEngine.SelectedIndex = 2;
		SetPreferredWebsiteHeight(NativeWebsiteQualityHeight(quality));
		TextBox_SelectVideo.Text = "best";
		if (BrowserSessionRecoveryPolicy.RequiresChromeRescan(pageUrl))
		{
			AppendLog("The previous CDN playlist is missing (HTTP 404/410).");
			AppendLog("The page extractor is unsupported and a separate WebView2 session may be denied.");
			AppendLog("Reload the ORIGINAL TAB in Chrome, play the video, then UVD Source Capture > Start/Scan.");
			AppendLog("Use the new confirmed HLS/MP4 entry. Do not retry the old playlist.");
			SetStatus(_uiEnglish ? "Stale media. Reload the original Chrome tab, play the video, then UVD Source Capture > Scan." : "영상 주소 만료. 원본 Chrome 탭 새로고침·재생 후 UVD Source Capture에서 다시 스캔하세요.", isError: true);
			ToggleButton_Log.IsChecked = true;
		}
		else
		{
			AppendLog("Refreshing the source from its original page.");
			await StartWebsiteDownloadAsync();
		}
	}

	private void StartNativeInbox()
	{
		_nativeInboxTimer.Tick += delegate
		{
			DrainNativeInbox();
		};
		base.Closed += delegate
		{
			_nativeInboxTimer.Stop();
		};
		_nativeInboxTimer.Start();
		base.Dispatcher.BeginInvoke(new Action(DrainNativeInbox), DispatcherPriority.Background);
	}

	private void DrainNativeInbox()
	{
		if (_nativeInboxDraining || _downloadService.IsDownloading || _browserCaptureRunning || !Button_GO.IsEnabled || !base.IsEnabled)
		{
			return;
		}
		_nativeInboxDraining = true;
		try
		{
			Directory.CreateDirectory(NativeInboxPath);
			Uri result = default(Uri);
			Uri result3 = default(Uri);
			foreach (string item in Directory.EnumerateFiles(NativeInboxPath, "*.json").OrderBy(File.GetCreationTimeUtc).Take(1))
			{
				string text = item + ".processing";
				try
				{
					File.Move(item, text);
				}
				catch (IOException)
				{
					continue;
				}
				try
				{
					string text2 = File.ReadAllText(text);
					if (text2.Length > 262144)
					{
						throw new InvalidDataException("Native message too large.");
					}
					NativeJob nativeJob = JsonSerializer.Deserialize<NativeJob>(text2);
					bool flag = (object)nativeJob == null || nativeJob.version != 1 || !Uri.TryCreate(nativeJob.url, UriKind.Absolute, out result);
					string scheme;
					bool flag2;
					if (!flag)
					{
						scheme = result.Scheme;
						flag2 = ((scheme == "https" || scheme == "http") ? true : false);
						flag = !flag2;
					}
					bool flag3 = flag || !string.IsNullOrEmpty(result.UserInfo) || nativeJob.url.Length > 8192;
					if (!flag3)
					{
						switch (nativeJob.kind)
						{
						case "HLS":
						case "DASH":
						case "MSS":
						case "FILE":
						case "PAGE":
							flag2 = true;
							break;
						default:
							flag2 = false;
							break;
						}
						flag3 = !flag2;
					}
					if (flag3)
					{
						throw new InvalidDataException("Invalid native media job.");
					}
					string[] safe = new string[5] { "Referer", "Origin", "User-Agent", "Accept", "Accept-Language" };
					IEnumerable<string> values = from h in (nativeJob.headers ?? Array.Empty<NativeHeader>()).Where((NativeHeader h) => (object)h != null && safe.Contains<string>(h.name, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(h.value) && h.value.Length <= 2048 && h.value.IndexOfAny(new char[3] { '\r', '\n', '\0' }) < 0).Take(12)
						select h.name + ": " + h.value;
					int? num = NativeWebsiteQualityHeight(nativeJob.maxQuality);
					if (nativeJob.kind == "MSS" && num.HasValue)
					{
						throw new InvalidDataException("MSS has a fixed rendition.");
					}
					scheme = nativeJob.pageUrl;
					flag3 = scheme != null && scheme.Length > 0;
					if (flag3)
					{
						flag = !Uri.TryCreate(nativeJob.pageUrl, UriKind.Absolute, out Uri result2);
						if (!flag)
						{
							scheme = result2.Scheme;
							flag2 = ((scheme == "https" || scheme == "http") ? true : false);
							flag = !flag2;
						}
						flag3 = flag || !string.IsNullOrEmpty(result2.UserInfo);
					}
					if (flag3)
					{
						throw new InvalidDataException("Invalid native page fallback.");
					}
					scheme = nativeJob.pageUrl;
					int num2;
					object obj;
					if (scheme != null && scheme.Length > 0)
					{
						num2 = (((object)SiteAdapterRegistry.ResolvePage(nativeJob.pageUrl) != null) ? 1 : 0);
						if (num2 != 0)
						{
							obj = "PAGE";
							goto IL_0353;
						}
					}
					else
					{
						num2 = 0;
					}
					obj = nativeJob.kind;
					goto IL_0353;
					IL_0353:
					string text3 = (string)obj;
					string text4 = ((num2 != 0) ? nativeJob.pageUrl : nativeJob.url);
					_nativeRenditions.Clear();
					if (num2 == 0)
					{
						foreach (NativeRendition item2 in (nativeJob.renditions ?? Array.Empty<NativeRendition>()).OrderByDescending((NativeRendition x) => x.height).Take(12))
						{
							flag3 = (object)item2 == null;
							if (!flag3)
							{
								int height = item2.height;
								bool flag4 = ((height < 144 || height > 4320) ? true : false);
								flag3 = flag4;
							}
							flag = flag3;
							if (!flag)
							{
								bool flag4;
								switch (item2.kind)
								{
								case "HLS":
								case "DASH":
								case "FILE":
									flag4 = true;
									break;
								default:
									flag4 = false;
									break;
								}
								flag = !flag4;
							}
							flag2 = flag || !Uri.TryCreate(item2.url, UriKind.Absolute, out result3);
							if (!flag2)
							{
								scheme = result3.Scheme;
								bool flag4 = ((scheme == "https" || scheme == "http") ? true : false);
								flag2 = !flag4;
							}
							if (!flag2 && string.IsNullOrEmpty(result3.UserInfo))
							{
								_nativeRenditions.TryAdd(item2.height, item2);
							}
						}
					}
					NativeRendition nativeRendition = null;
					int? num3 = num ?? ((_nativeRenditions.Count > 0) ? new int?(_nativeRenditions.Keys.Max()) : ((int?)null));
					if (num3.HasValue && _nativeRenditions.TryGetValue(num3.Value, out NativeRendition value))
					{
						nativeRendition = value;
						text3 = value.kind;
						text4 = value.url;
					}
					_nativeFallbackPageUrl = ((text3 == "PAGE") ? null : nativeJob.pageUrl);
					_nativeFallbackQuality = ((text3 == "PAGE") ? null : nativeJob.maxQuality);
					TextBox_Headers.Text = ((text3 == "PAGE") ? string.Empty : (((object)nativeRendition != null) ? string.Join("\n", from h in nativeRendition.headers ?? Array.Empty<NativeHeader>()
						where (object)h != null
						select h.name + ": " + h.value) : string.Join("\n", values)));
					SetPreferredWebsiteHeight(num3);
					TextBox_URL.Text = text4;
					string value2 = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
					if (string.IsNullOrWhiteSpace(TextBox_WorkDir.Text) || Path.GetFullPath(TextBox_WorkDir.Text.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Equals(value2, StringComparison.OrdinalIgnoreCase))
					{
						TextBox_WorkDir.Text = Path.Combine(AppContext.BaseDirectory, "Downloads");
					}
					Directory.CreateDirectory(TextBox_WorkDir.Text);
					SetLinkedFileName(NativeSuggestedTitle(nativeJob.title));
					ComboBox comboBox_DownloadEngine = ComboBox_DownloadEngine;
					flag2 = ((text3 == "FILE" || text3 == "PAGE") ? true : false);
					comboBox_DownloadEngine.SelectedIndex = ((!flag2) ? 1 : 2);
					switch (text3)
					{
					case "PAGE":
					case "HLS":
					case "DASH":
					case "FILE":
						flag2 = true;
						break;
					default:
						flag2 = false;
						break;
					}
					SetPreferredWebsiteHeight(flag2 ? num3 : ((int?)null));
					CheckBox_AutoSelect.IsChecked = true;
					TextBox textBox_SelectVideo = TextBox_SelectVideo;
					if ((object)nativeRendition != null)
					{
						scheme = "best";
					}
					else
					{
						flag2 = ((text3 == "HLS" || text3 == "DASH") ? true : false);
						scheme = (flag2 ? NativeStreamSelection(nativeJob.maxQuality) : "best");
					}
					textBox_SelectVideo.Text = scheme;
					_previewProbeCts?.Cancel();
					ScheduleLinkPreview();
					scheme = nativeJob.maxQuality;
					flag2 = ((scheme == null || scheme == "best") ? true : false);
					SetStatus(flag2 ? "Chrome UVD: 가져오기 완료 · 메타데이터·파일명·저장 위치를 확인한 뒤 GO를 누르세요." : ("Chrome UVD: " + nativeJob.maxQuality + "p 선택값 유지 · 설정을 확인한 뒤 GO를 누르세요."));
					BringNativeHandoffToFront();
				}
				catch (Exception)
				{
					SetStatus("Chrome UVD: invalid local media handoff was ignored.", isError: true);
				}
				finally
				{
					try
					{
						File.Delete(text);
					}
					catch (IOException)
					{
					}
				}
			}
		}
		catch (IOException)
		{
		}
		finally
		{
			_nativeInboxDraining = false;
		}
	}

	private bool ResolveOutputNameConflictBeforeDownload()
	{
		if (_batchScriptService.IsBatchInput(TextBox_URL.Text))
		{
			return true;
		}
		string text = OutputConflictPolicy.NormalizeBaseName(TextBox_Title.Text);
		if (string.IsNullOrWhiteSpace(text))
		{
			return true;
		}
		string text2 = OptionValueNormalizer.NormalizeSaveDir(TextBox_WorkDir.Text) ?? Path.Combine(AppContext.BaseDirectory, "Downloads");
		Directory.CreateDirectory(text2);
		IReadOnlyList<string> readOnlyList = OutputConflictPolicy.FindConflicts(text2, text);
		if (readOnlyList.Count == 0)
		{
			return true;
		}
		string text3 = string.Join(Environment.NewLine, from path2 in readOnlyList.Take(4)
			select "• " + Path.GetFileName(path2));
		if (readOnlyList.Count > 4)
		{
			text3 = text3 + Environment.NewLine + $"• 외 {readOnlyList.Count - 4}개";
		}
		FileConflictWindow fileConflictWindow = new FileConflictWindow(text3)
		{
			Owner = this
		};
		if (fileConflictWindow.ShowDialog() != true || fileConflictWindow.Choice == FileConflictChoice.Cancel)
		{
			SetStatus(_uiEnglish ? "Download cancelled." : "다운로드를 취소했습니다.");
			return false;
		}
		if (fileConflictWindow.Choice == FileConflictChoice.Rename)
		{
			string text4 = OutputConflictPolicy.FindUniqueBaseName(text2, text);
			SetLinkedFileName(text4);
			_titleEditedForCurrentUrl = true;
			GetParameter();
			SetStatus(_uiEnglish ? ("Filename changed to " + text4 + ".") : ("파일명을 '" + text4 + "'(으)로 변경했습니다."));
			return true;
		}
		if (fileConflictWindow.Choice != FileConflictChoice.Overwrite)
		{
			return false;
		}
		try
		{
			foreach (string item in readOnlyList)
			{
				File.Delete(item);
			}
			string[] array = new string[7] { ".mp4", ".mkv", ".webm", ".mov", ".m4v", ".ts", ".m2ts" };
			foreach (string text5 in array)
			{
				string[] array2 = new string[2] { ".part", ".resume.json" };
				foreach (string text6 in array2)
				{
					string path = Path.Combine(text2, text + text5 + text6);
					if (File.Exists(path))
					{
						File.Delete(path);
					}
				}
			}
			return true;
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			MessageBox.Show(_uiEnglish ? "The existing file could not be replaced. Close any program using it and try again." : "기존 파일을 덮어쓸 수 없습니다. 파일을 사용 중인 프로그램을 닫은 뒤 다시 시도하세요.", _uiEnglish ? "File in use" : "파일 사용 중", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return false;
		}
	}

	private async Task<bool> RejectKnownMissingChromeHlsAsync()
	{
		if (!BrowserSessionRecoveryPolicy.RequiresChromeRescan(_nativeFallbackPageUrl) || !IsHlsUrl(TextBox_URL.Text))
		{
			return false;
		}
		List<string> list = new List<string>
		{
			"-sS", "--location", "--max-redirs", "3", "--proto", "=http,https", "--proto-redir", "=http,https", "--connect-timeout", "5",
			"--max-time", "10", "--max-filesize", "2097152", "--output", "NUL", "--write-out", "%{http_code}"
		};
		string[] array = (TextBox_Headers.Text ?? string.Empty).Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			int num = text.IndexOf(':');
			if (num > 0)
			{
				string text2 = text.Substring(0, num).Trim();
				string text3 = text;
				int num2 = num + 1;
				string text4 = text3.Substring(num2, text3.Length - num2).Trim();
				bool flag = !CurlHlsHeaderNames.Contains(text2);
				if (!flag)
				{
					num2 = text4.Length;
					bool flag2 = ((num2 < 1 || num2 > 2048) ? true : false);
					flag = flag2;
				}
				if (!flag && text4.IndexOfAny(new char[3] { '\r', '\n', '\0' }) < 0)
				{
					list.Add("-H");
					list.Add(text2 + ": " + text4);
				}
			}
		}
		list.Add("--");
		list.Add(TextBox_URL.Text.Trim());
		try
		{
			using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12L));
			(string, bool) tuple = await RunMetadataProcessAsync("curl.exe", list, timeout.Token);
			if (!BrowserSessionRecoveryPolicy.IsDefinitivelyMissingStatus(tuple.Item1))
			{
				return false;
			}
			if (!TryTakeNativeFallback(out string pageUrl, out string quality))
			{
				return false;
			}
			AppendLog("CDN preflight returned HTTP " + tuple.Item1.Trim() + ". Skipping repeated downloads of the invalid playlist.");
			await RecoverOriginalPageAsync(pageUrl, quality);
			return true;
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		catch (Win32Exception)
		{
			return false;
		}
		catch (IOException)
		{
			return false;
		}
	}

	private CancellationTokenSource BeginCancellableOperation()
	{
		CancellationTokenSource activeOperationCts = _activeOperationCts;
		CancellationTokenSource result = (_activeOperationCts = new CancellationTokenSource());
		if (activeOperationCts != null)
		{
			try
			{
				activeOperationCts.Cancel();
			}
			catch (ObjectDisposedException)
			{
			}
			try
			{
				activeOperationCts.Dispose();
			}
			catch (ObjectDisposedException)
			{
			}
		}
		return result;
	}

	private void EndCancellableOperation(CancellationTokenSource cts)
	{
		if (_activeOperationCts == cts)
		{
			_activeOperationCts = null;
		}
		try
		{
			cts.Dispose();
		}
		catch (ObjectDisposedException)
		{
		}
	}

	private static SolidColorBrush CreateFrozenBrush(Color color)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(color);
		if (solidColorBrush.CanFreeze)
		{
			solidColorBrush.Freeze();
		}
		return solidColorBrush;
	}

	private static string BundledPath(string fileName)
	{
		string text = Path.Combine(AppContext.BaseDirectory, "Runtime", fileName);
		if (File.Exists(text))
		{
			return text;
		}
		return Path.Combine(AppContext.BaseDirectory, fileName);
	}

	private void ApplySimplifiedDefaults()
	{
		ComboBox_DownloadEngine.SelectedIndex = 0;
		TextBox_EXE.Text = BundledPath("N_m3u8DL-RE.exe");
		TextBox_TmpDir.Text = string.Empty;
		CheckBox_TopMost.IsChecked = false;
		CheckBox_Del.IsChecked = false;
		CheckBox_DisableDate.IsChecked = false;
		CheckBox_DisableProxy.IsChecked = false;
		CheckBox_ParserOnly.IsChecked = false;
		CheckBox_Concurrent.IsChecked = false;
		CheckBox_DisableMerge.IsChecked = false;
		CheckBox_BinaryMerge.IsChecked = false;
		CheckBox_AudioOnly.IsChecked = false;
		CheckBox_DisableCheck.IsChecked = false;
		CheckBox_SubOnly.IsChecked = false;
		CheckBox_AutoSubFix.IsChecked = false;
		CheckBox_AutoSelect.IsChecked = true;
		Combo_SubFormat.SelectedIndex = 0;
		TextBox_Key.Text = string.Empty;
		TextBox_CustomHLSKey.Text = string.Empty;
		TextBox_IV.Text = string.Empty;
		TextBox_KeyTextFile.Text = string.Empty;
		TextBox_DecryptionBinPath.Text = string.Empty;
		Combo_DecryptionEngine.SelectedIndex = 0;
		Combo_CustomHLSMethod.SelectedIndex = 0;
		CheckBox_MP4RealTimeDecryption.IsChecked = false;
		TextBox_SelectVideo.Text = "best";
		TextBox_SelectAudio.Text = string.Empty;
		TextBox_SelectSubtitle.Text = string.Empty;
		TextBox_DropVideo.Text = string.Empty;
		TextBox_DropAudio.Text = string.Empty;
		TextBox_DropSubtitle.Text = string.Empty;
		TextBox_MuxJson.Text = string.Empty;
		TextBox_MuxBinPath.Text = string.Empty;
		CheckBox_MuxAfterDone.IsChecked = false;
		CheckBox_MuxKeepFiles.IsChecked = false;
		CheckBox_MuxSkipSub.IsChecked = false;
		Combo_MuxFormat.SelectedIndex = 0;
		Combo_Muxer.SelectedIndex = 0;
		CheckBox_LivePerformAsVod.IsChecked = false;
		CheckBox_LiveRealTimeMerge.IsChecked = false;
		CheckBox_LiveKeepSegments.IsChecked = false;
		CheckBox_LivePipeMux.IsChecked = false;
		CheckBox_LiveFixVttByAudio.IsChecked = false;
		TextBox_LiveRecordLimit.Text = string.Empty;
		TextBox_LiveWaitTime.Text = string.Empty;
		TextBox_LiveTakeCount.Text = "16";
		TextBox_SavePattern.Text = string.Empty;
		TextBox_LogFilePath.Text = string.Empty;
		TextBox_FFmpegPath.Text = BundledPath("ffmpeg.exe");
		TextBox_AdKeyword.Text = string.Empty;
		TextBox_UrlProcessorArgs.Text = string.Empty;
		TextBox_TaskStartAt.Text = string.Empty;
		Combo_LogLevel.SelectedIndex = 2;
		Combo_UILanguage.SelectedIndex = 0;
		CheckBox_AppendUrlParams.IsChecked = false;
		CheckBox_NoLog.IsChecked = true;
		CheckBox_WriteMetaJson.IsChecked = false;
		CheckBox_UseFFmpegConcat.IsChecked = false;
		CheckBox_AllowHlsMultiExtMap.IsChecked = false;
		CheckBox_DisableUpdateCheck.IsChecked = true;
		CheckBox_ForceAnsiConsole.IsChecked = false;
		CheckBox_NoAnsiColor.IsChecked = false;
	}

	public MainWindow()
	{
		InitializeComponent();
		_downloadButtonLabel = Button_GO.Content;
		Version version = Assembly.GetExecutingAssembly().GetName().Version;
		if (version != null)
		{
			string text = $"v{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
			base.Title = "UVD — Universal Video Downloader " + text;
			if (TextBlock_AppVersion != null)
			{
				TextBlock_AppVersion.Text = text;
			}
		}
		base.CommandBindings.Add(new CommandBinding(StartDownloadRoutedCommand, delegate
		{
			Button_GO_Click(Button_GO, new RoutedEventArgs());
		}, delegate(object _, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = base.IsEnabled && Button_GO.IsEnabled;
		}));
		base.CommandBindings.Add(new CommandBinding(StopDownloadRoutedCommand, delegate
		{
			Button_Stop_Click(Button_Stop, new RoutedEventArgs());
		}, delegate(object _, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = Button_Stop.Visibility == Visibility.Visible;
		}));
		TextBox_URL.Focus();
		DataObject.AddPastingHandler(TextBox_URL, TextBox_URL_Pasting);
		IServiceProvider serviceProvider = ViewModelLocator.ServiceProvider;
		_utilityService = serviceProvider.GetRequiredService<IUtilityService>();
		_configService = serviceProvider.GetRequiredService<IConfigService>();
		_batchScriptService = serviceProvider.GetRequiredService<IBatchScriptService>();
		_dragDropService = serviceProvider.GetRequiredService<IDragDropService>();
		_downloadService = serviceProvider.GetRequiredService<IDownloadService>();
		_loadingTimer.Tick += delegate
		{
			UpdateLoadingLabel();
		};
		_telemetryTimer.Tick += delegate
		{
			if (_telemetryActive)
			{
				RenderDownloadTelemetry();
				if (Button_GO.IsEnabled && !_awaitingFirstProgress && !_downloadService.IsDownloading)
				{
					_telemetryActive = false;
				}
			}
		};
		_telemetryTimer.Start();
		base.Closed += delegate
		{
			_telemetryTimer.Stop();
		};
	}

	private void Button_SelectDir_Click(object sender, RoutedEventArgs e)
	{
		string text = _utilityService.SelectFolder("Choose a folder — downloads will be saved here");
		if (!string.IsNullOrEmpty(text))
		{
			TextBox_WorkDir.Text = text;
		}
	}

	private void GetParameter()
	{
		if (!_suspendParameterRefresh && TextBox_Parameter != null)
		{
			TextBox_Parameter.Text = BuildArgsRE(TextBox_URL.Text);
		}
	}

	private void ApplyValidationState(TextBox? textBox, bool isValid)
	{
		if (textBox != null)
		{
			textBox.Tag = (isValid ? null : "invalid");
		}
	}

	private void RefreshValidationState(object? sender = null)
	{
		if (sender == null || sender == TextBox_URL)
		{
			ApplyValidationState(TextBox_URL, TextBox_URL == null || InputValidation.IsLikelyValidInput(TextBox_URL.Text));
		}
		if (sender == null || sender == TextBox_Proxy)
		{
			ApplyValidationState(TextBox_Proxy, TextBox_Proxy == null || InputValidation.IsValidProxy(TextBox_Proxy.Text));
		}
		if (sender == null || sender == TextBox_EXE)
		{
			ApplyValidationState(TextBox_EXE, TextBox_EXE == null || string.IsNullOrWhiteSpace(TextBox_EXE.Text) || File.Exists(TextBox_EXE.Text));
		}
	}

	private string BuildArgsRE(string? inputOverride = null)
	{
		string saveDir = OptionValueNormalizer.NormalizeSaveDir(TextBox_WorkDir.Text);
		string text = TextBox_Title.Text;
		string tmpDir = ResumePaths.ResolveTmpDir(TextBox_TmpDir?.Text, _activeResumeJob?.TmpDir, _activeResumeJob?.SaveName, saveDir, text);
		DownloadOptions obj = new DownloadOptions
		{
			Input = (string.IsNullOrWhiteSpace(inputOverride) ? TextBox_URL.Text : inputOverride),
			SaveDir = saveDir,
			TmpDir = tmpDir,
			SaveName = text,
			Headers = TextBox_Headers.Text,
			BaseUrl = TextBox_Baseurl.Text,
			MuxImport = TextBox_MuxJson.Text?.Trim(),
			Key = TextBox_Key.Text?.Trim(),
			CustomHLSKey = TextBox_CustomHLSKey?.Text?.Trim(),
			CustomHLSIv = TextBox_IV.Text?.Trim(),
			KeyTextFile = TextBox_KeyTextFile?.Text?.Trim(),
			DecryptionEngine = ((Combo_DecryptionEngine?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "MP4DECRYPT"),
			DecryptionBinaryPath = TextBox_DecryptionBinPath?.Text?.Trim(),
			MP4RealTimeDecryption = (CheckBox_MP4RealTimeDecryption?.IsChecked == true),
			CustomHLSMethod = GetComboValue(Combo_CustomHLSMethod),
			Proxy = TextBox_Proxy.Text?.Trim()
		};
		CheckBox checkBox_DisableProxy = CheckBox_DisableProxy;
		obj.UseSystemProxy = checkBox_DisableProxy == null || checkBox_DisableProxy.IsChecked != true;
		obj.RangeStart = TextBox_RangeStart.Text;
		obj.RangeEnd = TextBox_RangeEnd.Text;
		obj.ThreadCount = (int.TryParse(TextBox_Max.Text, out var result) ? result : Environment.ProcessorCount);
		obj.DownloadRetryCount = (int.TryParse(TextBox_Retry.Text, out var result2) ? result2 : 3);
		obj.HttpRequestTimeout = (int.TryParse(TextBox_Timeout.Text, out var result3) ? result3 : 100);
		obj.MaxSpeed = TextBox_MaxSpeed.Text?.Trim();
		obj.DelAfterDone = CheckBox_Del.IsChecked == true;
		obj.NoDateInfo = CheckBox_DisableDate.IsChecked == true;
		obj.SkipDownload = CheckBox_ParserOnly.IsChecked == true;
		obj.SkipMerge = CheckBox_DisableMerge.IsChecked == true;
		obj.BinaryMerge = CheckBox_BinaryMerge.IsChecked == true;
		CheckBox checkBox_DisableCheck = CheckBox_DisableCheck;
		obj.CheckSegmentsCount = checkBox_DisableCheck == null || checkBox_DisableCheck.IsChecked != true;
		obj.ConcurrentDownload = CheckBox_Concurrent?.IsChecked == true;
		obj.SubOnly = CheckBox_SubOnly?.IsChecked == true;
		obj.AutoSubtitleFix = CheckBox_AutoSubFix?.IsChecked == true;
		obj.AutoSelect = CheckBox_AutoSelect?.IsChecked == true;
		obj.SubFormat = (Combo_SubFormat?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "SRT";
		obj.MuxAfterDone = CheckBox_MuxAfterDone?.IsChecked == true;
		obj.MuxFormat = (Combo_MuxFormat?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "mp4";
		obj.Muxer = (Combo_Muxer?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ffmpeg";
		obj.MuxBinPath = TextBox_MuxBinPath?.Text?.Trim();
		obj.MuxKeepFiles = CheckBox_MuxKeepFiles?.IsChecked == true;
		obj.MuxSkipSubtitle = CheckBox_MuxSkipSub?.IsChecked == true;
		obj.LivePerformAsVod = CheckBox_LivePerformAsVod?.IsChecked == true;
		obj.LiveRealTimeMerge = CheckBox_LiveRealTimeMerge?.IsChecked == true;
		CheckBox checkBox_LiveKeepSegments = CheckBox_LiveKeepSegments;
		obj.LiveKeepSegments = checkBox_LiveKeepSegments == null || checkBox_LiveKeepSegments.IsChecked != false;
		obj.LivePipeMux = CheckBox_LivePipeMux?.IsChecked == true;
		obj.LiveFixVttByAudio = CheckBox_LiveFixVttByAudio?.IsChecked == true;
		obj.LiveRecordLimit = TextBox_LiveRecordLimit?.Text?.Trim();
		obj.LiveWaitTime = (int.TryParse(TextBox_LiveWaitTime?.Text, out var result4) ? new int?(result4) : ((int?)null));
		obj.LiveTakeCount = (int.TryParse(TextBox_LiveTakeCount?.Text, out var result5) ? result5 : 16);
		obj.SelectVideo = TextBox_SelectVideo?.Text?.Trim();
		CheckBox checkBox_AudioOnly = CheckBox_AudioOnly;
		obj.SelectAudio = ((checkBox_AudioOnly == null || checkBox_AudioOnly.IsChecked != true) ? TextBox_SelectAudio?.Text?.Trim() : (string.IsNullOrWhiteSpace(TextBox_SelectAudio?.Text) ? "best" : TextBox_SelectAudio.Text.Trim()));
		obj.SelectSubtitle = TextBox_SelectSubtitle?.Text?.Trim();
		CheckBox checkBox_AudioOnly2 = CheckBox_AudioOnly;
		obj.DropVideo = ((checkBox_AudioOnly2 != null && checkBox_AudioOnly2.IsChecked == true) ? ".*" : TextBox_DropVideo?.Text?.Trim());
		obj.DropAudio = TextBox_DropAudio?.Text?.Trim();
		obj.DropSubtitle = TextBox_DropSubtitle?.Text?.Trim();
		obj.SavePattern = TextBox_SavePattern?.Text?.Trim();
		obj.LogFilePath = TextBox_LogFilePath?.Text?.Trim();
		obj.FFmpegBinaryPath = TextBox_FFmpegPath?.Text?.Trim();
		obj.MkvmergeBinaryPath = ((!string.Equals((Combo_Muxer?.SelectedItem as ComboBoxItem)?.Content?.ToString(), "mkvmerge", StringComparison.OrdinalIgnoreCase)) ? null : TextBox_MuxBinPath?.Text?.Trim());
		obj.AdKeyword = TextBox_AdKeyword?.Text?.Trim();
		obj.UrlProcessorArgs = TextBox_UrlProcessorArgs?.Text?.Trim();
		obj.TaskStartAt = TextBox_TaskStartAt?.Text?.Trim();
		obj.LogLevel = (Combo_LogLevel?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "INFO";
		obj.UILanguage = GetComboValue(Combo_UILanguage);
		obj.AppendUrlParams = CheckBox_AppendUrlParams?.IsChecked == true;
		obj.NoLog = CheckBox_NoLog?.IsChecked == true;
		CheckBox checkBox_WriteMetaJson = CheckBox_WriteMetaJson;
		obj.WriteMetaJson = checkBox_WriteMetaJson == null || checkBox_WriteMetaJson.IsChecked != false;
		obj.UseFFmpegConcatDemuxer = CheckBox_UseFFmpegConcat?.IsChecked == true;
		obj.AllowHlsMultiExtMap = CheckBox_AllowHlsMultiExtMap?.IsChecked == true;
		obj.ForceAnsiConsole = CheckBox_ForceAnsiConsole?.IsChecked == true;
		obj.NoAnsiColor = true;
		obj.DisableUpdateCheck = CheckBox_DisableUpdateCheck?.IsChecked == true;
		return ArgsBuilder.Build(obj);
	}

	private DownloadOptions BuildDownloadOptions()
	{
		string saveDir = OptionValueNormalizer.NormalizeSaveDir(TextBox_WorkDir.Text);
		string text = TextBox_Title.Text;
		string tmpDir = ResumePaths.ResolveTmpDir(TextBox_TmpDir?.Text, _activeResumeJob?.TmpDir, _activeResumeJob?.SaveName, saveDir, text);
		DownloadOptions obj = new DownloadOptions
		{
			ExePath = (string.IsNullOrWhiteSpace(TextBox_EXE?.Text) ? null : TextBox_EXE.Text.Trim()),
			Input = TextBox_URL.Text,
			SaveDir = saveDir,
			TmpDir = tmpDir,
			SaveName = text,
			Headers = TextBox_Headers.Text,
			BaseUrl = TextBox_Baseurl.Text,
			MuxImport = TextBox_MuxJson.Text?.Trim(),
			Key = TextBox_Key.Text?.Trim(),
			CustomHLSKey = TextBox_CustomHLSKey?.Text?.Trim(),
			CustomHLSIv = TextBox_IV.Text?.Trim(),
			KeyTextFile = TextBox_KeyTextFile?.Text?.Trim(),
			DecryptionEngine = ((Combo_DecryptionEngine?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "MP4DECRYPT"),
			DecryptionBinaryPath = TextBox_DecryptionBinPath?.Text?.Trim(),
			MP4RealTimeDecryption = (CheckBox_MP4RealTimeDecryption?.IsChecked == true),
			CustomHLSMethod = GetComboValue(Combo_CustomHLSMethod),
			Proxy = TextBox_Proxy.Text?.Trim()
		};
		CheckBox checkBox_DisableProxy = CheckBox_DisableProxy;
		obj.UseSystemProxy = checkBox_DisableProxy == null || checkBox_DisableProxy.IsChecked != true;
		obj.RangeStart = TextBox_RangeStart.Text;
		obj.RangeEnd = TextBox_RangeEnd.Text;
		obj.ThreadCount = (int.TryParse(TextBox_Max.Text, out var result) ? result : Environment.ProcessorCount);
		obj.DownloadRetryCount = (int.TryParse(TextBox_Retry.Text, out var result2) ? result2 : 3);
		obj.HttpRequestTimeout = (int.TryParse(TextBox_Timeout.Text, out var result3) ? result3 : 100);
		obj.MaxSpeed = TextBox_MaxSpeed.Text?.Trim();
		obj.DelAfterDone = CheckBox_Del.IsChecked == true;
		obj.NoDateInfo = CheckBox_DisableDate.IsChecked == true;
		obj.SkipDownload = CheckBox_ParserOnly.IsChecked == true;
		obj.SkipMerge = CheckBox_DisableMerge.IsChecked == true;
		obj.BinaryMerge = CheckBox_BinaryMerge.IsChecked == true;
		CheckBox checkBox_DisableCheck = CheckBox_DisableCheck;
		obj.CheckSegmentsCount = checkBox_DisableCheck == null || checkBox_DisableCheck.IsChecked != true;
		obj.ConcurrentDownload = CheckBox_Concurrent?.IsChecked == true;
		obj.SubOnly = CheckBox_SubOnly?.IsChecked == true;
		obj.AutoSubtitleFix = CheckBox_AutoSubFix?.IsChecked == true;
		obj.AutoSelect = CheckBox_AutoSelect?.IsChecked == true;
		obj.SubFormat = (Combo_SubFormat?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "SRT";
		obj.MuxAfterDone = CheckBox_MuxAfterDone?.IsChecked == true;
		obj.MuxFormat = (Combo_MuxFormat?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "mp4";
		obj.Muxer = (Combo_Muxer?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ffmpeg";
		obj.MuxBinPath = TextBox_MuxBinPath?.Text?.Trim();
		obj.MuxKeepFiles = CheckBox_MuxKeepFiles?.IsChecked == true;
		obj.MuxSkipSubtitle = CheckBox_MuxSkipSub?.IsChecked == true;
		obj.LivePerformAsVod = CheckBox_LivePerformAsVod?.IsChecked == true;
		obj.LiveRealTimeMerge = CheckBox_LiveRealTimeMerge?.IsChecked == true;
		CheckBox checkBox_LiveKeepSegments = CheckBox_LiveKeepSegments;
		obj.LiveKeepSegments = checkBox_LiveKeepSegments == null || checkBox_LiveKeepSegments.IsChecked != false;
		obj.LivePipeMux = CheckBox_LivePipeMux?.IsChecked == true;
		obj.LiveFixVttByAudio = CheckBox_LiveFixVttByAudio?.IsChecked == true;
		obj.LiveRecordLimit = TextBox_LiveRecordLimit?.Text?.Trim();
		obj.LiveWaitTime = (int.TryParse(TextBox_LiveWaitTime?.Text, out var result4) ? new int?(result4) : ((int?)null));
		obj.LiveTakeCount = (int.TryParse(TextBox_LiveTakeCount?.Text, out var result5) ? result5 : 16);
		obj.SelectVideo = TextBox_SelectVideo?.Text?.Trim();
		CheckBox checkBox_AudioOnly = CheckBox_AudioOnly;
		obj.SelectAudio = ((checkBox_AudioOnly == null || checkBox_AudioOnly.IsChecked != true) ? TextBox_SelectAudio?.Text?.Trim() : (string.IsNullOrWhiteSpace(TextBox_SelectAudio?.Text) ? "best" : TextBox_SelectAudio.Text.Trim()));
		obj.SelectSubtitle = TextBox_SelectSubtitle?.Text?.Trim();
		CheckBox checkBox_AudioOnly2 = CheckBox_AudioOnly;
		obj.DropVideo = ((checkBox_AudioOnly2 != null && checkBox_AudioOnly2.IsChecked == true) ? ".*" : TextBox_DropVideo?.Text?.Trim());
		obj.DropAudio = TextBox_DropAudio?.Text?.Trim();
		obj.DropSubtitle = TextBox_DropSubtitle?.Text?.Trim();
		obj.SavePattern = TextBox_SavePattern?.Text?.Trim();
		obj.LogFilePath = TextBox_LogFilePath?.Text?.Trim();
		obj.FFmpegBinaryPath = TextBox_FFmpegPath?.Text?.Trim();
		obj.MkvmergeBinaryPath = ((!string.Equals((Combo_Muxer?.SelectedItem as ComboBoxItem)?.Content?.ToString(), "mkvmerge", StringComparison.OrdinalIgnoreCase)) ? null : TextBox_MuxBinPath?.Text?.Trim());
		obj.AdKeyword = TextBox_AdKeyword?.Text?.Trim();
		obj.UrlProcessorArgs = TextBox_UrlProcessorArgs?.Text?.Trim();
		obj.TaskStartAt = TextBox_TaskStartAt?.Text?.Trim();
		obj.LogLevel = (Combo_LogLevel?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "INFO";
		obj.UILanguage = GetComboValue(Combo_UILanguage);
		obj.AppendUrlParams = CheckBox_AppendUrlParams?.IsChecked == true;
		obj.NoLog = CheckBox_NoLog?.IsChecked == true;
		CheckBox checkBox_WriteMetaJson = CheckBox_WriteMetaJson;
		obj.WriteMetaJson = checkBox_WriteMetaJson == null || checkBox_WriteMetaJson.IsChecked != false;
		obj.UseFFmpegConcatDemuxer = CheckBox_UseFFmpegConcat?.IsChecked == true;
		obj.AllowHlsMultiExtMap = CheckBox_AllowHlsMultiExtMap?.IsChecked == true;
		obj.NoAnsiColor = true;
		obj.DisableUpdateCheck = CheckBox_DisableUpdateCheck?.IsChecked == true;
		return obj;
	}

	private static string? GetComboValue(ComboBox? combo)
	{
		string text = (combo?.SelectedItem as ComboBoxItem)?.Content?.ToString();
		if (!(text == "(Default)") && !string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return null;
	}

	private void TextChanged(object sender, TextChangedEventArgs e)
	{
		RefreshValidationState(sender);
		GetParameter();
		if (sender == TextBox_URL)
		{
			ResetLinkedTitleForUrl();
			ScheduleLinkPreview();
		}
		else if (sender == TextBox_Title)
		{
			MarkFileNameEditedByUser();
		}
	}

	private void CheckBoxChanged(object sender, RoutedEventArgs e)
	{
		SyncDependentControlStates();
		GetParameter();
	}

	private void SyncDependentControlStates()
	{
		bool flag = CheckBox_AudioOnly?.IsChecked == true;
		if (TextBox_SelectAudio != null)
		{
			TextBox_SelectAudio.IsEnabled = !flag;
			TextBox_SelectAudio.ToolTip = (flag ? "Overridden by Audio Only, which forces the best audio track." : "Regex selecting which audio track to download");
		}
		if (TextBox_DropVideo != null)
		{
			TextBox_DropVideo.IsEnabled = !flag;
			TextBox_DropVideo.ToolTip = (flag ? "Overridden by Audio Only, which drops every video track." : "Regex selecting which video tracks to discard");
		}
	}

	private void Combo_SubFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		GetParameter();
	}

	private void Combo_MuxFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		GetParameter();
	}

	private void Combo_Muxer_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		GetParameter();
	}

	private void Combo_DecryptionEngine_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		GetParameter();
	}

	private void Combo_HLSMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		GetParameter();
	}

	private void Combo_LogLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		GetParameter();
	}

	private void Combo_UILanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		GetParameter();
	}

	private void FlashTextBox(TextBox textBox)
	{
		Color color = (textBox.Background as SolidColorBrush)?.Color ?? Colors.White;
		SolidColorBrush value = (SolidColorBrush)(textBox.Background = new SolidColorBrush(color));
		ColorAnimation colorAnimation = new ColorAnimation
		{
			To = (Color)ColorConverter.ConvertFromString("#2ecc71"),
			Duration = TimeSpan.FromMilliseconds(300L, 0L)
		};
		ColorAnimation colorAnimation2 = new ColorAnimation
		{
			To = color,
			BeginTime = TimeSpan.FromMilliseconds(300L, 0L),
			Duration = TimeSpan.FromMilliseconds(1000L, 0L)
		};
		Storyboard obj = new Storyboard
		{
			Children = 
			{
				(Timeline)colorAnimation,
				(Timeline)colorAnimation2
			}
		};
		Storyboard.SetTarget(colorAnimation, value);
		Storyboard.SetTargetProperty(colorAnimation, new PropertyPath(SolidColorBrush.ColorProperty));
		Storyboard.SetTarget(colorAnimation2, value);
		Storyboard.SetTargetProperty(colorAnimation2, new PropertyPath(SolidColorBrush.ColorProperty));
		obj.Begin();
	}

	private void TextBox_URL_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		string text = SafeGetClipboardText();
		if (CurlCommandParser.LooksLikeCurl(text))
		{
			if (TryApplyCapturedRequest(CurlCommandParser.Parse(text)))
			{
				FlashTextBox(TextBox_URL);
			}
			else
			{
				SetStatus("cURL command has no valid target URL. Nothing imported.", isError: true);
			}
			return;
		}
		string text2 = InputValidation.ExtractFirstUrl(text);
		if (text2 != "" && text2 != TextBox_URL.Text)
		{
			TextBox_URL.Text = text2;
			FlashTextBox(TextBox_URL);
		}
	}

	private async void TextBox_Title_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (!string.IsNullOrEmpty(TextBox_URL.Text))
		{
			await PopulateTitleForInputAsync(TextBox_URL.Text, clearWhenUnknown: false);
		}
	}

	private async Task PopulateTitleForInputAsync(string input, bool clearWhenUnknown)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return;
		}
		if (InputValidation.IsHttpUrl(input))
		{
			CancellationTokenSource cts = BeginCancellableOperation();
			try
			{
				SetLinkedFileName(await _utilityService.GetTitleFromUrlAsync(input, cts.Token));
				return;
			}
			catch (OperationCanceledException)
			{
				return;
			}
			finally
			{
				EndCancellableOperation(cts);
			}
		}
		if (File.Exists(input) && DropInputRules.ShouldAutoFillTitleFromFileName(input))
		{
			SetLinkedFileName(Path.GetFileNameWithoutExtension(input));
		}
		else if (clearWhenUnknown)
		{
			SetLinkedFileName(string.Empty);
		}
	}

	private static bool HasFileDropData(DragEventArgs e)
	{
		return e.Data.GetDataPresent(DataFormats.FileDrop, autoConvert: false);
	}

	private static void MarkDragCopy(DragEventArgs e)
	{
		e.Effects = DragDropEffects.Copy;
		e.Handled = true;
	}

	private bool TryGetFirstDroppedPath(DragEventArgs e, out string path)
	{
		path = string.Empty;
		if (!HasFileDropData(e))
		{
			return false;
		}
		string[] filePaths = _dragDropService.GetFilePaths(e.Data);
		if (filePaths.Length == 0 || string.IsNullOrWhiteSpace(filePaths[0]))
		{
			return false;
		}
		path = filePaths[0];
		return true;
	}

	private void TextBox_URL_PreviewDragOver(object sender, DragEventArgs e)
	{
		if (HasFileDropData(e))
		{
			MarkDragCopy(e);
		}
	}

	private void TextBox_URL_PreviewDragEnter(object sender, DragEventArgs e)
	{
		if (HasFileDropData(e))
		{
			MarkDragCopy(e);
		}
	}

	private void TextBox_URL_PreviewDrop(object sender, DragEventArgs e)
	{
		if (!TryGetFirstDroppedPath(e, out string path))
		{
			return;
		}
		if (DropInputRules.IsHarPath(path))
		{
			ImportFromHar(path);
			e.Handled = true;
		}
		else if (DropInputRules.IsSupportedUrlInputPath(path))
		{
			MarkDragCopy(e);
			if (TextBox_URL.Text != path)
			{
				FlashTextBox(TextBox_URL);
			}
			TextBox_URL.Text = path;
			if (DropInputRules.ShouldAutoFillTitleFromFileName(path))
			{
				SetLinkedFileName(Path.GetFileNameWithoutExtension(path));
			}
		}
	}

	private void ImportFromHar(string path)
	{
		IReadOnlyList<CapturedRequest> readOnlyList;
		try
		{
			readOnlyList = HarStreamExtractor.ExtractFromFile(path);
		}
		catch (Exception ex) when (((ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			SetStatus(ex.Message, isError: true);
			return;
		}
		if (readOnlyList.Count == 0)
		{
			SetStatus("No stream was found in that capture. Clear the network log, press play, let it run a few seconds, then save the HAR again.", isError: true);
			return;
		}
		if (readOnlyList.Count == 1)
		{
			TryApplyCapturedRequest(readOnlyList[0]);
			return;
		}
		StreamPickerWindow streamPickerWindow = new StreamPickerWindow(readOnlyList)
		{
			Owner = this
		};
		if (streamPickerWindow.ShowDialog() == true)
		{
			TryApplyCapturedRequest(streamPickerWindow.Selected);
		}
	}

	private void TextBox_MuxJson_PreviewDragEnter(object sender, DragEventArgs e)
	{
		if (HasFileDropData(e))
		{
			MarkDragCopy(e);
		}
	}

	private void TextBox_MuxJson_PreviewDragOver(object sender, DragEventArgs e)
	{
		if (HasFileDropData(e))
		{
			MarkDragCopy(e);
		}
	}

	private void TextBox_MuxJson_PreviewDrop(object sender, DragEventArgs e)
	{
		if (TryGetFirstDroppedPath(e, out string path) && DropInputRules.IsValidMuxImportPath(path))
		{
			MarkDragCopy(e);
			if (TextBox_MuxJson.Text != path)
			{
				FlashTextBox(TextBox_MuxJson);
			}
			TextBox_MuxJson.Text = path;
		}
	}

	private void Window_Closing(object sender, CancelEventArgs e)
	{
		AppConfigState state = MainWindowConfigMapper.Capture(this);
		_configService.Save(UserConfigPath, state);
	}

	private async void Window_Loaded(object sender, RoutedEventArgs e)
	{
		_suspendParameterRefresh = true;
		try
		{
			SetCurrentDirectoryToAppBase();
			AppConfigState config = _configService.Load(UserConfigPath);
			MainWindowConfigMapper.Restore(this, config);
			ApplySimplifiedDefaults();
			string value = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string text = TextBox_WorkDir.Text?.Trim() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(text) || (Path.IsPathFullyQualified(text) && Path.GetFullPath(text).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Equals(value, StringComparison.OrdinalIgnoreCase)))
			{
				TextBox_WorkDir.Text = Path.Combine(AppContext.BaseDirectory, "Downloads");
			}
			Directory.CreateDirectory(TextBox_WorkDir.Text);
			if (!File.Exists(TextBox_EXE.Text))
			{
				string currentDirectory = Environment.CurrentDirectory;
				if (!string.IsNullOrEmpty(currentDirectory))
				{
					FileInfo fileInfo = new DirectoryInfo(currentDirectory).GetFiles("N_m3u8DL-RE.exe").FirstOrDefault();
					if (fileInfo != null)
					{
						TextBox_EXE.Text = fileInfo.FullName;
					}
				}
			}
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			if (commandLineArgs.Length > 1)
			{
				string text2 = commandLineArgs[1];
				if (InputValidation.IsSupportedStartupInputArgument(text2))
				{
					TextBox_URL.Text = text2;
				}
				if (TextBox_URL.Text != "")
				{
					FlashTextBox(TextBox_URL);
					await PopulateTitleForInputAsync(TextBox_URL.Text, clearWhenUnknown: true);
				}
				return;
			}
			string text3 = SafeGetClipboardText();
			if (CurlCommandParser.LooksLikeCurl(text3))
			{
				if (TryApplyCapturedRequest(CurlCommandParser.Parse(text3)))
				{
					FlashTextBox(TextBox_URL);
					await PopulateTitleForInputAsync(TextBox_URL.Text, clearWhenUnknown: false);
				}
				return;
			}
			TextBox_URL.Text = InputValidation.ExtractFirstUrl(text3);
			if (TextBox_URL.Text != "")
			{
				FlashTextBox(TextBox_URL);
				await PopulateTitleForInputAsync(TextBox_URL.Text, clearWhenUnknown: false);
			}
		}
		finally
		{
			_suspendParameterRefresh = false;
			RefreshValidationState();
			SyncDependentControlStates();
			ClampToWorkArea();
			GetParameter();
			ApplyInterfaceLanguage();
			ScheduleLinkPreview();
			Task.Run(delegate
			{
				CleanStaleTempBatchFiles();
			});
			CheckForResumableJob();
			StartNativeInbox();
		}
	}

	private void CheckForResumableJob()
	{
		try
		{
			ResumeJob resumeJob = ResumeJobStore.Default.TryFindResumable();
			if (resumeJob != null && resumeJob.ExistingBytes > 0)
			{
				_activeResumeJob = resumeJob;
				string text = (string.IsNullOrWhiteSpace(resumeJob.SaveName) ? "Unfinished download" : ("Unfinished download — \"" + resumeJob.SaveName + "\""));
				TextBlock_ResumeTitle.Text = "⏸  " + text;
				string text2 = FormatByteSize(resumeJob.ExistingBytes);
				string text3 = FormatTimeAgo(resumeJob.StartedAt);
				string text4 = (string.IsNullOrWhiteSpace(resumeJob.SourceHost) ? string.Empty : (" · from " + resumeJob.SourceHost));
				TextBlock_ResumeDetail.Text = text2 + " already saved · stopped " + text3 + text4;
				Border_ResumeBanner.Visibility = Visibility.Visible;
			}
			else
			{
				_activeResumeJob = null;
				Border_ResumeBanner.Visibility = Visibility.Collapsed;
			}
		}
		catch (Exception)
		{
			_activeResumeJob = null;
			Border_ResumeBanner.Visibility = Visibility.Collapsed;
		}
	}

	private static string FormatByteSize(long bytes)
	{
		if (bytes >= 1024)
		{
			if (bytes >= 1048576)
			{
				if (bytes >= 1073741824)
				{
					return $"{(double)bytes / 1073741824.0:F2} GiB";
				}
				return $"{(double)bytes / 1048576.0:F1} MiB";
			}
			return $"{(double)bytes / 1024.0:F1} KB";
		}
		return $"{bytes} B";
	}

	private static string FormatTimeAgo(DateTimeOffset startedAt)
	{
		TimeSpan timeSpan = DateTimeOffset.UtcNow - startedAt;
		if (timeSpan.TotalMinutes < 1.0)
		{
			return "just now";
		}
		if (timeSpan.TotalMinutes < 60.0)
		{
			int num = Math.Max(1, (int)timeSpan.TotalMinutes);
			return $"{num} minute{((num == 1) ? "" : "s")} ago";
		}
		if (timeSpan.TotalHours < 24.0)
		{
			int num2 = (int)timeSpan.TotalHours;
			return $"{num2} hour{((num2 == 1) ? "" : "s")} ago";
		}
		int num3 = (int)timeSpan.TotalDays;
		return $"{num3} day{((num3 == 1) ? "" : "s")} ago";
	}

	private void Button_ResumeJob_Click(object sender, RoutedEventArgs e)
	{
		if (_activeResumeJob != null)
		{
			TextBox_URL.Text = string.Empty;
			if (!string.IsNullOrWhiteSpace(_activeResumeJob.SaveName))
			{
				SetLinkedFileName(_activeResumeJob.SaveName);
				_titleEditedForCurrentUrl = true;
			}
			if (!string.IsNullOrWhiteSpace(_activeResumeJob.SaveDir))
			{
				TextBox_WorkDir.Text = _activeResumeJob.SaveDir;
			}
			TextBox_URL.Focus();
			Border_ResumeBanner.Visibility = Visibility.Collapsed;
			SetStatus("Paste a fresh link for this video. The original link has expired — that is normal, and everything already downloaded will be kept.");
		}
	}

	private void Button_DiscardJob_Click(object sender, RoutedEventArgs e)
	{
		if (!(_activeResumeJob != null))
		{
			return;
		}
		string value = FormatByteSize(_activeResumeJob.ExistingBytes);
		string value2 = (string.IsNullOrWhiteSpace(_activeResumeJob.SaveName) ? "this download" : ("\"" + _activeResumeJob.SaveName + "\""));
		if (MessageBox.Show($"Delete {value} of partial download for {value2}? This cannot be undone.", "Discard Interrupted Download", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
		{
			if (ResumeJobStore.Default.Discard())
			{
				Border_ResumeBanner.Visibility = Visibility.Collapsed;
				_activeResumeJob = null;
				SetStatus("Partial download discarded.");
			}
			else
			{
				MessageBox.Show("Could not delete partial files. A file may still be in use by another process.", "Discard Failed", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}
	}

	private static void SetCurrentDirectoryToAppBase()
	{
		string baseDirectory = AppContext.BaseDirectory;
		if (!string.IsNullOrWhiteSpace(baseDirectory) && Directory.Exists(baseDirectory))
		{
			Environment.CurrentDirectory = baseDirectory;
			return;
		}
		string text = Process.GetCurrentProcess().MainModule?.FileName;
		string text2 = (string.IsNullOrWhiteSpace(text) ? null : Path.GetDirectoryName(text));
		if (!string.IsNullOrWhiteSpace(text2))
		{
			Environment.CurrentDirectory = text2;
		}
	}

	private void ClampToWorkArea()
	{
		Rect workArea = SystemParameters.WorkArea;
		if (base.ActualHeight > workArea.Height)
		{
			base.Height = workArea.Height;
		}
		if (base.ActualWidth > workArea.Width)
		{
			base.Width = workArea.Width;
		}
		if (base.Top + base.Height > workArea.Bottom)
		{
			base.Top = Math.Max(workArea.Top, workArea.Bottom - base.Height);
		}
	}

	private void BeginLoading(string english = "Preparing download", string korean = "다운로드 준비 중")
	{
		_awaitingFirstProgress = true;
		_loadingStartedUtc = DateTime.UtcNow;
		_loadingStageEn = english;
		_loadingStageKo = korean;
		ProgressBar_Download.IsIndeterminate = true;
		Button_GO.Content = (_uiEnglish ? "Loading…" : "로딩 중…");
		UpdateLoadingLabel();
		_loadingTimer.Start();
		RenderDownloadTelemetry();
	}

	private void UpdateLoadingStage(string english, string korean)
	{
		if (_awaitingFirstProgress)
		{
			_loadingStageEn = english;
			_loadingStageKo = korean;
			UpdateLoadingLabel();
		}
	}

	private void UpdateLoadingLabel()
	{
		if (_awaitingFirstProgress)
		{
			TimeSpan timeSpan = DateTime.UtcNow - _loadingStartedUtc;
			string value = (_uiEnglish ? _loadingStageEn : _loadingStageKo);
			SetStatus($"{value} · {Math.Max(0, (int)timeSpan.TotalMinutes):00}:{timeSpan.Seconds:00}");
		}
	}

	private void FinishLoading(bool transferStarted = false)
	{
		_loadingTimer.Stop();
		_awaitingFirstProgress = false;
		ProgressBar_Download.IsIndeterminate = false;
		Button_GO.Content = ((!transferStarted) ? _downloadButtonLabel : (_uiEnglish ? "Downloading…" : "다운로드 중…"));
	}

	private void OnDownloadProgress(int percent)
	{
		switch (percent)
		{
		default:
			return;
		case 1:
		case 2:
		case 3:
		case 4:
		case 5:
		case 6:
		case 7:
		case 8:
		case 9:
		case 10:
		case 11:
		case 12:
		case 13:
		case 14:
		case 15:
		case 16:
		case 17:
		case 18:
		case 19:
		case 20:
		case 21:
		case 22:
		case 23:
		case 24:
		case 25:
		case 26:
		case 27:
		case 28:
		case 29:
		case 30:
		case 31:
		case 32:
		case 33:
		case 34:
		case 35:
		case 36:
		case 37:
		case 38:
		case 39:
		case 40:
		case 41:
		case 42:
		case 43:
		case 44:
		case 45:
		case 46:
		case 47:
		case 48:
		case 49:
		case 50:
		case 51:
		case 52:
		case 53:
		case 54:
		case 55:
		case 56:
		case 57:
		case 58:
		case 59:
		case 60:
		case 61:
		case 62:
		case 63:
		case 64:
		case 65:
		case 66:
		case 67:
		case 68:
		case 69:
		case 70:
		case 71:
		case 72:
		case 73:
		case 74:
		case 75:
		case 76:
		case 77:
		case 78:
		case 79:
		case 80:
		case 81:
		case 82:
		case 83:
		case 84:
		case 85:
		case 86:
		case 87:
		case 88:
		case 89:
		case 90:
		case 91:
		case 92:
		case 93:
		case 94:
		case 95:
		case 96:
		case 97:
		case 98:
		case 99:
		case 100:
			if (_awaitingFirstProgress)
			{
				FinishLoading(transferStarted: true);
				SetStatus(_uiEnglish ? "Downloading video…" : "영상 다운로드 중…");
			}
			break;
		case 0:
			break;
		}
		ProgressBar_Download.Value = percent;
		if ((double)percent > _displayPercent || percent == 0)
		{
			_displayPercent = percent;
		}
		if (percent > 0)
		{
			long? obj;
			if (!_hasUvdTransferSample || _actualTransferredBytes <= 0)
			{
				long? expectedBytes = _expectedBytes;
				obj = ((expectedBytes.HasValue && expectedBytes.GetValueOrDefault() > 0) ? new long?((long)((double)(_expectedBytes.Value * percent) / 100.0)) : ((long?)null));
			}
			else
			{
				obj = _actualTransferredBytes;
			}
			long? bytes = obj;
			AppendUnifiedProgress(percent, bytes, _expectedBytes, _reportedBytesPerSecond, _reportedEta);
		}
		RenderDownloadTelemetry();
	}

	private void RenderDownloadTelemetry()
	{
		if (TextBlock_ProgressPercent == null)
		{
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		bool flag = _telemetryActive && _sessionStartedUtc != default(DateTime);
		TextBlock_Elapsed.Text = (flag ? FormatInitialEta(utcNow - _sessionStartedUtc) : "—");
		if (flag)
		{
			double? reportedBytesPerSecond = _reportedBytesPerSecond;
			if (reportedBytesPerSecond.HasValue && reportedBytesPerSecond.GetValueOrDefault() > 0.0 && (utcNow - _lastSpeedReportUtc).TotalSeconds < 6.0)
			{
				double num = ((_lastTelemetryRenderUtc == default(DateTime)) ? 0.0 : Math.Clamp((utcNow - _lastTelemetryRenderUtc).TotalSeconds, 0.0, 2.0));
				_integratedTransferBytes += _reportedBytesPerSecond.Value * num;
			}
		}
		_lastTelemetryRenderUtc = utcNow;
		double num2 = (flag ? (utcNow - _sessionStartedUtc).TotalSeconds : 0.0);
		double completedTrackBytes = _completedTrackBytes;
		long? expectedBytes = _expectedBytes;
		double num3 = completedTrackBytes + ((expectedBytes.HasValue && expectedBytes.GetValueOrDefault() > 0) ? Math.Max(_highestTrackBytes, (double)_expectedBytes.Value * _displayPercent / 100.0) : 0.0);
		double num4 = (_hasUvdTransferSample ? _integratedTransferBytes : ((num3 > 0.0) ? num3 : _integratedTransferBytes)) / Math.Max(1.0, num2);
		TextBlock_AverageSpeed.Text = ((flag && num2 >= 1.0 && num4 > 0.0) ? ("≈ " + FormatByteSize((long)num4) + "/s") : "—");
		if (_awaitingFirstProgress)
		{
			TextBlock_ProgressPercent.Text = "···";
			TextBlock_Downloaded.Text = "—";
			TextBlock_ExpectedSize.Text = ((_expectedBytes > 0) ? ((_previewApproximate ? "≈ " : "") + FormatByteSize(_expectedBytes.Value)) : (_uiEnglish ? "Checking…" : "확인 중…"));
			TextBlock_Speed.Text = "—";
			TextBlock_Eta.Text = (_uiEnglish ? "Waiting for data…" : "첫 데이터 대기 중…");
			return;
		}
		if (_previewActive)
		{
			TextBlock_ProgressPercent.Text = "—";
			TextBlock_Downloaded.Text = "—";
			TextBlock_Speed.Text = "—";
			TextBlock_ExpectedSize.Text = ((!_previewPending) ? ((_expectedBytes > 0) ? ((_previewApproximate ? "≈ " : "") + FormatByteSize(_expectedBytes.Value)) : (_uiEnglish ? "Unavailable" : "정보 없음")) : (_uiEnglish ? "Looking up…" : "조회 중…"));
			if (_previewPending)
			{
				TextBlock_Eta.Text = (_uiEnglish ? "Looking up…" : "조회 중…");
			}
			else if (_expectedBytes > 0)
			{
				TimeSpan span = TimeSpan.FromSeconds((double)_expectedBytes.Value / 10485760.0);
				TextBlock_Eta.Text = "≈ " + FormatInitialEta(span) + (_uiEnglish ? " (at 10 MiB/s)" : " (10 MiB/s 가정)");
			}
			else
			{
				TextBlock_Eta.Text = (_uiEnglish ? "Unavailable" : "정보 없음");
			}
			return;
		}
		int num5;
		if (_displayPercent == 0.0)
		{
			expectedBytes = _expectedBytes;
			if (!expectedBytes.HasValue && !_hasUvdTransferSample)
			{
				num5 = ((!_downloadService.IsDownloading) ? 1 : 0);
				goto IL_048a;
			}
		}
		num5 = 0;
		goto IL_048a;
		IL_048a:
		bool flag2 = (byte)num5 != 0;
		TextBlock_ProgressPercent.Text = (flag2 ? "—" : $"{_displayPercent:F1}%");
		TextBlock_ExpectedSize.Text = (_expectedBytes.HasValue ? ((_previewApproximate ? "≈ " : "") + FormatByteSize(_expectedBytes.Value)) : (flag2 ? "—" : (_uiEnglish ? "Checking…" : "확인 중…")));
		TextBlock_Downloaded.Text = (_hasUvdTransferSample ? FormatByteSize(_actualTransferredBytes) : (_expectedBytes.HasValue ? ("≈ " + FormatByteSize((long)((double)_expectedBytes.Value * _displayPercent / 100.0))) : "—"));
		double? num6 = _reportedBytesPerSecond;
		if (num6.HasValue && (utcNow - _lastSpeedReportUtc).TotalSeconds > 6.0)
		{
			num6 = null;
		}
		TextBlock_Speed.Text = ((num6 > 0.0) ? (FormatByteSize((long)num6.Value) + "/s") : "—");
		TimeSpan? timeSpan = _reportedEta;
		if (!timeSpan.HasValue && _expectedBytes > 0 && num6 > 0.0 && _displayPercent > 0.0 && _displayPercent < 100.0)
		{
			double num7 = (double)_expectedBytes.Value * (1.0 - _displayPercent / 100.0) / num6.Value;
			if (double.IsFinite(num7) && num7 >= 0.0 && num7 < TimeSpan.MaxValue.TotalSeconds)
			{
				timeSpan = TimeSpan.FromSeconds(num7);
			}
		}
		TextBlock_Eta.Text = ((_displayPercent >= 100.0) ? "00:00:00" : (timeSpan.HasValue ? FormatInitialEta(timeSpan.Value) : (flag2 ? "—" : (_uiEnglish ? "Calculating…" : "계산 중…"))));
	}

	private static string FormatInitialEta(TimeSpan span)
	{
		if (span.TotalHours >= 1.0)
		{
			return $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}";
		}
		return $"{span.Minutes:00}:{span.Seconds:00}";
	}

	private void Button_PasteCurl_Click(object sender, RoutedEventArgs e)
	{
		string text;
		try
		{
			text = Clipboard.GetText();
		}
		catch (Exception ex)
		{
			SetStatus("Could not read the clipboard: " + ex.Message, isError: true);
			return;
		}
		if (TryApplyCapturedRequest(CurlCommandParser.Parse(text)))
		{
			return;
		}
		if (BatchPasteHelper.LooksLikeBatchList(text))
		{
			if (CaptureDirectives.Parse(text).TryGetValue("headers", out string value) && !string.IsNullOrWhiteSpace(value))
			{
				TextBox_Headers.Text = value;
			}
			string text2 = BatchPasteHelper.WriteTempBatchFile(text);
			TextBox_URL.Text = text2;
			SetStatus("Imported batch list — click GO to download.");
		}
		else
		{
			SetStatus("Clipboard does not contain a cURL command or multi-stream batch list. In your browser extension: click 'Copy as cURL' or 'Copy as list'.", isError: true);
		}
	}

	private void TextBox_URL_Pasting(object sender, DataObjectPastingEventArgs e)
	{
		if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText))
		{
			return;
		}
		string text = e.DataObject.GetData(DataFormats.UnicodeText) as string;
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		if (CurlCommandParser.LooksLikeCurl(text))
		{
			if (!TryApplyCapturedRequest(CurlCommandParser.Parse(text)))
			{
				SetStatus("cURL command has no valid target URL. Nothing imported.", isError: true);
			}
			e.CancelCommand();
		}
		else if (BatchPasteHelper.LooksLikeBatchList(text))
		{
			if (CaptureDirectives.Parse(text).TryGetValue("headers", out string value) && !string.IsNullOrWhiteSpace(value))
			{
				TextBox_Headers.Text = value;
			}
			string text2 = BatchPasteHelper.WriteTempBatchFile(text);
			TextBox_URL.Text = text2;
			SetStatus("Imported batch list — click GO to download.");
			e.CancelCommand();
		}
	}

	private bool TryApplyCapturedRequest(CapturedRequest? captured)
	{
		if ((object)captured == null)
		{
			return false;
		}
		TextBox_URL.Text = captured.Url;
		TextBox_Headers.Text = captured.ToHeaderLines();
		CapturedStreamKind kind = captured.Kind;
		bool flag = (uint)(kind - 1) <= 2u;
		if (flag || (captured.Directives.TryGetValue("engine", out string value) && value.Equals("stream", StringComparison.OrdinalIgnoreCase)))
		{
			ComboBox_DownloadEngine.SelectedIndex = 1;
		}
		else if (captured.Kind == CapturedStreamKind.Page || (captured.Directives.TryGetValue("engine", out value) && value.Equals("website", StringComparison.OrdinalIgnoreCase)))
		{
			ComboBox_DownloadEngine.SelectedIndex = 2;
		}
		if (captured.Directives.TryGetValue("select-video", out string value2) && !string.IsNullOrWhiteSpace(value2))
		{
			TextBox_SelectVideo.Text = value2;
		}
		else if (MediaRequestSelector.IsHighConfidenceMaster(captured))
		{
			CheckBox_AutoSelect.IsChecked = true;
			TextBox_SelectVideo.Text = "best";
		}
		string value3 = ((captured.Kind == CapturedStreamKind.Unknown) ? "stream" : captured.Kind.ToString().ToUpperInvariant());
		string value4 = (captured.Directives.ContainsKey("select-video") ? (" (Quality: " + captured.Directives["select-video"] + ")") : "");
		SetStatus($"Imported {value3} — 1 URL, {captured.Headers.Count} header(s){value4}.");
		return true;
	}

	private void SetStatus(string text, bool isError = false)
	{
		if (isError && _awaitingFirstProgress)
		{
			FinishLoading();
			RenderDownloadTelemetry();
		}
		string text2;
		if (!_uiEnglish)
		{
			if (text == null)
			{
				goto IL_0112;
			}
			int length = text.Length;
			if (length <= 14)
			{
				if (length != 5)
				{
					if (length != 12)
					{
						if (length != 14 || !(text == "Running batch…"))
						{
							goto IL_0112;
						}
						text2 = "일괄 다운로드 중…";
					}
					else
					{
						if (!(text == "Downloading…"))
						{
							goto IL_0112;
						}
						text2 = "다운로드 중…";
					}
				}
				else
				{
					if (!(text == "Ready"))
					{
						goto IL_0112;
					}
					text2 = "준비 완료";
				}
			}
			else if (length <= 33)
			{
				if (length != 27)
				{
					if (length != 33 || !(text == "Downloading website via yt-dlp..."))
					{
						goto IL_0112;
					}
					text2 = "사이트 영상 다운로드 중…";
				}
				else
				{
					if (!(text == "Downloading site adapter..."))
					{
						goto IL_0112;
					}
					text2 = "사이트 전용 다운로드 중…";
				}
			}
			else if (length != 52)
			{
				if (length != 76 || !(text == "Website extractor failed. Check Log, or use browser capture and Stream mode."))
				{
					goto IL_0112;
				}
				text2 = "영상 추출에 실패했습니다. 로그를 확인하거나 브라우저 감지를 사용하세요.";
			}
			else
			{
				if (!(text == "Site-adapter download failed. Check Log for details."))
				{
					goto IL_0112;
				}
				text2 = "사이트 전용 다운로드 실패: 로그를 확인하세요.";
			}
			goto IL_014a;
		}
		goto IL_014d;
		IL_0112:
		string text3;
		if (!text.StartsWith("Saved to ", StringComparison.Ordinal))
		{
			text3 = text;
		}
		else
		{
			string text4 = text;
			int length = "Saved to ".Length;
			text3 = "저장 완료: " + text4.Substring(length, text4.Length - length);
		}
		text2 = text3;
		goto IL_014a;
		IL_014a:
		text = text2;
		goto IL_014d;
		IL_014d:
		TextBlock_Status.Text = text;
		TextBlock_Status.Foreground = (isError ? ErrorBorderBrush : DefaultStatusBrush);
	}

	private void AppendLogLineDirect(string message)
	{
		_logBuffer.AppendLine(message);
		TextBox_Log.AppendText(message + Environment.NewLine);
		if (TextBox_Log.Visibility == Visibility.Visible)
		{
			TextBox_Log.ScrollToEnd();
		}
	}

	private void SetDownloadMethod(string method, string format)
	{
		_activeDownloadMethod = (string.IsNullOrWhiteSpace(method) ? "UVD" : method.Trim());
		_activeDownloadFormat = (string.IsNullOrWhiteSpace(format) ? "자동 감지" : format.Trim());
		_lastUnifiedProgressLog = -1;
		AppendLogLineDirect("[방식] " + _activeDownloadMethod + " | 형식=" + _activeDownloadFormat);
	}

	private void AppendUnifiedProgress(double percent, long? bytes = null, long? total = null, double? speedBytesPerSecond = null, TimeSpan? eta = null)
	{
		int num = Math.Clamp((int)Math.Floor(percent), 0, 100);
		if (num <= _lastUnifiedProgressLog)
		{
			return;
		}
		for (int i = Math.Max(0, _lastUnifiedProgressLog + 1); i <= num; i++)
		{
			if (i == 0 && num > 0)
			{
				continue;
			}
			List<string> list = new List<string> { $"[진행] {i}%" };
			long? num2 = ((total.HasValue && total.GetValueOrDefault() > 0) ? new long?((long)((double)(total.Value * i) / 100.0)) : ((i == num) ? bytes : ((long?)null)));
			if (num2.HasValue && num2.GetValueOrDefault() > 0)
			{
				string text = FormatByteSize(num2.Value);
				if (total.HasValue && total.GetValueOrDefault() > 0)
				{
					text = text + " / " + FormatByteSize(total.Value);
				}
				list.Add("받음=" + text);
			}
			if (speedBytesPerSecond.HasValue && speedBytesPerSecond.GetValueOrDefault() > 0.0)
			{
				list.Add("속도=" + FormatByteSize((long)speedBytesPerSecond.Value) + "/s");
			}
			TimeSpan? timeSpan = null;
			if (total.HasValue && total.GetValueOrDefault() > 0 && num2.HasValue && num2.GetValueOrDefault() > 0 && speedBytesPerSecond.HasValue && speedBytesPerSecond.GetValueOrDefault() > 0.0)
			{
				timeSpan = TimeSpan.FromSeconds(Math.Max(0.0, (double)(total.Value - num2.Value) / speedBytesPerSecond.Value));
			}
			else if (i == num)
			{
				timeSpan = eta;
			}
			if (timeSpan.HasValue && timeSpan.Value >= TimeSpan.Zero)
			{
				list.Add("남은=" + FormatInitialEta(timeSpan.Value));
			}
			list.Add("방식=" + _activeDownloadMethod);
			AppendLogLineDirect(string.Join(" | ", list));
		}
		_lastUnifiedProgressLog = num;
	}

	private void AppendLog(string message)
	{
		if (UvdTransferSample.TryParse(message, out var result))
		{
			_hasUvdTransferSample = true;
			_actualTransferredBytes = result.Bytes;
			_expectedBytes = result.Total;
			_previewApproximate = result.EstimatedTotal;
			double val = Math.Floor(result.Percent);
			_displayPercent = Math.Max(_displayPercent, val);
			ProgressBar_Download.Value = _displayPercent;
			if (_awaitingFirstProgress)
			{
				FinishLoading(transferStarted: true);
				SetStatus(_uiEnglish ? "Receiving media data…" : "영상 데이터 수신 중…");
			}
			if (result.SpeedBytesPerSecond > 0)
			{
				_reportedBytesPerSecond = result.SpeedBytesPerSecond;
				_lastSpeedReportUtc = DateTime.UtcNow;
			}
			if (result.EtaSeconds >= 0)
			{
				_reportedEta = TimeSpan.FromSeconds(result.EtaSeconds);
			}
			if (result.Percent >= 100.0)
			{
				_reportedBytesPerSecond = null;
				_reportedEta = TimeSpan.Zero;
			}
			AppendUnifiedProgress(result.Percent, result.Bytes, result.Total, (result.SpeedBytesPerSecond > 0) ? new double?(result.SpeedBytesPerSecond) : ((double?)null), (result.EtaSeconds >= 0) ? new TimeSpan?(TimeSpan.FromSeconds(result.EtaSeconds)) : ((TimeSpan?)null));
			RenderDownloadTelemetry();
			return;
		}
		if (message.Contains("Content Matched:", StringComparison.OrdinalIgnoreCase) || message.Contains("Selected streams:", StringComparison.OrdinalIgnoreCase))
		{
			_streamManifestRead = true;
		}
		if (message.Contains("Start downloading...", StringComparison.OrdinalIgnoreCase))
		{
			if (_awaitingFirstProgress)
			{
				UpdateLoadingStage("Requesting video segments", "영상 조각 요청 중");
			}
			else
			{
				SetStatus(_uiEnglish ? "Receiving video segments…" : "영상 조각 수신 중…");
			}
		}
		else if (message.Contains("Reading media info...", StringComparison.OrdinalIgnoreCase))
		{
			if (_awaitingFirstProgress)
			{
				UpdateLoadingStage("Reading first media data", "첫 영상 데이터 확인 중");
			}
			else
			{
				SetStatus(_uiEnglish ? "Received media data; analyzing…" : "영상 데이터 수신·분석 중…");
			}
		}
		string text = StreamUrlDiagnostics.ClassifyEngineOutput(message, _streamManifestRead);
		if (text != null)
		{
			_lastEngineErrorCode = text;
			SetStatus(StreamUrlDiagnostics.Message(text, !_uiEnglish), isError: true);
		}
		DownloadTelemetrySample downloadTelemetrySample = DownloadTelemetryParser.Parse(message);
		if (downloadTelemetrySample.Percent.HasValue && downloadTelemetrySample.Percent.Value + 20.0 < _displayPercent)
		{
			_completedTrackBytes += _highestTrackBytes;
			_highestTrackBytes = 0.0;
			_expectedBytes = null;
			_reportedBytesPerSecond = null;
			_reportedEta = null;
			_downloadStartedUtc = DateTime.UtcNow;
		}
		if (downloadTelemetrySample.TotalBytes > 0)
		{
			_expectedBytes = downloadTelemetrySample.TotalBytes;
		}
		if (_awaitingFirstProgress && downloadTelemetrySample.Percent > 0.0)
		{
			FinishLoading(transferStarted: true);
			SetStatus(_uiEnglish ? "Downloading video…" : "영상 다운로드 중…");
		}
		if (downloadTelemetrySample.Percent.HasValue)
		{
			double value = (_displayPercent = Math.Floor(downloadTelemetrySample.Percent.Value));
			ProgressBar_Download.Value = value;
		}
		if (downloadTelemetrySample.BytesPerSecond > 0.0)
		{
			_reportedBytesPerSecond = downloadTelemetrySample.BytesPerSecond;
			_lastSpeedReportUtc = DateTime.UtcNow;
		}
		if (_expectedBytes > 0 && downloadTelemetrySample.Percent.HasValue)
		{
			_highestTrackBytes = Math.Max(_highestTrackBytes, (double)_expectedBytes.Value * downloadTelemetrySample.Percent.Value / 100.0);
		}
		if (downloadTelemetrySample.Eta.HasValue)
		{
			_reportedEta = downloadTelemetrySample.Eta;
		}
		if (downloadTelemetrySample.Percent.HasValue || downloadTelemetrySample.TotalBytes.HasValue || downloadTelemetrySample.BytesPerSecond.HasValue || downloadTelemetrySample.Eta.HasValue)
		{
			if (downloadTelemetrySample.Percent.HasValue)
			{
				long? totalBytes = downloadTelemetrySample.TotalBytes;
				long? bytes = ((totalBytes.HasValue && totalBytes.GetValueOrDefault() > 0) ? new long?((long)((double)downloadTelemetrySample.TotalBytes.Value * downloadTelemetrySample.Percent.Value / 100.0)) : ((long?)null));
				AppendUnifiedProgress(downloadTelemetrySample.Percent.Value, bytes, downloadTelemetrySample.TotalBytes, downloadTelemetrySample.BytesPerSecond, downloadTelemetrySample.Eta);
			}
			RenderDownloadTelemetry();
		}
		if (downloadTelemetrySample.Percent.HasValue)
		{
			return;
		}
		if (Regex.IsMatch(message, "^\\[\\d{1,3}%\\]"))
		{
			if (message.Contains("Reading HLS playlists", StringComparison.OrdinalIgnoreCase))
			{
				AppendLogLineDirect("[단계] HLS 재생목록 분석");
			}
			else if (message.Contains("Remuxing", StringComparison.OrdinalIgnoreCase))
			{
				AppendLogLineDirect("[단계] HLS 조각 검증 완료 | MP4 병합 중");
			}
			else if (message.Contains("complete", StringComparison.OrdinalIgnoreCase))
			{
				AppendLogLineDirect("[완료] " + _activeDownloadMethod + " | 형식=" + _activeDownloadFormat);
			}
		}
		else
		{
			AppendLogLineDirect("[엔진] " + message);
		}
	}

	private void ResetRunState()
	{
		_lastEngineErrorCode = null;
		_streamManifestRead = false;
		_lastUnifiedProgressLog = -1;
		_logBuffer.Clear();
		TextBox_Log.Text = string.Empty;
		ProgressBar_Download.Value = 0.0;
		_downloadStartedUtc = DateTime.UtcNow;
		_sessionStartedUtc = _downloadStartedUtc;
		_lastTelemetryRenderUtc = _downloadStartedUtc;
		_lastSpeedReportUtc = default(DateTime);
		_integratedTransferBytes = 0.0;
		_completedTrackBytes = 0.0;
		_highestTrackBytes = 0.0;
		_telemetryActive = true;
		_hasUvdTransferSample = false;
		_actualTransferredBytes = 0L;
		_expectedBytes = ((_previewUrl == TextBox_URL.Text.Trim()) ? _previewTotalBytes : ((long?)null));
		_previewProbeCts?.Cancel();
		_previewActive = false;
		_previewPending = false;
		_previewUnavailable = false;
		_reportedBytesPerSecond = null;
		_reportedEta = null;
		_displayPercent = 0.0;
		if (!_awaitingFirstProgress)
		{
			BeginLoading();
		}
		else
		{
			UpdateLoadingStage("Preparing download", "다운로드 준비 중");
			RenderDownloadTelemetry();
		}
		Button_OpenFolder.Visibility = Visibility.Collapsed;
	}

	private void ToggleButton_Log_Changed(object sender, RoutedEventArgs e)
	{
		TextBox_Log.Visibility = ((ToggleButton_Log.IsChecked != true) ? Visibility.Collapsed : Visibility.Visible);
	}

	private void Button_OpenFolder_Click(object sender, RoutedEventArgs e)
	{
		if (!string.IsNullOrWhiteSpace(_lastOutputDirectory) && Directory.Exists(_lastOutputDirectory))
		{
			StartShellTarget(_lastOutputDirectory);
		}
	}

	private async void Button_GO_Click(object sender, RoutedEventArgs e)
	{
		_userCancelledDownload = false;
		if (!string.IsNullOrWhiteSpace(TextBox_Key.Text))
		{
			string text = TextBox_Key.Text.Trim();
			if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					string s = text.Substring(2).Replace("-", "").Replace(" ", "");
					TextBox_Key.Text = Convert.ToBase64String(Convert.FromHexString(s));
				}
				catch (Exception)
				{
					MessageBox.Show("Invalid Hex Key format. Please check your key.", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
					return;
				}
			}
			else
			{
				string text2 = text.Replace("-", "").Replace(" ", "");
				if (text2.Length > 0 && text2.Length % 2 == 0 && Regex.IsMatch(text2, "\\A\\b[0-9a-fA-F]+\\b\\Z") && !text.Contains(':') && !text.EndsWith("="))
				{
					try
					{
						TextBox_Key.Text = Convert.ToBase64String(Convert.FromHexString(text2));
					}
					catch (Exception)
					{
						MessageBox.Show("Invalid Hex Key format. Please check your key.", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
						return;
					}
				}
			}
		}
		UniversalEngine universalEngine = UniversalDownloadRouter.Select(TextBox_URL.Text, ComboBox_DownloadEngine.SelectedIndex);
		string text3 = ((universalEngine == UniversalEngine.Stream) ? StreamUrlDiagnostics.CheckStreamInput(TextBox_URL.Text) : null);
		if (text3 != null)
		{
			SetStatus(StreamUrlDiagnostics.Message(text3, !_uiEnglish), isError: true);
			return;
		}
		if (universalEngine != UniversalEngine.Website && !File.Exists(TextBox_EXE.Text))
		{
			MessageBox.Show("N_m3u8DL-RE.exe was not found.\n\nSet its path on the Download tab, or right-click the Executable field and choose Get Downloader.", "Downloader Not Found", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (TextBox_URL.Text == "")
		{
			MessageBox.Show("Enter a URL or file path first.", "Missing Input", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
		else if (!InputValidation.IsValidProxy(TextBox_Proxy.Text))
		{
			MessageBox.Show("Proxy must start with http:// or socks5://.", "Invalid Proxy", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
		else
		{
			if (!ResolveOutputNameConflictBeforeDownload())
			{
				return;
			}
			BeginLoading();
			if (AbyssMetadataFetcher.IsAbyssUrl(TextBox_URL.Text))
			{
				Button_GO.IsEnabled = false;
				Button_Stop.Visibility = Visibility.Visible;
				try
				{
					await StartAbyssDownloadAsync(TextBox_URL.Text);
					return;
				}
				finally
				{
					FinishLoading();
					RenderDownloadTelemetry();
					Button_GO.IsEnabled = true;
					Button_Stop.Visibility = Visibility.Collapsed;
				}
			}
			if (!_batchScriptService.IsBatchInput(TextBox_URL.Text))
			{
				Button_GO.IsEnabled = false;
				Button_Stop.Visibility = Visibility.Visible;
				try
				{
					if (universalEngine == UniversalEngine.Website)
					{
						if (PreferCurlMp4Transport(TextBox_URL.Text))
						{
							ResetRunState();
							if (await StartCurlMp4TransportAsync())
							{
								OnDownloadProgress(100);
								SetStatus("Saved to " + _lastOutputDirectory);
								Button_OpenFolder.Visibility = Visibility.Visible;
							}
							else if (!_userCancelledDownload)
							{
								SetStatus("Direct MP4 transfer incomplete. Verified ranges were preserved.", isError: true);
								ToggleButton_Log.IsChecked = true;
							}
						}
						else
						{
							await StartWebsiteDownloadAsync();
						}
						return;
					}
					if (PreferCurlHlsTransport(TextBox_URL.Text))
					{
						ResetRunState();
						string pageUrl;
						string quality;
						if (await StartCurlHlsTransportAsync("this CDN rejects N_m3u8DL-RE/FFmpeg HTTP clients but accepts Windows curl"))
						{
							ProgressBar_Download.Value = 100.0;
							SetStatus("Saved to " + _lastOutputDirectory);
							Button_OpenFolder.Visibility = Visibility.Visible;
						}
						else if (!_userCancelledDownload && TryTakeNativeFallback(out pageUrl, out quality))
						{
							AppendLog("curl HLS transport failed. Recovering from the original page.");
							await RecoverOriginalPageAsync(pageUrl, quality);
						}
						else if (!_userCancelledDownload)
						{
							SetStatus("Browser-compatible HLS transfer failed — open the Log for details.", isError: true);
							ToggleButton_Log.IsChecked = true;
						}
						return;
					}
					string text4 = BuildArgsRE();
					TextBox_Parameter.Text = text4;
					DownloadOptions options = BuildDownloadOptions();
					_lastOutputDirectory = (string.IsNullOrWhiteSpace(options.SaveDir) ? Environment.CurrentDirectory : options.SaveDir);
					ResetRunState();
					string text5 = options.Input ?? string.Empty;
					string format = (IsHlsUrl(text5) ? "HLS / M3U8" : (text5.Contains(".mpd", StringComparison.OrdinalIgnoreCase) ? "MPEG-DASH / MPD" : (text5.Contains(".mp4", StringComparison.OrdinalIgnoreCase) ? "MP4 직접 스트림" : "스트림 자동 감지")));
					SetDownloadMethod("N_m3u8DL-RE", format);
					if (await RejectKnownMissingChromeHlsAsync())
					{
						return;
					}
					UpdateLoadingStage("Connecting to stream server", "영상 서버 연결 중");
					ResumeJobStore.Default.Begin(options.Input, options.SaveName ?? string.Empty, options.SaveDir ?? string.Empty, options.TmpDir ?? string.Empty);
					Progress<int> progressCallback = new Progress<int>(OnDownloadProgress);
					Action<string> logCallback = delegate(string line)
					{
						base.Dispatcher.InvokeAsync(delegate
						{
							AppendLog(line);
						});
					};
					if (await _downloadService.StartDownloadAsync(options, progressCallback, logCallback))
					{
						ResumeJobStore.Default.Complete();
						_activeResumeJob = null;
						ProgressBar_Download.Value = 100.0;
						SetStatus("Saved to " + _lastOutputDirectory);
						Button_OpenFolder.Visibility = Visibility.Visible;
						return;
					}
					if (_userCancelledDownload)
					{
						SetStatus(_uiEnglish ? "Download stopped." : "다운로드 중지됨");
						return;
					}
					bool flag = !_userCancelledDownload;
					if (flag)
					{
						bool flag2;
						switch (_lastEngineErrorCode)
						{
						case "MANIFEST_GONE":
						case "SEGMENT_GONE":
						case "MANIFEST_NOT_FOUND":
						case "SEGMENT_NOT_FOUND":
						case "MANIFEST_ACCESS_DENIED":
						case "SEGMENT_ACCESS_DENIED":
							flag2 = true;
							break;
						default:
							flag2 = false;
							break;
						}
						flag = flag2;
					}
					if (flag && IsHlsUrl(TextBox_URL.Text))
					{
						ResumeJobStore.Default.Discard();
						_activeResumeJob = null;
						string pageUrl2;
						string quality2;
						if (await StartCurlHlsTransportAsync("N_m3u8DL-RE received an HTTP access/not-found/gone response"))
						{
							ProgressBar_Download.Value = 100.0;
							SetStatus("Saved to " + _lastOutputDirectory);
							Button_OpenFolder.Visibility = Visibility.Visible;
						}
						else if (TryTakeNativeFallback(out pageUrl2, out quality2))
						{
							AppendLog("curl HLS recovery failed. Rejecting the expired CDN source.");
							await RecoverOriginalPageAsync(pageUrl2, quality2);
						}
						else
						{
							SetStatus("HLS transport recovery failed — open the Log for details.", isError: true);
							ToggleButton_Log.IsChecked = true;
						}
						return;
					}
					switch (_lastEngineErrorCode)
					{
					case "MANIFEST_GONE":
					case "SEGMENT_GONE":
					case "MANIFEST_NOT_FOUND":
					case "SEGMENT_NOT_FOUND":
						flag = true;
						break;
					default:
						flag = false;
						break;
					}
					if (flag && TryTakeNativeFallback(out string pageUrl3, out string quality3))
					{
						ResumeJobStore.Default.Discard();
						_activeResumeJob = null;
						AppendLog("Captured CDN URL is stale. Recovering from the original page.");
						await RecoverOriginalPageAsync(pageUrl3, quality3);
					}
					else
					{
						TryTakeNativeFallback(out string _, out string _);
						MainWindow mainWindow = this;
						string lastEngineErrorCode = _lastEngineErrorCode;
						mainWindow.SetStatus((lastEngineErrorCode != null) ? StreamUrlDiagnostics.Message(lastEngineErrorCode, !_uiEnglish) : "Download failed — open the Log for details.", isError: true);
						ToggleButton_Log.IsChecked = true;
					}
					return;
				}
				catch (Exception ex3)
				{
					SetStatus("Download error: " + ex3.Message, isError: true);
					ToggleButton_Log.IsChecked = true;
					return;
				}
				finally
				{
					FinishLoading();
					RenderDownloadTelemetry();
					Button_GO.IsEnabled = true;
					Button_Stop.Visibility = Visibility.Collapsed;
				}
			}
			base.IsEnabled = false;
			Button_GO.Content = "Working…";
			BatchScriptBuildResult result = null;
			CancellationTokenSource cts = BeginCancellableOperation();
			try
			{
				CancellationToken token = cts.Token;
				result = await _batchScriptService.BuildScriptAsync(TextBox_URL.Text, TextBox_EXE.Text, (string url) => _utilityService.GetTitleFromUrlAsync(url, token), BuildArgsRE, delegate(string title)
				{
					TextBox_Title.Text = title;
				}, token);
				_batchScriptService.SaveScript(result.FilePath, result.Content);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception ex5)
			{
				MessageBox.Show(ex5.Message, "Batch build failed", MessageBoxButton.OK, MessageBoxImage.Hand);
				return;
			}
			finally
			{
				EndCancellableOperation(cts);
				FinishLoading();
				RenderDownloadTelemetry();
				Button_GO.Content = _downloadButtonLabel;
				base.IsEnabled = true;
			}
			if (!(result != null))
			{
				return;
			}
			Button_GO.IsEnabled = false;
			Button_Stop.Visibility = Visibility.Visible;
			_lastOutputDirectory = OptionValueNormalizer.NormalizeSaveDir(TextBox_WorkDir.Text) ?? Environment.CurrentDirectory;
			ResetRunState();
			UpdateLoadingStage("Preparing batch download", "일괄 다운로드 준비 중");
			Progress<int> progressCallback2 = new Progress<int>(OnDownloadProgress);
			Action<string> logCallback2 = delegate(string line)
			{
				base.Dispatcher.InvokeAsync(delegate
				{
					AppendLog(line);
				});
			};
			try
			{
				if (await _downloadService.StartProcessAsync(result.FilePath, string.Empty, logCallback2, progressCallback2))
				{
					ProgressBar_Download.Value = 100.0;
					SetStatus("Batch finished. Saved to " + _lastOutputDirectory);
					Button_OpenFolder.Visibility = Visibility.Visible;
				}
				else
				{
					SetStatus("Batch failed — open the Log for details.", isError: true);
					ToggleButton_Log.IsChecked = true;
				}
			}
			catch (Exception ex6)
			{
				SetStatus("Batch error: " + ex6.Message, isError: true);
				ToggleButton_Log.IsChecked = true;
			}
			finally
			{
				FinishLoading();
				RenderDownloadTelemetry();
				Button_GO.IsEnabled = true;
				Button_Stop.Visibility = Visibility.Collapsed;
				try
				{
					if (File.Exists(result.FilePath))
					{
						File.Delete(result.FilePath);
					}
				}
				catch (Exception)
				{
				}
			}
		}
	}

	private async Task StartWebsiteDownloadAsync()
	{
		DownloadOptions options = BuildDownloadOptions();
		int num = SelectedWebsiteHeight();
		string text = Path.Combine(AppContext.BaseDirectory, "Downloads");
		Directory.CreateDirectory(text);
		string text2 = ((!string.IsNullOrWhiteSpace(options.SaveDir)) ? options.SaveDir : text);
		SiteAdapterDescriptor siteAdapter = SiteAdapterRegistry.ResolvePage(options.Input);
		bool directRangedMp4 = UniversalDownloadRouter.IsPornvovDirectMp4(options.Input);
		string fileName;
		IReadOnlyList<string> arguments;
		if ((object)siteAdapter != null)
		{
			string text3 = BundledPath(siteAdapter.DownloadHelper);
			if (!File.Exists(text3))
			{
				throw new FileNotFoundException("The registered site-adapter helper is not bundled.", text3);
			}
			fileName = "node.exe";
			arguments = new string[7]
			{
				text3,
				options.Input ?? string.Empty,
				text2,
				num.ToString(CultureInfo.InvariantCulture),
				((options.ThreadCount > 0) ? options.ThreadCount : 128).ToString(CultureInfo.InvariantCulture),
				"--name",
				options.SaveName ?? string.Empty
			};
		}
		else if (directRangedMp4)
		{
			string text4 = BundledPath("range-mp4.cjs");
			if (!File.Exists(text4))
			{
				throw new FileNotFoundException("Ranged MP4 downloader is missing.", text4);
			}
			fileName = "node.exe";
			arguments = new string[4]
			{
				text4,
				options.Input ?? string.Empty,
				text2,
				"6"
			};
		}
		else
		{
			string text5 = BundledPath("yt-dlp.exe");
			fileName = (File.Exists(text5) ? text5 : "yt-dlp.exe");
			List<string> list = WebsiteArgsBuilder.Build(options.Input ?? string.Empty, text2, options.SaveName, (options.ThreadCount > 0) ? options.ThreadCount : 128, num).ToList();
			string path = BundledPath("ffmpeg.exe");
			if (File.Exists(path))
			{
				list.Insert(0, Path.GetDirectoryName(path) ?? AppContext.BaseDirectory);
				list.Insert(0, "--ffmpeg-location");
			}
			if (!string.IsNullOrWhiteSpace(options.Headers))
			{
				HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Referer", "Origin", "User-Agent", "Accept", "Accept-Language" };
				string[] array = options.Headers.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string text6 in array)
				{
					int num2 = text6.IndexOf(':');
					if (num2 > 0)
					{
						string text7 = text6.Substring(0, num2).Trim();
						string text8 = text6;
						int num3 = num2 + 1;
						string text9 = text8.Substring(num3, text8.Length - num3).Trim();
						bool flag = !hashSet.Contains(text7);
						if (!flag)
						{
							num3 = text9.Length;
							bool flag2 = ((num3 < 1 || num3 > 2048) ? true : false);
							flag = flag2;
						}
						if (!flag && !text9.Contains('\0'))
						{
							list.Insert(list.Count - 1, "--add-header");
							list.Insert(list.Count - 1, text7 + ": " + text9);
						}
					}
				}
			}
			arguments = list;
		}
		_lastOutputDirectory = text2;
		ResetRunState();
		SetDownloadMethod(directRangedMp4 ? "HTTP Range" : (siteAdapter?.DownloadMethod ?? "yt-dlp"), directRangedMp4 ? "MP4 직접 파일" : (siteAdapter?.OutputFormat ?? "페이지 추출 → 영상/음성 병합"));
		TextBox_Parameter.Text = "The video URL and signed media tokens are not printed here.";
		UpdateLoadingStage(directRangedMp4 ? "Connecting to selected MP4 rendition" : (siteAdapter?.LoadingStageEn ?? "Extracting website media"), directRangedMp4 ? "선택한 고화질 파일 병렬 전송 중" : (siteAdapter?.LoadingStageKo ?? "사이트 영상 분석 중"));
		AppendLog(directRangedMp4 ? "Direct player rendition: verified HTTP 206 ranged transfer, 6 concurrent requests, 8 MiB segments, atomic resume." : (((object)siteAdapter != null) ? $"{siteAdapter.LogLabel}; configured concurrency {Math.Clamp(options.ThreadCount, 1, 256)}." : $"Website engine: yt-dlp; max quality {((num == 0) ? "source" : (num + "p"))}; fragments {Math.Clamp(options.ThreadCount, 1, 256)}."));
		AppendLog("Video credentials and signed URLs are not added to the command preview.");
		Progress<int> progressCallback = new Progress<int>(OnDownloadProgress);
		Action<string> logCallback = delegate(string line)
		{
			base.Dispatcher.InvokeAsync(delegate
			{
				AppendLog(line);
			});
		};
		Uri result;
		if (await _downloadService.StartProcessAsync(fileName, arguments, logCallback, progressCallback))
		{
			ProgressBar_Download.Value = 100.0;
			SetStatus("Saved to " + _lastOutputDirectory);
			Button_OpenFolder.Visibility = Visibility.Visible;
		}
		else if (directRangedMp4)
		{
			SetStatus(_userCancelledDownload ? "Ranged download stopped. Resume data retained." : "Direct MP4 transfer paused/failed. Existing .part and .resume.json are retained for retry.", !_userCancelledDownload);
		}
		else if ((object)siteAdapter == null && !_userCancelledDownload && Uri.TryCreate(options.Input, UriKind.Absolute, out result) && (result.Scheme == Uri.UriSchemeHttps || result.Scheme == Uri.UriSchemeHttp))
		{
			AppendLog("Website extractor did not complete. Opening embedded media detection…");
			BeginLoading("Opening browser fallback", "브라우저 대체 감지 준비 중");
			await TryBrowserCaptureAsync(options.Input);
		}
		else
		{
			SetStatus(((object)siteAdapter != null) ? siteAdapter.FailureStatusEn : (_userCancelledDownload ? "Download stopped." : "Website extractor failed. Use the existing Chrome extension as fallback."), !_userCancelledDownload);
		}
	}

	private void Button_Stop_Click(object sender, RoutedEventArgs e)
	{
		_userCancelledDownload = true;
		CancellationTokenSource activeOperationCts = _activeOperationCts;
		if (activeOperationCts != null)
		{
			try
			{
				activeOperationCts.Cancel();
			}
			catch (ObjectDisposedException)
			{
			}
		}
		_downloadService.StopDownload();
		FinishLoading();
		RenderDownloadTelemetry();
		SetStatus(_uiEnglish ? "Stopping download…" : "다운로드 중지 중…");
		Button_Stop.Visibility = Visibility.Collapsed;
	}

	private void Button_CopyCommand_Click(object sender, RoutedEventArgs e)
	{
		if (!string.IsNullOrWhiteSpace(TextBox_Parameter?.Text))
		{
			try
			{
				Clipboard.SetText(TextBox_Parameter.Text);
				FlashTextBox(TextBox_Parameter);
			}
			catch (Exception)
			{
			}
		}
	}

	private void TextBox_URL_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return)
		{
			Button_GO.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
		}
	}

	private void TextBox_Title_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return)
		{
			Button_GO.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
		}
	}

	private void SetTopMost(object sender, RoutedEventArgs e)
	{
		base.Topmost = CheckBox_TopMost.IsChecked == true;
	}

	private void Menu_GetDownloader(object sender, RoutedEventArgs e)
	{
		StartShellTarget("https://github.com/nilaoda/N_m3u8DL-RE/releases");
	}

	private static void StartShellTarget(string targetPath)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = targetPath,
				UseShellExecute = true
			});
		}
		catch (Win32Exception)
		{
		}
	}

	private async Task StartAbyssDownloadAsync(string url)
	{
		CancellationTokenSource cts = BeginCancellableOperation();
		_lastOutputDirectory = OptionValueNormalizer.NormalizeSaveDir(TextBox_WorkDir.Text) ?? Environment.CurrentDirectory;
		ResetRunState();
		SetStatus("Fetching Abyss/Hydrax video metadata…");
		AppendLog($"{DateTime.Now:HH:mm:ss.fff} INFO : Abyss/Hydrax video stream detected: {url}");
		try
		{
			Dictionary<string, string> customHeaders = HeaderParser.Parse(TextBox_Headers?.Text);
			if (customHeaders.TryGetValue("Referer", out string value))
			{
				AppendLog($"{DateTime.Now:HH:mm:ss.fff} INFO : Custom Referer: {value}");
			}
			AbyssMp4 abyssMp = await AbyssMetadataFetcher.FetchMetadataAsync(url, customHeaders, null, cts.Token);
			if (abyssMp.Sources == null || abyssMp.Sources.Count == 0)
			{
				throw new InvalidOperationException("No downloadable video streams found in Abyss metadata.");
			}
			AbyssSource abyssSource = abyssMp.Sources.OrderByDescending((AbyssSource s) => s.Size).First();
			string text = _utilityService.GetValidFileName(TextBox_Title.Text);
			if (string.IsNullOrWhiteSpace(text))
			{
				text = "abyss_" + abyssMp.Slug + "_" + abyssSource.Label;
			}
			if (!text.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
			{
				text += ".mp4";
			}
			string outputPath = Path.Combine(_lastOutputDirectory, text);
			AppendLog($"{DateTime.Now:HH:mm:ss.fff} INFO : Video: {abyssMp.Slug} | Quality: {abyssSource.Label} | Size: {(double)abyssSource.Size / 1048576.0:F1} MB | Codec: {abyssSource.Codec}");
			AppendLog($"{DateTime.Now:HH:mm:ss.fff} INFO : Output file: {outputPath}");
			SetStatus("Downloading Abyss stream: " + abyssSource.Label + "…");
			AbyssDownloadService abyssDownloadService = new AbyssDownloadService();
			Progress<AbyssDownloadProgress> progress = new Progress<AbyssDownloadProgress>(delegate(AbyssDownloadProgress p)
			{
				base.Dispatcher.InvokeAsync(delegate
				{
					ProgressBar_Download.Value = (int)p.Percentage;
					SetStatus($"Downloading Abyss: {p}");
				});
			});
			Action<string> log = delegate(string line)
			{
				base.Dispatcher.InvokeAsync(delegate
				{
					AppendLog(line);
				});
			};
			await abyssDownloadService.DownloadAsync(abyssMp, abyssSource, outputPath, customHeaders, 8, progress, log, cts.Token);
			ProgressBar_Download.Value = 100.0;
			SetStatus("Saved to " + outputPath);
			Button_OpenFolder.Visibility = Visibility.Visible;
		}
		catch (OperationCanceledException)
		{
			SetStatus("Download stopped by user.");
			AppendLog($"{DateTime.Now:HH:mm:ss.fff} WARN : Download cancelled by user.");
		}
		catch (Exception ex2)
		{
			SetStatus("Download error: " + ex2.Message, isError: true);
			AppendLog($"{DateTime.Now:HH:mm:ss.fff} ERROR : {ex2.Message}");
			ToggleButton_Log.IsChecked = true;
		}
		finally
		{
			EndCancellableOperation(cts);
		}
	}


	private static void CleanStaleTempBatchFiles()
	{
		try
		{
			DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetTempPath());
			string[] array = new string[2] { "batch_*.bat", "paste_*.txt" };
			foreach (string searchPattern in array)
			{
				foreach (FileInfo item in from f in directoryInfo.GetFiles(searchPattern)
					where (DateTime.Now - f.LastWriteTime).TotalHours > 1.0
					select f)
				{
					try { item.Delete(); } catch { }
				}
			}
		}
		catch (Exception) { }
	}

	private static string SafeGetClipboardText()
	{
		try
		{
			return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
		}
		catch (ExternalException)
		{
			return string.Empty;
		}
		catch (InvalidOperationException)
		{
			return string.Empty;
		}
	}

	private static void StartExecutableWithArguments(string executablePath, string arguments)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = executablePath,
			Arguments = arguments,
			UseShellExecute = false
		});
	}

	public static Encoding DetectFileEncoding(string filePath)
	{
		return TextEncodingDetector.DetectFromFile(filePath);
	}

	public static Encoding DetectFileEncoding(FileStream stream)
	{
		return TextEncodingDetector.DetectFromStream(stream);
	}

	private void TextBox_Key_PreviewDragEnter(object sender, DragEventArgs e)
	{
		if (HasFileDropData(e))
		{
			MarkDragCopy(e);
		}
	}

	private void TextBox_Key_PreviewDragOver(object sender, DragEventArgs e)
	{
		if (HasFileDropData(e))
		{
			MarkDragCopy(e);
		}
	}

	private void TextBox_Key_PreviewDrop(object sender, DragEventArgs e)
	{
		if (TryGetFirstDroppedPath(e, out string path))
		{
			MarkDragCopy(e);
			if (DropInputRules.IsValidKeyFilePath(path, 16L))
			{
				TextBox_Key.Text = path;
			}
			else
			{
				MessageBox.Show("That file is not a valid key file. A raw HLS key must be exactly 16 bytes.", "Invalid Key File", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
		}
	}

}
