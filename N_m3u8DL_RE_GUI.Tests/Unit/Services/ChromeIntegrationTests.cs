using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using N_m3u8DL_RE_GUI.Services;
using Xunit;

namespace N_m3u8DL_RE_GUI.Tests.Unit.Services;

public class ChromeIntegrationTests
{
    [Fact]
    public void ManifestKey_ProducesTheFixedExtensionId()
    {
        var root = FindRepositoryRoot();
        var manifestPath = Path.Combine(root, "UVD-Source-Capture", "manifest.json");
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var key = document.RootElement.GetProperty("key").GetString();

        Assert.False(string.IsNullOrWhiteSpace(key));
        Assert.Equal(ChromeIntegrationService.FixedExtensionId, ComputeExtensionId(key!));
    }

    [Fact]
    public void Installer_DefaultsToTheFixedExtensionId()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "Setup", "install-native-host.ps1"));

        Assert.Contains($"[string]$ExtensionId='{ChromeIntegrationService.FixedExtensionId}'", script);
        Assert.DoesNotContain("[Parameter(Mandatory=$true)]", script);
    }

    [Fact]
    public void TroubleshootingUi_ExposesChromeIntegrationControls()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "N_m3u8DL_RE_GUI", "MainWindow.xaml"));

        Assert.Contains("TextBlock_ChromeIntegrationStatus", xaml);
        Assert.Contains("Button_ChromeIntegrationSetup", xaml);
        Assert.Contains("Button_ChromeIntegrationTest", xaml);
        Assert.Contains("Button_ChromeExtensionOpen", xaml);
        Assert.Contains("Button_ChromeExtensionFolder", xaml);
    }

    [Fact]
    public void NativeHost_HandoffLaunch_IsDetachedAndUsesVersionedInboxMarker()
    {
        var root = FindRepositoryRoot();
        var host = File.ReadAllText(Path.Combine(root, "UvdNativeHost", "Program.cs"));
        var app = File.ReadAllText(Path.Combine(root, "N_m3u8DL_RE_GUI", "App.xaml.cs"));
        var main = File.ReadAllText(Path.Combine(root, "N_m3u8DL_RE_GUI", "MainWindow.cs"));

        Assert.Contains("UseShellExecute = true", host);
        Assert.Contains("Mutex.OpenExisting(NativeInboxInstanceMarkerName)", host);
        Assert.DoesNotContain("Process.GetProcessesByName(\"UniversalVideoDownloader\")", host);
        Assert.Contains("NativeInboxV2", app);
        Assert.Contains("NativeInboxV2", main);
    }

    private static string ComputeExtensionId(string publicKeyBase64)
    {
        var publicKey = Convert.FromBase64String(publicKeyBase64);
        var hash = SHA256.HashData(publicKey);
        const string alphabet = "abcdefghijklmnop";
        var builder = new StringBuilder(32);

        for (var i = 0; i < 16; i++)
        {
            builder.Append(alphabet[(hash[i] >> 4) & 0x0F]);
            builder.Append(alphabet[hash[i] & 0x0F]);
        }

        return builder.ToString();
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "UVD-Source-Capture", "manifest.json")) &&
                Directory.Exists(Path.Combine(dir.FullName, "N_m3u8DL_RE_GUI")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the UVD source root.");
    }
}
