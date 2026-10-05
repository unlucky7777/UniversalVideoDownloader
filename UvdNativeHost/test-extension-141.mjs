import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';
import vm from 'node:vm';
const dir='D:\\UniversalVideoDownloader\\release\\UVD-Source-Capture\\';
const kv=new Map();
const nativeRequests=[];
let currentPageUrl='https://www.pornhub.com/view_video.php?viewkey=sample';
const page={flashvars_42:{mediaDefinitions:[
  {format:'hls',quality:720,defaultQuality:true,videoUrl:'https://cv.test/720P.m3u8?token=720'},
  {format:'hls',quality:1080,videoUrl:'https://cv.test/1080P.m3u8?token=1080'},
  {format:'hls',quality:[1080,720,480],videoUrl:'https://cv.test/480P.m3u8?token=480'}
]}};
const events={};function event(name){return {addListener(fn){events[name]=fn;}};}
const chrome={
  runtime:{id:'ext-local-test',onMessage:event('message'),onInstalled:event('installed'),
    getURL:path=>'chrome-extension://ext-local-test/'+path,
    async sendNativeMessage(host,request){
      assert.equal(host,'com.uvd.bridge');
      nativeRequests.push(request);
      return request.type==='PING'
        ?{ok:true,nativeHost:host,version:3,appPresent:true,inboxWritable:true}
        :{ok:true,queued:true,launched:true};
    }},
  tabs:{get:async()=>({url:currentPageUrl}),
    onRemoved:event('removed'),onUpdated:event('updated')},
  storage:{session:{
    async get(keys){const arr=Array.isArray(keys)?keys:[keys];
      return Object.fromEntries(arr.filter(k=>kv.has(k)).map(k=>[k,kv.get(k)]));},
    async set(items){for(const [k,v] of Object.entries(items))kv.set(k,v);},
    async remove(keys){for(const k of Array.isArray(keys)?keys:[keys])kv.delete(k);},
    async setAccessLevel(){}
  }},
  scripting:{async executeScript(args){
    if(args.files)return [];
    const output=vm.runInNewContext('('+args.func.toString()+')()',{
      window:page,location:{href:'https://www.pornhub.com/view_video.php?viewkey=sample'},
      URL,Object,Array,String,Number,RegExp
    },{timeout:1000});
    return [{result:output,frameId:0}];
  }},
  webRequest:{
    onBeforeRequest:event('beforeRequest'),
    onBeforeSendHeaders:event('beforeHeaders'),
    onHeadersReceived:event('headersReceived'),
    onCompleted:event('completed'),onErrorOccurred:event('error')
  },
  contextMenus:{onClicked:event('contextClick'),removeAll(cb){cb();},create(){}},
  action:{async setBadgeText(){},async setBadgeBackgroundColor(){}},
  windows:{async create(){}}
};
globalThis.chrome=chrome;
Object.defineProperty(globalThis,'navigator',{value:{userAgent:'test-browser'},configurable:true});
await import(pathToFileURL(dir+'background.js'));
async function message(type,tabId=4,data={}){
 return await new Promise((resolve,reject)=>{
   events.message({type,tabId,...data},{id:'ext-local-test'},reply=>{
     reply?.ok?resolve(reply.data):reject(Error(reply?.error||'no reply'));
   });
 });
}
await message('START_MEDIA');
const result=await message('GET_MEDIA');
assert.equal(result.discovery.configs,1);
assert.equal(result.discovery.candidates,3);
assert.equal(result.discovery.maxDeclaredHeight,1080);
assert.equal(result.items.length,3);
assert.equal(result.items[0].declaredHeight,1080);
assert(result.items[0].url.includes('1080P'));
const arrayRow=result.items.find(x=>x.url.includes('480P'));
assert(!arrayRow.declaredHeight);
assert(arrayRow.needsManifestVerification);
assert(result.items.some(x=>x.declaredHeight===720)); // no premature hiding
const ping=await message('NATIVE_PING');
assert.equal(ping.inboxWritable,true);
const sent=await message('NATIVE_DOWNLOAD',4,{url:result.items[0].url,
  headers:[{name:'Cookie',value:'never-trust-popup'}]});
assert.equal(sent.queued,true);
assert.equal(nativeRequests[1].url,result.items[0].url);
assert(!nativeRequests[1].headers.some(h=>h.name==='Cookie'));
await assert.rejects(message('NATIVE_DOWNLOAD',4,{url:'https://fake.invalid/other.m3u8'}));
assert.equal(nativeRequests.length,2);
const generic=await message('NATIVE_PAGE_DOWNLOAD',4,{url:'https://malicious.invalid/forged'});
assert.equal(generic.queued,true);
assert.equal(nativeRequests[2].kind,'PAGE');
assert.equal(nativeRequests[2].url,currentPageUrl);
assert.deepEqual(nativeRequests[2].headers,[]);
currentPageUrl='https://www.youtube.com/watch?v=jNQXAC9IVRw';
const youtube=await message('NATIVE_PAGE_DOWNLOAD');
assert.equal(youtube.queued,true);
assert.equal(nativeRequests[3].kind,'PAGE');
assert.equal(nativeRequests[3].url,currentPageUrl);
assert.deepEqual(nativeRequests[3].headers,[]);
currentPageUrl='chrome://extensions';
await assert.rejects(message('NATIVE_PAGE_DOWNLOAD'));
assert.equal(nativeRequests.length,4);
console.log('EXTENSION_ROUTE_E2E=PASS 1080 stream + independent generic/YouTube PAGE + popup URL spoof rejection');
