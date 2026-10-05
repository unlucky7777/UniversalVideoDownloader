#nullable enable
using System;

namespace N_m3u8DL_RE_GUI.Core;

/// <summary>
/// Input and HTTP error classification for the stream workflow.
/// Never parses or persists query tokens; signed media URLs must remain intact.
/// </summary>
public static class StreamUrlDiagnostics
{
    public static string? CheckStreamInput(string? input)
    {
        if (!Uri.TryCreate(input?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return null; // Local manifests and batch files remain supported.

        var path = uri.AbsolutePath.TrimEnd('/');
        // A host root is a website request, never an HLS/DASH/ISM manifest.
        if (path.Length == 0)
            return "SITE_ROOT_NOT_STREAM";
        // RIPNSFW video pages are SPA routes returning 404 to a CLI HTTP client.
        if ((uri.Host.Equals("ripnsfw.com", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Equals("www.ripnsfw.com", StringComparison.OrdinalIgnoreCase)) &&
            path.StartsWith("/model/", StringComparison.OrdinalIgnoreCase) &&
            !path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
            return "RIPNSFW_PAGE_NOT_STREAM";

        return null;
    }

    public static string? ClassifyEngineOutput(string? output, bool manifestRead)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;
        if (output.Contains("403 (Forbidden)", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("HTTP Error 403", StringComparison.OrdinalIgnoreCase))
            return manifestRead ? "SEGMENT_ACCESS_DENIED" : "MANIFEST_ACCESS_DENIED";
        if (output.Contains("404 (Not Found)", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("HTTP Error 404", StringComparison.OrdinalIgnoreCase))
            return manifestRead ? "SEGMENT_NOT_FOUND" : "MANIFEST_NOT_FOUND";
        if (output.Contains("410 (Gone)", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("HTTP Error 410", StringComparison.OrdinalIgnoreCase))
            return manifestRead ? "SEGMENT_GONE" : "MANIFEST_GONE";
        return null;
    }

    public static string Message(string code, bool korean) => code switch
    {
        "SITE_ROOT_NOT_STREAM" => korean
            ? "복사한 주소는 사이트 첫 화면이야. Network의 실제 master.m3u8 또는 .mpd 요청을 선택해야 해."
            : "This is a website root, not a stream. Copy the real master.m3u8 or .mpd request.",
        "RIPNSFW_PAGE_NOT_STREAM" => korean
            ? "RIPNSFW 상세 페이지는 M3U8 주소가 아니야. 브라우저의 미디어 요청을 복사해 줘."
            : "RIPNSFW's video page is not a media manifest; capture the actual stream request.",
        "MANIFEST_ACCESS_DENIED" => korean
            ? "HTTP 403: 재생 목록 요청이 서버에서 거부됐어. 서명/접속 권한 또는 요청 헤더를 확인해야 해."
            : "HTTP 403: server denied the manifest; check the signed URL, access and request headers.",
        "SEGMENT_ACCESS_DENIED" => korean
            ? "HTTP 403: 목록은 열렸지만 영상 조각 서버가 접근을 거부했어. 유효기간·헤더·동시 연결을 확인해야 해."
            : "HTTP 403: manifest opened but media segments were denied; check access, headers and concurrency.",
        "MANIFEST_NOT_FOUND" => korean
            ? "HTTP 404: 입력 URL에서 재생 목록을 찾을 수 없어. 사이트 페이지나 잘못된 요청을 복사했는지 확인해 줘."
            : "HTTP 404: no manifest at this URL. Check whether a website/document request was copied.",
        "SEGMENT_NOT_FOUND" => korean
            ? "HTTP 404: 재생 목록은 열렸지만 일부 영상 조각이 없어. 만료된 재생 목록일 수 있어."
            : "HTTP 404: manifest opened but segment URLs are missing or stale.",
        "MANIFEST_GONE" => korean
            ? "HTTP 410: 이 재생 목록 주소는 CDN에서 폐기됐어. 원래 페이지에서 최신 주소를 다시 추출해야 해."
            : "HTTP 410: this manifest URL was retired by the CDN; refresh it from the original page.",
        "SEGMENT_GONE" => korean
            ? "HTTP 410: 재생 목록은 열렸지만 일부 영상 조각 주소가 폐기됐어. 최신 재생 목록을 다시 받아야 해."
            : "HTTP 410: manifest opened but one or more segment URLs were retired; fetch a fresh manifest.",
        _ => code
    };
}
