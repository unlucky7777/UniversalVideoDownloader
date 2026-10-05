const fs=require('node:fs'),p=require('node:path');
const dir=p.join(process.env.LOCALAPPDATA,'Google','Chrome','User Data','Default');
for(const name of ['Preferences','Secure Preferences']){
 const path=p.join(dir,name),raw=fs.readFileSync(path,'utf8'),j=JSON.parse(raw);
 console.log(name,'BYTES',raw.length,'KEYS',Object.keys(j).slice(0,50));
 console.log('EXTENSION_KEYS',Object.keys(j.extensions||{}));
 console.log('SETTINGS_TYPE',typeof j.extensions?.settings);
 console.log('TARGET_VALUE_TYPE',typeof j.extensions?.settings?.ahldilknefbkdlkddkfcniaeepnknfdd);
 console.log('PROTECTION_KEYS',Object.keys(j.protection||{}));
}

const secure=JSON.parse(fs.readFileSync(p.join(dir,'Secure Preferences'),'utf8')).extensions.settings;
const target=secure.ahldilknefbkdlkddkfcniaeepnknfdd;
console.log('TARGET_NULL',target===null,'TARGET_TYPE',typeof target,'TARGET_KEYS',Object.keys(target||{}));
console.log('TARGET_VALUE_IF_PRIMITIVE',target===null?'<null>':typeof target!=='object'?String(target).slice(0,150):'<object>');
console.log('SETTINGS_ENTRIES',Object.entries(secure).map(([k,v])=>[k,typeof v,v===null?'<null>':(v?.manifest?.name||v?.path||'<no descriptive fields>')]).filter(x=>x[0]==='ahldilknefbkdlkddkfcniaeepnknfdd'||x[2]!=='<no descriptive fields>'));
