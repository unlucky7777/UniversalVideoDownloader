// Central registry for site-specific browser/page exceptions.
// Generic HLS/DASH/direct media and generic yt-dlp pages must not be listed here.
// Add an adapter only when a site requires browser/API discovery or a dedicated
// native page helper.

const ADAPTERS = [
  {
    id: 'eporner',
    host: /^(?:www\.)?eporner\.com$/i,
    path: /^\/(?:video-[A-Za-z0-9]+|embed\/[A-Za-z0-9]+)(?:\/|$)/,
    preferPageSource: true,
    pageLabel: '사이트 전용 MP4 화질',
    browserDiscovery: null
  },
  {
    id: 'pornhub',
    host: /^(?:www\.)?pornhub(?:premium)?\.(?:com|net|org)$/i,
    path: /^(?:\/view_video\.php|\/embed\/[\da-z]+(?:\/|$))/i,
    preferPageSource: false,
    pageLabel: null,
    browserDiscovery: 'media-definitions-get-media'
  }
];

export function resolveSiteAdapter(input) {
  if (typeof input !== 'string' || input.length > 8192) return null;
  let url;
  try { url = new URL(input); } catch { return null; }
  if (!['https:', 'http:'].includes(url.protocol) || url.username || url.password)
    return null;
  const match = ADAPTERS.find(x => x.host.test(url.hostname) && x.path.test(url.pathname));
  if (!match) return null;
  return {
    id: match.id,
    preferPageSource: match.preferPageSource,
    pageLabel: match.pageLabel,
    browserDiscovery: match.browserDiscovery
  };
}

export function browserDiscoveryConfig(input) {
  const adapter = resolveSiteAdapter(input);
  if (!adapter?.browserDiscovery) return null;
  if(adapter.id==='pornhub'){
    let pageHost=null;
    try{pageHost=new URL(input).hostname.toLowerCase();}catch{}
    const canonicalHost=pageHost&&pageHost.startsWith('www.')
      ?pageHost:'www.'+(pageHost||'pornhub.com');
    const root='https://'+canonicalHost;
    return {
      adapterId:adapter.id,
      mode:adapter.browserDiscovery,
      groupQueryParam:'viewkey',
      groupPathPrefix:'/embed/',
      endpointPathPrefix:'/video/get_media',
      // This CDN requires the canonical site root as Origin/Referer for
      // manifest requests. A full video-page Referer can be rejected (412).
      transportHeaders:{origin:root,referer:root+'/'},
      allowedEndpointHosts:[
        'pornhub.com','www.pornhub.com',
        'pornhub.net','www.pornhub.net',
        'pornhub.org','www.pornhub.org',
        'pornhubpremium.com','www.pornhubpremium.com'
      ]
    };
  }
  return {adapterId:adapter.id,mode:adapter.browserDiscovery};
}
