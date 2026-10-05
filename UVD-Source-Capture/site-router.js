// Route explicit web-page downloads. Generic pages use yt-dlp; registered
// site adapters can opt into a dedicated PAGE route. Never pass secrets.
import { resolveSiteAdapter } from './site-adapters.js';

export function describePage(input) {
  if(typeof input!=='string' || input.length>8192)return null;
  let u;try{u=new URL(input);}catch{return null;}
  if(!['https:','http:'].includes(u.protocol) || u.username || u.password)return null;
  if(!u.hostname || /^(?:localhost|127(?:\.\d+){3}|\[::1\])$/i.test(u.hostname))return null;

  const host=u.hostname.toLowerCase().replace(/^www\./,'');
  const path=u.pathname;
  const youtube=(host==='youtube.com'||host==='m.youtube.com'||
    host==='music.youtube.com'||host==='youtube-nocookie.com'||
    host==='youtu.be');
  const ytVideo=youtube && (
    host==='youtu.be'&&/^\/[a-zA-Z0-9_-]{8,15}(?:\/|$)/.test(path) ||
    /^\/(?:watch|shorts\/[a-zA-Z0-9_-]{8,15}|live\/[a-zA-Z0-9_-]{8,15}|embed\/[a-zA-Z0-9_-]{8,15})(?:\/|$)/.test(path)
  ) && (path!=='/watch'||u.searchParams.has('v'));

  const adapter=resolveSiteAdapter(u.href);
  if(adapter?.preferPageSource){
    return {
      url:u.href,
      kind:'site-adapter',
      adapter:adapter.id,
      label:adapter.pageLabel||'사이트 전용 영상'
    };
  }

  return {
    url:u.href,
    kind:ytVideo?'youtube':'website',
    adapter:adapter?.id||null,
    label:ytVideo?'YouTube 최고 영상 + 음성':'현재 페이지 (yt-dlp)'
  };
}
