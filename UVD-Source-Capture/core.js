// Original UVD Source Capture code; independent of FetchV.
export const SAFE_HEADER_NAMES = Object.freeze(['referer','origin','user-agent','accept','accept-language']);
const SAFE_HEADERS = new Set(SAFE_HEADER_NAMES);
const IGNORE_PATH = /\.(?:ts|m4s|cmfv|cmfa|aac|vtt|srt|key|jpe?g|png|gif|svg|webp|woff2?|css|js)$|\/init\.mp4$|\/segments?\//i;
const VIDEO_EXT = /\.(?:mp4|webm|mkv|mov|m4v)$/i;
export function classify(url, mime = '', requestType = '') {
  if (typeof url !== 'string' || url.length > 8192) return null;
  let parsed;
  try { parsed = new URL(url); if (!['http:', 'https:'].includes(parsed.protocol)) return null; }
  catch { return null; }
  const path = parsed.pathname.toLowerCase();
  if (IGNORE_PATH.test(path) || /(?:^|\.)googleads|doubleclick|\/ads?(?:\/|$)/i.test(parsed.hostname + path)) return null;
  const type = String(mime).split(';')[0].trim().toLowerCase();
  if (/\.m3u8?$/.test(path) || /(?:x-mpegurl|vnd\.apple\.mpegurl)/.test(type)) return 'HLS';
  if (/\.mpd$/.test(path) || /dash\+xml/.test(type)) return 'DASH';
  if (/\.ism\/manifest$|\.isml\/manifest$/i.test(path) || /ms-sstr/.test(type)) return 'MSS';
  if (VIDEO_EXT.test(path) || (/^video\//.test(type) && !/mp2t/.test(type))) return 'FILE';
  if (requestType === 'media' && VIDEO_EXT.test(path)) return 'FILE';
  return null;
}
export function safeHeaders(headers = []) {
  const map = new Map();
  for (const item of headers) {
    const name = String(item?.name || '').trim();
    const value = String(item?.value ?? '').trim();
    if (!SAFE_HEADERS.has(name.toLowerCase()) || !value || /[\r\n\0]/.test(name + value) || value.length > 2048) continue;
    map.set(name.toLowerCase(), { name: canonicalHeader(name), value });
  }
  return [...map.values()];
}
function canonicalHeader(n) {
  return {'referer':'Referer','origin':'Origin','user-agent':'User-Agent',
    'accept':'Accept','accept-language':'Accept-Language'}[n.toLowerCase()] || n;
}
export function score(item) {
  if (!item) return -1;
  let num = ({HLS:100,DASH:95,MSS:95,FILE:55})[item.kind] ?? 0;
  if(item.isMain===true)num+=500; // Exact player-config main video, not ads.
  // A URL observed succeeding on the wire is much more trustworthy than a
  // player configuration entry. Some sites retain already-retired signed
  // renditions in mediaDefinitions after the player has moved to a new CDN URL.
  if(item.networkVerified===true || item.curlVerified===true)num+=260;
  else if(item.definitionSource===true)num-=160;
  if(item.status>=400 && item.curlVerified!==true)num-=700;
  let path;
  try { path = new URL(item.url).pathname.toLowerCase(); } catch { return -1; }
  if (/(?:\/|^)master\.(?:m3u8?|mpd)$/.test(path)) num += 45;
  // Only a single numeric quality explicitly associated with THIS URL may
  // increase its priority. Array-valued mediaDefinitions require a manifest
  // probe: a 480p URL listing [1080,720,480] is NOT a verified 1080p URL.
  if (item.qualityEvidence==='player-definition' &&
      Number.isInteger(item.declaredHeight))
    num += Math.min(250,Math.floor(item.declaredHeight/8));
  else if(Number.isInteger(item.pathHeight) &&
          !/(?:\/|^)master\.(?:m3u8?|mpd)$/.test(path))
    num += Math.min(160,Math.floor(item.pathHeight/12));
  if (/playlist|manifest|index\.m3u8/.test(path)) num += 10;
  if (/\/(?:sample|preview|thumb|trailer)\b/.test(path)) num -= 22;
  if (item.status && item.status >= 400) num -= 80;
  return num;
}
export function displaySource(item) {
  const u = new URL(item.url);
  return u.hostname + (u.pathname.length > 115 ? u.pathname.slice(0,112) + '…' : u.pathname);
}
export function mergeCandidate(previous, next) {
  if (!previous) return next;
  return {...previous,...next,
    bytes:next.bytes||previous.bytes||null,
    declaredHeight:next.declaredHeight||previous.declaredHeight||null,
    qualityEvidence:next.qualityEvidence||previous.qualityEvidence||null,
    isMain:Boolean(previous.isMain||next.isMain),
    mainGroup:next.mainGroup||previous.mainGroup||null,
    videoOrdinal:previous.videoOrdinal||next.videoOrdinal||null,
    definitionSource:Boolean(previous.definitionSource||next.definitionSource),
    networkVerified:Boolean(previous.networkVerified||next.networkVerified),
    verifiedAt:Math.max(previous.verifiedAt||0,next.verifiedAt||0)||null,
    curlVerified:Boolean(previous.curlVerified||next.curlVerified),
    curlVerifiedAt:Math.max(previous.curlVerifiedAt||0,next.curlVerifiedAt||0)||null,
    curlHeight:next.curlHeight||previous.curlHeight||null,
    // A later real network result replaces a stale failure status. A later
    // terminal failure replaces an older success.
    status:(next.lastSeen||0)>=(previous.lastSeen||0)
      ?(next.status??previous.status??null):(previous.status??next.status??null),
    headers:safeHeaders([...(previous.headers||[]),...(next.headers||[])]),
    firstSeen:previous.firstSeen||next.firstSeen,
    lastSeen:Math.max(previous.lastSeen||0,next.lastSeen||0)};
}
export function capCandidates(input,max=65) {
  const cutoff=Date.now()-7200000;
  return input.filter(x=>x.url&&x.lastSeen>=cutoff)
    .sort((a,b)=>score(b)-score(a)||b.lastSeen-a.lastSeen).slice(0,max);
}
export function buildCurl(item) {
  if (!item||!classify(item.url,item.mime||'')) throw new Error('No valid media URL selected.');
  const quote=value=>{
    const text=String(value);
    if (/[\r\n\0"$\\]/.test(text)) throw new Error('Unsafe quoting in URL or header.');
    return '"'+text+'"';
  };
  const command = ['curl','--url',quote(item.url),
    ...safeHeaders(item.headers).flatMap(h=>['-H',quote(h.name+': '+h.value)])].join(' ');
  // The GUI preserves this hint even when a site's manifest has no .m3u8 extension.
  return command + (isManifest(item) ? '\n# nre-engine: stream' : '');
}
export function isManifest(item) { return ['HLS','DASH','MSS'].includes(item?.kind); }
export function observedSource(url,mime,headers,tabId,now=Date.now()) {
  const kind=classify(url,mime);
  if (!kind) return null;
  let pathHeight=null;
  try{
    const path=new URL(url).pathname;
    const match=/(?:^|[\/_.-])(2160|1440|1080|720|540|480|360|240)[pP](?=[\/_.-]|$)/.exec(path);
    if(match)pathHeight=Number(match[1]);
  }catch{}
  return {url,mime:String(mime||'').slice(0,100),kind,pathHeight,
    headers:safeHeaders(headers),tabId,firstSeen:now,lastSeen:now,
    status:null,networkVerified:false,verifiedAt:null};
}
