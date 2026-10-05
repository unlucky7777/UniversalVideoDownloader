import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';
import vm from 'node:vm';
const root='D:\\UniversalVideoDownloader\\release\\UVD-Source-Capture\\';
const {discoverPageDefinitions} = await import(pathToFileURL(root+'page-definitions.js'));
const {score,observedSource,mergeCandidate}=await import(pathToFileURL(root+'core.js'));
const player={mediaDefinitions:[
 {format:'hls',quality:720,defaultQuality:true,
   videoUrl:'https://cv.test/videos/720P_1500K_.m3u8?hash=signed720'},
 {format:'hls',quality:1080,defaultQuality:false,
   videoUrl:'https://cv.test/videos/1080P_3000K_.m3u8?hash=signed1080'},
 {format:'hls',quality:[1080,720,480],defaultQuality:false,
   videoUrl:'https://cv.test/videos/480P_600K_.m3u8?hash=signed480'},
 {format:'mp4',quality:1080,videoUrl:'javascript:alert(1)'},
 {format:'hls',quality:2160,videoUrl:'https://user:pass@cv.test/secret.m3u8'}
]};
const page={flashvars_42:player,playerObjList:{one:{flashvars:{embedId:42}}}};
const result=vm.runInNewContext('('+discoverPageDefinitions.toString()+')()', {
 window:page, location:{href:'https://www.pornhub.com/view_video.php?viewkey=sample'},URL,
 Object,Number,Array,RegExp,String
},{timeout:1000});
assert.equal(result.sourceConfigs,1);
assert.equal(result.definitions.length,3);
const high=result.definitions.find(x=>x.declaredHeight===1080);
assert(high && high.url.includes('1080P'));
const multi=result.definitions.find(x=>x.qualityChoices.length);
assert.equal(multi.declaredHeight,null); // 480P URI never labelled 1080P.
assert(multi.url.includes('480P'));
const source=observedSource(high.url,'',[],1);
source.declaredHeight=high.declaredHeight;
source.qualityEvidence='player-definition';
const low=observedSource(result.definitions[0].url,'',[],1);
low.declaredHeight=720;low.qualityEvidence='player-definition';
assert(score(source)>score(low));
assert.equal(mergeCandidate(source,observedSource(source.url,'',[],1)).declaredHeight,1080);
console.log('PAGE_DEFINITIONS_TEST=PASS flashvars/playerObjList, URL guard, numeric 1080 priority, array quality not trusted');
