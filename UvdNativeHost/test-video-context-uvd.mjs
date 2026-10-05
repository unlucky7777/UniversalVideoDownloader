import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';
const listeners=new Map();
class FakeEvent{
 constructor(type,target){this.type=type;this.target=target;this.prevented=false;this.stopped=false;}
 composedPath(){return [this.target];}
 preventDefault(){this.prevented=true;}
 stopImmediatePropagation(){this.stopped=true;}
}
globalThis.Event=FakeEvent;
globalThis.window={addEventListener:(name,cb)=>listeners.set(name,cb)};
globalThis.document={querySelectorAll:name=>name==='video'?[{tagName:'VIDEO'}]:[]};
const {setVideoContextMenu}=await import(pathToFileURL(
 'D:\\UniversalVideoDownloader\\release\\UVD-Source-Capture\\video-context.js'));
let status=setVideoContextMenu(true);
assert.equal(status.enabled,true);assert.equal(status.videos,1);
const video={tagName:'VIDEO'};
const right=new FakeEvent('contextmenu',video);
right.preventDefault();assert.equal(right.prevented,false);
listeners.get('contextmenu')(right);assert.equal(right.stopped,true);
const elsewhere=new FakeEvent('contextmenu',{tagName:'DIV'});
elsewhere.preventDefault();assert.equal(elsewhere.prevented,true);
const click=new FakeEvent('click',video);click.preventDefault();assert.equal(click.prevented,true);
setVideoContextMenu(false);
const restored=new FakeEvent('contextmenu',video);restored.preventDefault();
assert.equal(restored.prevented,true);
console.log('VIDEO_CONTEXT=PASS opt-in video-only suppression, regular events unchanged, disable works');
