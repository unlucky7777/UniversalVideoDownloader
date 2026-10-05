// UVD Source Capture MV3 worker. Local-only, non-blocking webRequest observer.
// Browser credentials (Cookie, Authorization, CSRF, Set-Cookie) are never copied,
// stored, exported or sent elsewhere. Only known video URLs are retained.
import { classify, safeHeaders, score, capCandidates, mergeCandidate, observedSource } from './core.js';
import { scanPageMedia } from './scan-page.js';
import { discoverPageDefinitions } from './page-definitions.js';
import { fetchNaverVariants, isValidNaverModule } from './naver-cafe.js';
import { describePage } from './site-router.js';
import { browserDiscoveryConfig } from './site-adapters.js';
import { setVideoContextMenu } from './video-context.js';
import { familyCandidates, qualityFamilyKey } from './quality-options.js';
import { pageMediaHeaders, trustedMediaHeaders } from './transport-headers.js';

const requests = new Map();
const queues = new Map();
const armed = new Set();
const armedReady = chrome.storage.session.get('armedTabIds').then(
  value => { for (const id of value.armedTabIds || []) armed.add(id); }
).catch(()=>{});
const MAX_PENDING = 400;
const tabKey = id => 'media_tab_' + id;
const qualityKey = id => 'quality_tab_' + id;
const playerKey = id => 'player_tab_' + id;
const discoveryKey = id => 'discovery_tab_' + id;
const metadataKey = id => 'metadata_tab_' + id;
const contextKey = id => 'rightclick_tab_' + id;
const ALLOWED_QUALITY = new Set(['best','4320','2880','2160','1440','1080','720','480','360','240']);
function requestedQuality(message) {
  const value=message?.maxQuality??'best';
  if(typeof value!=='string'||!ALLOWED_QUALITY.has(value))
    throw Error('Unsupported quality selection.');
  return value;
}
async function discoverAndStore(tabId,{allowUnarmed=false}={}) {
  if(!armed.has(tabId)&&!allowUnarmed)return {configs:0,candidates:0};
  let discoveryConfig=null;
  try{
    const tab=await chrome.tabs.get(tabId);
    discoveryConfig=browserDiscoveryConfig(tab?.url||'');
  }catch{}
  let frames;
  try{
    frames=await chrome.scripting.executeScript({
      target:{tabId,allFrames:true},world:'MAIN',
      func:discoverPageDefinitions,args:[discoveryConfig]
    });
  }catch(_){
    try{
      frames=await chrome.scripting.executeScript({
        target:{tabId,allFrames:false},world:'MAIN',
        func:discoverPageDefinitions,args:[discoveryConfig]
      });
    }catch(e){return {configs:0,candidates:0,reason:'page-injection-unavailable'};}
  }
  let configs=0,candidates=0,multivariant=0,maxDeclaredHeight=null;
  let siteApiRenditions=0;
  const seenNaver=new Set();
  // Preserve ordinals across rescans when lazy modules mount separately.
  const previous=(await chrome.storage.session.get(tabKey(tabId)))[tabKey(tabId)]||[];
  const ordinalByGroup=new Map();
  for(const candidate of previous){
    if(typeof candidate.mainGroup==='string' &&
       candidate.mainGroup.startsWith('naver-') &&
       Number.isInteger(candidate.videoOrdinal) && candidate.videoOrdinal>0)
      ordinalByGroup.set(candidate.mainGroup,candidate.videoOrdinal);
  }
  let naverOrdinal=Math.max(0,...ordinalByGroup.values());
  for(const frame of frames){
    const result=frame.result;
    if(!result)continue;
    configs+=Number(result.sourceConfigs)||0;
    siteApiRenditions+=Number(result.siteApiRenditions)||0;
    for(const item of (result.naverVideos||[]).slice(0,10)){
      if(!isValidNaverModule(item) || seenNaver.has(item.vid) || seenNaver.size>=10)continue;
      seenNaver.add(item.vid);
      const group='naver-'+item.vid;
      const ordinal=ordinalByGroup.get(group)||++naverOrdinal;
      // Read only the public signed playback API. Credentials never leave
      // Chrome; the token is transient and is not saved in extension storage.
      const variants=await fetchNaverVariants(item);
      for(const variant of variants.slice(0,12)){
        const row=observedSource(variant.url,'video/mp4',[
          {name:'Referer',value:result.frameUrl||''},
          {name:'User-Agent',value:navigator.userAgent||''}
        ],tabId);
        if(!row || row.kind!=='FILE')continue;
        row.isMain=true;
        row.mainGroup='naver-'+item.vid;
        row.videoOrdinal=ordinal;
        if(variant.height){
          row.declaredHeight=variant.height;
          row.qualityEvidence='player-definition';
          maxDeclaredHeight=Math.max(maxDeclaredHeight||0,variant.height);
        }
        // An API-advertised size is NOT an independently verified HEAD size.
        row.definitionSource=true;
        await store(row);
        candidates++;
      }
    }
    for(const definition of (result.definitions||[]).slice(0,60)){
      const row=observedSource(definition.url,'',
        pageMediaHeaders(result.frameUrl||'',navigator.userAgent||'',
          navigator.language||'',discoveryConfig?.transportHeaders||null),tabId);
      if(!row)continue; // Never trust arbitrary config endpoint as a media file.
      if(definition.declaredHeight&&Number.isInteger(definition.declaredHeight)){
        row.declaredHeight=definition.declaredHeight;
        row.qualityEvidence=definition.qualityEvidence==='site-api'
          ?'site-api':'player-definition';
        maxDeclaredHeight=Math.max(maxDeclaredHeight||0,row.declaredHeight);
      }
      if(Array.isArray(definition.qualityChoices)&&definition.qualityChoices.length){
        row.possibleHeights=definition.qualityChoices;
        row.needsManifestVerification=true;
        multivariant++;
      }
      row.definitionSource=true;
      if(definition.isMain===true){
        row.isMain=true;
        row.mainGroup=String(definition.mainGroup||'').slice(0,80);
      }
      await store(row);
      candidates++;
    }
  }
  await pruneSupersededApiRenditions(tabId);
  const status={configs,candidates,multivariant,maxDeclaredHeight,
    naverVideos:seenNaver.size,siteApiRenditions,at:Date.now()};
  await chrome.storage.session.set({[discoveryKey(tabId)]:status});
  return status;
}

const API_REFRESH_DELAYS=[250,700];
const API_REFRESH_CACHE_MS=1000;
const apiRefreshRuns=new Map();
const apiRefreshCache=new Map();
const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function refreshApiRenditions(tabId){
  const cached=apiRefreshCache.get(tabId);
  if(cached&&Date.now()-cached.at<API_REFRESH_CACHE_MS)return cached.rows;
  const existing=apiRefreshRuns.get(tabId);
  if(existing)return existing;
  const run=(async()=>{
    await discoverAndStore(tabId,{allowUnarmed:true});
    const rows=await storedMediaRows(tabId);
    apiRefreshCache.set(tabId,{at:Date.now(),rows});
    return rows;
  })().finally(()=>apiRefreshRuns.delete(tabId));
  apiRefreshRuns.set(tabId,run);
  return run;
}
const renditionHeight=row=>Number(row?.declaredHeight||row?.pathHeight||row?.curlHeight)||null;

async function storedMediaRows(tabId){
  const key=tabKey(tabId);
  const data=await chrome.storage.session.get(key);
  return Array.isArray(data[key])?data[key]:[];
}

async function pruneSupersededApiRenditions(tabId){
  const key=tabKey(tabId);
  await enqueue(tabId,async()=>{
    const data=await chrome.storage.session.get(key);
    const rows=Array.isArray(data[key])?data[key]:[];
    const groups=new Map();
    for(const row of rows){
      if(row?.qualityEvidence!=='site-api')continue;
      const height=renditionHeight(row);
      const family=qualityFamilyKey(row)||row.mainGroup||'';
      if(!height||!family)continue;
      const groupKey=family+'|'+row.kind+'|'+height;
      if(!groups.has(groupKey))groups.set(groupKey,[]);
      groups.get(groupKey).push(row);
    }
    const remove=new Set();
    for(const group of groups.values()){
      group.sort((a,b)=>(b.lastSeen||0)-(a.lastSeen||0));
      for(const row of group.slice(2))remove.add(row.url);
    }
    if(remove.size)
      await chrome.storage.session.set({[key]:capCandidates(
        rows.filter(row=>!remove.has(row.url)))});
  });
}

function latestApiRendition(rows,source){
  if(!source||source.qualityEvidence!=='site-api')return source;
  const height=renditionHeight(source);
  const family=qualityFamilyKey(source);
  const candidates=(rows||[]).filter(row=>
    row?.kind===source.kind &&
    row.qualityEvidence==='site-api' &&
    renditionHeight(row)===height &&
    (family?qualityFamilyKey(row)===family:
      source.mainGroup?row.mainGroup===source.mainGroup:true)
  ).sort((a,b)=>(b.lastSeen||0)-(a.lastSeen||0));
  return candidates[0]||source;
}

async function markNativeCurlProof(tabId,source,reply){
  const key=tabKey(tabId);
  await enqueue(tabId,async()=>{
    const data=await chrome.storage.session.get(key);
    const rows=Array.isArray(data[key])?data[key]:[];
    const at=rows.findIndex(x=>x.url===source.url);
    if(at<0)return;
    rows[at]={...rows[at],curlVerified:true,curlVerifiedAt:Date.now(),
      curlHeight:Number(reply.height)||renditionHeight(source)||null};
    await chrome.storage.session.set({[key]:capCandidates(rows)});
  });
}

async function probeNativeStreamWithRefresh(tabId,source,initialRows,{refreshFirst=false}={}){
  let current=source;
  let rows=Array.isArray(initialRows)?initialRows:[];
  let lastError='Native stream metadata failed';
  const refreshable=source?.qualityEvidence==='site-api';
  const attempts=refreshable?3:1;
  let refreshed=false;

  for(let attempt=0;attempt<attempts;attempt++){
    if(refreshable&&(refreshFirst||attempt>0)){
      if(attempt>0)await delay(API_REFRESH_DELAYS[Math.min(attempt-1,API_REFRESH_DELAYS.length-1)]);
      try{
        rows=await refreshApiRenditions(tabId);
        const latest=latestApiRendition(rows,current);
        if(latest?.url!==current?.url)refreshed=true;
        current=latest||current;
        refreshed=true;
      }catch{}
    }

    const reply=await chrome.runtime.sendNativeMessage('com.uvd.bridge',{
      type:'STREAM_METADATA',url:current.url,kind:current.kind,
      headers:trustedMediaHeaders(current,rows)
    }).catch(error=>({ok:false,error:String(error?.message||error)}));

    if(reply?.ok){
      await markNativeCurlProof(tabId,current,reply);
      return {
        reply:{...reply,sourceUrl:current.url,refreshed,refreshAttempt:attempt},
        source:current,
        rows:await storedMediaRows(tabId)
      };
    }
    lastError=String(reply?.error||'Native stream metadata failed').slice(0,180);
    if(!refreshable)break;
  }

  throw Error(lastError);
}

async function startQualitySelection(tabId) {
  // Only execute on a tab explicitly armed by its owner. A blocked iframe does
  // not disable the passive HLS/DASH network observer.
  async function inject(allFrames) {
    await chrome.scripting.executeScript({
      target:{tabId,allFrames},world:'ISOLATED',files:['quality-bridge.js']
    });
    await chrome.scripting.executeScript({
      target:{tabId,allFrames},world:'MAIN',files:['quality-control.js']
    });
  }
  try { await inject(true); }
  catch (_) { try { await inject(false); } catch (_) {} }
}
const acceptedTypes = ['xmlhttprequest','media','other','sub_frame','object'];
chrome.storage.session.setAccessLevel({accessLevel:'TRUSTED_CONTEXTS'}).catch(()=>{});

function enqueue(tabId, action) {
  const old = queues.get(tabId) || Promise.resolve();
  const run = old.catch(()=>{}).then(action);
  queues.set(tabId, run);
  run.finally(()=>{ if (queues.get(tabId) === run) queues.delete(tabId); }).catch(()=>{});
  return run;
}
async function store(item) {
  if (!item || !Number.isInteger(item.tabId) || item.tabId < 0) return;
  await enqueue(item.tabId, async () => {
    const key = tabKey(item.tabId);
    const result = await chrome.storage.session.get(key);
    const candidates = Array.isArray(result[key]) ? result[key] : [];
    const at = candidates.findIndex(x => x.url === item.url);
    if (at < 0) candidates.push(item);
    else candidates[at] = mergeCandidate(candidates[at], item);
    const bounded = capCandidates(candidates);
    await chrome.storage.session.set({[key]:bounded});
    await chrome.action.setBadgeText({tabId:item.tabId,
      text:bounded.length ? String(Math.min(99,bounded.length)) : ''}).catch(()=>{});
    await chrome.action.setBadgeBackgroundColor({tabId:item.tabId,color:'#2676b8'}).catch(()=>{});
  });
}
async function markResponse(tabId,url,status) {
  if(!Number.isInteger(tabId)||tabId<0||typeof url!=='string')return;
  await enqueue(tabId,async()=>{
    const key=tabKey(tabId);
    const result=await chrome.storage.session.get(key);
    const candidates=Array.isArray(result[key])?result[key]:[];
    const at=candidates.findIndex(x=>x.url===url);
    if(at<0)return;
    const now=Date.now();
    candidates[at]={...candidates[at],status,lastSeen:now,
      networkVerified:status>=200&&status<400,
      verifiedAt:status>=200&&status<400?now:(candidates[at].verifiedAt||null)};
    await chrome.storage.session.set({[key]:capCandidates(candidates)});
  });
}
const filter = {urls:['http://*/*','https://*/*'],types:acceptedTypes};

chrome.webRequest.onBeforeRequest.addListener(details => {
  if (details.tabId < 0 || !armed.has(details.tabId)) return;
  const pathKind = classify(details.url,'',details.type);
  requests.set(details.requestId,{url:details.url,tabId:details.tabId,
    type:details.type,kind:pathKind,headers:[],recorded:false,at:Date.now()});
  // Pending is only in volatile worker memory; do not persist arbitrary site traffic.
  if (requests.size > MAX_PENDING) {
    const first = requests.keys().next().value;
    requests.delete(first);
  }
},filter);

chrome.webRequest.onBeforeSendHeaders.addListener(details => {
  if (details.tabId < 0 || !armed.has(details.tabId)) return;
  const item = requests.get(details.requestId) || {
    url:details.url,tabId:details.tabId,type:details.type,at:Date.now(),recorded:false
  };
  item.headers = safeHeaders(details.requestHeaders || []);
  requests.set(details.requestId,item);
},filter,['requestHeaders','extraHeaders']);

chrome.webRequest.onHeadersReceived.addListener(details => {
  if (details.tabId < 0 || !armed.has(details.tabId)) return;
  if(details.statusCode===404||details.statusCode===410) {
    void markResponse(details.tabId,details.url,details.statusCode);
    return;
  }
  if(details.statusCode < 200 || details.statusCode >= 400) return;
  const responseHeaders=details.responseHeaders || [];
  const header=name=>responseHeaders.find(h=>h.name.toLowerCase()===name)?.value||'';
  const mime=header('content-type');
  const item = requests.get(details.requestId);
  const record = observedSource(details.url,mime,item?.headers || [],details.tabId);
  if (record) {
    record.status = details.statusCode;
    record.networkVerified = true;
    record.verifiedAt = Date.now();
    // For a complete 200 response Content-Length is the server-reported size.
    // For partial 206 responses only Content-Range's TOTAL is useful.
    const size=details.statusCode===206
      ? Number(/\/(\d+)\s*$/.exec(header('content-range'))?.[1])
      : details.statusCode===200 ? Number(header('content-length')) : NaN;
    if (record.kind==='FILE' && Number.isSafeInteger(size) && size>0)
      record.bytes=size;
    void store(record);
    if(item) item.recorded=true;
  }
},filter,['responseHeaders']);

chrome.webRequest.onCompleted.addListener(details => {
  const prior=requests.get(details.requestId);
  requests.delete(details.requestId);
  // Some cached responses do not fire headersReceived. URL-typed streams only.
  if(prior && !prior.recorded && details.statusCode >= 200 && details.statusCode < 400 &&
     prior.kind) {
    const row=observedSource(prior.url,'',prior.headers||[],prior.tabId);
    if(row){row.status=details.statusCode;row.networkVerified=true;row.verifiedAt=Date.now();void store(row);}
  }
},filter);
chrome.webRequest.onErrorOccurred.addListener(details => requests.delete(details.requestId),filter);
chrome.tabs.onRemoved.addListener(tabId => {
  void chrome.storage.session.remove([tabKey(tabId),qualityKey(tabId),playerKey(tabId),discoveryKey(tabId),metadataKey(tabId),contextKey(tabId)]);
  void armedReady.then(async()=>{ armed.delete(tabId);
    await chrome.storage.session.set({armedTabIds:[...armed]}); });
  apiRefreshRuns.delete(tabId);
  apiRefreshCache.delete(tabId);
  queues.delete(tabId);
});
chrome.tabs.onUpdated.addListener((tabId,changeInfo) => {
  if (changeInfo.status === 'complete') {
    apiRefreshCache.delete(tabId);
    void armedReady.then(async () => {
      if (armed.has(tabId)){
        await startQualitySelection(tabId);
        await discoverAndStore(tabId);
      }
      const isOptIn=(await chrome.storage.session.get(contextKey(tabId)))[contextKey(tabId)];
      if(isOptIn)await chrome.scripting.executeScript({
        target:{tabId,allFrames:true},world:'MAIN',
        func:setVideoContextMenu,args:[true]
      }).catch(()=>{});
    }).catch(()=>{});
  }
  if (changeInfo.url || changeInfo.status === 'loading') {
    // A same-URL reload must discard expired CDN URLs too. changeInfo.url
    // can be absent on Refresh, leaving a dead HLS as the top candidate.
    // Keep the opt-in armed flag, so the freshly loaded page is re-scanned
    // at 'complete' without requiring the user to register the extension again.
    void enqueue(tabId, async()=>{
      await chrome.storage.session.remove([tabKey(tabId),qualityKey(tabId),playerKey(tabId),discoveryKey(tabId),metadataKey(tabId),contextKey(tabId)]);
      await chrome.action.setBadgeText({tabId,text:''}).catch(()=>{});
    });
  }
});

// A Chrome "app-like" lecture viewer may hide the extensions toolbar.
// Provide an independent native Chrome right-click entry that opens our own
// full-size detector window for the exact clicked tab (not the popup tab).
const CONTEXT_ITEM_ID = 'uvd-source-capture-current-tab';
chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.removeAll(() => {
    chrome.contextMenus.create({
      id: CONTEXT_ITEM_ID,
      title: 'UVD · 이 페이지 영상 소스 감지',
      contexts: ['all'],
      documentUrlPatterns: ['http://*/*', 'https://*/*']
    });
  });
});
chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId !== CONTEXT_ITEM_ID || !Number.isInteger(tab?.id) ||
      tab.id < 0 || !/^https?:/i.test(tab.url || '')) return;
  const tabId = tab.id;
  void (async () => {
    await armedReady;
    armed.add(tabId);
    await chrome.storage.session.set({armedTabIds:[...armed]});
    await startQualitySelection(tabId);
    await discoverAndStore(tabId);
    // On a real <video> element Chrome supplies srcUrl; record it directly.
    // An overlay/player iframe may have no srcUrl, so scan the selected tab.
    if (info.srcUrl) {
      const candidate = observedSource(info.srcUrl, '', [
        {name:'Referer',value:info.frameUrl || info.pageUrl || tab.url},
        {name:'User-Agent',value:navigator.userAgent || ''}
      ], tabId);
      if (candidate) await store(candidate);
    }
    try {
      const frames = await chrome.scripting.executeScript({
        target:{tabId,allFrames:true},func:scanPageMedia
      });
      let scanned = 0, bestPlayer = null;
      for (const frame of frames) {
        const row = frame.result;
        if (!row) continue;
        if (row.video && (!bestPlayer ||
            (row.video.width||0)*(row.video.height||0)>
            (bestPlayer.width||0)*(bestPlayer.height||0)))
          bestPlayer = row.video;
        for (const mediaUrl of (row.urls || [])) {
          if (scanned++ >= 100) break;
          const candidate = observedSource(mediaUrl, '', [
            {name:'Referer',value:row.referrer || ''},
            {name:'User-Agent',value:row.userAgent || ''}
          ], tabId);
          if (candidate) await store(candidate);
        }
      }
      if(bestPlayer) {
        const clean=n=>Number.isFinite(n)&&n>0?n:null;
        await chrome.storage.session.set({
          [playerKey(tabId)]:{duration:clean(bestPlayer.duration),
            width:clean(bestPlayer.width),height:clean(bestPlayer.height),at:Date.now()}
        });
      }
    } catch {
      // Some university player frames prohibit script injection. Keep the
      // network observer armed; replay/refresh will still collect requests.
    }
    const url = chrome.runtime.getURL('popup.html?tabId=' + tabId);
    try {
      await chrome.windows.create({
        url, type:'popup', width:500, height:690, focused:true
      });
    } catch {
      // Fallback if Chrome policy blocks detached extension windows.
      await chrome.tabs.create({url});
    }
  })().catch(()=>{ console.warn('UVD right-click media window unavailable.'); });
});

chrome.runtime.onMessage.addListener((message,sender,sendResponse) => {
  (async()=>{
    if (message?.type === 'UVD_QUALITY_EVENT') {
      const frameTabId = sender.tab?.id;
      if (sender.id !== chrome.runtime.id || !Number.isInteger(frameTabId) ||
          !armed.has(frameTabId)) return {ignored:true};
      const selected = /^selected:(2160|1440|1080|720|540|480|360|320|240)$/.exec(message.code || '');
      if (selected) {
        const key=qualityKey(frameTabId);
        const old=(await chrome.storage.session.get(key))[key];
        const height=Number(selected[1]);
        // A lower-quality iframe must not overwrite a higher quality already
        // selected by another frame on the same tab.
        if(!old || !old.height || height>old.height)
          await chrome.storage.session.set({[key]:{height,selectedAt:Date.now()}});
      }
      return {ack:true};
    }
    const tabId=message?.tabId;
    if (!Number.isInteger(tabId) || tabId < 0) throw Error('Invalid tab.');
    // All messages originate from this extension's own popup.
    if (sender.id !== chrome.runtime.id) throw Error('Invalid sender.');
    await armedReady;
    if(message.type==='GET_PAGE_METADATA') {
      // Never trust a popup-provided URL. Fetch only the active Chrome tab.
      const tab=await chrome.tabs.get(tabId);
      const page=describePage(tab.url);
      if(!page)throw Error('Open a normal HTTP(S) video page first.');
      const key=metadataKey(tabId);
      const stored=(await chrome.storage.session.get(key))[key];
      if(!message.force && stored?.url===page.url && Date.now()-stored.at<180000)
        return stored.result;
      const reply=await chrome.runtime.sendNativeMessage('com.uvd.bridge',{
        type:'METADATA',url:page.url
      });
      const result=reply?.ok?reply:{ok:false,error:String(reply?.error||'Metadata unavailable').slice(0,130)};
      await chrome.storage.session.set({[key]:{url:page.url,at:Date.now(),result}});
      return result;
    }
    if(message.type==='VIDEO_CONTEXT_STATUS') {
      const v=(await chrome.storage.session.get(contextKey(tabId)))[contextKey(tabId)];
      return {enabled:!!v};
    }
    if(message.type==='VIDEO_CONTEXT_TOGGLE') {
      const tab=await chrome.tabs.get(tabId);
      if(!/^https?:/i.test(tab.url||''))throw Error('Only ordinary webpage tabs are supported.');
      const enable=message.enabled===true;
      let frames;
      try{
        frames=await chrome.scripting.executeScript({
          target:{tabId,allFrames:true},world:'MAIN',
          func:setVideoContextMenu,args:[enable]
        });
      }catch{
        frames=await chrome.scripting.executeScript({
          target:{tabId,allFrames:false},world:'MAIN',
          func:setVideoContextMenu,args:[enable]
        });
      }
      await chrome.storage.session.set({[contextKey(tabId)]:enable});
      return {enabled:enable,frames:frames.length,
        videos:frames.reduce((n,f)=>n+(f.result?.videos||0),0)};
    }
    if(message.type==='NATIVE_PING') {
      // Run Native Messaging from the MV3 worker rather than the transient
      // popup. A closed popup must not cancel native process handoff.
      const reply=await chrome.runtime.sendNativeMessage(
        'com.uvd.bridge',{type:'PING'});
      if(!reply?.ok)throw Error(String(reply?.error||'Native host did not respond').slice(0,180));
      return reply;
    }
    if(message.type==='NATIVE_PAGE_DOWNLOAD') {
      // The page URL is fetched from Chrome's tab registry, NEVER accepted
      // from popup input. Page extraction uses yt-dlp for separated A/V.
      const tab=await chrome.tabs.get(tabId);
      const page=describePage(tab.url);
      if(!page)throw Error('Open a regular HTTP(S) video page first.');
      const cap=requestedQuality(message);
      const cached=(await chrome.storage.session.get(metadataKey(tabId)))[metadataKey(tabId)];
      const title=typeof cached?.result?.title==='string'&&cached.result.title.trim()
        ?cached.result.title.trim().slice(0,180)
        :String(tab.title||'').trim().slice(0,180);
      const reply=await chrome.runtime.sendNativeMessage('com.uvd.bridge',{
        type:'DOWNLOAD',version:1,url:page.url,kind:'PAGE',
        headers:[],maxQuality:cap,title
      });
      if(!reply?.ok)throw Error(String(reply?.error||'Native host rejected page job').slice(0,180));
      return reply;
    }
    if(message.type==='NATIVE_STREAM_METADATA') {
      const key=tabKey(tabId);
      const stored=(await chrome.storage.session.get(key))[key]||[];
      const source=stored.find(item=>item.url===message.url);
      if(!source)throw Error('Selected source is no longer in the active tab.');
      if(source.kind!=='HLS')throw Error('Native stream metadata currently supports HLS only.');
      // API-discovered signed URLs may expire independently per rendition.
      // On failure, rediscover only through the registered browser API adapter
      // and retry with a fresh exact URL. Never synthesize or edit signed URLs.
      const outcome=await probeNativeStreamWithRefresh(tabId,source,stored);
      return outcome.reply;
    }
    if(message.type==='NATIVE_DOWNLOAD') {
      // Stopping capture retains the user's previously captured candidates
      // for this tab until Clear or navigation; allow those to be downloaded.
      const key=tabKey(tabId);
      let stored=(await chrome.storage.session.get(key))[key]||[];
      // Source and safe headers originate in the active tab's stored data.
      // Never trust a URL or headers provided by a popup message directly.
      let source=stored.find(item=>item.url===message.url);
      if(!source)throw Error('Selected source is no longer in the active tab.');

      // Refresh API-issued signed renditions immediately before handoff.
      // This keeps a quality that was verified moments ago from failing only
      // because its CDN token expired between review and download.
      if(source.kind==='HLS'&&source.qualityEvidence==='site-api'){
        const refreshed=await probeNativeStreamWithRefresh(
          tabId,source,stored,{refreshFirst:true});
        source=refreshed.source;
        stored=refreshed.rows;
      }
      const cap=requestedQuality(message);
      if(source.kind==='FILE'&&cap!=='best')
        throw Error('Direct files have an exact quality URL, not a quality cap.');
      if(source.kind==='MSS'&&cap!=='best')
        throw Error('MSS rendition selection is unavailable.');
      if((source.status===404||source.status===410) &&
         !(source.curlVerified && Date.now()-(source.curlVerifiedAt||0)<120000))
        throw Error('Selected CDN source is stale. Recheck its manifest before download.');
      const tab=await chrome.tabs.get(tabId);
      const page=describePage(tab.url);
      const cached=(await chrome.storage.session.get(metadataKey(tabId)))[metadataKey(tabId)];
      const title=typeof cached?.result?.title==='string'&&cached.result.title.trim()
        ?cached.result.title.trim().slice(0,180)
        :String(tab.title||'').trim().slice(0,180);

      // Carry only independently curl-verified sibling renditions of the same
      // captured video family. UVD can then expose all real site qualities
      // without fabricating signed URLs or depending on the currently playing
      // rendition.
      const familyKey=qualityFamilyKey(source);
      const renditionRows=familyKey
        ?familyCandidates(source,stored)
        :[source];
      const renditions=renditionRows
        .filter(row=>row?.kind==='HLS'&&row.curlVerified===true&&
          Date.now()-(row.curlVerifiedAt||0)<180000&&
          Number.isInteger(Number(row.curlHeight)))
        .map(row=>({
          height:Number(row.curlHeight),
          url:row.url,
          kind:'HLS',
          headers:trustedMediaHeaders(row,stored)
        }))
        .filter((row,index,all)=>row.height>=144&&row.height<=4320&&
          all.findIndex(x=>x.height===row.height)===index)
        .sort((a,b)=>b.height-a.height)
        .slice(0,12);

      const reply=await chrome.runtime.sendNativeMessage('com.uvd.bridge',{
        type:'DOWNLOAD',version:1,url:source.url,kind:source.kind,
        headers:trustedMediaHeaders(source,stored),maxQuality:cap,
        pageUrl:page?.url||null,title,renditions
      });
      if(!reply?.ok)throw Error(String(reply?.error||'Native host rejected the task').slice(0,180));
      return reply;
    }
    if(message.type==='GET_MEDIA') {
      const key=tabKey(tabId);
      const result=await chrome.storage.session.get(
        [key,qualityKey(tabId),playerKey(tabId),discoveryKey(tabId)]);
      const quality=result[qualityKey(tabId)] || null;
      // A click/API request is only an ATTEMPT. Do not silently hide the
      // original 720p address until the new rendition is actually observed.
      const items=result[key]||[];
      return {armed:armed.has(tabId),quality,
        discovery:result[discoveryKey(tabId)]||null,
        player:result[playerKey(tabId)]||null,
        items:items.sort((a,b)=>score(b)-score(a)||b.lastSeen-a.lastSeen)};
    }
    if(message.type==='START_MEDIA') {
      const tab=await chrome.tabs.get(tabId);
      if(!/^https?:/i.test(tab.url||'')) throw Error('Open a regular website first.');
      armed.add(tabId);
      await chrome.storage.session.set({armedTabIds:[...armed]});
      await startQualitySelection(tabId);
      const discovery=await discoverAndStore(tabId);
      return {armed:true,discovery};
    }
    if(message.type==='STOP_MEDIA') {
      apiRefreshRuns.delete(tabId);
      apiRefreshCache.delete(tabId);
      armed.delete(tabId);
      await chrome.storage.session.set({armedTabIds:[...armed]});
      await chrome.storage.session.remove([qualityKey(tabId),discoveryKey(tabId)]);
      for(const [key,info] of requests) if(info.tabId===tabId) requests.delete(key);
      return {armed:false};
    }
    if(message.type==='CLEAR_MEDIA') {
      apiRefreshRuns.delete(tabId);
      apiRefreshCache.delete(tabId);
      await enqueue(tabId,async()=>{
        await chrome.storage.session.remove([tabKey(tabId),qualityKey(tabId),playerKey(tabId),discoveryKey(tabId),metadataKey(tabId)]);
        await chrome.action.setBadgeText({tabId,text:''}).catch(()=>{});
      });
      return [];
    }
    if(message.type==='SCAN_MEDIA') {
      await startQualitySelection(tabId);
      const discovery=await discoverAndStore(tabId);
      const tab=await chrome.tabs.get(tabId);
      if (!tab.url || !/^https?:/i.test(tab.url)) throw Error('Not an HTTP(S) page.');
      const supplied=Array.isArray(message.found) ? message.found.slice(0,100) : [];
      if(message.player && typeof message.player==='object') {
        const v=message.player, clean=n=>Number.isFinite(n)&&n>0?n:null;
        await chrome.storage.session.set({
          [playerKey(tabId)]:{duration:clean(v.duration),width:clean(v.width),
            height:clean(v.height),at:Date.now()}
        });
      }
      for(const row of supplied) {
        const safeUrl=typeof row.url==='string' ? row.url : '';
        const detected=observedSource(safeUrl,'',[
          {name:'Referer',value:String(row.referrer||'').slice(0,2048)},
          {name:'User-Agent',value:String(row.userAgent||'').slice(0,350)}
        ],tabId);
        if(detected) await store(detected);
      }
      return {added:supplied.length,discovery};
    }
    throw Error('Unknown request.');
  })().then(result=>sendResponse({ok:true,data:result}))
      .catch(err=>sendResponse({ok:false,error:String(err.message||err)}));
  return true;
});
