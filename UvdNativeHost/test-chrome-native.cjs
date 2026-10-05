const {spawn}=require('node:child_process');
const fs=require('node:fs'),path=require('node:path');
const chromePath='C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const ext='D:\\UniversalVideoDownloader\\release\\UniversalVideoDownloader-ExtensionFirst\\extension';
const profile='D:\\UniversalVideoDownloader\\.tools\\native-browser-test-profile';
const port=39273;
let child;
async function main(){
 if(!fs.existsSync(chromePath))throw Error('Chrome missing');
 child=spawn(chromePath,[
  '--headless=new','--no-first-run','--no-default-browser-check','--disable-gpu',
  '--disable-extensions-except='+ext,'--load-extension='+ext,
  '--remote-debugging-port='+port,'--user-data-dir='+profile,'about:blank'
 ],{windowsHide:true,stdio:'ignore'});
 let targets=[];
 for(let k=0;k<35;k++){
  await new Promise(r=>setTimeout(r,300));
  try {
   const res=await fetch('http://127.0.0.1:'+port+'/json/list',
     {signal:AbortSignal.timeout(900)});
   if(res.ok){
    targets=await res.json();
    if(targets.some(x=>x.type==='service_worker'))break;
   }
  }catch{}
 }
 const worker=targets.find(x=>x.type==='service_worker'&&
  x.url.includes('background.js'));
 if(!worker){
  console.log('CHROME_WORKER_UNAVAILABLE; targets='+targets.map(x=>x.type+':'+(x.url.startsWith('chrome-extension:')?x.url.slice(0,125):'[system]')).join(','));
  return;
 }
 const id=worker.url.match(/chrome-extension:\/\/([^/]+)/)?.[1]||'unknown';
 console.log('TEST_EXTENSION_ID='+id);
 const socket=new WebSocket(worker.webSocketDebuggerUrl);
 await new Promise((ok,fail)=>{
  socket.addEventListener('open',ok,{once:true});
  socket.addEventListener('error',fail,{once:true});
 });
 const reply=await new Promise((resolve,reject)=>{
  let timeout=setTimeout(()=>reject(Error('CDP timeout')),7000);
  socket.addEventListener('message',ev=>{
   try {
    const obj=JSON.parse(ev.data);
    if(obj.id===1){
     clearTimeout(timeout);resolve(obj);
    }
   }catch{}
  });
  socket.send(JSON.stringify({id:1,method:'Runtime.evaluate',params:{
   expression:'chrome.runtime.sendNativeMessage("com.uvd.bridge",{type:"PING"}).then(v=>({ok:true,data:v}),e=>({ok:false,error:e.message}))',
   awaitPromise:true,returnByValue:true
  }}));
 });
 console.log('NATIVE_BROWSER_PING='+JSON.stringify(reply.result?.result?.value||
  reply.error||'no_result'));
 socket.close();
}
main().catch(e=>{console.log('PROBE_ERROR='+e.message);process.exitCode=1})
 .finally(()=>{if(child&&!child.killed)child.kill();});
