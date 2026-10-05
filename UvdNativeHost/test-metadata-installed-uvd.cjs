const assert=require('node:assert/strict');
const {spawn}=require('node:child_process');
const path=require('node:path');
const base='D:\\UniversalVideoDownloader\\release\\UniversalVideoDownloader-ExtensionFirst';
const exe=path.join(base,'UvdNativeHost.exe');
async function native(message){
 return new Promise((resolve,reject)=>{
  const child=spawn(exe,[],{windowsHide:true}),chunks=[];
  child.stdout.on('data',c=>chunks.push(c));
  child.on('error',reject);
  child.on('close',()=>{
   try{const buffer=Buffer.concat(chunks);
   assert(buffer.length>=4);assert.equal(buffer.readUInt32LE(0),buffer.length-4);
   resolve(JSON.parse(buffer.subarray(4).toString('utf8')));}catch(e){reject(e)}
  });
  const buf=Buffer.from(JSON.stringify(message)),size=Buffer.alloc(4);
  size.writeUInt32LE(buf.length);child.stdin.end(Buffer.concat([size,buf]));
 });
}
(async()=>{
 const ping=await native({type:'PING'});
 assert.equal(ping.ok,true);
 assert.equal(ping.pageMetadataSupported,true);
 assert.equal(ping.version,5);
 const unsafe=await native({type:'METADATA',url:'file:///C:/Users/user/private'});
 assert.equal(unsafe.ok,false);
 console.log('NATIVE_METADATA_PROTOCOL=PASS');
 const info=await native({type:'METADATA',
 url:'https://www.youtube.com/watch?v=jNQXAC9IVRw'});
 console.log('YOUTUBE_METADATA_SMOKE='+JSON.stringify({
 ok:info.ok,duration:info.duration,formatCount:info.formats?.length||0,
 firstHeight:info.formats?.[0]?.height||null,
 available:!!info.formats?.length,
 error:info.error?String(info.error).slice(0,90):undefined
 }));
 if(info.ok){
  assert(typeof info.duration==='number'||info.duration===null);
  assert(Array.isArray(info.formats));
  assert(!JSON.stringify(info).includes('googlevideo.com'));
 }
})().catch(e=>{console.error(e.message);process.exitCode=1});
