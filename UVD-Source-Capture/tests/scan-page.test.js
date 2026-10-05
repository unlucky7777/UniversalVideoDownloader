import { test } from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { scanPageMedia } from '../scan-page.js';

test('right-click scan finds a previously loaded embedded player resource', () => {
  let cookieTouched = false;
  const frame = {
    location:{href:'https://player.school.example.edu/embed/17'},
    navigator:{userAgent:'Chrome/154'},
    document:{
      querySelectorAll(selector) {
        if (selector === 'video,source') return [
          {currentSrc:'blob:https://player.school.example.edu/uuid',src:'',
           getAttribute:()=>''}
        ];
        return [];
      },
      get cookie() { cookieTouched = true; throw Error('must not read cookies'); }
    },
    performance:{
      getEntriesByType:()=>[
        {name:'https://cdn.school.example.edu/hls/master.m3u8?t=abc&expires=1'},
        {name:'https://cdn.school.example.edu/hls/0001.ts'},
        {name:'https://school.example.edu/page.css'}
      ]
    },
    URL
  };
  const result = vm.runInNewContext('(' + scanPageMedia.toString() + ')()',frame);
  assert.equal(result.urls.length,1);
  assert.equal(result.urls[0],'https://cdn.school.example.edu/hls/master.m3u8?t=abc&expires=1');
  assert.equal(result.blobCount,1);
  assert.equal(result.referrer,frame.location.href);
  assert.equal(cookieTouched,false);
});
