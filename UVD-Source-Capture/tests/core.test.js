import {test} from 'node:test';
import assert from 'node:assert/strict';
import {classify,safeHeaders,score,displaySource,buildCurl,mergeCandidate,capCandidates,observedSource} from '../core.js';

const signed='https://cdn.example.edu/course/master.m3u8?t=abc%2Fxyz&s=1791060272&e=28800&f=42';
test('HLS master preserves signed URL',()=>{
  const result=observedSource(signed,'application/vnd.apple.mpegurl',[],4);
  assert.equal(result.url,signed);assert.equal(result.kind,'HLS');
});
test('HLS path detected with query',()=>assert.equal(classify(signed),'HLS'));
test('DASH path and MIME',()=>{
  assert.equal(classify('https://cdn.test/main.mpd?t=1'),'DASH');
  assert.equal(classify('https://cdn.test/get?id=1','application/dash+xml'),'DASH');
});
test('HLS extensionless MIME',()=>assert.equal(classify('https://cdn.test/source?token=abc','application/x-mpegURL'),'HLS'));
test('MSS manifest',()=>assert.equal(classify('https://cdn.test/path/a.ism/Manifest'),'MSS'));
test('progressive MP4 path',()=>assert.equal(classify('https://cdn.test/video.mp4?secret=1'),'FILE'));
test('progressive MP4 MIME extensionless',()=>assert.equal(classify('https://cdn.test/media?id=4','video/mp4'),'FILE'));
test('MP4 init fragments excluded even if response says video',()=>assert.equal(classify('https://cdn.test/init.mp4','video/mp4'),null));
test('M4S fragments excluded',()=>assert.equal(classify('https://cdn.test/1.m4s','video/mp4'),null));
test('TS fragment excluded',()=>assert.equal(classify('https://cdn.test/00001.ts','video/mp2t'),null));
test('script and image excluded',()=>{
  assert.equal(classify('https://site.test/app.js'),null);
  assert.equal(classify('https://site.test/thumb.jpg'),null);
});
test('blob URL excluded',()=>assert.equal(classify('blob:https://site.test/abcd','video/mp4'),null));
test('not arbitrarily collect HTML',()=>assert.equal(classify('https://school.edu/lecture/123','text/html'),null));
test('advertising URLs excluded',()=>assert.equal(classify('https://googleads.test/ad.mp4'),null));
test('header allowlist excludes browser credentials and transport controls',()=>{
  const headers=safeHeaders([
    {name:'referer',value:'https://school.edu/lecture/123'},
    {name:'Origin',value:'https://school.edu'},
    {name:'User-Agent',value:'Chrome/154'},
    {name:'Cookie',value:'auth=SECRET'},
    {name:'Authorization',value:'Bearer SECRET'},
    {name:'X-CSRF-Token',value:'SECRET'},
    {name:'Host',value:'cdn.test'},
    {name:'Range',value:'bytes=0-1023'},
    {name:'Accept',value:'*/*'},
    {name:'Accept-Language',value:'ko-KR'}
  ]);
  assert.equal(headers.length,5);
  assert(!JSON.stringify(headers).includes('SECRET'));
});
test('CRLF header injection rejected',()=>assert.equal(safeHeaders([{name:'Referer',value:'https://a/\r\nCookie: secret'}]).length,0));
test('duplicate header keys take last',()=>{
  assert.equal(safeHeaders([{name:'REFERER',value:'old'},{name:'referer',value:'new'}])[0].value,'new');
});
test('actual main manifest ranks over MP4 preview',()=>{
  const master=observedSource(signed,'',[],2);
  const clip=observedSource('https://cdn.test/preview.mp4','',[],2);
  assert(score(master)>score(clip));
});
test('display hides query and token',()=>{
  const s=displaySource(observedSource(signed,'',[],2));
  assert(!s.includes('abc%2Fxyz'));assert(!s.includes('s='));
});
test('cURL preserves signed URL and safe headers',()=>{
  const source=observedSource(signed,'',[
    {name:'Referer',value:'https://school.edu/lecture/123'},
    {name:'Origin',value:'https://school.edu'},
    {name:'Cookie',value:'secret'}
  ],2);
  const cmd=buildCurl(source);
  assert(cmd.includes('--url "'+signed+'"'));
  assert(cmd.includes('-H "Referer: https://school.edu/lecture/123"'));
  assert(!cmd.includes('secret'));
});
test('unsafe shell interpolation is not serialized',()=>{
  const source=observedSource('https://cdn.test/master.m3u8?x=$HOME','',[],2);
  assert.throws(()=>buildCurl(source),/Unsafe quoting/);
});
test('duplicate source candidates merge without losing headers',()=>{
  const a=observedSource(signed,'',[{name:'Origin',value:'https://school.edu'}],2,100);
  const b=observedSource(signed,'',[{name:'Referer',value:'https://school.edu/1'}],2,200);
  const c=mergeCandidate(a,b);
  assert.equal(c.headers.length,2);assert.equal(c.firstSeen,100);assert.equal(c.lastSeen,200);
});
test('candidate list bounded by cap',()=>{
  const now=Date.now();
  const rows=Array.from({length:88},(_,i)=>observedSource('https://cdn.test/'+i+'.mp4','',[],2,now+i));
  assert.equal(capCandidates(rows).length,65);
});
test('stale candidates filtered',()=>{
  const stale=observedSource(signed,'',[],2,Date.now()-7200001);
  assert.equal(capCandidates([stale]).length,0);
});

test('network verified stream outranks stale player definition',()=>{
  const config=observedSource('https://cdn.test/1080P/master.m3u8','',[],2,100);
  config.definitionSource=true;config.declaredHeight=1080;config.qualityEvidence='player-definition';
  const live=observedSource('https://cdn.test/current/master.m3u8','',[],2,200);
  live.networkVerified=true;live.status=200;live.verifiedAt=200;
  assert(score(live)>score(config));
  const gone=mergeCandidate(config,{...config,status:410,lastSeen:300});
  assert.equal(gone.status,410);
  assert(score(live)>score(gone));
});
