using System;
using System.IO;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.UI;

public sealed class AutoDetectUiParityTests
{
    private static string ReadGuiSource(string file)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "N_m3u8DL_RE_GUI", file));
        return File.ReadAllText(path);
    }

    private static string ReadSource(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", relative));
        return File.ReadAllText(path);
    }

    private static string ReadMainWindow() => ReadGuiSource("MainWindow.cs");

    [Fact]
    public void AutoDetect_ExposesExtensionParityTools()
    {
        var tools = ReadGuiSource("BrowserCaptureWindow.Tools.cs");
        foreach (var label in new[]
        {
            "UVD 연결 확인", "영상 우클릭 허용", "URL + 헤더 복사",
            "URL 복사", "진단 정보 복사", "감지 기록 지우기", "감지 중지"
        })
            Assert.Contains(label, tools);
    }

    [Fact]
    public void AutoDetect_UsesSharedReadOnlyPageMetadataProbe()
    {
        var window = ReadGuiSource("BrowserCaptureWindow.cs");
        var tools = ReadGuiSource("BrowserCaptureWindow.Tools.cs");
        var probe = ReadGuiSource("YtDlpMetadataProbe.cs");

        Assert.Contains("BrowserPageCandidate.Create(pageUrl)", window);
        Assert.Contains("YtDlpMetadataProbe.ProbeAsync", tools);
        Assert.Contains("--dump-single-json", probe);
        Assert.Contains("--skip-download", probe);
        Assert.DoesNotContain("\"-f\", \"bv*+ba/b\"", probe);
    }

    [Fact]
    public void SupportedFormatsUi_DoesNotNameSpecialCaseSite()
    {
        var xaml = ReadGuiSource("MainWindow.xaml");
        Assert.DoesNotContain("Eporner", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnifiedReviewUi_ExposesMetadataFilenameQualityAndDestination()
    {
        var xaml = ReadGuiSource("MainWindow.xaml");
        foreach (var name in new[]
        {
            "Border_LinkMetadata", "TextBlock_MetaTitle", "TextBlock_MetaQuality",
            "TextBlock_MetaDuration", "TextBlock_MetaSize", "TextBlock_MetaFormat",
            "TextBlock_MetaCodec", "TextBox_WorkDir", "ComboBox_WebsiteQuality"
        })
            Assert.Contains($"x:Name=\"{name}\"", xaml);

        Assert.Equal(1, xaml.Split("x:Name=\"TextBox_Title\"").Length - 1);
        Assert.Contains("File name", xaml);
    }

    [Fact]
    public void NativeInbox_ImportsForReviewAndNeverClicksGo()
    {
        var main = ReadMainWindow();
        Assert.Contains("ScheduleLinkPreview();", main);
        Assert.Contains("Button_GO.Focus();", main);
        Assert.Contains("NativeSuggestedTitle(", main);
        Assert.Contains("BringNativeHandoffToFront();", main);
        Assert.DoesNotContain("Button_GO_Click(Button_GO,new RoutedEventArgs())", main);
    }

    [Fact]
    public void WebsiteQualitySelector_IsBuiltFromActualMetadataOnlyAndSortedDescending()
    {
        var xaml = ReadGuiSource("MainWindow.xaml");
        var main = ReadMainWindow();

        Assert.DoesNotContain("<ComboBoxItem Content=\"4K (2160p)\" Tag=\"2160\"/>", xaml);
        Assert.DoesNotContain("<ComboBoxItem Content=\"1080p\" Tag=\"1080\"/>", xaml);
        Assert.Contains("ComboBox_WebsiteQuality.Items.Clear();", main);
        Assert.Contains("OrderByDescending", main);
        Assert.Contains("WebsiteQualityLabel(", main);
    }

    [Fact]
    public void MainReviewLayout_UsesReadableMetadataGridAndLargerDefaultWindow()
    {
        var xaml = ReadGuiSource("MainWindow.xaml");
        Assert.Contains("Height=\"760\" Width=\"1100\"", xaml);
        Assert.Contains("Foreground=\"{StaticResource TextPrimaryBrush}\" FontSize=\"12\"", xaml);
        Assert.Contains("TextBlock_MetaQuality", xaml);
        Assert.Contains("TextBlock_MetaCodec", xaml);
    }

    [Fact]
    public void NewUrl_ReplacesAutoFileNameWithFreshMetadataUnlessUserEditedIt()
    {
        var main = ReadMainWindow();
        Assert.Contains("ResetLinkedTitleForUrl();", main);
        Assert.Contains("MarkFileNameEditedByUser();", main);
        Assert.Contains("SetLinkedFileName(", main);
        Assert.Contains("_titleEditedForCurrentUrl", main);
    }

    [Fact]
    public void ExistingOutput_ShowsExplicitOverwriteRenameCancelChoiceBeforeDownload()
    {
        var main = ReadMainWindow();
        var dialog = ReadGuiSource(Path.Combine("Views", "FileConflictWindow.xaml"));

        Assert.Contains("ResolveOutputNameConflictBeforeDownload()", main);
        Assert.Contains("OutputConflictPolicy.FindUniqueBaseName", main);
        Assert.Contains("new FileConflictWindow", main);
        Assert.Contains("ShowDialog()", main);
        Assert.Contains("Content=\"덮어쓰기\"", dialog);
        Assert.Contains("Content=\"이름 바꾸기\"", dialog);
        Assert.Contains("Content=\"취소\"", dialog);
    }

    [Fact]
    public void SiteAdapter_HandoffUsesCentralRegistryAndCommonProbeContract()
    {
        var main = ReadMainWindow();
        var registry = ReadSource(Path.Combine("N_m3u8DL_RE_GUI.Core", "SiteAdapterRegistry.cs"));
        var nativeAdapters = ReadSource(Path.Combine("UvdNativeHost", "SiteMetadataAdapters.cs"));

        Assert.Contains("SiteAdapterRegistry.ResolvePage(", main);
        Assert.Contains("ProbeSiteAdapterMetadataAsync", main);
        Assert.Contains("MetadataHelper", main);
        Assert.Contains("probe-json", main);
        Assert.Contains("Central registry for site-specific exceptions", registry);
        Assert.Contains("common", nativeAdapters, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NativeHost_DeclaresReviewBeforeDownloadAndCarriesTitle()
    {
        var host = ReadSource(Path.Combine("UvdNativeHost", "Program.cs"));
        Assert.Contains("reviewBeforeDownload=true", host);
        Assert.Contains("reviewRequired = true", host);
        Assert.Contains("suggestedTitle", host);
    }

    [Fact]
    public void ExtensionPrimaryActions_OpenUvdReviewInsteadOfClaimingImmediateDownload()
    {
        var popup = ReadSource(Path.Combine("UVD-Source-Capture", "popup.js"));
        Assert.Contains("UVD 검토 화면", popup);
        Assert.Contains("선택한 화질은 그대로 유지됩니다", popup);
        Assert.DoesNotContain("UVD에 다운로드 작업을 전달했습니다.", popup);
    }

    [Fact]
    public void NativeHandoff_CarriesVerifiedRenditionsAndUvdCanSwitchExactQuality()
    {
        var host = ReadSource(Path.Combine("UvdNativeHost", "Program.cs"));
        var main = ReadMainWindow();
        var worker = ReadSource(Path.Combine("UVD-Source-Capture", "background.js"));

        Assert.Contains("renditionHandoffSupported=true", host);
        Assert.Contains("record Rendition", host);
        Assert.Contains("renditions.OrderByDescending", host);
        Assert.Contains("_nativeRenditions", main);
        Assert.Contains("TryApplyNativeRendition", main);
        Assert.Contains("NativeRenditionHeightsFor", main);
        Assert.Contains("familyCandidates(source,stored)", worker);
        Assert.Contains("curlVerified===true", worker);
    }

    [Fact]
    public void DownloadLogs_UseUnifiedMethodAndOnePercentProgressRecords()
    {
        var main = ReadMainWindow();

        Assert.Contains("SetDownloadMethod(", main);
        Assert.Contains("SetDownloadMethod(\"Windows curl HLS\"", main);
        Assert.Contains("Math.Floor(", main);
        Assert.Contains("OnDownloadProgress(", main);
    }
}
