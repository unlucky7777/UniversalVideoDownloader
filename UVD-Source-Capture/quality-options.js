// A site may expose one HLS master per quality rather than a multivariant
// master. Never synthesize a signed rendition URL by editing its path.
// Only combine captured URLs with a provable shared video identifier.
export const PAGE_QUALITY_CAPS=Object.freeze([2160,1440,1080,720,480,360]);
const heightOf=item=>Number(item?.declaredHeight||item?.pathHeight)||null;
export function qualityFamilyKey(item){
  if(!item)return null;
  if(item.mainGroup && item.isMain)return 'explicit:'+item.mainGroup;
  if(item.kind!=='HLS')return null;
  try {
    const url=new URL(item.url);
    if(!/(?:^|\.)phncdn\.com$/i.test(url.hostname))return null;
    const match=/^\/hls\/videos\/(\d{6})\/(\d{2})\/(\d{6,15})\/(\d{3,4})P_[^/]*?_(\d{6,15})\.mp4\/(?:master|index[^/]*)\.m3u8$/i.exec(url.pathname);
    if(!match || match[3]!==match[5])return null;
    return 'phncdn:'+match[1]+':'+match[2]+':'+match[3];
  } catch { return null; }
}
export function familyCandidates(main,allItems=[]){
  if(!main)return [];
  const key=qualityFamilyKey(main);
  if(!key)return [main];
  const master=row=>/\/master\.m3u8$/i.test(new URL(row.url).pathname)?1:0;
  return (allItems||[]).filter(item=>item?.kind===main.kind &&
    qualityFamilyKey(item)===key && item.url).sort((a,b)=>
      (heightOf(b)||0)-(heightOf(a)||0)||
      master(b)-master(a)||(b.lastSeen||0)-(a.lastSeen||0));
}
function verifiedFamilyRows(main,allItems,proofByUrl){
  return familyCandidates(main,allItems).map(row=>{
    const proof=proofByUrl.get(row.url);
    const statedHeight=heightOf(row);
    // A CDN path label is not proof of its served resolution. An explicit
    // curl manifest is authoritative; mismatch must never label the old height.
    return {row,proof,height:proof?.status==='verified'
      ?Number(proof.height)||null:statedHeight};
  }).filter(item=>Number.isInteger(item.height)&&item.height>=144&&item.height<=4320);
}
export function qualityOptions(main,allItems=[],manifestVariants=[],proofByUrl=new Map()){
  if(!main)return [];
  if(main.kind==='PAGE'){
    const accepted=new Set([4320,2880,2160,1440,1080,720,480,360,240]);
    const heights=[...new Set(manifestVariants.map(v=>Number(v.height))
      .filter(h=>accepted.has(h)))].sort((a,b)=>b-a);
    return [{value:'best',label:'최고 제공 화질',mode:'page'},
      ...heights.map(h=>({value:String(h),label:h+'p · 실제 제공',mode:'page'}))];
  }
  if(main.kind==='FILE' && main.isMain && main.mainGroup){
    const heights=[...new Set(allItems.filter(row=>
      row?.kind==='FILE' && row.isMain && row.mainGroup===main.mainGroup &&
      Number.isInteger(heightOf(row)) && heightOf(row)>0
    ).map(heightOf))].sort((a,b)=>b-a);
    return heights.map(h=>({value:String(h),label:h+'p · 사이트 제공 파일',mode:'exact'}));
  }
  if(['HLS','DASH'].includes(main.kind)){
    const variants=[...new Set(manifestVariants.map(row=>Number(row.height))
      .filter(n=>Number.isInteger(n)&&n>=144&&n<=4320))];
    const group=main.kind==='HLS'?verifiedFamilyRows(main,allItems,proofByUrl):[];
    const familyHeights=[...new Set(group.map(x=>x.height))];
    const heights=[...new Set([...variants,...familyHeights])].sort((a,b)=>b-a);
    if(!heights.length)return [{value:'best',
      label:'자동 (화질 분석 중/미확인)',mode:'unverified'}];
    return [{value:'best',label:'최고 확인 화질 자동',mode:'manifest'},
      ...heights.map(h=>{
        const entries=group.filter(x=>x.height===h);
        const confirmed=entries.some(x=>x.proof?.status==='verified');
        if(confirmed){
          const proof=entries.find(x=>x.proof?.status==='verified')?.proof;
          const native=proof?.metadata;
          const manifestQuality=Array.isArray(native?.formats) &&
            native.formats.some(v=>Number(v.height)===h);
          const status=proof?.refreshed
            ?'새 주소 재검증'
            :manifestQuality?'재생목록 확인':'URL 접근 확인 · 화질은 사이트 표기';
          return {value:String(h),label:h+'p · '+status,mode:'exact-hls'};
        }
        if(entries.length && entries.every(x=>x.proof?.status==='unavailable'))
          return {value:String(h),label:h+'p · CDN 주소 접근 불가',
            disabled:true,mode:'unavailable'};
        if(variants.includes(h))return {value:String(h),label:h+'p · 재생목록 확인',mode:'manifest'};
        const checking=entries.some(x=>x.proof?.status==='checking'||!x.proof);
        const label=checking?'주소 확인 중':entries.some(x=>x.proof?.status==='unverified')
          ?'URL 접근됨 · 해상도 미확인':'CDN 주소 접근 불가';
        return {value:String(h),
          label:h+'p · '+label,
          disabled:true,mode:'unavailable'};
      })];
  }
  const h=heightOf(main);
  return [{value:'best',label:h?h+'p · 직접 파일 1개':'감지된 원본 (화질 미확인)',
    mode:'single'}];
}
export function exactQualitySource(main,allItems,selection,proofByUrl=new Map()){
  if(!main)return null;
  if(main.kind==='FILE'&&main.isMain&&main.mainGroup)
    return (allItems||[]).find(row=>row?.kind==='FILE'&&row.isMain&&
      row.mainGroup===main.mainGroup&&heightOf(row)===Number(selection))||null;
  if(main.kind!=='HLS')return main;
  const group=familyCandidates(main,allItems);
  if(group.length<2)return main;
  const verified=group.map(row=>({row,proof:proofByUrl.get(row.url)}))
    .filter(x=>x.proof?.status==='verified'&&Number.isInteger(Number(x.proof.height)))
    .sort((a,b)=>Number(b.proof.height)-Number(a.proof.height));
  if(selection==='best')return verified[0]?.row || main;
  const h=Number(selection);
  const exact=verified.find(x=>Number(x.proof.height)===h);
  if(exact)return exact.row;
  // An HLS variant inside the currently selected master is handled by the
  // downloader's normal --select-video cap, not by a fabricated sibling URL.
  return main;
}
