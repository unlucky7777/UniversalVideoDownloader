import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';
const dir='D:\\UniversalVideoDownloader\\release\\UVD-Source-Capture\\';
const {qualityOptions,exactQualitySource}=await import(pathToFileURL(dir+'quality-options.js'));
const {selectMainMedia}=await import(pathToFileURL(dir+'main-media.js'));
const all=[360,480,720,1080,2160].map(h=>({
  url:'https://www.pornvov.net/get_file/10273-'+h+'.mp4?signature='+h,
  kind:'FILE',isMain:true,mainGroup:'pornvov-10273',declaredHeight:h,
  headers:[],lastSeen:Date.now()
}));
const main=selectMainMedia(all,{kind:'website',
 url:'https://www.pornvov.net/video/test/'})[0];
assert.equal(main.declaredHeight,2160);
const choices=qualityOptions(main,all);
assert.deepEqual(choices.map(x=>x.value),['2160','1080','720','480','360']);
assert(choices.every(x=>x.mode==='exact'));
assert.equal(exactQualitySource(main,all,'720').declaredHeight,720);
assert.equal(exactQualitySource(main,all,'360').declaredHeight,360);
assert.equal(exactQualitySource(main,all,'9999'),null);
const page={url:'https://www.youtube.com/watch?v=jNQXAC9IVRw',kind:'PAGE'};
assert.deepEqual(qualityOptions(page).map(x=>x.value),
  ['best','2160','1440','1080','720','480','360']);
const master={url:'https://other.org/master.m3u8',kind:'HLS'};
assert.deepEqual(qualityOptions(master,[],[{height:720},{height:1080}]).map(x=>x.value),
 ['best','1080','720']);
assert.deepEqual(qualityOptions(master).map(x=>x.value),['best']);
const single={url:'https://other.org/file.mp4',kind:'FILE',pathHeight:480};
assert.equal(qualityOptions(single).length,1);
console.log('QUALITY_MENU=PASS exact five signed FILE sources, PAGE caps, verified HLS variants, single FILE');
