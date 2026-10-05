import {test} from 'node:test';
import assert from 'node:assert/strict';
import {observedSource} from '../core.js';
import {qualityFamilyKey,familyCandidates,qualityOptions,exactQualitySource} from '../quality-options.js';

const make=(height,id='435335881',suffix='x')=>{
  const url='https://hv-h.phncdn.com/hls/videos/202307/12/'+id+'/'+height+
    'P_4000K_'+id+'.mp4/master.m3u8?h='+suffix+'&e=99999&f=1';
  return observedSource(url,'',[],3,Date.now());
};
test('the same video 1080p and 720p HLS masters form one family',()=>{
 const a=make(1080),b=make(720);
 assert.equal(qualityFamilyKey(a),qualityFamilyKey(b));
 assert.equal(familyCandidates(b,[b,a]).length,2);
 assert.notEqual(qualityFamilyKey(a),qualityFamilyKey(make(1080,'435335882')));
 assert.equal(qualityFamilyKey(observedSource('https://other.example.com/hls/videos/202307/12/435335881/1080P_4000K_435335881.mp4/master.m3u8','',[],3)),null);
});
test('unverified 1080p stays disabled; verified 720p is selectable',()=>{
 const a=make(1080),b=make(720); const proof=new Map([
  [a.url,{status:'unavailable',height:1080}],
  [b.url,{status:'verified',height:720,metadata:{formats:[{height:720}]}}]
 ]);
 const options=qualityOptions(b,[a,b],[{height:720,bandwidth:1611176}],proof);
 assert.equal(options.find(x=>x.value==='1080').disabled,true);
 assert.equal(options.find(x=>x.value==='720').disabled,undefined);
 assert.equal(exactQualitySource(b,[a,b],'best',proof).url,b.url);
 assert.equal(exactQualitySource(b,[a,b],'720',proof).url,b.url);
});
test('selecting separately verified 1080p sends that exact signed URL',()=>{
 const a=make(1080),b=make(720);const proof=new Map([
  [a.url,{status:'verified',height:1080,metadata:{formats:[{height:1080}]}}],
  [b.url,{status:'verified',height:720,metadata:{formats:[{height:720}]}}]
 ]);
 const options=qualityOptions(b,[a,b],[{height:720}],proof);
 assert.deepEqual(options.filter(o=>!o.disabled).map(o=>o.value),['best','1080','720']);
 assert.equal(exactQualitySource(b,[a,b],'1080',proof).url,a.url);
 assert.equal(exactQualitySource(b,[a,b],'720',proof).url,b.url);
 assert.equal(exactQualitySource(b,[a,b],'best',proof).url,a.url);
});
test('native manifest height overrides an incorrect 1080p URL label',()=>{
 const a=make(1080),b=make(720);const proof=new Map([
  [a.url,{status:'verified',height:720,metadata:{formats:[{height:720}]}}],
  [b.url,{status:'unavailable',height:720}]
 ]);
 const options=qualityOptions(a,[a,b],[],proof);
 assert(!options.some(o=>o.value==='1080'&&!o.disabled));
 assert.equal(exactQualitySource(a,[a,b],'best',proof).url,a.url);
});
test('a one-variant master still exposes explicit 720p selection',()=>{
 const b=make(720);
 assert.deepEqual(qualityOptions(b,[b],[{height:720}],new Map()).map(o=>o.value),['best','720']);
});
