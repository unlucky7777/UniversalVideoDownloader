import {test} from 'node:test';
import assert from 'node:assert/strict';
import {discoverPageDefinitions} from '../page-definitions.js';
import {browserDiscoveryConfig} from '../site-adapters.js';

test('Pornhub MAIN-world discovery resolves get_media without changing player state', async()=>{
  const old={
    window:globalThis.window,
    document:globalThis.document,
    location:globalThis.location,
    fetch:globalThis.fetch
  };
  const page='https://www.pornhub.com/view_video.php?viewkey=6a5fc3e04cd06';
  const api='https://www.pornhub.com/video/get_media?s=tokenized';
  const playerState={src:'blob:unchanged',currentTime:321.5,quality:'720'};
  const calls=[];
  globalThis.location=new URL(page);
  globalThis.document={querySelectorAll:()=>[]};
  globalThis.window={
    flashvars:{
      mediaDefinitions:[
        {format:'hls',quality:[1080,720,480,240],videoUrl:api,defaultQuality:true}
      ]
    },
    playerObjList:{main:{flashvars:{}}},
    page_params:{video_player_setup:{playervars:{}}}
  };
  globalThis.fetch=async(url,options)=>{
    calls.push({url:String(url),options});
    return {
      ok:true,
      headers:{get:()=>null},
      text:async()=>JSON.stringify([
        {quality:'1080',videoUrl:'https://cdn.example/hls/1080/master.m3u8'},
        {quality:'720',videoUrl:'https://cdn.example/hls/720/master.m3u8'},
        {quality:'480',videoUrl:'https://cdn.example/hls/480/master.m3u8'},
        {quality:'240',videoUrl:'https://cdn.example/hls/240/master.m3u8'}
      ])
    };
  };
  try{
    const result=await discoverPageDefinitions(browserDiscoveryConfig(page));
    assert.equal(calls.length,1);
    assert.equal(calls[0].url,api);
    assert.equal(calls[0].options.credentials,'include');
    assert.equal(result.siteApiRenditions,4);
    const rows=result.definitions.filter(x=>x.qualityEvidence==='site-api');
    assert.deepEqual(rows.map(x=>x.declaredHeight),[1080,720,480,240]);
    assert(rows.every(x=>x.isMain===true));
    assert(rows.every(x=>x.mainGroup==='pornhub-6a5fc3e04cd06'));
    assert(rows.every(x=>x.format==='hls'));
    assert.deepEqual(playerState,{src:'blob:unchanged',currentTime:321.5,quality:'720'});
  }finally{
    if(old.window===undefined)delete globalThis.window;else globalThis.window=old.window;
    if(old.document===undefined)delete globalThis.document;else globalThis.document=old.document;
    if(old.location===undefined)delete globalThis.location;else globalThis.location=old.location;
    if(old.fetch===undefined)delete globalThis.fetch;else globalThis.fetch=old.fetch;
  }
});

test('Pornhub discovery rejects non-Pornhub get_media endpoints', async()=>{
  const old={window:globalThis.window,document:globalThis.document,
    location:globalThis.location,fetch:globalThis.fetch};
  globalThis.location=new URL('https://www.pornhub.com/view_video.php?viewkey=testkey');
  globalThis.document={querySelectorAll:()=>[]};
  globalThis.window={flashvars:{mediaDefinitions:[
    {format:'hls',quality:[1080,720],videoUrl:'https://evil.example/video/get_media?x=1'}
  ]}};
  let called=false;
  globalThis.fetch=async()=>{called=true;throw new Error('must not fetch');};
  try{
    const result=await discoverPageDefinitions(
      browserDiscoveryConfig('https://www.pornhub.com/view_video.php?viewkey=testkey'));
    assert.equal(called,false);
    assert.equal(result.siteApiRenditions,0);
  }finally{
    if(old.window===undefined)delete globalThis.window;else globalThis.window=old.window;
    if(old.document===undefined)delete globalThis.document;else globalThis.document=old.document;
    if(old.location===undefined)delete globalThis.location;else globalThis.location=old.location;
    if(old.fetch===undefined)delete globalThis.fetch;else globalThis.fetch=old.fetch;
  }
});
