// Local metadata inference. Unknown information is always null; do not
// represent the player-reported quality as a verified network rendition.
export function formatDuration(seconds) {
  if (!Number.isFinite(seconds) || seconds <= 0) return '미제공';
  const n=Math.round(seconds), h=Math.floor(n/3600),m=Math.floor(n%3600/60),s=n%60;
  return h ? [h,m,s].map((v,i)=>i?String(v).padStart(2,'0'):String(v)).join(':')
           : m+':'+String(s).padStart(2,'0');
}
export function formatBytes(bytes) {
  if (!Number.isFinite(bytes) || bytes <= 0) return '미제공';
  const unit=bytes>=1073741824?['GB',1073741824]:bytes>=1048576?['MB',1048576]:['KB',1024];
  return (bytes/unit[1]).toFixed(bytes/unit[1]>=100?0:1)+' '+unit[0];
}
export function parseHls(text) {
  if (typeof text!=='string'||!text.startsWith('#EXTM3U')) return null;
  const lines=text.split(/\r?\n/), variants=[];
  let duration=0, segments=0, bandwidth=null, nextInfo=null, endlist=false;
  for(const line of lines) {
    const s=line.trim();
    if(s.startsWith('#EXT-X-STREAM-INF:')) {
      const attrs=s.slice(18);
      const wh=/(?:^|,)RESOLUTION=(\d+)x(\d+)/i.exec(attrs);
      const bw=/(?:^|,)(?:AVERAGE-)?BANDWIDTH=(\d+)/i.exec(attrs);
      nextInfo={width:wh?+wh[1]:null,height:wh?+wh[2]:null,bandwidth:bw?+bw[1]:null};
    } else if(nextInfo && s && !s.startsWith('#')) {
      variants.push({...nextInfo,uri:s});nextInfo=null;
    } else if(s.startsWith('#EXTINF:')) {
      const n=parseFloat(s.slice(8));if(Number.isFinite(n)&&n>=0){duration+=n;segments++;}
    } else if(s==='#EXT-X-ENDLIST') endlist=true;
    else if(s.startsWith('#EXT-X-BITRATE:')) {
      const n=Number(s.slice(15));if(Number.isFinite(n)&&n>0)bandwidth=n*1000;
    }
  }
  variants.sort((a,b)=>(b.height||0)-(a.height||0)||(b.bandwidth||0)-(a.bandwidth||0));
  return {kind:variants.length?'master':'media',variants,
    duration:segments&&endlist?duration:null,bandwidth};
}
export function parseDash(text) {
  if(typeof text!=='string'||!/<MPD[\s>]/i.test(text))return null;
  const d=/mediaPresentationDuration=["']PT(?:(\d+(?:\.\d+)?)H)?(?:(\d+(?:\.\d+)?)M)?(?:(\d+(?:\.\d+)?)S)?["']/i.exec(text);
  const duration=d?(Number(d[1]||0)*3600+Number(d[2]||0)*60+Number(d[3]||0)):null;
  const variants=[...text.matchAll(/<Representation\b([^>]+)>?/gi)].map(match=>{
    const attrs=match[1],get=n=>Number(new RegExp('\\b'+n+'=["\'](\\d+)["\']','i').exec(attrs)?.[1]||0)||null;
    return {width:get('width'),height:get('height'),bandwidth:get('bandwidth')};
  }).sort((a,b)=>(b.height||0)-(a.height||0)||(b.bandwidth||0)-(a.bandwidth||0));
  return {kind:'dash',variants,duration};
}
async function limitedText(response, limit=1048576) {
  const reader=response.body?.getReader();
  if(!reader)return (await response.text()).slice(0,limit);
  let total=0;const parts=[];
  while(true){
    const {value,done}=await reader.read();if(done)break;
    total+=value.length;if(total>limit){await reader.cancel();throw Error('manifest too large');}
    parts.push(value);
  }
  const all=new Uint8Array(total);let offset=0;
  for(const part of parts){all.set(part,offset);offset+=part.length;}
  return new TextDecoder('utf-8').decode(all);
}
export async function probeSource(source,pageMeta={}) {
  const out={
    height:Number.isInteger(source.declaredHeight)&&source.declaredHeight>0
      ?source.declaredHeight
      :Number.isInteger(source.pathHeight)&&source.pathHeight>0?source.pathHeight
      :Number.isFinite(pageMeta.height)&&pageMeta.height>0?pageMeta.height:null,
    width:Number.isInteger(source.declaredHeight)&&source.declaredHeight>0
      ?null:Number.isFinite(pageMeta.width)&&pageMeta.width>0?pageMeta.width:null,
    qualityBasis:source.qualityEvidence==='player-definition'?'player-definition':
      Number.isInteger(source.pathHeight)?'url-pattern':'player',
    duration:Number.isFinite(pageMeta.duration)&&pageMeta.duration>0?pageMeta.duration:null,
    formatMime:source.mime||null,
    exactBytes:Number.isSafeInteger(source.bytes)&&source.bytes>0?source.bytes:null,
    estimatedBytes:null,variants:[],note:null,
    reachable:null,httpStatus:null,stale:false
  };
  if(!['HLS','DASH','MSS','FILE'].includes(source.kind))return out;
  if(source.kind==='FILE' && out.exactBytes){
    if(source.isMain && Number.isInteger(source.declaredHeight)){
      out.qualityBasis='file-head-site-label';
      out.note='사이트의 화질별 파일 주소입니다. 파일 크기는 서버에서 확인했습니다. 실제 화소 수는 재생 파일 분석 시 확정됩니다.';
    }
    return out;
  }
  const ctrl=new AbortController(),timeout=setTimeout(()=>ctrl.abort(),6500);
  try {
    const init={method:source.kind==='FILE'?'HEAD':'GET',credentials:'omit',
      cache:'no-store',redirect:'follow',signal:ctrl.signal};
    const response=await fetch(source.url,init);
    out.httpStatus=response.status;
    if(!response.ok){
      out.reachable=false;
      out.stale=response.status===404||response.status===410;
      out.note=out.stale
        ?'이 CDN 주소는 더 이상 유효하지 않습니다 (HTTP '+response.status+'). 최신 주소를 다시 탐색합니다.'
        :'메타데이터 조회 HTTP '+response.status+' (다운로드 가능 여부는 요청 헤더에 따라 다를 수 있음)';
      return out;
    }
    out.reachable=true;
    if(source.kind==='FILE'){
      out.formatMime=String(response.headers.get('content-type')||source.mime||'')
        .split(';')[0].slice(0,100)||null;
      const len=Number(response.headers.get('content-length'));
      if(Number.isSafeInteger(len)&&len>0)out.exactBytes=len;
      if(source.isMain && out.exactBytes && Number.isInteger(source.declaredHeight)){
        out.qualityBasis='file-head-site-label';
        out.note='사이트의 해당 화질 주소에서 전체 파일 크기를 직접 확인했습니다. 화질은 사이트 표기이며 실제 화소 수는 내려받은 파일로 최종 확인됩니다.';
      }
      return out;
    }
    if(source.kind==='MSS')return out;
    const raw=await limitedText(response);
    const result=source.kind==='HLS'?parseHls(raw):parseDash(raw);
    if(!result){out.note='재생목록을 읽을 수 없음';return out;}
    out.variants=result.variants||[];
    const best=out.variants[0];
    if(source.kind==='HLS' && result.kind==='media' && !best){
      out.height=null;
      out.width=null;
      out.qualityBasis='unverified';
      out.note='단일 HLS 미디어 목록에는 해상도 정보가 없습니다. 플레이어의 4K 표기를 이 파일의 확인된 화질로 사용하지 않습니다.';
    }
    if(best?.height){
      out.height=best.height;out.width=best.width;
      out.qualityBasis='manifest';
    }
    if(result.duration)out.duration=result.duration;
    const bitrate=best?.bandwidth||result.bandwidth;
    if(bitrate&&out.duration&&!out.exactBytes){
      const estimate=bitrate*out.duration/8;
      if(Number.isSafeInteger(Math.round(estimate))&&estimate>0)
        out.estimatedBytes=Math.round(estimate);
    }
    return out;
  } catch(_) {
    // Network/CORS/extension fetch restrictions are not proof that the media
    // URL itself is dead. Only explicit 404/410 responses are treated stale.
    out.reachable=null;
    out.note='추가 메타데이터 조회 불가 (사이트/서명/접근 제한 가능)';
    return out;
  } finally {clearTimeout(timeout);}
}
