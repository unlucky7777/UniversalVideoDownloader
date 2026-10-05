const assert=require('node:assert/strict');
const {spawn}=require('node:child_process');
const exe='D:\\UniversalVideoDownloader\\release\\UniversalVideoDownloader-ExtensionFirst\\UvdNativeHost.exe';
function call(payload){
 return new Promise((resolve,reject)=>{
  const p=spawn(exe,[],{windowsHide:true}),chunks=[];
  p.stdout.on('data',c=>chunks.push(c));
  p.on('error',reject);
  p.on('close',()=>{
    try{
      const b=Buffer.concat(chunks);
      assert(b.length>=4);
      assert.equal(b.readUInt32LE(0),b.length-4);
      resolve(JSON.parse(b.subarray(4).toString('utf8')));
    }catch(e){reject(e)}
  });
  const raw=Buffer.from(JSON.stringify(payload)),n=Buffer.alloc(4);
  n.writeUInt32LE(raw.length);p.stdin.end(Buffer.concat([n,raw]));
 });
}
(async()=>{
 const ping=await call({type:'PING'});
 assert.equal(ping.ok,true);
 assert.equal(ping.version,4);
 assert.equal(ping.qualitySelectionSupported,true);
 assert.equal(ping.appPresent,true);
 const invalid=await call({type:'DOWNLOAD',version:1,
  url:'https://example.org/master.m3u8',kind:'HLS',headers:[],maxQuality:'4320'});
 assert.equal(invalid.ok,false);
 const invalidFile=await call({type:'DOWNLOAD',version:1,
  url:'https://example.org/1080.mp4',kind:'FILE',headers:[],maxQuality:'720'});
 assert.equal(invalidFile.ok,false);
 console.log('HOST_QUALITY_PROTOCOL=PASS v4 and invalid-cap rejection without download queueing');
})().catch(err=>{console.error(err);process.exitCode=1});
