import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';
const dir='D:\\UniversalVideoDownloader\\release\\UVD-Source-Capture\\';
const {qualityOptions}=await import(pathToFileURL(dir+'quality-options.js'));
const page={kind:'PAGE',url:'https://www.youtube.com/watch?v=jNQXAC9IVRw'};
assert.deepEqual(qualityOptions(page).map(x=>x.value),['best']);
assert.deepEqual(qualityOptions(page,[],[
 {height:4320},{height:2880},{height:2160},{height:1080},
 {height:720},{height:240},{height:123},{height:4320}
]).map(x=>x.value),['best','4320','2880','2160','1080','720','240']);
const manifest={kind:'HLS',url:'https://example.net/master.m3u8'};
assert.deepEqual(qualityOptions(manifest,[],[{height:1080},{height:720}]).map(x=>x.value),
 ['best','1080','720']);
console.log('METADATA_QUALITY_MENU=PASS actual PAGE renditions incl 8K, unknown formats not advertised, verified HLS heights');
