
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {observedSource} from '../core.js';
import {requiresDirectMainSource,selectMainMedia} from '../main-media.js';

test('known unsupported embedded-page does not offer a fake yt-dlp PAGE download',()=>{
 const url='https://yako49.com/korea/190892';
 assert.equal(requiresDirectMainSource({url,kind:'website'}),true);
 assert.equal(requiresDirectMainSource({url:'https://yako49.com.evil.example/korea/190892',kind:'website'}),false);
 assert.deepEqual(selectMainMedia([],{url,kind:'website'}),[]);
});
test('confirmed 404/410 playlists are excluded unless a recent native curl proof exists',()=>{
 const url='https://cdn.ddal.pw/a/hls/playlist.m3u8';
 const row=observedSource(url,'',[],4);row.status=404;row.networkVerified=false;
 const page={url:'https://yako49.com/korea/190892',kind:'website'};
 assert.equal(selectMainMedia([row],page).length,0);
 row.status=410;assert.equal(selectMainMedia([row],page).length,0);
 row.curlVerified=true;row.curlVerifiedAt=Date.now();
 assert.equal(selectMainMedia([row],page).length,1);
 row.curlVerifiedAt=Date.now()-180000;
 assert.equal(selectMainMedia([row],page).length,0);
});
