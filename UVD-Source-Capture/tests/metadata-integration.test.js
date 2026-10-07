import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';

const popup=await readFile(new URL('../popup.js',import.meta.url),'utf8');
const background=await readFile(new URL('../background.js',import.meta.url),'utf8');

test('candidate rows render each candidate own URL instead of current selection URL',()=>{
  assert.match(popup,/displaySource\(source\)/);
  assert.doesNotMatch(popup,/selected\(\)\s*\|\|\s*source/);
});

test('direct and manifest sources use native generic metadata fallback',()=>{
  assert.match(popup,/NATIVE_MEDIA_METADATA/);
  assert.match(popup,/videoCodec:native\.videoCodec/);
  assert.match(popup,/audioCodec:native\.audioCodec/);
  assert.match(popup,/duration:native\.duration/);
  assert.match(background,/type:'MEDIA_METADATA'/);
  assert.match(background,/\['FILE','HLS','DASH','MSS'\]/);
});
