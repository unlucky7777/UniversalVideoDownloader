// This self-contained function is serialized by chrome.scripting.executeScript.
// It examines media element/resource URL metadata in the user-selected tab.
// It does not access page text, cookies, forms, password fields, or HTML source.
export function scanPageMedia() {
  const found = new Set();
  let blobCount = 0;
  const mediaPattern = /\.(?:m3u8?|mpd|mp4|webm|mkv|mov|m4v)(?:$|[?#])/i;
  const add = value => {
    if (!value || typeof value !== 'string') return;
    if (value.startsWith('blob:')) { blobCount++; return; }
    try {
      const resolved = new URL(value, location.href);
      if (['http:', 'https:'].includes(resolved.protocol) && mediaPattern.test(resolved.href))
        found.add(resolved.href);
    } catch {}
  };
  const videos=[];
  document.querySelectorAll('video,source').forEach(el => {
    add(el.currentSrc); add(el.src); add(el.getAttribute('src'));
    if(el.tagName?.toLowerCase()==='video'){
      const duration=Number(el.duration),width=Number(el.videoWidth),height=Number(el.videoHeight);
      videos.push({duration:Number.isFinite(duration)&&duration>0?duration:null,
        width:Number.isFinite(width)&&width>0?width:null,
        height:Number.isFinite(height)&&height>0?height:null,
        currentSrc:typeof el.currentSrc==='string'&&!el.currentSrc.startsWith('blob:')
          ?el.currentSrc:null});
    }
  });
  document.querySelectorAll(
    'meta[property="og:video"],meta[property="og:video:url"],meta[name="twitter:player:stream"]'
  ).forEach(el => add(el.content));
  performance.getEntriesByType('resource').slice(-1200).forEach(entry => add(entry.name));
  return { urls:[...found].slice(0,80), referrer:location.href,
    userAgent:navigator.userAgent, blobCount,
    // Player metadata is displayed as such, not claimed to be the actual
    // rendition of every independently captured URL.
    video:videos.sort((a,b)=>(b.width||0)*(b.height||0)-(a.width||0)*(a.height||0))[0]||null };
}
