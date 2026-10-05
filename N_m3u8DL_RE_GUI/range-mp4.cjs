'use strict';
// Resumable byte-range downloader for a verified Pornvov main MP4.
// Do not infer signed links, disable TLS checks, or forward credentials.
const fs=require('node:fs/promises');
const path=require('node:path');
const crypto=require('node:crypto');
const CHUNK=8*1024*1024, MAX_TOTAL=32*1024*1024*1024;
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
function validateSource(input){
  const url=new URL(input);
  if(url.protocol!=='https:'||url.username||url.password||
     !/(^|\.)pornvov\.net$/i.test(url.hostname))throw Error('Unsupported direct media origin');
  const name=path.posix.basename(url.pathname);
  if(!/^\d{1,15}-(?:240|360|480|720|1080|1440|2160)\.mp4$/i.test(name))
    throw Error('Expected a site-advertised main-video MP4 URL');
  return {url,name};
}
function totalFromContentRange(value,from,to){
  const m=/^bytes (\d+)-(\d+)\/(\d+)$/i.exec(String(value||''));
  if(!m||Number(m[1])!==from||Number(m[2])!==to)throw Error('Unverified media range');
  const total=Number(m[3]);
  if(!Number.isSafeInteger(total)||total<to+1||total>MAX_TOTAL)
    throw Error('Invalid total media length');
  return total;
}
function headers(origin,range){
  return {'User-Agent':'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/154.0 Safari/537.36',
    'Accept-Encoding':'identity','Referer':origin+'/','Range':range};
}
async function request(url,range){
  const r=await fetch(url.href,{headers:headers(url.origin,range),
    signal:AbortSignal.timeout(60000),redirect:'follow'});
  const final=new URL(r.url);
  // The site legitimately redirects media GET ranges to its VK-backed CDN.
  // Permit only these two validated HTTPS domain families; not arbitrary
  // redirects or local/private addresses.
  if(final.protocol!=='https:'||
     !/(^|\.)(?:pornvov\.net|vkuser\.net)$/i.test(final.hostname)){
    await r.body?.cancel();throw Error('Unexpected media redirect');
  }
  if(r.status!==206){await r.body?.cancel();throw Error('Media range HTTP '+r.status);}
  return r;
}
async function inspect(url){
  const r=await request(url,'bytes=0-0');
  const total=totalFromContentRange(r.headers.get('content-range'),0,0);
  let n=0;for await(const part of r.body)n+=part.length;
  if(n!==1)throw Error('Range probe received unexpected bytes');
  return total;
}
async function rangeToFile(url,start,end,total,handle){
  const r=await request(url,'bytes='+start+'-'+end);
  if(totalFromContentRange(r.headers.get('content-range'),start,end)!==total){
    await r.body?.cancel();throw Error('Media size changed');
  }
  let position=start;
  for await(const value of r.body){
    const chunk=Buffer.from(value);let offset=0;
    while(offset<chunk.length){
      const {bytesWritten}=await handle.write(chunk,offset,chunk.length-offset,position);
      if(bytesWritten<=0)throw Error('File write returned zero bytes');
      offset+=bytesWritten;position+=bytesWritten;
    }
    if(position>end+1)throw Error('Oversized range response');
  }
  if(position!==end+1)throw Error('Incomplete range');
}
async function download(input,outputDir,threadArg=6,mode='download'){
  const {url,name}=validateSource(input);
  const total=await inspect(url);
  const limit=Math.min(8,Math.max(2,Math.trunc(Number(threadArg)||6)));
  console.log('[media] '+name+', '+(total/1073741824).toFixed(2)+
    ' GiB; verified Content-Range, concurrency '+limit);
  console.log('[size] '+total);
  if(mode==='--probe'){console.log('RANGE_PROBE_OK');return {total,name};}
  if(!path.isAbsolute(outputDir))throw Error('Absolute output directory required');
  // Tiny live smoke: verify two independent 1 KiB ranges and positional file
  // writes without allocating or downloading the multi-GiB full video.
  if(mode==='--smoke'){
    await fs.mkdir(outputDir,{recursive:true});
    const file=path.join(outputDir,'.uvd-range-smoke-'+process.pid+'.part');
    const h=await fs.open(file,'w+');
    try{
      await h.truncate(2048);
      await Promise.all([
        rangeToFile(url,0,1023,total,h),
        rangeToFile(url,1024,2047,total,h)
      ]);
      await h.sync();
      if((await h.stat()).size!==2048)throw Error('Smoke output length mismatch');
    }finally{await h.close();await fs.rm(file,{force:true});}
    console.log('RANGE_PARALLEL_SMOKE_OK');
    return {total,name};
  }
  await fs.mkdir(outputDir,{recursive:true});
  const file=path.join(outputDir,name),part=file+'.part',statePath=file+'.resume.json';
  try{
    const old=await fs.stat(file);
    if(old.size===total){console.log('[download] 100% existing output');return {total,name};}
    throw Error('Existing output has a different size; refusing to overwrite it');
  }catch(e){if(e.code!=='ENOENT')throw e;}
  const key=crypto.createHash('sha256').update(url.origin+url.pathname).digest('hex');
  const count=Math.ceil(total/CHUNK);let completed=new Set(),resume=false;
  try{
    const state=JSON.parse(await fs.readFile(statePath,'utf8'));
    const old=await fs.stat(part);
    if(state.key===key&&state.total===total&&state.chunk===CHUNK&&
       old.size===total&&Array.isArray(state.completed)){
      completed=new Set(state.completed.filter(i=>Number.isInteger(i)&&i>=0&&i<count));
      resume=true;
    }
  }catch{}
  const handle=await fs.open(part,resume?'r+':'w+');
  try{
    if(!resume)await handle.truncate(total);
    let next=0,fatal=null,writing=Promise.resolve(),lastLog=0;
    let resumeBytes=0;
    for(const i of completed)resumeBytes+=Math.min(CHUNK,total-CHUNK*i);
    const started=Date.now();
    async function record(i){
      completed.add(i);
      writing=writing.then(async()=>{
        const tmp=statePath+'.tmp';
        await fs.writeFile(tmp,JSON.stringify({
          key,total,chunk:CHUNK,completed:[...completed].sort((a,b)=>a-b)
        }));
        await fs.rename(tmp,statePath);
      });
      await writing;
      if(Date.now()-lastLog>1100||completed.size===count){
        lastLog=Date.now();
        let bytes=0;for(const x of completed)bytes+=Math.min(CHUNK,total-x*CHUNK);
        const speed=Math.max(0,(bytes-resumeBytes)/1048576/
          Math.max(1,(Date.now()-started)/1000));
        const eta=Math.max(0,Math.ceil((total-bytes)/Math.max(0.01,speed*1048576)));
        const hrs=Math.floor(eta/3600),mins=Math.floor(eta%3600/60),secs=eta%60;
        const clock=hrs?String(Math.min(999,hrs))+':'+String(mins).padStart(2,'0')+
          ':'+String(secs).padStart(2,'0'):
          String(Math.min(999,mins))+':'+String(secs).padStart(2,'0');
        console.log('[download] '+(100*bytes/total).toFixed(1)+'% ('+
          completed.size+'/'+count+' blocks) at '+speed.toFixed(2)+' MiB/s ETA '+clock);
      }
    }
    async function worker(){
      while(!fatal){
        const i=next++;
        if(i>=count)return;
        if(completed.has(i))continue;
        const start=i*CHUNK,end=Math.min(total-1,start+CHUNK-1);
        let success=false,err;
        for(let retry=0;retry<5&&!success;retry++){
          try{await rangeToFile(url,start,end,total,handle);await record(i);success=true;}
          catch(e){err=e;if(retry<4)await sleep(Math.min(10000,1000*2**retry));}
        }
        if(!success)fatal=err||Error('Repeated range failure');
      }
    }
    await Promise.all(Array.from({length:limit},()=>worker()));
    await writing;
    if(fatal)throw fatal;
    if(completed.size!==count)throw Error('Incomplete range download');
    await handle.sync();
  }finally{await handle.close();}
  await fs.rename(part,file);
  await fs.rm(statePath,{force:true});
  console.log('[download] 100% COMPLETED '+name);
  return {total,name};
}
if(require.main===module){
  const [url,folder,threads,mode]=process.argv.slice(2);
  download(url,folder,threads,mode).catch(error=>{
    console.error('[error] '+String(error.message||'Download failed')
      .replace(/https?:\/\/\S+/g,'[redacted]'));
    process.exitCode=1;
  });
}
module.exports={validateSource,totalFromContentRange,download};
