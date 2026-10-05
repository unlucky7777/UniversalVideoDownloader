// Select ONE representative per current page. All observed candidates remain
// in the background store for download and fallback; this is presentation only.
import {score} from './core.js';
const NOISE=/(?:^|[/_.-])(adserver|ads?|advert|poster|thumb|thumbnail|preview|sample|trailer|sprite|banner)(?:[/_.-]|$)/i;
export function requiresDirectMainSource(page) {
  try{
    const u=new URL(page?.url||'');
    return (/(^|\.)pornvov\.net$/i.test(u.hostname) &&
      /^\/video\//i.test(u.pathname)) ||
      // This embed site is unsupported by the bundled yt-dlp generic page
      // extractor; prefer fresh captured streams over a guaranteed dead PAGE.
      (/^(?:www\.)?yako\d+\.com$/i.test(u.hostname) &&
       /^\/korea\/\d+(?:\/|$)/i.test(u.pathname));
  }catch{return false;}
}
export function selectMainMedia(items=[],page=null) {
  if(page?.kind==='youtube'||page?.kind==='site-adapter')return [{
    url:page.url,kind:'PAGE',isMain:true,headers:[],
    adapter:page.adapter||null,
    label:page.kind==='youtube'
      ?'YouTube 본영상 · 최고 영상 + 음성'
      :'사이트 어댑터 본영상 · 실제 제공 화질'
  }];
  const rows=(Array.isArray(items)?items:[]).filter(x=>{
    if(!x?.url||!['HLS','DASH','MSS','FILE'].includes(x.kind))return false;
    // Never present a confirmed HTTP 404/410 as a download option after a
    // failed site playback. A newer native curl proof is an explicit exception.
    if((x.status===404||x.status===410) &&
       !(x.curlVerified && Date.now()-(x.curlVerifiedAt||0)<120000))return false;
    let u;try{u=new URL(x.url);}catch{return false;}
    if(NOISE.test(u.pathname))return false;
    if(x.kind==='FILE' && Number.isFinite(x.bytes)&&x.bytes>0 && x.bytes<1048576)return false;
    return true;
  });
  const ranked=rows.sort((a,b)=>{
    if(Boolean(a.isMain)!==Boolean(b.isMain))return a.isMain?-1:1;
    if(a.mainGroup&&a.mainGroup===b.mainGroup){
      const h=(b.declaredHeight||b.pathHeight||0)-(a.declaredHeight||a.pathHeight||0);
      if(h)return h;
    }
    return score(b)-score(a)||b.lastSeen-a.lastSeen;
  });
  if(ranked.length){
    // Naver Cafe posts may contain multiple independently attached videos,
    // each with its own set of quality URLs. Preserve the best source for
    // every explicit Naver module, in DOM order; do not collapse the post to 1.
    const naver=ranked.filter(r=>r.isMain&&
      typeof r.mainGroup==='string'&&r.mainGroup.startsWith('naver-'));
    if(naver.length){
      const groups=new Map();
      for(const r of naver)if(!groups.has(r.mainGroup))groups.set(r.mainGroup,r);
      return [...groups.values()].sort((a,b)=>
        (a.videoOrdinal||999)-(b.videoOrdinal||999));
    }
    return [ranked[0]];
  }
  // This site's generic yt-dlp page extractor exposes only the 360p default.
  // Never present a PAGE fallback as "best video" before direct rendition
  // discovery has succeeded.
  if(requiresDirectMainSource(page))return [];
  return page?[{
    url:page.url,kind:'PAGE',isMain:true,headers:[],
    label:'현재 페이지 · yt-dlp 분석 필요'
  }]:[];
}
