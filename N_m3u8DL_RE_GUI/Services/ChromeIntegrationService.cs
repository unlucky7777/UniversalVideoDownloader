using Microsoft.Win32;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace N_m3u8DL_RE_GUI.Services;

public sealed record ChromeIntegrationStatus(
    bool FilesPresent,
    bool RegistryRegistered,
    bool ManifestValid,
    string ExtensionId,
    string ExtensionDirectory,
    string ManifestPath,
    string NativeHostPath)
{
    public bool IsReady => FilesPresent && RegistryRegistered && ManifestValid;

    public string Summary => IsReady
        ? $"연결됨 · Extension {ExtensionId}"
        : $"연결 필요 · files={(FilesPresent ? "ok" : "missing")}, registry={(RegistryRegistered ? "ok" : "missing")}, manifest={(ManifestValid ? "ok" : "invalid")}";
}

public sealed class ChromeIntegrationService
{
    public const string NativeHostName = "com.uvd.bridge";
    public const string FixedExtensionId = "mpncbnfjghomdnfaakmepbbkmdffeage";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string InstallRoot => AppContext.BaseDirectory.TrimEnd(
        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public string ExtensionDirectory => Path.Combine(InstallRoot, "Extension", "UVD-Source-Capture");
    public string NativeHostPath => Path.Combine(InstallRoot, "Runtime", "UvdNativeHost.exe");
    public string SetupDirectory => Path.Combine(InstallRoot, "Setup");
    public string ManifestPath => Path.Combine(SetupDirectory, "com.uvd.bridge.json");

    private const string RegistryPath = @"Software\Google\Chrome\NativeMessagingHosts\com.uvd.bridge";

    public ChromeIntegrationStatus GetStatus()
    {
        var filesPresent = File.Exists(NativeHostPath) &&
                           File.Exists(Path.Combine(InstallRoot, "UniversalVideoDownloader.exe")) &&
                           Directory.Exists(ExtensionDirectory);

        string? registeredManifest = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: false);
            registeredManifest = key?.GetValue(null) as string;
        }
        catch
        {
            registeredManifest = null;
        }

        var registryRegistered =
            !string.IsNullOrWhiteSpace(registeredManifest) &&
            PathsEqual(registeredManifest!, ManifestPath);

        var manifestValid = IsManifestValid();

        return new ChromeIntegrationStatus(
            filesPresent,
            registryRegistered,
            manifestValid,
            FixedExtensionId,
            ExtensionDirectory,
            ManifestPath,
            NativeHostPath);
    }

    public ChromeIntegrationStatus RegisterOrRepair()
    {
        if (!File.Exists(NativeHostPath))
            throw new FileNotFoundException("UvdNativeHost.exe를 찾을 수 없습니다.", NativeHostPath);
        if (!File.Exists(Path.Combine(InstallRoot, "UniversalVideoDownloader.exe")))
            throw new FileNotFoundException("UniversalVideoDownloader.exe를 찾을 수 없습니다.");
        if (!Directory.Exists(ExtensionDirectory))
            throw new DirectoryNotFoundException("UVD Source Capture 확장프로그램 폴더를 찾을 수 없습니다.");

        Directory.CreateDirectory(SetupDirectory);

        var manifest = new
        {
            name = NativeHostName,
            description = "UVD Source Capture native bridge",
            path = NativeHostPath,
            type = "stdio",
            allowed_origins = new[] { $"chrome-extension://{FixedExtensionId}/" }
        };

        File.WriteAllText(
            ManifestPath,
            JsonSerializer.Serialize(manifest, JsonOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true)
            ?? throw new InvalidOperationException("Chrome Native Messaging 레지스트리 키를 만들 수 없습니다.");
        key.SetValue(null, ManifestPath, RegistryValueKind.String);

        return GetStatus();
    }

    public async Task<string> TestNativeHostAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(NativeHostPath))
            throw new FileNotFoundException("UvdNativeHost.exe를 찾을 수 없습니다.", NativeHostPath);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        var startInfo = new ProcessStartInfo
        {
            FileName = NativeHostPath,
            WorkingDirectory = InstallRoot,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Native Host를 시작하지 못했습니다.");

        var payload = JsonSerializer.SerializeToUtf8Bytes(new { type = "PING" });
        var prefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);

        await process.StandardInput.BaseStream.WriteAsync(prefix, timeout.Token);
        await process.StandardInput.BaseStream.WriteAsync(payload, timeout.Token);
        await process.StandardInput.BaseStream.FlushAsync(timeout.Token);
        process.StandardInput.Close();

        var replyPrefix = new byte[4];
        await process.StandardOutput.BaseStream.ReadExactlyAsync(replyPrefix, timeout.Token);
        var replyLength = BinaryPrimitives.ReadInt32LittleEndian(replyPrefix);
        if (replyLength is < 1 or > 262144)
            throw new InvalidDataException("Native Host 응답 길이가 올바르지 않습니다.");

        var reply = new byte[replyLength];
        await process.StandardOutput.BaseStream.ReadExactlyAsync(reply, timeout.Token);
        await process.WaitForExitAsync(timeout.Token);

        using var document = JsonDocument.Parse(reply);
        var root = document.RootElement;
        if (!root.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            throw new InvalidOperationException("Native Host가 정상 응답하지 않았습니다.");

        var appPresent = root.TryGetProperty("appPresent", out var app) && app.GetBoolean();
        var inboxWritable = root.TryGetProperty("inboxWritable", out var inbox) && inbox.GetBoolean();

        if (!appPresent)
            throw new InvalidOperationException("Native Host가 UVD 실행 파일을 찾지 못했습니다.");
        if (!inboxWritable)
            throw new InvalidOperationException("Native Inbox에 쓸 수 없습니다.");

        return "Native Host PING 정상 · UVD 감지 정상 · Native Inbox 쓰기 정상";
    }

    public void OpenChromeExtensionsPage()
    {
        var chrome = FindChromeExecutable();
        if (chrome is null)
            throw new FileNotFoundException("Chrome 실행 파일을 찾을 수 없습니다.");

        var info = new ProcessStartInfo
        {
            FileName = chrome,
            UseShellExecute = true
        };
        info.ArgumentList.Add("chrome://extensions");
        Process.Start(info);
    }

    private bool IsManifestValid()
    {
        if (!File.Exists(ManifestPath))
            return false;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(ManifestPath));
            var root = document.RootElement;
            if (root.GetProperty("name").GetString() != NativeHostName)
                return false;
            if (!PathsEqual(root.GetProperty("path").GetString() ?? string.Empty, NativeHostPath))
                return false;
            if (root.GetProperty("type").GetString() != "stdio")
                return false;

            var expectedOrigin = $"chrome-extension://{FixedExtensionId}/";
            return root.GetProperty("allowed_origins")
                .EnumerateArray()
                .Any(x => string.Equals(x.GetString(), expectedOrigin, StringComparison.Ordinal));
        }
        catch
        {
            return false;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? FindChromeExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
