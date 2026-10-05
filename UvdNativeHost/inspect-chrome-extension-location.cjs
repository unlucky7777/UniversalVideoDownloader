const fs=require('node:fs'),path=require('node:path');
const base=path.join(process.env.LOCALAPPDATA,'Google','Chrome','User Data');
const id='ahldilknefbkdlkddkfcniaeepnknfdd';
for(const de of fs.readdirSync(base,{withFileTypes:true})){
 if(!de.isDirectory()||!(/^(Default|Profile \d+)$/.test(de.name)))continue;
 for(const name of ['Preferences','Secure Preferences']){
  const file=path.join(base,de.name,name);if(!fs.existsSync(file))continue;
  try{
   const j=JSON.parse(fs.readFileSync(file,'utf8'));
   const settings=j.extensions?.settings??{};
   console.log('FILE='+de.name+'/'+name+' REGISTERED_COUNT='+Object.keys(settings).length);
   for(const [key,e] of Object.entries(settings)){
    if(key===id || /uvd|video.*download|source capture/i.test(e?.manifest?.name||'')){
     console.log(JSON.stringify({targetId:key,name:e.manifest?.name,version:e.manifest?.version,
       path:e.path,location:e.location,state:e.state,
       install_source:e.install_source,from_webstore:e.from_webstore,
       keyExists:!!e.manifest?.key,entryKeys:Object.keys(e).slice(0,20)}));
    }
   }
   const secure=j.protection?.macs?.extensions?.settings??{};
   if(secure[id])console.log('SECURE_HASH_PRESENT='+name);
  }catch(e){console.log('READ_ERROR='+de.name+'/'+name+' '+e.name);}
 }
}