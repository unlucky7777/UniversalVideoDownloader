import {test} from 'node:test';
import assert from 'node:assert/strict';

function event() {
  const handlers=[];
  return { addListener(fn){handlers.push(fn);},
    emit(...args){for(const fn of handlers) fn(...args);} };
}
function chromeFixture() {
  const db=new Map();
  const request={
    onBeforeRequest:event(),onBeforeSendHeaders:event(),
    onHeadersReceived:event(),onCompleted:event(),onErrorOccurred:event()
  };
  const tabEvents={onRemoved:event(),onUpdated:event()};
  const message=event();
  const installed=event();
  const clicked=event();
  const menuItems=[];
  const openedWindows=[];
  const fixture={
    runtime:{id:'uvd-test-extension',onMessage:message,onInstalled:installed,
      getURL:path=>'chrome-extension://uvd-test-extension/'+path},
    contextMenus:{onClicked:clicked,removeAll:cb=>cb(),create:info=>menuItems.push(info)},
    windows:{create:async info=>{openedWindows.push(info);return {id:7};}},
    scripting:{executeScript:async()=>[{result:{
      urls:['https://cdn.school.example.edu/current.mp4'],
      referrer:'https://school.example.edu/lecture/17',userAgent:'Chrome/154'
    }}]},
    webRequest:request,
    tabs:{...tabEvents,get:async id=>({id,url:'https://school.example.edu/lecture/17'}),
      create:async info=>({id:71,...info})},
    storage:{session:{
      setAccessLevel:async()=>{},
      get:async key=>{
        if(Array.isArray(key)){
          const out={};for(const k of key)out[k]=db.get(k);return out;
        }
        return {[key]:db.get(key)};
      },
      set:async obj=>{for(const [k,v] of Object.entries(obj))db.set(k,v);},
      remove:async key=>{
        for(const k of (Array.isArray(key)?key:[key]))db.delete(k);
      }
    }},
    action:{
      setBadgeText:async()=>{},setBadgeBackgroundColor:async()=>{}
    }
  };
  const send=async(type,tabId=4,extra={})=>new Promise((resolve,reject)=>{
    message.emit({type,tabId,...extra},{id:'uvd-test-extension'},answer=>
      answer.ok?resolve(answer.data):reject(new Error(answer.error)));
  });
  return {fixture,request,db,send,tabEvents,installed,clicked,menuItems,openedWindows};
}
const wait=()=>new Promise(resolve=>setTimeout(resolve,25));

test('worker is explicitly opt-in per tab; safe headers saved after real media response',async()=>{
  const x=chromeFixture();globalThis.chrome=x.fixture;
  await import('../background.js?test=privacy');
  // Unarmed tab makes a media request: nothing is retained.
  const signed='https://cdn.school.example.edu/hls/master.m3u8?t=token&exp=1234';
  x.request.onBeforeRequest.emit({requestId:'unarmed',tabId:4,url:signed,type:'xmlhttprequest'});
  x.request.onHeadersReceived.emit({requestId:'unarmed',tabId:4,url:signed,type:'xmlhttprequest',
    statusCode:200,responseHeaders:[{name:'Content-Type',value:'application/x-mpegURL'}]});
  await wait();
  let result=await x.send('GET_MEDIA');
  assert.equal(result.armed,false);assert.equal(result.items.length,0);
  await x.send('START_MEDIA');
  // Observe the exact request of a logged-in lecture; credentials must be dropped.
  x.request.onBeforeRequest.emit({requestId:'r1',tabId:4,url:signed,type:'xmlhttprequest'});
  x.request.onBeforeSendHeaders.emit({requestId:'r1',tabId:4,url:signed,type:'xmlhttprequest',
    requestHeaders:[
      {name:'Referer',value:'https://school.example.edu/lecture/17'},
      {name:'Origin',value:'https://school.example.edu'},
      {name:'User-Agent',value:'Chrome/154'},
      {name:'Cookie',value:'session=TOP_SECRET'},
      {name:'Authorization',value:'Bearer TOP_SECRET'},
      {name:'Accept-Language',value:'ko'}
    ]});
  x.request.onHeadersReceived.emit({requestId:'r1',tabId:4,url:signed,type:'xmlhttprequest',
    statusCode:200,responseHeaders:[{name:'Content-Type',value:'application/vnd.apple.mpegurl; charset=utf-8'}]});
  x.request.onCompleted.emit({requestId:'r1',tabId:4,url:signed,type:'xmlhttprequest',statusCode:200});
  await wait();
  result=await x.send('GET_MEDIA');
  assert.equal(result.armed,true);
  assert.equal(result.items.length,1);
  assert.equal(result.items[0].kind,'HLS');
  assert.equal(result.items[0].url,signed);
  assert(result.items[0].headers.some(h=>h.name==='Referer'));
  assert(!JSON.stringify([...x.db]).includes('TOP_SECRET'));
  // Another unarmed tab's media must not be captured.
  x.request.onBeforeRequest.emit({requestId:'other',tabId:5,url:signed,type:'media'});
  x.request.onHeadersReceived.emit({requestId:'other',tabId:5,url:signed,type:'media',statusCode:200,
    responseHeaders:[{name:'Content-Type',value:'application/vnd.apple.mpegurl'}]});
  await wait();
  assert.equal((await x.send('GET_MEDIA',5)).items.length,0);
  // Extensionless but correctly typed manifest.
  const endpoint='https://cdn.school.example.edu/vod/endpoint?id=ab12';
  x.request.onBeforeRequest.emit({requestId:'r2',tabId:4,url:endpoint,type:'xmlhttprequest'});
  x.request.onHeadersReceived.emit({requestId:'r2',tabId:4,url:endpoint,type:'xmlhttprequest',statusCode:206,
    responseHeaders:[{name:'Content-Type',value:'application/dash+xml'}]});
  await wait();
  assert.equal((await x.send('GET_MEDIA')).items.length,2);
  await x.send('CLEAR_MEDIA');
  assert.equal((await x.send('GET_MEDIA')).items.length,0);
  await x.send('STOP_MEDIA');
  assert.equal((await x.send('GET_MEDIA')).armed,false);
  x.installed.emit({reason:'update'});
  assert.equal(x.menuItems.length,1);
  assert(x.menuItems[0].contexts.includes('all'));
  x.clicked.emit({menuItemId:'uvd-source-capture-current-tab',srcUrl:signed},
    {id:4,url:'https://school.example.edu/lecture/17'});
  await wait();
  const fromRightClick=await x.send('GET_MEDIA');
  assert.equal(fromRightClick.armed,true);
  assert(fromRightClick.items.some(item=>item.url===signed));
  assert(fromRightClick.items.some(item=>item.url.includes('current.mp4')));
  assert.equal(x.openedWindows.length,1);
  assert(x.openedWindows[0].url.endsWith('popup.html?tabId=4'));
});

test('native curl proof permits the exact 1080p sibling URL despite Chrome HTTP 410',async()=>{
 const x=chromeFixture();const handoffs=[];
 x.fixture.runtime.sendNativeMessage=async(_host,msg)=>{
   handoffs.push(msg);
   if(msg.type==='STREAM_METADATA')return {
     ok:true,height:1080,width:1920,duration:760,estimatedBytes:250000000,
     formats:[{height:1080,width:1920,bandwidth:3000000}]
   };
   if(msg.type==='DOWNLOAD')return {ok:true,queued:true,launched:true};
   return {ok:true,version:7};
 };
 globalThis.chrome=x.fixture;
 await import('../background.js?test=native-exact-quality');
 await x.send('START_MEDIA');
 const exact='https://hv-h.phncdn.com/hls/videos/202307/12/435335881/1080P_4000K_435335881.mp4/master.m3u8?h=verified&e=9999';
 await x.send('SCAN_MEDIA',4,{found:[{url:exact,referrer:'https://school.example.edu/lecture/17',userAgent:'Chrome/154'}]});
 x.request.onHeadersReceived.emit({requestId:'gone1080',tabId:4,url:exact,
   type:'xmlhttprequest',statusCode:410,responseHeaders:[]});
 await wait();
 await assert.rejects(x.send('NATIVE_DOWNLOAD',4,{url:exact,maxQuality:'1080'}),/stale/);
 const proof=await x.send('NATIVE_STREAM_METADATA',4,{url:exact});
 assert.equal(proof.height,1080);
 const sent=await x.send('NATIVE_DOWNLOAD',4,{url:exact,maxQuality:'1080'});
 assert.equal(sent.queued,true);
 const command=handoffs.find(m=>m.type==='DOWNLOAD');
 assert.equal(command.url,exact);
 assert.equal(command.maxQuality,'1080');
 await assert.rejects(x.send('NATIVE_DOWNLOAD',4,{
   url:'https://different.example.com/another.m3u8',maxQuality:'1080'
 }),/no longer in/);
});

test('verified Pornhub-style HLS siblings are handed to UVD as all real qualities',async()=>{
 const x=chromeFixture();const handoffs=[];
 x.fixture.tabs.get=async id=>({id,url:'https://www.pornhub.com/view_video.php?viewkey=test'});
 x.fixture.runtime.sendNativeMessage=async(_host,msg)=>{
   handoffs.push(msg);
   if(msg.type==='STREAM_METADATA'){
     const h=Number(/\/(1080|720|480|240)P_/.exec(msg.url)?.[1]);
     return {ok:true,height:h,width:h===1080?1920:null,duration:120,
       estimatedBytes:h*100000,formats:[{height:h,bandwidth:h*2000}]};
   }
   if(msg.type==='DOWNLOAD')return {ok:true,queued:true,launched:true};
   return {ok:true,version:9,renditionHandoffSupported:true};
 };
 globalThis.chrome=x.fixture;
 await import('../background.js?test=four-native-renditions');
 await x.send('START_MEDIA');
 const make=h=>'https://hv-h.phncdn.com/hls/videos/202307/12/435335881/'+h+
   'P_4000K_435335881.mp4/master.m3u8?h='+h+'&e=9999';
 const urls=[1080,720,480,240].map(make);
 await x.send('SCAN_MEDIA',4,{found:urls.map(url=>({
   url,referrer:'https://www.pornhub.com/view_video.php?viewkey=test',userAgent:'Chrome/154'
 }))});
 for(const url of urls)await x.send('NATIVE_STREAM_METADATA',4,{url});
 const sent=await x.send('NATIVE_DOWNLOAD',4,{url:urls[0],maxQuality:'1080'});
 assert.equal(sent.queued,true);
 const command=handoffs.filter(m=>m.type==='DOWNLOAD').at(-1);
 assert.deepEqual(command.renditions.map(r=>r.height),[1080,720,480,240]);
 assert(command.renditions.every(r=>r.kind==='HLS'));
});

test('expired API rendition is rediscovered, reverified, and refreshed again before download',async()=>{
  const x=chromeFixture();const handoffs=[];
  const page='https://www.pornhub.com/view_video.php?viewkey=refreshcase';
  const oldUrl='https://hv-h.phncdn.com/hls/videos/202307/12/435335881/720P_2000K_435335881.mp4/master.m3u8?h=old&e=1';
  const freshUrl='https://hv-h.phncdn.com/hls/videos/202307/12/435335881/720P_2000K_435335881.mp4/master.m3u8?h=fresh&e=9999';
  let discoveries=0;
  x.fixture.tabs.get=async id=>({id,url:page,title:'Refresh test'});
  x.fixture.scripting.executeScript=async arg=>{
    if(arg.func?.name==='discoverPageDefinitions'){
      discoveries++;
      const url=discoveries===1?oldUrl:freshUrl;
      return [{result:{frameUrl:page,sourceConfigs:1,siteApiRenditions:1,
        definitions:[{url,format:'hls',declaredHeight:720,
          qualityEvidence:'site-api',isMain:true,mainGroup:'pornhub-refreshcase'}]}}];
    }
    return [{result:{urls:[],referrer:page,userAgent:'Chrome/154'}}];
  };
  x.fixture.runtime.sendNativeMessage=async(_host,msg)=>{
    handoffs.push(msg);
    if(msg.type==='STREAM_METADATA'){
      if(msg.url===oldUrl)return {ok:false,error:'HTTP 410 Gone'};
      if(msg.url===freshUrl)return {ok:true,height:720,width:1280,duration:120,
        estimatedBytes:70000000,formats:[{height:720,bandwidth:2000000}]};
    }
    if(msg.type==='DOWNLOAD')return {ok:true,queued:true,launched:true};
    return {ok:true,version:10};
  };
  globalThis.chrome=x.fixture;
  await import('../background.js?test=api-rendition-refresh');
  await x.send('START_MEDIA');

  const first=(await x.send('GET_MEDIA')).items.find(row=>row.url===oldUrl);
  assert(first);
  const proof=await x.send('NATIVE_STREAM_METADATA',4,{url:oldUrl});
  assert.equal(proof.ok,true);
  assert.equal(proof.height,720);
  assert.equal(proof.sourceUrl,freshUrl);
  assert.equal(proof.refreshed,true);
  assert(discoveries>=2);

  const after=(await x.send('GET_MEDIA')).items;
  const fresh=after.find(row=>row.url===freshUrl);
  assert(fresh);
  assert.equal(fresh.curlVerified,true);
  assert.equal(fresh.curlHeight,720);

  const sent=await x.send('NATIVE_DOWNLOAD',4,{url:oldUrl,maxQuality:'720'});
  assert.equal(sent.queued,true);
  const command=handoffs.filter(msg=>msg.type==='DOWNLOAD').at(-1);
  assert.equal(command.url,freshUrl);
  assert.equal(command.maxQuality,'720');
});

test('concurrent expired API qualities share one rediscovery run',async()=>{
  const x=chromeFixture();
  const page='https://www.pornhub.com/view_video.php?viewkey=singleflight';
  const old1080='https://cdn.example/video/1080/master.m3u8?token=old';
  const old720='https://cdn.example/video/720/master.m3u8?token=old';
  const new1080='https://cdn.example/video/1080/master.m3u8?token=new';
  const new720='https://cdn.example/video/720/master.m3u8?token=new';
  let discoveries=0;

  x.fixture.tabs.get=async id=>({id,url:page,title:'Single flight'});
  x.fixture.scripting.executeScript=async arg=>{
    if(arg.func?.name==='discoverPageDefinitions'){
      discoveries++;
      const fresh=discoveries>1;
      return [{result:{frameUrl:page,sourceConfigs:1,siteApiRenditions:2,
        definitions:[
          {url:fresh?new1080:old1080,format:'hls',declaredHeight:1080,
            qualityEvidence:'site-api',isMain:true,mainGroup:'pornhub-singleflight'},
          {url:fresh?new720:old720,format:'hls',declaredHeight:720,
            qualityEvidence:'site-api',isMain:true,mainGroup:'pornhub-singleflight'}
        ]}}];
    }
    return [{result:{urls:[],referrer:page,userAgent:'Chrome/154'}}];
  };
  x.fixture.runtime.sendNativeMessage=async(_host,msg)=>{
    if(msg.type==='STREAM_METADATA'){
      if(msg.url===old1080||msg.url===old720)return {ok:false,error:'HTTP 410 Gone'};
      if(msg.url===new1080)return {ok:true,height:1080,formats:[{height:1080}]};
      if(msg.url===new720)return {ok:true,height:720,formats:[{height:720}]};
    }
    return {ok:true,version:10};
  };

  globalThis.chrome=x.fixture;
  await import('../background.js?test=api-refresh-single-flight');
  await x.send('START_MEDIA');

  const before=(await x.send('GET_MEDIA')).items;
  assert(before.find(row=>row.url===old1080));
  assert(before.find(row=>row.url===old720));

  const [r1080,r720]=await Promise.all([
    x.send('NATIVE_STREAM_METADATA',4,{url:old1080}),
    x.send('NATIVE_STREAM_METADATA',4,{url:old720})
  ]);

  assert.equal(r1080.ok,true);
  assert.equal(r720.ok,true);
  assert.equal(r1080.sourceUrl,new1080);
  assert.equal(r720.sourceUrl,new720);
  assert.equal(discoveries,2); // initial discovery + one shared refresh
});

test('Naver worker stores both video modules as two separate selectable quality families without persisting API keys',async()=>{
  const {selectMainMedia}=await import('../main-media.js');
  const x=chromeFixture();
  const url='https://cafe.naver.com/f-e/cafes/30888761/articles/10960';
  x.fixture.tabs.get=async id=>({id,url});
  const modules=[
    {vid:'NVFIRST1111111',key:'KEYFIRST11111111'},
    {vid:'NVSECOND222222',key:'KEYSECOND2222222'}
  ];
  x.fixture.scripting.executeScript=async arg=>
    arg.func?.name==='discoverPageDefinitions'?[{result:{
      frameUrl:url,definitions:[],naverVideos:modules,sourceConfigs:2
    }}]:[{result:{urls:[],referrer:url,userAgent:'Chrome/154'}}];
  const realFetch=globalThis.fetch,queried=[];
  globalThis.fetch=async request=>{
    queried.push(request);
    const id=request.includes('NVFIRST1111111')?'first':
      request.includes('NVTHIRD333333')?'third':'second';
    return {ok:true,headers:{get:()=>null},text:async()=>JSON.stringify({
      videos:{list:[360,1080].map(height=>({
        source:'https://vod.pstatic.net/'+id+'/'+height+'.mp4?signature=temporary',
        encodingOption:{height},size:5000000*height
      }))}
    })};
  };
  try{
    globalThis.chrome=x.fixture;
    await import('../background.js?test=naver-2-modules');
    const result=await x.send('START_MEDIA');
    assert.equal(result.discovery.naverVideos,2);
    assert.equal(result.discovery.candidates,4);
    const rows=(await x.send('GET_MEDIA')).items;
    assert.equal(rows.length,4);
    const main=selectMainMedia(rows,{kind:'website',url});
    assert.deepEqual(main.map(s=>s.videoOrdinal),[1,2]);
    assert.deepEqual(main.map(s=>s.declaredHeight),[1080,1080]);

    // An exact Naver MP4 handoff must keep the MP4 URL primary. 1080p is
    // review metadata (selectedHeight), not a maxQuality selector for FILE.
    const handoffs=[];
    x.fixture.runtime.sendNativeMessage=async(_host,msg)=>{
      handoffs.push(msg);
      if(msg.type==='DOWNLOAD')return {ok:true,queued:true,launched:true};
      return {ok:true,version:11};
    };
    const first1080=rows.find(row=>row.videoOrdinal===1&&row.declaredHeight===1080);
    const sent=await x.send('NATIVE_DOWNLOAD',4,{
      url:first1080.url,maxQuality:'best',selectedHeight:1080
    });
    assert.equal(sent.queued,true);
    const direct=handoffs.find(msg=>msg.type==='DOWNLOAD');
    assert.equal(direct.url,first1080.url);
    assert.equal(direct.kind,'FILE');
    assert.equal(direct.maxQuality,'best');
    assert.equal(direct.selectedHeight,1080);
    assert.equal(direct.pageUrl,url);

    assert.equal(queried.length,2);
    assert(!JSON.stringify([...x.db]).includes('KEYFIRST11111111'));
    assert(!JSON.stringify([...x.db]).includes('KEYSECOND2222222'));
    // A later lazy-loaded third video gets its own ordinal instead of
    // inheriting #1 after the first module scrolls out of the live DOM.
    x.fixture.scripting.executeScript=async arg=>
      arg.func?.name==='discoverPageDefinitions'?[{result:{
        frameUrl:url,definitions:[],naverVideos:[{vid:'NVTHIRD333333',key:'KEYTHIRD333333333'}],
        sourceConfigs:1
      }}]:[{result:{urls:[],referrer:url,userAgent:'Chrome/154'}}];
    await x.send('SCAN_MEDIA',4,{found:[]});
    const more=(await x.send('GET_MEDIA')).items;
    assert.deepEqual(selectMainMedia(more,{kind:'website',url}).map(s=>s.videoOrdinal),[1,2,3]);
  }finally{globalThis.fetch=realFetch;}
});

test('same-URL reload clears an expired stream but keeps the existing capture arm',async()=>{
  const x=chromeFixture(); globalThis.chrome=x.fixture;
  await import('../background.js?test=stale-reload');
  await x.send('START_MEDIA');
  const url='https://cdn.ddal.pw/video/hls/playlist.m3u8';
  await x.send('SCAN_MEDIA',4,{found:[{url,referrer:'https://school.example.edu/lecture/17'}]});
  assert.equal((await x.send('GET_MEDIA')).items.length,1);
  // Chrome need not emit changeInfo.url when reloading the same URL.
  x.tabEvents.onUpdated.emit(4,{status:'loading'},{id:4});
  await wait();
  const state=await x.send('GET_MEDIA');
  assert.equal(state.armed,true);
  assert.equal(state.items.length,0);
});
