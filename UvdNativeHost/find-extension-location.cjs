const fs=require('node:fs'),path=require('node:path');
const base=path.join(process.env.LOCALAPPDATA,'Google','Chrome','User Data');
const id='ahldilknefbkdlkddkfcniaeepnknfdd';
for(const dir of fs.readdirSync(base,{withFileTypes:true})){
 if(!dir.isDirectory()||!(/^(Default|Profile \d+)$/.test(dir.name)))continue;
 for(const name of ['Preferences','Secure Preferences']){
  const file=path.join(base,dir.name,name);
  try{
   const obj=JSON.parse(fs.readFileSync(file,'utf8'));
   const ext=obj.extensions?.settings?.[id];
   console.log('PROFILE='+dir.name+' FILE='+name+' FOUND='+!!ext);
   if(ext) console.log('EXTENSION_PATH='+(ext.path||'')+' LOCATION='+ext.location+' STATE='+ext.state);
  }catch(e){ console.log('PROFILE_FILE_ERROR='+name+' '+e.name); }
 }
}
