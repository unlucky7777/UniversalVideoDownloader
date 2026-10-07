# Universal Video Downloader (UVD)

Universal Video Downloader is a Windows desktop application for reviewing and downloading media from HLS, DASH, MSS, direct media files, and supported web pages.

The project combines a WPF desktop application, a Chrome Manifest V3 companion extension, a Native Messaging bridge, and established media tools such as N_m3u8DL-RE, yt-dlp and FFmpeg.

## Current architecture

UVD prefers standardized extraction before using site-specific behavior:

1. Generic HLS / DASH / MSS / direct-media handling.
2. Generic page extraction through yt-dlp.
3. Browser-observed media and browser/API discovery when the page exposes usable media metadata.
4. A small site-adapter registry only for sites that cannot be handled safely by the generic paths.

Site-specific rules are kept out of the main download pipeline and share common metadata, quality, transport and review contracts.

## Review-before-download flow

Pasted URLs, Auto Detect and Chrome Source Capture converge on the same review flow. Before download, UVD can show:

- detected title;
- actual supported heights;
- duration;
- exact server size or bitrate-based estimate where available;
- actual resolution and frame rate when exposed by the container/manifest;
- container / transport information;
- video and audio codec information;
- filename and destination;
- the selected quality.

Metadata uses layered probes rather than site-specific guesses: site/page metadata first, manifest parsing for HLS/DASH/MSS, byte-range size checks for direct files, and a bounded FFmpeg input probe as the common fallback. Estimated values are explicitly labeled; UVD does not invent a resolution, codec, duration or file size when the source does not expose one.

A browser-extension handoff opens or focuses UVD for review rather than silently starting a download.

## Chrome Source Capture

The unpacked extension lives in `UVD-Source-Capture/`.

The extension uses broad HTTP(S) host permission because a page and its media CDN frequently use different origins. Media observation is scoped in application logic to a tab after the user starts detection. Cookie and Authorization headers are not exported to the Native Messaging host.

The extension has a fixed manifest key, so its Chrome extension ID remains stable after the UVD folder is moved. In a packaged build, use **고급 / 문제 해결 → Chrome 확장 연동**:

1. Press **확장 설치 열기**.
2. In `chrome://extensions`, enable Developer mode and choose **Load unpacked**.
3. Select the opened `Extension\UVD-Source-Capture` folder.
4. Press **연결 테스트** in UVD.

UVD automatically creates or repairs the per-user Native Messaging registration on startup and when **연동 설정 / 복구** is pressed. In the extension popup, **페이지 주소로 UVD 재분석** intentionally sends the web-page URL back through UVD's page extractor, while **선택한 실제 영상 UVD로 가져오기** sends the exact selected MP4/HLS/DASH URL, its selected height and verified metadata. The page URL is retained only as recovery context for a selected-media handoff and never replaces that exact media URL.

The fixed extension ID is `mpncbnfjghomdnfaakmepbbkmdffeage`.

The PowerShell installer remains as a fallback and no longer requires an extension-ID argument:

```powershell
powershell -ExecutionPolicy Bypass -File .\Setup\install-native-host.ps1
```

To remove the registration:

```powershell
powershell -ExecutionPolicy Bypass -File .\Setup\uninstall-native-host.ps1
```

The Native Messaging manifest is generated locally because it contains the installation path. It must not be committed to Git.

## Source build

Requirements:

- Windows 10/11 x64;
- .NET 9 SDK;
- Node.js for extension tests and the current site-adapter helper that requires it.

Run the regression suites:

```powershell
dotnet test .\N_m3u8DL_RE_GUI.Tests\N_m3u8DL_RE_GUI.Tests.csproj -c Release
Push-Location .\UVD-Source-Capture
npm test
Pop-Location
```

Runtime executables are intentionally excluded from source control. Their pinned versions and SHA256 values are recorded in `runtime-manifest.json`.

A maintainer release build expects the matching `ffmpeg.exe`, `yt-dlp.exe` and `N_m3u8DL-RE.exe` in the repository root, verifies their hashes, and produces a clean ZIP:

```powershell
.\scripts\build-release.ps1
```

## Security and privacy

See [SECURITY.md](SECURITY.md).

Important design constraints include:

- Native Host messages are size-limited and schema-validated.
- Browser-originated commands do not invoke a command shell.
- Only a small allowlist of non-credential transport headers crosses the Native Messaging boundary.
- Loopback/local/private/link-local/special HTTP(S) targets are rejected by the Native Host; ordinary hostnames are DNS-checked before handoff.
- The retired cookie-based Cloudflare-bypass path is not part of UVD 1.0.0.
- Machine-specific manifests, configs, browser profiles, logs and downloads are excluded from source control and release packaging.
- Signed media URLs and header values are not intentionally written to user-visible logs.

## Third-party software

UVD source is derived from the MIT-licensed N_m3u8DL-RE GUI project. See [NOTICE.md](NOTICE.md) and [LICENSE](LICENSE).

Bundled runtime programs have their own licenses. In particular, the pinned Windows yt-dlp standalone executable and the bundled GPL-enabled FFmpeg build have GPL requirements. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), `THIRD_PARTY_LICENSES/`, and `runtime-manifest.json`.

## Public-release checks

Before pushing or publishing:

```powershell
.\scripts\verify-public-tree.ps1 -Strict
```

Then follow [PUBLIC_RELEASE_CHECKLIST.md](PUBLIC_RELEASE_CHECKLIST.md).

GitHub CI runs the .NET and extension regression suites. CodeQL and Dependabot configuration are included under `.github/`.

## Responsible use

Use UVD only for media you are authorized to access and download. Website behavior and access controls can change without notice. UVD does not guarantee support for every page on a domain.

## License

The UVD source tree retains the upstream MIT license notice. Third-party executables and libraries remain under their respective licenses.
