/* Isolated-world bridge: MAIN-world player script does not have extension APIs. */
(() => {
  if (window.__uvdQualityBridgeReady) return;
  window.__uvdQualityBridgeReady=true;
  window.addEventListener('uvd-quality-observation', e=>{
    const code=typeof e.detail==='string'?e.detail:'';
    if(!/^(?:selected:(?:2160|1440|1080|720|540|480|360|320|240)|menu-opened|unavailable|click-failed)$/.test(code))
      return;
    chrome.runtime.sendMessage({type:'UVD_QUALITY_EVENT',code}).catch(()=>{});
  });
})();