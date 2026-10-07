import {buildCurl, displaySource, score} from './core.js';
import {scanPageMedia} from './scan-page.js';
import {probeSource,formatDuration,formatBytes} from './metadata.js';
import {describePage} from './site-router.js';
import {selectMainMedia,requiresDirectMainSource} from './main-media.js';
import {qualityOptions,exactQualitySource,familyCandidates,qualityFamilyKey} from './quality-options.js';

const $ = id => document.getElementById(id);
let tabId = null;
let pageDescriptor = null;
let pageMeta = null, pageMetaAttempted = false, rightClickEnabled = false;
let sources = [];
let allItems = [];
let chosenUrl = null;
let chosenQuality = 'best';
let running = false;
let manualChoice = false;
let lastQualityAt = 0;
let playerMeta = {};
let playerQuality = null;
const detailCache = new Map();
const qualityProof = new Map(); // Native curl confirmation for each exact signed rendition URL.
const familyProbeRuns = new Map();
let analysisToken = 0;
const nativeHost = 'com.uvd.bridge';
// Distinguish a Chrome registration error from a local UVD launch failure,
// without exposing video URL, header values, or browser credentials.
function nativeError(error) {
  const raw=String(error?.message||error||'Unknown native messaging error')
    .replace(/https?:\/\/\S+/gi,'[URL]').slice(0,170);
  if(/forbidden|not allowed|Access to the specified/i.test(raw))
    return '등록된 Chrome 확장프로그램 ID와 Native Host allowed_origins가 일치하지 않습니다. ('+raw+')';
  if(/not found|specified native messaging host/i.test(raw))
    return 'Chrome이 Native Host 등록을 찾지 못했습니다. 설치 경로/레지스트리를 확인하세요. ('+raw+')';
  if(/exited|failed to start|pipe|disconnect/i.test(raw))
    return 'Chrome에서 로컬 Host 실행 또는 응답에 실패했습니다. ('+raw+')';
  return 'Chrome↔UVD 연결 오류: '+raw;
}
async function pingNative() {
  let result;
  try { result=await call('NATIVE_PING'); }
  catch(err) {throw Error(nativeError(err));}
  if(!result?.ok)throw Error('Native Host 진단 응답 실패: '+
    String(result?.error||'empty response').slice(0,150));
  if(result.appPresent===false)
    throw Error('Chrome↔Native Host 연결 성공. 다만 UVD 실행 파일이 같은 폴더에 없습니다.');
  if(result.inboxWritable===false)
    throw Error('Chrome↔Native Host 연결 성공. 다만 UVD 다운로드 수신 폴더에 쓸 수 없습니다.');
  return result;
}
function showDetails(source, result) {
  if(source?.kind==='PAGE'){
    const info=result?.ok?result:null;
    const variants=info?.formats||[];
    const chosen=chosenQuality==='best'?variants[0]:
      variants.find(v=>Number(v.height)===Number(chosenQuality));
    $('detail-quality').textContent=chosen?chosen.height+'p'+(chosen.fps?' · '+chosen.fps+' FPS':'')
      :info?'원본 해상도 · 사이트 미표기':'메타데이터 조회 전';
    $('detail-duration').textContent=formatDuration(info?.duration);
    $('detail-size').textContent=chosen?.sizeBytes
      ?formatBytes(chosen.sizeBytes)+' (트랙 크기 합계, 병합 전)'
      :chosen?.estimatedBytes? '약 '+formatBytes(chosen.estimatedBytes)+' (영상+음성 추정)'
      :'사이트 미제공';
    const specialMp4=pageDescriptor?.kind==='site-adapter';
    $('detail-format').textContent=chosen
      ?specialMp4?'MP4 직접 파일':chosen.ext+' 원본 / MP4 병합 시도'
      :specialMp4?'사이트 전용 MP4':'웹 페이지 미디어';
    $('detail-codec').textContent=chosen
      ?specialMp4?(chosen.videoCodec||'사이트 코덱 미표기'):
        (chosen.fps?chosen.fps+' FPS · ':'')+(chosen.videoCodec||'사이트 코덱 미표기')
      :'사이트 코덱/FPS 미표기';
    $('detail-note').textContent=info
      ?'제공 화질: '+variants.map(v=>v.height+'p').join(' · ')+
        (specialMp4?'. 사이트 API가 반환한 실제 MP4 화질입니다.':
         '. 코덱/컨테이너는 원본 트랙에 따라 달라질 수 있습니다.')
      :pageMetaAttempted?'사전 메타데이터를 가져오지 못했습니다. 사이트 접근 제한 또는 일시적 오류일 수 있습니다.'
      :'yt-dlp로 메타데이터 조회 중… 실제 미디어는 다운로드하지 않습니다.';
    return;
  }
  const rendition=['HLS','DASH'].includes(source?.kind)&&chosenQuality!=='best'
    ?(result?.variants||[]).find(v=>Number(v.height)===Number(chosenQuality))
    :null;
  const height=rendition?.height||result?.height;
  $('detail-quality').textContent = height
    ? (rendition?'선택한 '+height+'p (재생목록 확인)'
      :(result.qualityBasis==='manifest'||result.qualityBasis==='native-curl-manifest')
        ?'최대 '+height+'p (재생목록 검증)'
      :result.qualityBasis==='file-head-site-label'?height+'p (사이트 표기·파일 크기 확인)'
      :result.qualityBasis==='native-ffmpeg'?height+'p (컨테이너 확인)'
      :result.qualityBasis==='player-definition'?height+'p (사이트 제공·검증 전)'
      :result.qualityBasis==='url-pattern'?height+'p (URL 표기·검증 전)'
      :height+'p (현재 재생 기준)')
    : playerQuality?.height ? playerQuality.height+'p (플레이어 기준)' : '원본 해상도 · 미표기';
  $('detail-duration').textContent=Number.isFinite(result?.duration)&&result.duration>0
    ?formatDuration(result.duration):'컨테이너/manifest 미제공';
  const chosenEstimate=rendition?.bandwidth&&result?.duration
    ?Math.round(rendition.bandwidth*result.duration/8):null;
  $('detail-size').textContent=chosenEstimate
    ?'약 '+formatBytes(chosenEstimate)+' (선택 화질 추정)'
    :rendition?'선택 화질 용량 미제공'
    :result?.exactBytes ? formatBytes(result.exactBytes)+' (서버 제공)'
    :result?.estimatedBytes ? '약 '+formatBytes(result.estimatedBytes)+' (비트레이트 추정)'
    :'서버 미제공';
  $('detail-format').textContent=result?.format||
    (source?.kind==='FILE'?(result?.formatMime||source?.mime||'직접 파일')+' · 직접 파일':
    source?.kind==='HLS'?'HLS / M3U8':source?.kind==='DASH'?'MPEG-DASH / MPD':
    source?.kind==='MSS'?'MSS / ISM':source?.kind||'Media stream');
  const codecParts=[];
  if(Number.isFinite(result?.fps)&&result.fps>0)codecParts.push(result.fps.toFixed(2).replace(/\.00$/,'')+' FPS');
  if(result?.videoCodec)codecParts.push(result.videoCodec);
  if(result?.audioCodec)codecParts.push(result.audioCodec);
  $('detail-codec').textContent=codecParts.length?codecParts.join(' · '):'코덱/FPS 미표기';
  const qualities=[...new Set((result?.variants||[]).map(v=>v.height).filter(Boolean))]
    .sort((a,b)=>b-a).map(h=>h+'p');
  $('detail-note').textContent=result?.note||
    (qualities.length?'제공 화질: '+qualities.join(' · ')+
       (result?.estimatedBytes?' / 용량은 비트레이트 기반 추정치':''):
     '스트림별 실제 화질·크기는 사이트 응답에 따라 확인이 제한될 수 있습니다.');
}
async function analyseSelected(force=false) {
  const item=selected(),token=++analysisToken;
  $('probe').disabled=!item;
  if(!item){showDetails(null,null);return null;}
  if(item.kind==='PAGE'){
    showDetails(item,pageMeta);
    if(force||!pageMetaAttempted){
      pageMetaAttempted=true;
      $('detail-note').textContent='yt-dlp 메타데이터 조회 중 (파일 미다운로드)…';
      try {
        const result=await call('GET_PAGE_METADATA',{force});
        if(token!==analysisToken)return null;
        pageMeta=result?.ok?result:null;
      }catch{
        if(token!==analysisToken)return null;
        pageMeta=null;
      }
      renderQualitySelector();
      showDetails(item,pageMeta);
    }
    return pageMeta;
  }
  const cached=detailCache.get(item.url);
  const proven=qualityProof.get(item.url);
  if(proven?.status==='verified' && cached?.qualityBasis==='native-curl-manifest' && !force){
    showDetails(item,cached);return cached;
  }
  if(cached&&!force){showDetails(item,cached);return cached;}
  showDetails(item,{
    height:item.declaredHeight||item.pathHeight||playerMeta.height,
    width:item.declaredHeight?null:playerMeta.width,
    duration:playerMeta.duration,
    qualityBasis:item.qualityEvidence==='player-definition'?'player-definition':
      item.pathHeight?'url-pattern':'player',
    exactBytes:item.bytes,note:'재생목록/용량 확인 중…'
  });
  let result=await probeSource(item,playerMeta);
  let effectiveItem=item;
  if(token!==analysisToken)return;
  if(item.kind==='HLS'){
    try{
      const native=await call('NATIVE_STREAM_METADATA',{url:item.url});
      if(token!==analysisToken)return;
      if(native?.ok){
        effectiveItem=await resolveNativeProofItem(item,native);
        cacheNativeProof(effectiveItem,native);
        result={...result,
          height:native.height??result.height,
          width:native.width??result.width,
          fps:native.fps??result.fps,
          duration:native.duration??result.duration,
          estimatedBytes:native.estimatedBytes??result.estimatedBytes,
          variants:Array.isArray(native.formats)&&native.formats.length?native.formats:result.variants,
          format:native.format||'HLS / M3U8',
          videoCodec:native.videoCodec||result.videoCodec||null,
          audioCodec:native.audioCodec||result.audioCodec||null,
          bitrate:native.bitrate??result.bitrate??null,
          qualityBasis:'native-curl-manifest',
          reachable:true,stale:false,httpStatus:200,
          note:native.refreshed
            ?'만료된 화질 주소를 자동 재발급한 뒤 재생목록/코덱 메타데이터를 검증했습니다.'
            :'Windows curl + FFmpeg로 HLS 재생목록/컨테이너 메타데이터를 검증했습니다.'};
      }
    }catch{}
  }else if(['FILE','DASH','MSS'].includes(item.kind)){
    try{
      const native=await call('NATIVE_MEDIA_METADATA',{url:item.url});
      if(token!==analysisToken)return;
      if(native?.ok){
        result={...result,
          height:native.height??result.height,
          width:native.width??result.width,
          fps:native.fps??result.fps,
          duration:native.duration??result.duration,
          exactBytes:native.exactBytes??result.exactBytes,
          estimatedBytes:native.exactBytes?result.estimatedBytes:
            (native.estimatedBytes??result.estimatedBytes),
          format:native.format||result.format||null,
          videoCodec:native.videoCodec||result.videoCodec||null,
          audioCodec:native.audioCodec||result.audioCodec||null,
          bitrate:native.bitrate??result.bitrate??null,
          variants:Array.isArray(native.formats)&&native.formats.length?native.formats:result.variants,
          qualityBasis:'native-ffmpeg',
          reachable:true,
          note:'FFmpeg 입력 probe로 길이·해상도·FPS·코덱·컨테이너를 확인했습니다.'};
      }
    }catch{}
  }
  detailCache.set(effectiveItem.url,result);
  if(effectiveItem.url!==item.url)detailCache.delete(item.url);
  renderQualitySelector();
  showDetails(effectiveItem,result);
  return result;
}

function say(message, error = false) {
  $('status').textContent = message;
  $('status').classList.toggle('error', error);
}
async function call(type, data = {}) {
  if (!Number.isInteger(tabId)) throw Error('Open this extension in a normal website tab.');
  const response = await chrome.runtime.sendMessage({type,tabId,...data});
  if (!response?.ok) throw Error(response?.error || 'Extension service worker did not respond.');
  return response.data;
}
function setRunning(value) {
  running = Boolean(value);
  $('mode').textContent = running ? '감지 중' : '대기 중';
  $('mode').classList.toggle('on',running);
  $('start').disabled = running;
  $('start').hidden = running;
  $('stop').disabled = !running;
  $('stop').hidden = !running;
}
function mainSelected(){return sources.find(x=>x.url===chosenUrl)||null;}
function selected(){
  const main=mainSelected();
  if(!main)return null;
  return exactQualitySource(main,allItems,chosenQuality,qualityProof);
}
function cacheNativeProof(item,native){
  if(!native?.ok)return false;
  const height=Number(native.height)||null;
  qualityProof.set(item.url,{
    status:height?'verified':'unverified',height,metadata:native,at:Date.now(),
    refreshed:Boolean(native.refreshed),refreshAttempt:Number(native.refreshAttempt)||0
  });
  const before=detailCache.get(item.url)||{};
  detailCache.set(item.url,{...before,
    height:height??before.height??null,
    width:native.width??before.width??null,
    duration:native.duration??before.duration??null,
    estimatedBytes:native.estimatedBytes??before.estimatedBytes??null,
    variants:Array.isArray(native.formats)?native.formats:before.variants||[],
    qualityBasis:'native-curl-manifest',reachable:true,stale:false,httpStatus:200,
    note:native.refreshed
      ?'만료된 주소를 새로 발급받아 Windows curl로 다시 검증했습니다.'
      :'Windows curl로 해당 화질 재생목록을 검증했습니다.'
  });
  return true;
}
async function resolveNativeProofItem(item,native){
  const resolved=typeof native?.sourceUrl==='string'?native.sourceUrl:null;
  if(!resolved||resolved===item.url)return item;
  try{
    const state=await call('GET_MEDIA');
    if(Array.isArray(state?.items))allItems=state.items;
    const replacement=allItems.find(row=>row.url===resolved);
    if(replacement){
      qualityProof.set(item.url,{
        status:'superseded',
        height:Number(item.declaredHeight||item.pathHeight)||null,
        replacementUrl:resolved,at:Date.now()
      });
      return replacement;
    }
  }catch{}
  return item;
}
// Verify each captured sibling URL independently; never rewrite a signed URL
// or infer an available 1080p rendition merely from a player settings label.
async function verifyQualityFamily(force=false){
  const main=mainSelected();
  if(main?.kind!=='HLS')return;
  const key=qualityFamilyKey(main);
  const perHeight=new Map();
  const candidates=familyCandidates(main,allItems).filter(row=>{
    const h=Number(row.declaredHeight||row.pathHeight);
    if(row.kind!=='HLS'||!h)return false;
    const count=perHeight.get(h)||0;
    if(count>=2)return false;
    perHeight.set(h,count+1);
    return true;
  }).slice(0,8);
  if(!key||candidates.length<2)return;
  if(familyProbeRuns.has(key))return familyProbeRuns.get(key);
  if(force)for(const row of candidates)qualityProof.delete(row.url);
  const work=(async()=>{
    const pending=candidates.filter(row=>!qualityProof.has(row.url));
    for(let i=0;i<pending.length;i+=3){
      const batch=pending.slice(i,i+3);
      for(const row of batch)qualityProof.set(row.url,{
        status:'checking',height:Number(row.declaredHeight||row.pathHeight)||null
      });
      renderQualitySelector();
      await Promise.all(batch.map(async row=>{
        try {
          const native=await call('NATIVE_STREAM_METADATA',{url:row.url});
          const proofItem=await resolveNativeProofItem(row,native);
          if(!cacheNativeProof(proofItem,native))
            qualityProof.set(row.url,{status:'unavailable'});
        }catch{
          qualityProof.set(row.url,{status:'unavailable',
            height:Number(row.declaredHeight||row.pathHeight)||null});
        }
      }));
      if(qualityFamilyKey(mainSelected())===key)render();
    }
    if(qualityFamilyKey(mainSelected())===key){
      render();void analyseSelected();
      const currentFamily=familyCandidates(mainSelected(),allItems);
      if(currentFamily.some(row=>row.qualityEvidence==='site-api')){
        const verifiedRows=currentFamily.filter(row=>
          qualityProof.get(row.url)?.status==='verified');
        const verifiedHeights=new Set(verifiedRows.map(row=>
          Number(qualityProof.get(row.url)?.height||
            row.declaredHeight||row.pathHeight)).filter(Boolean));
        const refreshed=verifiedRows.filter(row=>
          qualityProof.get(row.url)?.refreshed).length;
        say(verifiedHeights.size
          ?'플레이어 API 화질 '+verifiedHeights.size+'개를 검증했습니다.'+
            (refreshed?' 만료 주소 '+refreshed+'개는 자동 갱신했습니다.':'')+
            ' 재생 화면은 변경하지 않았습니다.'
          :'플레이어 API 화질 주소를 자동 재조회했지만 현재 접근 가능한 스트림은 확인하지 못했습니다.');
      }
    }
  })().finally(()=>familyProbeRuns.delete(key));
  familyProbeRuns.set(key,work);
  return work;
}
function renderQualitySelector(){
  const main=mainSelected(),control=$('quality-select');
  const effective=selected();
  const cached=effective?detailCache.get(effective.url):null;
  const options=qualityOptions(main,allItems,
    main?.kind==='PAGE'?(pageMeta?.formats||[]):(cached?.variants||[]),qualityProof);
  const selectable=options.filter(o=>!o.disabled);
  const valid=new Set(selectable.map(o=>o.value));
  if(!valid.has(chosenQuality))chosenQuality=selectable[0]?.value||'best';
  control.replaceChildren();
  for(const item of options){
    const opt=document.createElement('option');
    opt.value=item.value;opt.textContent=item.label;
    opt.disabled=Boolean(item.disabled);
    control.append(opt);
  }
  control.value=chosenQuality;
  control.disabled=!main||selectable.length<2;
  $('quality-hint').textContent=main?.kind==='PAGE'
    ?pageMeta?.ok
       ?'사이트가 제공하는 화질 목록입니다.'
       :'화질 정보를 확인하지 못해 최고 화질 자동 선택을 사용합니다.'
    :main?.kind==='FILE'&&main.mainGroup
      ?'사이트가 제공한 실제 파일 화질입니다.'
      :['HLS','DASH'].includes(main?.kind)
        ?selectable.length>2
          ?'검증된 화질만 선택할 수 있습니다.'
          :selectable.length===2
            ?'현재 확인된 화질: '+selectable.find(o=>o.value!=='best')?.value+'p'
            :'화질을 확인 중입니다.'
        :'추가 화질 정보가 없습니다.';
}
$('quality-select').addEventListener('change',()=>{
  chosenQuality=$('quality-select').value;
  render();
  void analyseSelected();
});
function render() {
  renderQualitySelector();
  const node = $('items'); node.replaceChildren();
  $('count').textContent = sources.length ? sources.length+'건' : '0건';
  if (!sources.length) {
    const empty = document.createElement('p');
    empty.className = 'empty';
    empty.textContent = '감지를 시작하고 영상을 재생하세요.';
    node.append(empty);
  }
  for (const source of sources) {
    const button=document.createElement('button');
    button.type='button';
    button.className='item'+(source.url===chosenUrl?' selected':'');
    button.setAttribute('role','option');
    button.setAttribute('aria-selected',String(source.url===chosenUrl));
    const meta=document.createElement('span'); meta.className='meta';
    const hint=source.kind==='PAGE'?'페이지 기반 본영상 · ':source.declaredHeight
      ?source.declaredHeight+'p (사이트 화질 목록) · '
      :source.possibleHeights?.length
        ?'복수 화질 포함 가능 (재생목록 검증 필요) · '
        :source.pathHeight
          ?source.pathHeight+'p (URL 표기·검증 전) · '
        :/(?:^|\/)master\.(?:m3u8?|mpd)(?:\?|$)/i.test(source.url)
          ?'Master playlist · ':'';
    meta.textContent=source.kind==='PAGE'
      ?'본영상 · '+(chosenQuality==='best'?'최고 화질':'최대 '+chosenQuality+'p')+' · yt-dlp'
      :source.mainGroup
        ?(source.mainGroup.startsWith('naver-')
          ?'첨부 영상 #'+(source.videoOrdinal||1)+' · 선택 '+chosenQuality+'p · 실제 네이버 화질 파일'
          :'본영상 · 선택 '+chosenQuality+'p · 실제 사이트 파일')
        :source.kind+' · '+hint+'직접 영상 소스';
    const url=document.createElement('span');url.className='path';
    url.textContent=source.kind==='PAGE'
      ?'이 페이지 영상 · 영상+음성 병합':displaySource(source);
    button.append(meta,url);
    button.addEventListener('click',()=>{
      chosenUrl=source.url;manualChoice=true;render();void analyseSelected();
    });
    node.append(button);
  }
  const has=!!selected();
  $('copy-curl').disabled=!has||selected()?.kind==='PAGE';
  $('copy-url').disabled=!has;
  $('download').disabled=!has;
  $('download').hidden=!has;
  $('download').textContent=selected()?.kind==='PAGE'
    ?'페이지 추출 결과를 UVD로 가져오기'
    :'선택한 '+(chosenQuality==='best'?'원본':chosenQuality+'p')+' 실제 영상 UVD로 가져오기';
}
async function refresh() {
  const result=await call('GET_MEDIA');
  setRunning(result.armed);
  playerMeta=result.player||{};
  playerQuality=result.quality||null;
  allItems=result.items||[];
  sources=selectMainMedia(allItems,pageDescriptor);
  if(result.quality?.selectedAt && result.quality.selectedAt!==lastQualityAt) {
    lastQualityAt=result.quality.selectedAt;
    // Clicking a quality control is NOT confirmation of a new rendition.
    // Keep manual selection; only a verified better source may replace it.
  }
  if(!manualChoice || !chosenUrl || !sources.some(s=>s.url===chosenUrl)){
    if(chosenUrl!==sources[0]?.url)chosenQuality='best';
    chosenUrl=sources[0]?.url || null;
  }
  $('download-page').hidden=requiresDirectMainSource(pageDescriptor)||!!selected();
  if(!$('download-page').hidden)
    $('download-page').textContent='페이지 주소로 UVD 재분석';
  render();
  void analyseSelected();
  void verifyQualityFamily();
  const advertised=result.discovery?.maxDeclaredHeight;
  if(requiresDirectMainSource(pageDescriptor)&&sources.length===0)
    say('영상을 재생한 뒤 다시 탐색하세요. 만료된 주소는 자동으로 제외합니다.');
  else if(sources[0]?.kind==='PAGE' && pageDescriptor?.kind==='youtube')
    say('YouTube 영상을 페이지 방식으로 분석합니다.');
  else if(result.discovery?.siteApiRenditions)
    say('플레이어 API에서 '+result.discovery.siteApiRenditions+
      '개 화질 주소를 찾았습니다. 화면 변경 없이 접근 가능 여부를 검증 중입니다.');
  else if(sources[0]?.isMain)
    say('가장 높은 확인 화질을 선택했습니다.');
  else if(advertised)
    say('최대 '+advertised+'p 소스를 발견했습니다.');
  else if(result.discovery?.configs)
    say('화질별 실제 주소를 확인 중입니다.');
  else if(result.quality)
    say(result.quality.height+'p 전환을 확인 중입니다.');
  else if(running)
    say('감지 중입니다.');
}
async function action(task) {
  try { await task(); }
  catch(err) { say(String(err.message||err),true); }
}
async function scan() {
  if(!running) await call('START_MEDIA');
  setRunning(true);
  let reports;
  try {
    reports=await chrome.scripting.executeScript({
      target:{tabId,allFrames:true},func:scanPageMedia
    });
  } catch {
    // Some university embeds disallow injection into a child frame.
    reports=await chrome.scripting.executeScript({
      target:{tabId,allFrames:false},func:scanPageMedia
    });
  }
  const found=[];
  let blobs=0, video=null;
  for(const report of reports) {
    const result=report.result;
    if(!result) continue;
    blobs+=result.blobCount||0;
    if(result.video && (!video ||
        ((result.video.width||0)*(result.video.height||0)>
         (video.width||0)*(video.height||0)))) video=result.video;
    for(const url of result.urls || [])
      found.push({url,referrer:result.referrer,userAgent:result.userAgent});
  }
  await call('SCAN_MEDIA',{found,player:video});
  await refresh();
  say(sources[0]?.kind==='PAGE'
    ?'페이지 방식으로 영상을 분석합니다.'
    :sources.length
    ?'영상 소스를 갱신했습니다.'
    :blobs?'재생 중인 네트워크 요청을 기다리고 있습니다.'
      :'영상을 재생하거나 페이지를 새로고침하세요.');
}

async function copy(text, label) {
  await navigator.clipboard.writeText(text);
  say(label+' 복사 완료. UVD에서 브라우저 붙여넣기를 사용하세요.');
}
$('video-rightclick').addEventListener('click',()=>action(async()=>{
  const next=!rightClickEnabled;
  const status=await call('VIDEO_CONTEXT_TOGGLE',{enabled:next});
  rightClickEnabled=!!status.enabled;
  $('video-rightclick').setAttribute('aria-pressed',String(rightClickEnabled));
  $('video-rightclick').textContent=rightClickEnabled?'영상 우클릭 허용 중 · 끄기':'영상 우클릭 허용';
  say(rightClickEnabled
    ?'이 탭의 영상 우클릭을 허용했습니다.'
    :'영상 우클릭 허용을 껐습니다.');
}));
$('start').addEventListener('click',()=>action(async()=>{
  await call('START_MEDIA');setRunning(true);
  say('감지 중입니다. 영상을 재생하거나 페이지를 새로고침하세요.');
  await scan();
}));
$('stop').addEventListener('click',()=>action(async()=>{
  await call('STOP_MEDIA');setRunning(false);
  say('감지를 중지했습니다.');
}));
$('scan').addEventListener('click',()=>action(scan));
$('clear').addEventListener('click',()=>action(async()=>{
  await call('CLEAR_MEDIA');chosenUrl=null;chosenQuality='best';manualChoice=false;lastQualityAt=0;
  await refresh();say('감지 기록을 지웠습니다.');
}));
$('copy-diagnostics').addEventListener('click',()=>action(async()=>{
  const data=await call('GET_MEDIA');
  // Signed links, URL paths, headers, cookies and page titles are excluded.
  const diagnostic={
    extension:chrome.runtime.getManifest().version,
    qualityAttempted:data.quality?.height||null,
    discovery:data.discovery||null,
    video:{width:data.player?.width||null,height:data.player?.height||null,
      hasDuration:Boolean(data.player?.duration)},
    candidates:(data.items||[]).slice(0,25).map(s=>({
      kind:s.kind,
      declaredHeight:s.declaredHeight||null,
      pathHeight:s.pathHeight||null,
      multiQualityNeedsVerification:Boolean(s.needsManifestVerification),
      possibleHeights:s.possibleHeights||[],
      fromPlayerDefinition:Boolean(s.definitionSource),
      parsedHeight:detailCache.get(s.url)?.height||null,
      parsedBasis:detailCache.get(s.url)?.qualityBasis||null,
      nativeCurlProof:qualityProof.get(s.url)?.status||null,
      nativeCurlHeight:qualityProof.get(s.url)?.height||null
    }))
  };
  await navigator.clipboard.writeText(JSON.stringify(diagnostic,null,2));
  say('서명 URL과 일반 요청 헤더를 제외한 진단 결과를 복사했습니다.');
}));
$('probe').addEventListener('click',()=>action(async()=>{
  await verifyQualityFamily(true);
  await analyseSelected(true);
}));
$('download-page').addEventListener('click',()=>action(async()=>{
  if(!pageDescriptor)return;
  $('download-page').disabled=true;
  say('UVD 검토 화면을 여는 중…');
  try {
    const bridge=await pingNative();
    if(!bridge.pageSupported)throw Error('페이지 다운로드를 사용하려면 새 Native Host 설치가 필요합니다.');
    if(chosenQuality!=='best'&&!bridge.qualitySelectionSupported)
      throw Error('구형 Native Host에서는 요청 화질이 적용되지 않습니다. UVD 본체를 업데이트해 주세요.');
    const reply=await call('NATIVE_PAGE_DOWNLOAD',{maxQuality:chosenQuality});
    if(!reply?.queued)throw Error('페이지 작업이 수신되지 않았습니다.');
    say(reply.launched===false
      ?'검토 항목을 등록했습니다. UVD를 실행해 파일명·저장 위치·화질을 확인하세요.'
      :'UVD 검토 화면으로 전달했습니다. 파일명·저장 위치·화질을 확인한 뒤 GO를 누르세요.');
  }catch(err){
    say('UVD 가져오기 실패: '+nativeError(err),true);
  }finally{$('download-page').disabled=!pageDescriptor;}
}));
$('native-test').addEventListener('click',()=>action(async()=>{
  $('native-test').disabled=true;
  say('Chrome ↔ Native Host 연결 검사 중…');
  try {
    const reply=await pingNative();
    say('UVD 연결 정상 · Native Host v'+(reply.version||'?'));
  }catch(err) { say(String(err.message||err),true); }
  finally { $('native-test').disabled=false; }
}));
$('download').addEventListener('click',()=>action(async()=>{
  let source=selected();if(!source)return;
  $('download').disabled=true;
  say('선택한 화질을 유지한 채 UVD 검토 화면으로 가져오는 중…');
  try {
    // Do not quietly download a lower rendition while higher captured
    // sibling URLs are still awaiting an independent curl manifest probe.
    if(source.kind==='HLS')await verifyQualityFamily();
    source=selected();
    if(!source)throw Error('선택한 화질의 실제 URL이 확인되지 않았습니다.');
    let check=await analyseSelected(true);
    const refreshed=selected();
    if(refreshed && refreshed.url!==source.url){
      source=refreshed;
      check=await analyseSelected(true);
    }
    const bridge=await pingNative();
    let reply;

    // Signed CDN manifests can remain in a player's configuration after the
    // CDN has retired them. Explicit 404/410 is proof that this exact URL is
    // stale; rescan the live tab and prefer a network-verified replacement.
    if(source.kind!=='PAGE' && check?.stale){
      const failedUrl=source.url;
      say('현재 HTTP 클라이언트에서 URL 검증 실패. 새 화질별 주소를 다시 확인하는 중…');
      manualChoice=false;
      chosenUrl=null;
      detailCache.delete(failedUrl);
      await scan();
      source=selected();
      if(source && source.url!==failedUrl)
        check=await analyseSelected(true);
      else source=null;
    }

    if(!source || (source.kind!=='PAGE' && check?.stale)){
      if(!pageDescriptor || !bridge.pageSupported)
        throw Error('현재 CDN 주소가 HTTP 404/410으로 폐기됐고 새 주소를 찾지 못했습니다. 영상을 재생한 뒤 다시 시도해 주세요.');
      reply=await call('NATIVE_PAGE_DOWNLOAD',{maxQuality:chosenQuality});
      if(!reply?.ok)throw Error(String(reply?.error||'Page fallback failed').slice(0,170));
      if(!reply.queued)throw Error('페이지 재추출 작업이 수신되지 않았습니다.');
      say('직접 CDN 주소가 폐기되어 페이지 주소를 UVD 검토 화면으로 전달했습니다. GO를 누르기 전 다시 분석됩니다.');
      return;
    }

    if(chosenQuality!=='best'&&['PAGE','HLS','DASH'].includes(source.kind)&&
       !bridge.qualitySelectionSupported)
      throw Error('구형 Native Host에서는 선택한 화질이 적용되지 않습니다. UVD 본체를 업데이트해 주세요.');

    if(source.kind==='PAGE'){
      if(!bridge.pageSupported)throw Error('Native Host 페이지 다운로드 기능이 없습니다.');
      reply=await call('NATIVE_PAGE_DOWNLOAD',{maxQuality:chosenQuality});
    }else{
      // For an explicit main-video FILE, HEAD must prove the actual file size.
      if(source.isMain && source.kind==='FILE' && !check?.exactBytes)
        throw Error('선택한 최고 화질 파일의 실제 서버 크기를 검증하지 못했습니다. 다시 분석해 주세요.');
      const selectedHeight=Number(chosenQuality==='best'
        ?(source.declaredHeight||source.pathHeight||check?.height||0)
        :chosenQuality)||null;
      reply=await call('NATIVE_DOWNLOAD',{
        url:source.url,
        // A direct FILE URL is already an exact rendition. maxQuality is only
        // a selector/cap for manifest/page extraction; selectedHeight carries
        // the UI hint for an exact direct file.
        maxQuality:['HLS','DASH'].includes(source.kind)?chosenQuality:'best',
        selectedHeight
      });
    }
    if(!reply?.ok)throw Error(String(reply?.error||'Host returned no acknowledgment').slice(0,170));
    if(!reply.queued)throw Error('Native Host가 로컬 작업 등록을 확인하지 않았습니다.');
    say(reply.launched===false
      ?'검토 항목을 등록했습니다. UVD를 실행해 설정을 확인하세요.'
      :'UVD 검토 화면으로 전달했습니다. 확장프로그램에서 선택한 화질은 그대로 유지됩니다.');
  }catch(err){
    say('UVD 가져오기 실패: '+nativeError(err)+' / 영상을 재생한 뒤 다시 스캔하면 최신 주소를 다시 수집합니다.',true);
  }finally{$('download').disabled=!selected();}
}));
$('copy-curl').addEventListener('click',()=>action(async()=>{
  const source=selected();if(!source) return;
  await copy(buildCurl(source),'URL + 일반 요청 헤더');
}));
$('copy-url').addEventListener('click',()=>action(async()=>{
  const source=selected();if(!source) return;
  await copy(source.url,'미디어 URL');
}));

(async()=>{
  try {
    const providedId = new URLSearchParams(location.search).get('tabId');
    const tabs = providedId ? [await chrome.tabs.get(Number(providedId))]
      : await chrome.tabs.query({active:true,currentWindow:true});
    const tab=tabs[0];
    if (!Number.isInteger(tab?.id) || !/^https?:/i.test(tab.url || ''))
      throw Error('학교 강의 또는 일반 웹사이트 탭에서 확장프로그램을 열어줘.');
    tabId=tab.id;
    pageDescriptor=describePage(tab.url);
    try{
      rightClickEnabled=!!(await call('VIDEO_CONTEXT_STATUS')).enabled;
      $('video-rightclick').setAttribute('aria-pressed',String(rightClickEnabled));
      $('video-rightclick').textContent=rightClickEnabled
        ?'영상 우클릭 허용 중 · 끄기':'영상 우클릭 허용';
    }catch{rightClickEnabled=false;}
    // Site-specific explicit renditions are read on popup open so the user's
    // first actionable result is not a low-quality generic page fallback.
    if(requiresDirectMainSource(pageDescriptor))await call('START_MEDIA');
    $('download-page').disabled=!pageDescriptor;
    if(pageDescriptor?.kind==='youtube'){
      $('download-page').textContent='YouTube 영상 UVD로 가져오기';
      $('site-route-note').textContent='YouTube는 페이지 분석 방식으로 UVD 검토 화면에 가져옵니다.';
      $('site-route-note').hidden=false;
    } else if(pageDescriptor?.kind==='site-adapter'){
      $('download-page').textContent='사이트 영상 UVD로 가져오기';
      $('site-route-note').textContent='등록된 사이트 어댑터로 실제 제공 화질을 조회합니다.';
      $('site-route-note').hidden=false;
    } else {
      $('site-route-note').hidden=true;
    }
    await refresh();
    say(pageDescriptor?.kind==='youtube'||pageDescriptor?.kind==='site-adapter'
      ?'화질을 확인한 뒤 UVD 검토 화면으로 가져오세요.'
      :running?'감지 중입니다.':'감지를 시작하거나 페이지 영상을 UVD로 가져오세요.');
  } catch(err) {
    say(String(err.message||err),true);
    for(const id of ['start','stop','scan','clear']) $(id).disabled=true;
  }
})();
