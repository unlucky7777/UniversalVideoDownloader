// Naver Cafe SmartEditor signed playback metadata. Only real CDN URLs returned
// by the playback API may enter Source Capture; never manufacture quality links.
const VALID_ID=/^[A-Za-z0-9_-]{8,96}$/;
const VALID_KEY=/^[A-Za-z0-9_-]{8,512}$/;
export function isValidNaverModule(item){
  return Boolean(item && typeof item.vid==='string' && typeof item.key==='string' &&
    VALID_ID.test(item.vid) && VALID_KEY.test(item.key));
}
export function parseNaverVariants(data){
  const list=data?.videos?.list;
  if(!Array.isArray(list))return [];
  const byUrl=new Map();
  for(const item of list.slice(0,32)){
    if(typeof item?.source!=='string'||item.source.length>8192)continue;
    let uri;try{uri=new URL(item.source.replace(/&amp;/g,'&'));}catch{continue;}
    if(uri.protocol!=='https:'||uri.username||uri.password||
       !(uri.hostname==='pstatic.net'||uri.hostname.endsWith('.pstatic.net'))||
       !uri.pathname.toLowerCase().endsWith('.mp4'))continue;
    const o=item.encodingOption||{};
    let height=Number(o.height);
    if(!Number.isInteger(height)||height<144||height>4320){
      const m=/(?<!\d)(2160|1440|1080|720|540|480|360|240)p?\b/i.exec(String(o.name||''));
      height=m?Number(m[1]):null;
    }
    // Unknown quality is retained as a source but not presented as a
    // selectable invented height.
    const bytes=Number(item.size);
    byUrl.set(item.source,{url:uri.href,
      height:Number.isInteger(height)&&height>=144&&height<=4320?height:null,
      bytes:Number.isSafeInteger(bytes)&&bytes>0&&bytes<=34359738368?bytes:null});
  }
  return [...byUrl.values()].sort((a,b)=>(b.height||0)-(a.height||0));
}
const cache=new Map(); // worker-memory only; contains temporary signed tokens
export async function fetchNaverVariants(module,fetcher=fetch){
  if(!isValidNaverModule(module))return [];
  const identity=module.vid+':'+module.key;
  const cached=cache.get(identity);
  if(cached&&Date.now()-cached.at<60000)return cached.variants;
  const endpoint='https://apis.naver.com/rmcnmv/rmcnmv/vod/play/v2.0/'+
    encodeURIComponent(module.vid)+'?key='+encodeURIComponent(module.key);
  const abort=new AbortController(),timer=setTimeout(()=>abort.abort(),10000);
  try{
    const response=await fetcher(endpoint,{
      method:'GET',credentials:'omit',redirect:'error',cache:'no-store',
      signal:abort.signal,headers:{Accept:'application/json'}
    });
    if(!response?.ok || Number(response.headers?.get?.('content-length')||0)>1048576)return [];
    const body=await response.text();
    if(body.length>1048576)return [];
    const variants=parseNaverVariants(JSON.parse(body));
    if(variants.length)cache.set(identity,{at:Date.now(),variants});
    if(cache.size>24)cache.delete(cache.keys().next().value);
    return variants;
  }catch{return [];}finally{clearTimeout(timer);}
}
