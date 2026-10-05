const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const target='ahldilknefbkdlkddkfcniaeepnknfdd';
const folder='D:\\UniversalVideoDownloader\\release';
const candidates=[];
function walk(dir,depth=0){
 if(depth>3)return;
 try{
  if(fs.existsSync(path.join(dir,'manifest.json'))){
   try{const m=JSON.parse(fs.readFileSync(path.join(dir,'manifest.json'),'utf8'));
    if(m.name==='UVD Source Capture')candidates.push(dir);
   }catch{}
  }
  for(const e of fs.readdirSync(dir,{withFileTypes:true})){
   if(e.isDirectory() && !/node_modules|\.git|win-x64|publish/.test(e.name))walk(path.join(dir,e.name),depth+1);
  }
 }catch{}
}
walk(folder);
const variations=s=>new Set([
 s,s.toLowerCase(),s.toUpperCase(),s.replaceAll('\\','/'),
 s.toLowerCase().replaceAll('\\','/'),s.toUpperCase().replaceAll('\\','/'),
 s+'\\',s+'/',s.toLowerCase()+'\\',s.toLowerCase()+'/'
]);
const output=[];
for(const candidate of candidates)for(const variation of variations(candidate))
 for(const encoding of ['utf8','utf16le']){
  const hex=crypto.createHash('sha256').update(Buffer.from(variation,encoding)).digest('hex').slice(0,32);
  const ext=[...hex].map(n=>String.fromCharCode(97+parseInt(n,16))).join('');
  if(ext===target)output.push({candidate,variation,encoding});
 }
console.log('CANDIDATES',candidates);
console.log('PATH_HASH_MATCH',output);
