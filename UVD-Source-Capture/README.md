UVD SOURCE CAPTURE — VIDEO PREVIEW & CONTEXT MENU

Stable Chrome unpacked folder: D:\UniversalVideoDownloader\release\UVD\Extension\UVD-Source-Capture
Extension internal version: 1.6.5. The public folder and archive names omit versions.

SUPPORTED DISCOVERY PATHS
- Generic direct FILE (MP4/WebM etc via extension and video MIME).
- HLS M3U8 master/leaf; MPEG-DASH MPD; MSS Smooth Streaming.
- Video element and iframe discovery, network requests, selected HTML player
  configuration (including explicitly provided main-video rendition URLs).
- PAGE fallback to bundled yt-dlp for many supported sites. Site restrictions,
  logged-in requests, expiring tokens and DRM are not automatically solved.
- YouTube appears as ONE main video, with separate audio/video merged in UVD.
- No DRM circumvention or account credential export.

METADATA BEFORE DOWNLOADING
- FILE: content length from server HEAD (exact when available), reported
  video MIME/container and declared-quality caveat.
- HLS/DASH: manifest resolution/bitrate; estimated size when duration exists.
- PAGE including YouTube: the Chrome background worker sends a READ-ONLY
  METADATA request to the registered Native Host. Bundled yt-dlp extracts
  title, duration, available resolutions up to 8K, FPS, video codec, source
  container and video+audio input size (exact component totals when supplied,
  otherwise estimate). A complete movie is NOT downloaded for metadata.
- Selection list for PAGE uses returned actual formats, not a fabricated fixed
  360p–4K menu. If extraction fails, show unknown and allow best-auto.
- Metadata responses contain no signed media URLs, cookies or auth headers.
  Stored metadata is session scoped and cached for three minutes; manual
  detail refresh bypasses the cache.

VIDEO RIGHT-CLICK REENABLE
- Press '영상 우클릭 허용' in the extension popup on the current tab.
- Opt-in only: suppress page JavaScript preventDefault/stop handlers on actual
  VIDEO contextmenu events. Other events and sites/tabs are unaffected.
- Press the same button again to disable. Reload reapplies only on enabled
  tab; navigation to a DIFFERENT page clears the permission.
- Pages with overlays, inaccessible iframes or strict browser/OS policies may
  still not expose the native video menu. Existing 'UVD 이 페이지 영상 소스 감지'
  Chrome context-menu action remains available.

APP TRANSFER STATS
The GUI always displays time elapsed, average transfer speed (approximate
when only per-track percentages/rates are available), current speed based on
recent downloader output, total size and ETA. Video-to-audio track transitions
do NOT reset session elapsed time. Unavailable data is shown as unknown.

Default output: app Downloads folder. Personal data and old .part files are
preserved. One UVD-current.zip and one UVD-previous.zip are retained.
