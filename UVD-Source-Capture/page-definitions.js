// Read-only MAIN-world player-configuration discovery.
// No inline script evaluation and no reconstruction of signed URLs.
export async function discoverPageDefinitions(adapterConfig=null) {
  const output=[],visited=new Set();
  let sources=0;
  const naverVideos=[];
  const siteApiEndpoints=new Set();
  let siteApiRenditions=0;
  const siteApi=adapterConfig&&adapterConfig.mode==='media-definitions-get-media'
    ?adapterConfig:null;
  let siteApiGroup=null;
  if(siteApi){
    try{
      const here=new URL(location.href);
      let groupId=siteApi.groupQueryParam
        ?here.searchParams.get(String(siteApi.groupQueryParam)):null;
      if(!groupId&&siteApi.groupPathPrefix&&
         here.pathname.startsWith(String(siteApi.groupPathPrefix))){
        groupId=here.pathname.slice(String(siteApi.groupPathPrefix).length)
          .split('/')[0]||null;
      }
      if(groupId&&/^[A-Za-z0-9_-]{1,70}$/.test(groupId))
        siteApiGroup=String(siteApi.adapterId||'site')+'-'+groupId;
    }catch{}
  }
  // SmartEditor normally mounts several independent data-module video blocks.
  // Inspect only Naver-hosted document/frame DOM, not arbitrary page scripts.
  if(/^(?:cafe|m\.cafe|blog|m\.blog)\.naver\.com$/i.test(location.hostname)){
    const seen=new Set();
    for(const el of [...document.querySelectorAll('[data-module]')].slice(0,350)){
      if(naverVideos.length>=10)break;
      const raw=el.getAttribute('data-module')||'';
      if(raw.length<15||raw.length>8192||
         !/(?:vid|videoId)/.test(raw)||!/(?:inkey|inKey)/.test(raw))continue;
      try{
        const obj=JSON.parse(raw);
        const data=obj?.data?.data||obj?.data||obj;
        const vid=data?.vid||data?.videoId;
        const key=data?.inkey||data?.inKey;
        if(typeof vid!=='string'||typeof key!=='string'||
           !/^[A-Za-z0-9_-]{8,96}$/.test(vid)||
           !/^[A-Za-z0-9_-]{8,512}$/.test(key)||
           seen.has(vid+':'+key))continue;
        seen.add(vid+':'+key);
        naverVideos.push({vid,key,ordinal:naverVideos.length});
      }catch{}
    }
  }
  const add=object=>{
    if(!object || typeof object!=='object' || visited.has(object))return;
    visited.add(object);
    // Pornvov publishes actual main-video MP4 URLs as a video_id matched
    // family: video_url (360) plus video_alt_url through video_alt_url4.
    // Match the video ID and URI filename; never infer higher-quality URLs
    // by replacing "360" within a signed URL.
    try{
      const id=String(object.video_id||'');
      if (/(^|\.)pornvov\.net$/i.test(location.hostname) &&
          /^\d{1,15}$/.test(id) && typeof object.video_url==='string'){
        let found=0;
        for(const [key,height] of [
          ['video_url',360],['video_alt_url',480],
          ['video_alt_url2',720],['video_alt_url3',1080],
          ['video_alt_url4',2160]
        ]){
          const raw=object[key];
          if(typeof raw!=='string'||raw.length>8192)continue;
          try{
            const u=new URL(raw,location.href);
            if(!['https:','http:'].includes(u.protocol)||u.username||u.password)continue;
            if(!(new RegExp('(?:^|/)'+id+'-'+height+'\\.mp4$','i')).test(u.pathname))continue;
            output.push({url:u.href,format:'mp4',declaredHeight:height,
              qualityEvidence:'player-definition',isMain:true,
              mainGroup:'pornvov-'+id,frameUrl:location.href});
            found++;
          }catch{}
        }
        if(found)sources++;
      }
    }catch{}
    let definitions;
    try{definitions=object.mediaDefinitions;}catch{return;}
    if(!Array.isArray(definitions))return;
    sources++;
    for(const entry of definitions.slice(0,40)){
      if(!entry||typeof entry!=='object')continue;
      try{
        if(typeof entry.videoUrl!=='string'||entry.videoUrl.length>8192)continue;
        const uri=new URL(entry.videoUrl,location.href);
        if(!['https:','http:'].includes(uri.protocol)||uri.username||uri.password)continue;
        if(siteApi && siteApi.endpointPathPrefix &&
           uri.pathname.startsWith(String(siteApi.endpointPathPrefix))){
          const allowed=Array.isArray(siteApi.allowedEndpointHosts)
            ?siteApi.allowedEndpointHosts.map(x=>String(x).toLowerCase()):[];
          if(allowed.includes(uri.hostname.toLowerCase()))
            siteApiEndpoints.add(uri.href);
          continue;
        }
        const quality=Number(entry.quality);
        const height=Array.isArray(entry.quality)?null:
          Number.isInteger(quality)&&quality>=144&&quality<=4320?quality:null;
        const choices=Array.isArray(entry.quality)?
          entry.quality.map(Number).filter(x=>Number.isInteger(x)&&x>=144&&x<=4320).slice(0,12):[];
        const format=String(entry.format||'').toLowerCase();
        if(!['hls','dash','mp4','webm'].includes(format))continue;
        output.push({url:uri.href,format,declaredHeight:height,
          qualityChoices:choices,defaultQuality:entry.defaultQuality===true,
          qualityEvidence:siteApi?'player-definition':undefined,
          isMain:Boolean(siteApi&&siteApiGroup),mainGroup:siteApiGroup||undefined,
          frameUrl:location.href});
      }catch{}
    }
  };
  try{add(window.flashvars);}catch{}
  try{
    for(const key of Object.getOwnPropertyNames(window)
      .filter(k=>/^flashvars_\d{1,15}$/.test(k)).slice(0,30)){
      try{add(window[key]);}catch{}
    }
  }catch{}
  try{
    const players=window.playerObjList;
    if(players&&typeof players==='object'){
      for(const player of Object.values(players).slice(0,15)){
        try{
          add(player?.flashvars);
          const embedId=String(player?.flashvars?.embedId||'');
          if(/^\d{1,15}$/.test(embedId))add(window['flashvars_'+embedId]);
        }catch{}
      }
    }
  }catch{}
  try{add(window.page_params?.video_player_setup?.playervars);}catch{}

  // Some registered browser adapters expose a same-site JSON endpoint
  // from mediaDefinitions. Querying it does not change player quality,
  // currentTime, src or playback state; it only returns authorized renditions.
  if(siteApi&&siteApiEndpoints.size){
    for(const endpoint of [...siteApiEndpoints].slice(0,4)){
      const ctrl=new AbortController();
      const timer=setTimeout(()=>ctrl.abort(),6500);
      try{
        const response=await fetch(endpoint,{
          method:'GET',credentials:'include',cache:'no-store',redirect:'follow',
          signal:ctrl.signal,headers:{Accept:'application/json,text/plain,*/*'}
        });
        if(!response.ok)continue;
        const length=Number(response.headers.get('content-length'));
        if(Number.isFinite(length)&&length>1048576)continue;
        const raw=await response.text();
        if(raw.length>1048576)continue;
        const data=JSON.parse(raw);
        const rows=Array.isArray(data)?data:
          Array.isArray(data?.media)?data.media:
          Array.isArray(data?.formats)?data.formats:[];
        for(const media of rows.slice(0,40)){
          if(!media||typeof media!=='object'||typeof media.videoUrl!=='string'||
             media.videoUrl.length>8192)continue;
          const height=Number(media.quality);
          if(!Number.isInteger(height)||height<144||height>4320)continue;
          let url;
          try{url=new URL(media.videoUrl,location.href);}catch{continue;}
          if(!['https:','http:'].includes(url.protocol)||url.username||url.password)continue;
          const path=url.pathname.toLowerCase();
          const format=path.endsWith('.m3u8')?'hls':
            path.endsWith('.mpd')?'dash':
            path.endsWith('.mp4')?'mp4':
            path.endsWith('.webm')?'webm':null;
          if(!format)continue;
          output.push({url:url.href,format,declaredHeight:height,
            qualityEvidence:'site-api',isMain:Boolean(siteApiGroup),
            mainGroup:siteApiGroup||undefined,frameUrl:location.href});
          siteApiRenditions++;
        }
      }catch{}
      finally{clearTimeout(timer);}
    }
  }

  return {frameUrl:location.href,definitions:output.slice(0,80),
    sourceConfigs:sources+naverVideos.length,naverVideos,
    siteApiRenditions};
}
