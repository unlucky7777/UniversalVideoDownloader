#nullable enable
namespace N_m3u8DL_RE_GUI.Core.Capture;

public sealed record NativeHandoffRoute(string Kind,string Url,int? PreferredHeight,string? PageFallbackUrl);

public static class NativeHandoffRoutingPolicy
{
    private static readonly HashSet<string> AllowedKinds =
        new(StringComparer.Ordinal) { "PAGE","HLS","DASH","MSS","FILE" };

    public static NativeHandoffRoute Select(
        string kind,string url,string? pageFallbackUrl,int? selectedHeight,int? maxQualityHeight)
    {
        if(!AllowedKinds.Contains(kind))
            throw new ArgumentException("Unsupported native handoff kind.",nameof(kind));
        if(!Uri.TryCreate(url,UriKind.Absolute,out var media) ||
           media.Scheme is not ("http" or "https") ||
           !string.IsNullOrEmpty(media.UserInfo))
            throw new ArgumentException("Invalid native handoff URL.",nameof(url));

        if(!string.IsNullOrWhiteSpace(pageFallbackUrl))
        {
            if(!Uri.TryCreate(pageFallbackUrl,UriKind.Absolute,out var page) ||
               page.Scheme is not ("http" or "https") ||
               !string.IsNullOrEmpty(page.UserInfo))
                throw new ArgumentException("Invalid page fallback URL.",nameof(pageFallbackUrl));
        }

        var preferred = selectedHeight is >=144 and <=4320
            ? selectedHeight
            : maxQualityHeight is >=144 and <=4320 ? maxQualityHeight : null;

        // A media handoff always keeps the selected media URL primary.
        // pageFallbackUrl is recovery context only. A PAGE handoff already
        // carries the page URL in url, so the same rule works for both paths.
        return new NativeHandoffRoute(kind,url,preferred,pageFallbackUrl);
    }
}
