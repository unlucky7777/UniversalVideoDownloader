import assert from 'node:assert/strict';
import vm from 'node:vm';
import {pathToFileURL} from 'node:url';
const root='D:\\UniversalVideoDownloader\\release\\UVD-Source-Capture\\';
const {discoverPageDefinitions}=await import(pathToFileURL(root+'page-definitions.js'));
const {observedSource,mergeCandidate,score}=await import(pathToFileURL(root+'core.js'));
const {selectMainMedia}=await import(pathToFileURL(root+'main-media.js'));
const base='https://www.pornvov.net/get_file/';
const video={video_id:'10273',video_url:base+'10273-360.mp4?sig=a',
 video_alt_url:base+'10273-480.mp4?sig=b',
 video_alt_url2:base+'10273-720.mp4?sig=c',
 video_alt_url3:base+'10273-1080.mp4?sig=d',
 video_alt_url4:base+'10273-2160.mp4?sig=e'};
const evaluate=(flashvars,location='https://www.pornvov.net/video/example/')=>
vm.runInNewContext('('+discoverPageDefinitions.toString()+')()',{
 window:{flashvars},location:{href:location,hostname:new URL(location).hostname},
 URL,Object,Number,Array,RegExp,String
},{timeout:1000});
const definitions=evaluate(video).definitions;
assert.equal(definitions.length,5);
assert.deepEqual([...definitions.map(x=>x.declaredHeight)].sort((a,b)=>a-b),
 [360,480,720,1080,2160]);
assert(definitions.every(x=>x.isMain&&x.mainGroup==='pornvov-10273'));
assert.equal(evaluate({...video,video_alt_url4:base+'99999-2160.mp4?sig=e'}).definitions.length,4);
assert.equal(evaluate(video,'https://evil.example.com/other').definitions.length,0);
const candidates=definitions.map((d,i)=>({
 ...observedSource(d.url,'',[],4,Date.now()+i),
 isMain:d.isMain,mainGroup:d.mainGroup,declaredHeight:d.declaredHeight,
 qualityEvidence:'player-definition'
}));
const high=candidates.find(x=>x.declaredHeight===2160);
const merged=mergeCandidate(high,observedSource(high.url,'video/mp4',[],4));
assert.equal(merged.declaredHeight,2160);
assert.equal(merged.isMain,true);
const ad=observedSource('https://video.example.com/trailer.mp4','video/mp4',[],4);
const displayed=selectMainMedia([ad,...candidates],{kind:'website',url:'https://www.pornvov.net/video/example/'});
assert.equal(displayed.length,1);
assert.equal(displayed[0].declaredHeight,2160);
assert(score(merged)>score(ad));
assert.deepEqual(selectMainMedia([],{kind:'website',
  url:'https://www.pornvov.net/video/example/'}),[]);
const yt=selectMainMedia(candidates,{kind:'youtube',url:'https://www.youtube.com/watch?v=jNQXAC9IVRw'});
assert.equal(yt.length,1);assert.equal(yt[0].kind,'PAGE');
assert.equal(yt[0].isMain,true);
console.log('MAIN_MEDIA_141=PASS: five exact main renditions -> one highest FILE; unrelated excluded; YouTube one PAGE');
