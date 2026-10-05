// Offline player DOM test: no website or media request is made.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(
  path.join(__dirname, '..', 'N_m3u8DL_RE_GUI', 'Services', 'browser-quality.js'), 'utf8');

function run({heights=[],initiallyOpen=true,showTrigger=true}={}) {
  const calls=[], messages=[], callbacks=[];
  const items=heights.map(height=>({
    textContent:height+'p',
    getAttribute:()=>null, getClientRects:()=>[1],
    click(){calls.push(height);}
  }));
  let menuOpen=initiallyOpen;
  const menu={
    getClientRects:()=>menuOpen?[1]:[],
    querySelectorAll:()=>items
  };
  const trigger={
    getClientRects:()=>[1],
    click(){calls.push('open');menuOpen=true;}
  };
  const document={
    readyState:'complete',documentElement:{},
    querySelectorAll(sel){
      if(sel==='*') return [];
      if(sel==='.mgp_qualitiesMenu')return [menu];
      return [];
    },
    querySelector(sel) {
      return showTrigger&&sel==='.mgp_qualityButton'?trigger:null;
    }
  };
  const window={
    chrome:{webview:{postMessage(msg){messages.push(msg);}}},
    getComputedStyle:()=>({display:'block',visibility:'visible'})
  };
  const context={window,document,Date,
    setInterval(fn){callbacks.push(fn); return callbacks.length;},
    setTimeout(fn){callbacks.push(fn); return callbacks.length;},
    clearInterval(){},
    MutationObserver:class {
      observe(){} disconnect(){}
    }
  };
  vm.runInNewContext(source,context,{timeout:1000});
  return {calls,messages,window,callbacks};
}
let actual=run({heights:[240,720,1080,480]});
assert.deepEqual(actual.calls,[1080]);
assert(actual.messages.includes('uvd-quality:selected:1080'));
actual=run({heights:[720,1080],initiallyOpen:false});
assert.equal(actual.calls[0],'open');
actual.window.__uvdQualityKick();
assert.deepEqual(actual.calls,['open',1080]);
actual=run({heights:[],initiallyOpen:false,showTrigger:false});
assert.deepEqual(actual.calls,[]);
assert(!actual.messages.some(x=>x.startsWith('uvd-quality:selected:')));
console.log('QUALITY_DOM_TEST=PASS highest rendition, closed-menu sequence, no arbitrary-page clicks');
