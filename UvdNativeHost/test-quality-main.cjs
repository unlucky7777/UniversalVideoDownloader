const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const src=fs.readFileSync('D:\\UniversalVideoDownloader\\release\\UVD-Source-Capture-v1.3.0\\quality-control.js','utf8');
const outputs=[];let chosen=null;
const player={getQualityLevels(){return [{label:'360p'},{label:'1080p'},{label:'720p'}]},
  setCurrentQuality(i){chosen=i;}};
const window={
 jwplayer:()=>player,
 dispatchEvent(e){outputs.push(e.detail);},
 getComputedStyle:()=>({display:'block',visibility:'visible'}),
 chrome:{}
};
const doc={readyState:'complete',documentElement:{},
 querySelectorAll(){return []},querySelector(){return null}};
const sandbox={window,document:doc,CustomEvent:class {constructor(_,o){this.detail=o.detail}},
 Date,setTimeout:()=>1,setInterval:()=>1,clearInterval(){},
 MutationObserver:class {observe(){}disconnect(){}}
};
vm.runInNewContext(src,sandbox,{timeout:1000});
assert.equal(chosen,1);
assert(outputs.includes('selected:1080'));
console.log('QUALITY_MAIN_WORLD_API=PASS JWPlayer highest level and bridge event');
