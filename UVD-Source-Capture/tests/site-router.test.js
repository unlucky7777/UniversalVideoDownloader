import {test} from 'node:test';
import assert from 'node:assert/strict';
import {describePage} from '../site-router.js';
import {selectMainMedia} from '../main-media.js';

test('Eporner video pages use the page-special metadata route',()=>{
  const page=describePage('https://www.eporner.com/video-Sx6uzKFU4iy/example/');
  assert.equal(page.kind,'site-adapter');
  assert.equal(page.adapter,'eporner');
  const selected=selectMainMedia([
    {url:'https://cdn.example/only-played-quality.m3u8',kind:'HLS',headers:[],lastSeen:1}
  ],page);
  assert.equal(selected.length,1);
  assert.equal(selected[0].kind,'PAGE');
  assert.equal(selected[0].url,page.url);
});

test('Eporner embed pages use the same special route, unrelated pages do not',()=>{
  const embed=describePage('https://www.eporner.com/embed/Sx6uzKFU4iy/');
  assert.equal(embed.kind,'site-adapter');
  assert.equal(embed.adapter,'eporner');
  assert.equal(describePage('https://www.eporner.com/search/test').kind,'website');
  assert.equal(describePage('https://example.com/video-Sx6uzKFU4iy/').kind,'website');
});
