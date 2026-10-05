# Third-party components

UVD source and UVD's bundled runtime tools are separate components. Do not assume the top-level MIT license applies to third-party executables.

## N_m3u8DL-RE

- Component: `N_m3u8DL-RE.exe`
- Pinned build: `0.6.0+df70f0b3da0c630bd413bf617e758051f6b64757`
- License: MIT
- Source: https://github.com/nilaoda/N_m3u8DL-RE

Retain the upstream MIT notice when redistributing the binary.

## yt-dlp

- Component: `yt-dlp.exe`
- Pinned release: `2026.08.19`
- Source: https://github.com/yt-dlp/yt-dlp
- Upstream project source is Unlicense.
- Upstream states that PyInstaller-bundled standalone executables contain GPLv3+ components and the combined executable is distributed under GPLv3+.

A binary release that includes `yt-dlp.exe` must ship the applicable license / third-party notices and satisfy the corresponding source obligations for that executable.

## FFmpeg

- Component: `ffmpeg.exe`
- Pinned build: `2026-07-30-git-2ae2413488-essentials_build-www.gyan.dev`
- Build provider: https://www.gyan.dev/ffmpeg/builds/
- FFmpeg source revision: https://github.com/FFmpeg/FFmpeg/commit/2ae2413488
- The bundled executable reports `--enable-gpl --enable-version3`; the Gyan static builds are GPLv3.

A binary release that includes this FFmpeg executable must include the GPLv3 license and provide compliant access to the corresponding source/build information.

## Microsoft / NuGet dependencies

The .NET projects reference packages including CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection and Microsoft.Web.WebView2. NuGet package license metadata must be checked as part of each release build; do not copy the license of this repository onto those packages.

## Runtime provenance

Exact SHA256 values for pinned executable inputs are recorded in `runtime-manifest.json`.

This file is operational guidance, not legal advice. Before a public binary release, verify the licenses of the exact artifacts being shipped.
