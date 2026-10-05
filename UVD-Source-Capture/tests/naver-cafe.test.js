import {test} from 'node:test';
import assert from 'node:assert/strict';
import {discoverPageDefinitions} from '../page-definitions.js';
import {parseNaverVariants,fetchNaverVariants} from '../naver-cafe.js';
import {observedSource} from '../core.js';
import {selectMainMedia} from '../main-media.js';
import {qualityOptions,exactQualitySource} from '../quality-options.js';

test('one Naver post scans two distinct independent SmartEditor modules, no duplicate',async()=>{
 const prior={window:globalThis.window,document:globalThis.document,location:globalThis.location};
 try{
  globalThis.location={hostname:'cafe.naver.com',href:'https://cafe.naver.com/f-e/cafes/30888761/articles/10960'};
  const modules=[
   {type:'v2',data:{vid:'AA1111111111',inkey:'KEY111111111111'}},
   {type:'v2',data:{vid:'BB2222222222',inkey:'KEY222222222222'}}
  ];
  globalThis.document={querySelectorAll:()=>[...modules,modules[0]].map(v=>({getAttribute:()=>JSON.stringify(v)}))};
  globalThis.window={};
  const out=await discoverPageDefinitions();
  assert.deepEqual(out.naverVideos.map(x=>x.vid),['AA1111111111','BB2222222222']);
  assert.equal(out.naverVideos.length,2);
  assert.equal(out.sourceConfigs,2);
  globalThis.location={hostname:'untrusted.example',href:'https://untrusted.example/post'};
  assert.equal((await discoverPageDefinitions()).naverVideos.length,0);
 }finally{
  for(const key of Object.keys(prior)){
    if(prior[key]===undefined)delete globalThis[key];else globalThis[key]=prior[key];
  }
 }
});

const api=h=>({videos:{list:[
 {source:'https://vod.pstatic.net/'+h+'/360.mp4?auth=one',encodingOption:{height:360},size:10000000},
 {source:'https://vod.pstatic.net/'+h+'/1080.mp4?auth=two',encodingOption:{height:1080},size:50000000},
 {source:'https://untrusted.example/'+h+'/2160.mp4',encodingOption:{height:2160}},
 {source:'http://vod.pstatic.net/'+h+'/720.mp4',encodingOption:{height:720}}
]}});

test('real API rendition list yields only exact HTTPS pstatic MP4 qualities',()=>{
 const list=parseNaverVariants(api('first'));
 assert.deepEqual(list.map(x=>x.height),[1080,360]);
 assert.equal(list[0].bytes,50000000);
 assert.equal(list[0].url,'https://vod.pstatic.net/first/1080.mp4?auth=two');
 assert.deepEqual(parseNaverVariants({}),[]);
});
test('API fetch uses fixed Naver origin and omits credentials and redirects',async()=>{
 const calls=[];
 const values=await fetchNaverVariants({vid:'AB1234567890',key:'KEY123456789012'},async(url,opts)=>{
  calls.push([url,opts]);
  return {ok:true,headers:{get:()=>null},text:async()=>JSON.stringify(api('second'))};
 });
 assert.equal(values.length,2);
 assert.match(calls[0][0],/^https:\/\/apis\.naver\.com\/rmcnmv\/rmcnmv\/vod\/play\/v2\.0\//);
 assert.equal(calls[0][1].credentials,'omit');
 assert.equal(calls[0][1].redirect,'error');
});
test('per-video quality family keeps two Naver videos separately selectable',()=>{
 const rows=[];
 for(const [index,id] of ['first','second'].entries()){
  for(const v of parseNaverVariants(api(id))){
   const row=observedSource(v.url,'video/mp4',[],4);
   Object.assign(row,{isMain:true,mainGroup:'naver-'+id,videoOrdinal:index+1,
      declaredHeight:v.height,qualityEvidence:'player-definition',definitionSource:true});
   rows.push(row);
  }
 }
 const main=selectMainMedia(rows,{kind:'website',url:'https://cafe.naver.com/f-e/cafes/30888761/articles/10960'});
 assert.equal(main.length,2);
 assert.deepEqual(main.map(x=>x.videoOrdinal),[1,2]);
 assert.deepEqual(main.map(x=>x.declaredHeight),[1080,1080]);
 for(const chosen of main){
  const opts=qualityOptions(chosen,rows,[],new Map());
  assert.deepEqual(opts.map(x=>x.value),['1080','360']);
  const low=exactQualitySource(chosen,rows,'360');
  assert.equal(low.mainGroup,chosen.mainGroup);
  assert.match(low.url,/360\.mp4/);
 }
});
