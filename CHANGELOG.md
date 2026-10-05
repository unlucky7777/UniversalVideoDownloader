# Changelog

This changelog tracks public Universal Video Downloader releases. Upstream N_m3u8DL-RE GUI history predating UVD is not used as UVD version history; see NOTICE.md for attribution.

## Unreleased

## 1.0.0 - 2026-10-06

Initial public-release baseline.

### Added
- Review-before-download flow shared by pasted URLs, browser capture and site-page handoff.
- Generic HLS / DASH / MSS / direct-media paths, generic yt-dlp page extraction, browser/API discovery and a small site-adapter registry.
- Bounded signed-rendition freshness recovery with exact URL rediscovery and pre-handoff revalidation.
- Per-tab single-flight API refresh with a short result cache so concurrent quality probes share one discovery request.
- Native Messaging install and uninstall scripts.
- Reproducible release builder with runtime SHA256 pinning and third-party license staging.
- GitHub CI, CodeQL, Dependabot, SECURITY.md and public-release validation.

### Security
- Cookie and Authorization headers are excluded from Native Messaging handoff.
- Browser transport headers are restricted to a small allowlist.
- Signed URLs and header values are not intentionally emitted to user-visible logs.
- Runtime staging and release packages exclude configs, downloads, logs, browser profiles and generated Native Messaging registration files.
- Native Host rejects loopback/local/private/link-local/special network targets and checks DNS results before accepting an ordinary hostname.
- Removed the retired cookie-based Cloudflare-bypass path that could write a user-provided cookie into a temporary batch command.
- Removed the unused Chrome activeTab permission.

### Changed
- Site-specific behavior is isolated behind common adapter contracts instead of being scattered through the main download path.
- Extension suite-version generation targets UVD-Source-Capture directly.
- Support documentation is capability-based instead of claiming a static site list.
- Release packaging includes every required runtime helper, including range-mp4.cjs.

### Verification
- Full .NET regression suite passes except the intentionally skipped live-network integration test.
- Full Chrome Source Capture test suite passes, including stale signed-URL recovery and single-flight refresh coverage.
- NuGet known-vulnerability scan reports no vulnerable packages.
- Public-tree policy, clean release build, release ZIP content policy and Native Host private-target smoke tests pass.
