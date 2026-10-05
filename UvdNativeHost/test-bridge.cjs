const assert=require('node:assert/strict');
const {spawn}=require('node:child_process');
const path=require('node:path');
const exe='D:\\UniversalVideoDownloader\\release\\UVD-NativeHost-stage\\UvdNativeHost.exe';
function call(message){
  return new Promise((resolve,reject)=>{
    const child=spawn(exe,[],{windowsHide:true});
    let chunks=[];
    child.stdout.on('data',d=>chunks.push(d));
    child.on('error',reject);
    child.on('close',()=>{
      const b=Buffer.concat(chunks);
      try{
        assert(b.length>=4);
        assert.equal(b.readUInt32LE(0),b.length-4);
        resolve(JSON.parse(b.subarray(4).toString('utf8')));
      }catch(e){reject(e);}
    });
    const data=Buffer.from(JSON.stringify(message));
    const n=Buffer.alloc(4);n.writeUInt32LE(data.length);
    child.stdin.end(Buffer.concat([n,data]));
  });
}
(async()=>{
 const a=await call({type:'PING'});
 assert(a.ok && a.nativeHost==='com.uvd.bridge');
 const b=await call({type:'DOWNLOAD',version:1,url:'file:///malicious.exe',kind:'HLS',headers:[]});
 assert.equal(b.ok,false);
 const c=await call({type:'DOWNLOAD',version:1,url:'https://example.org/test.m3u8',
  kind:'HLS',headers:[{name:'Cookie',value:'secret'}]});
 assert.equal(c.ok,false);
 console.log('NATIVE_MESSAGING_FRAMING=PASS PING + URL validation + credential rejection');
})().catch(e=>{console.error(e);process.exit(1)});
