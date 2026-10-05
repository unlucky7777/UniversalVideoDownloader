import { safeHeaders } from './core.js';

function safePageUrl(input){
  try{
    const u=new URL(String(input||''));
    return ['http:','https:'].includes(u.protocol)&&!u.username&&!u.password?u:null;
  }catch{return null;}
}

export function pageMediaHeaders(pageUrl,userAgent='',language='',override=null){
  const page=safePageUrl(pageUrl);
  const overrideOrigin=safePageUrl(override?.origin)?.origin||null;
  const overrideReferer=safePageUrl(override?.referer)?.href||null;
  const rows=[];
  if(page||overrideOrigin||overrideReferer){
    const referer=overrideReferer||page?.href;
    const origin=overrideOrigin||page?.origin;
    if(referer)rows.push({name:'Referer',value:referer});
    if(origin)rows.push({name:'Origin',value:origin});
  }
  if(userAgent)rows.push({name:'User-Agent',value:String(userAgent).slice(0,350)});
  rows.push({name:'Accept',value:'*/*'});
  if(language){
    const first=String(language).split(',')[0].trim().slice(0,40);
    if(first)rows.push({name:'Accept-Language',value:first});
  }
  return safeHeaders(rows);
}

function sameFamily(a,b){
  if(!a||!b)return false;
  if(a.mainGroup&&b.mainGroup)return a.mainGroup===b.mainGroup;
  try{
    const ua=new URL(a.url),ub=new URL(b.url);
    return ua.hostname===ub.hostname;
  }catch{return false;}
}

export function trustedMediaHeaders(source,allItems=[]){
  if(!source)return [];
  const candidates=(allItems||[]).filter(row=>
    row&&row!==source&&sameFamily(source,row)&&
    (row.networkVerified===true||row.curlVerified===true)&&
    Array.isArray(row.headers)&&row.headers.length
  ).sort((a,b)=>
    Math.max(b.verifiedAt||0,b.curlVerifiedAt||0,b.lastSeen||0)-
    Math.max(a.verifiedAt||0,a.curlVerifiedAt||0,a.lastSeen||0));

  // The most recent proven browser request is authoritative for transport
  // headers. It overrides synthetic defaults on API-discovered siblings.
  return safeHeaders([
    ...(source.headers||[]),
    ...(candidates[0]?.headers||[])
  ]);
}
