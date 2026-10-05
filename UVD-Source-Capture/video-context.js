// MAIN-world opt-in workaround for page scripts which cancel contextmenu.
// Affects only right clicks directly on video (including composed shadow path).
// Never changes left clicks, network requests, DRM, or pages not enabled by user.
export function setVideoContextMenu(enabled) {
  const key='__uvdVideoContextOptIn';
  const targetsVideo=e=>{
    try {
      const path=typeof e.composedPath==='function'?e.composedPath():[e.target];
      return path.some(node=>node?.tagName==='VIDEO');
    }catch{return false;}
  };
  if(!window[key]){
    const original=Event.prototype.preventDefault;
    window[key]={enabled:false};
    Event.prototype.preventDefault=function(...args){
      if(window[key]?.enabled&&this.type==='contextmenu'&&targetsVideo(this))return;
      return Reflect.apply(original,this,args);
    };
    // Stop later document/element contextmenu interceptors from cancelling it.
    window.addEventListener('contextmenu',event=>{
      if(window[key]?.enabled&&targetsVideo(event)){
        event.stopImmediatePropagation();
      }
    },true);
  }
  window[key].enabled=enabled===true;
  return {enabled:window[key].enabled,
    videos:document.querySelectorAll('video').length};
}
