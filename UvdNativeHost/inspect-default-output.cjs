const fs=require('node:fs'),path=require('node:path');
const app='D:\\UniversalVideoDownloader\\release\\UniversalVideoDownloader-ExtensionFirst';
for(const f of ['config.txt','config.json']){
 const file=path.join(app,f),raw=fs.readFileSync(file,'utf8');
 try{
  const j=JSON.parse(raw);
  const b64=j['保存路径'];
  const decoded=typeof b64==='string'?Buffer.from(b64,'base64').toString('utf8'):'';
  const norm=s=>path.win32.normalize(s).replace(/[\\/]+$/,'').toLowerCase();
  console.log(f,'IS_JSON=true','HAS_SAVEDIR='+!!b64,'IS_LEGACY_ROOT='+(norm(decoded)===norm(app)),
    'ALREADY_DOWNLOADS='+(norm(decoded)===norm(path.join(app,'Downloads'))));
 }catch(e){
  console.log(f,'JSON_PARSE_ERROR='+e.name,'BYTES='+raw.length);
 }
}

const configRaw=fs.readFileSync(path.join(app,'config.txt'),'utf8');
const keys=configRaw.split(/\r?\n/).map(s=>s.split('=')[0]).filter(Boolean);
console.log('CONFIG_TXT_LINE_COUNT',keys.length,
  'HAS_CHINESE_SAVEDIR_KEY',keys.some(s=>s.includes('保存路径')));
for(const line of configRaw.split(/\r?\n/).filter(s=>s.includes('保存路径'))){
 let val=line.slice(line.indexOf('=')+1).trim();
 const normalized=s=>path.win32.normalize(s).replace(/[\\/]+$/,'').toLowerCase();
 const dec=Buffer.from(val,'base64').toString('utf8');
 console.log('TXT_SAVE_ROOT='+[normalized(dec)===normalized(app)],
   'TXT_SAVE_DOWNLOADS='+[normalized(dec)===normalized(path.join(app,'Downloads'))]);
}
