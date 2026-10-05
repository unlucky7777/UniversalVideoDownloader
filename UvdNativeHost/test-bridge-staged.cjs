const assert=require('node:assert/strict');
const {spawn}=require('node:child_process');
const exe='D:\\UniversalVideoDownloader\\.tools\\uvd-next\\app\\UvdNativeHost.exe';
function call(message){
  return new Promise((resolve,reject)=>{
    const child=spawn(exe,[],{windowsHide:true});
    const chunks=[];
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
    const len=Buffer.alloc(4);len.writeUInt32LE(data.length);
    child.stdin.end(Buffer.concat([len,data]));
  });
}
(async()=>{
 const a=await call({type:'PING'});
 assert.equal(a.ok,true); assert.equal(a.version,3);
 assert.equal(a.appPresent,true);assert.equal(a.inboxWritable,true);
 assert.equal(a.pageSupported,true);
 const b=await call({type:'DOWNLOAD',version:1,
   url:'file:///nope',kind:'PAGE',headers:[]});
 assert.equal(b.ok,false);
 const c=await call({type:'DOWNLOAD',version:1,
   url:'https://www.youtube.com/watch?v=jNQXAC9IVRw',
   kind:'PAGE',headers:[{name:'Cookie',value:'forbidden'}]});
 assert.equal(c.ok,false);
 console.log('NATIVE_HOST_V3=PASS PING PAGE capability + unsafe PAGE payload refusal');
})().catch(e=>{console.error(e);process.exitCode=1;});
