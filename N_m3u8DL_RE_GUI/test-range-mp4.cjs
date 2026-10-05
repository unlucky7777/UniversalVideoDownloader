const assert=require('node:assert/strict');
const {spawnSync}=require('node:child_process');
const {validateSource,totalFromContentRange}=require('./range-mp4.cjs');
assert.equal(validateSource('https://www.pornvov.net/get_file/10273-2160.mp4').name,'10273-2160.mp4');
for(const u of ['https://example.com/10273-2160.mp4',
 'file:///10273-2160.mp4','https://user:secret@pornvov.net/10273-2160.mp4',
 'https://www.pornvov.net/10273-360.mp4/evil']){
 assert.throws(()=>validateSource(u));
}
assert.equal(totalFromContentRange('bytes 0-1023/3879127643',0,1023),3879127643);
assert.throws(()=>totalFromContentRange('bytes 0-4095/3879127643',0,1023));
console.log('RANGE_UNIT=PASS origin guard, exact range/length');
(async()=>{
const page='https://www.pornvov.net/video/porn-date-with-a-russian-redhead-slut-and-a-healthy-black-man/';
const response=await fetch(page,{headers:{'User-Agent':'Mozilla/5.0'},signal:AbortSignal.timeout(30000)});
const html=await response.text();
const m=/video_alt_url4:\s*'([^']+)'/i.exec(html);
assert(m,'Page no longer supplies an explicit 2160p source');
const src=m[1].replaceAll('&amp;','&');
const {url,name}=validateSource(src);
assert.equal(name,'10273-2160.mp4');
const result=spawnSync(process.execPath,
 [require('node:path').join(__dirname,'range-mp4.cjs'),url.href,__dirname,'6','--probe'],
 {encoding:'utf8',timeout:40000});
assert.equal(result.status,0,
 (result.stderr||'').replace(/https?:\/\/\S+/g,'[redacted]'));
assert.match(result.stdout,/RANGE_PROBE_OK/);
const smoke=spawnSync(process.execPath,
 [require('node:path').join(__dirname,'range-mp4.cjs'),url.href,__dirname,'2','--smoke'],
 {encoding:'utf8',timeout:40000});
assert.equal(smoke.status,0,(smoke.stderr||'').replace(/https?:\/\/\S+/g,'[redacted]'));
assert.match(smoke.stdout,/RANGE_PARALLEL_SMOKE_OK/);
console.log('REAL_SITE_2160_RANGE_PROBE=PASS; PARALLEL_2KB_SMOKE=PASS; no full video downloaded');
})().catch(e=>{console.error('PROBE_FAILED: '+e.message);process.exitCode=1});
