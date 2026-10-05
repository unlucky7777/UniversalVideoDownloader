import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';
const p='D:\\UniversalVideoDownloader\\release\\UVD-Source-Capture\\metadata.js';
const {parseHls,parseDash,formatBytes,formatDuration,probeSource}=
  await import(pathToFileURL(p));
const master=['#EXTM3U',
  '#EXT-X-STREAM-INF:BANDWIDTH=800000,RESOLUTION=640x360','360/index.m3u8',
  '#EXT-X-STREAM-INF:BANDWIDTH=3000000,RESOLUTION=1920x1080','1080/index.m3u8',
  '#EXT-X-STREAM-INF:BANDWIDTH=1600000,RESOLUTION=1280x720','720/index.m3u8'].join('\n');
const h=parseHls(master);
assert.equal(h.kind,'master');
assert.equal(h.variants[0].height,1080);
assert.equal(h.variants[0].bandwidth,3000000);
const media=parseHls('#EXTM3U\n#EXTINF:5.5,\nabc.ts\n#EXTINF:4.5,\ndef.ts\n#EXT-X-ENDLIST');
assert.equal(media.duration,10);
assert.equal(parseHls('#EXTM3U\n#EXTINF:4,\na.ts').duration,null);
const dash=parseDash('<MPD mediaPresentationDuration="PT1H2M3S"><Representation width="1280" height="720" bandwidth="1600000"></Representation><Representation width="1920" height="1080" bandwidth="3200000"></Representation></MPD>');
assert.equal(dash.duration,3723);
assert.equal(dash.variants[0].height,1080);
assert.equal(formatDuration(3723),'1:02:03');
assert.equal(formatBytes(null),'확인 불가');
const old=globalThis.fetch;
globalThis.fetch=async()=>new Response(master,{status:200});
const v=await probeSource({url:'https://example.org/master.m3u8',kind:'HLS',headers:[]},
  {duration:120,height:720,width:1280});
globalThis.fetch=old;
assert.equal(v.height,1080);
assert.equal(v.qualityBasis,'manifest');
assert.equal(v.estimatedBytes,45000000);
assert.equal(v.exactBytes,null);
const oldFetch=globalThis.fetch;
globalThis.fetch=async()=>new Response(null,
 {status:200,headers:{'content-length':'3879127643','content-type':'video/mp4'}});
const large=await probeSource({
 url:'https://example.org/video/10273-2160.mp4',
 kind:'FILE',declaredHeight:2160,isMain:true,headers:[]
},{height:360,duration:1000});
assert.equal(large.exactBytes,3879127643);
assert.equal(large.qualityBasis,'file-head-site-label');
globalThis.fetch=async()=>new Response('#EXTM3U\n#EXTINF:8,\na.ts\n#EXT-X-ENDLIST',{status:200});
const leaf=await probeSource({
 url:'https://example.org/leaf.m3u8',
 kind:'HLS',declaredHeight:2160,headers:[]
},{height:2160});
assert.equal(leaf.height,null);
assert.equal(leaf.qualityBasis,'unverified');
globalThis.fetch=oldFetch;
console.log('METADATA_141=PASS HEAD file size and leaf HLS false-high-quality guard');
