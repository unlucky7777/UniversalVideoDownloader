import {test} from 'node:test';
import assert from 'node:assert/strict';
import {pageMediaHeaders,trustedMediaHeaders} from '../transport-headers.js';
import {browserDiscoveryConfig} from '../site-adapters.js';

const byName=(rows,name)=>rows.find(x=>x.name.toLowerCase()===name.toLowerCase())?.value;

test('Pornhub browser adapter uses canonical root Origin and Referer',()=>{
  const cfg=browserDiscoveryConfig(
    'https://www.pornhub.com/view_video.php?viewkey=6a5fc3e04cd06');
  assert.equal(cfg.adapterId,'pornhub');
  assert.equal(cfg.transportHeaders.origin,'https://www.pornhub.com');
  assert.equal(cfg.transportHeaders.referer,'https://www.pornhub.com/');
  const headers=pageMediaHeaders(
    'https://www.pornhub.com/view_video.php?viewkey=6a5fc3e04cd06',
    'Chrome/154','ko-KR',cfg.transportHeaders);
  assert.equal(byName(headers,'Origin'),'https://www.pornhub.com');
  assert.equal(byName(headers,'Referer'),'https://www.pornhub.com/');
  assert.equal(byName(headers,'Accept'),'*/*');
  assert.equal(byName(headers,'Accept-Language'),'ko-KR');
});

test('API rendition inherits most recent proven browser transport headers',()=>{
  const source={
    url:'https://cdn.example/1080/master.m3u8',
    mainGroup:'video-1',
    headers:[
      {name:'Referer',value:'https://site.example/watch/1'},
      {name:'Origin',value:'https://site.example'}
    ]
  };
  const current={
    url:'https://cdn.example/240/master.m3u8',
    mainGroup:'video-1',
    networkVerified:true,
    verifiedAt:200,
    headers:[
      {name:'Referer',value:'https://site.example/'},
      {name:'Origin',value:'https://site.example'},
      {name:'Accept',value:'*/*'},
      {name:'Accept-Language',value:'ko-KR'},
      {name:'User-Agent',value:'Browser UA'}
    ]
  };
  const headers=trustedMediaHeaders(source,[source,current]);
  assert.equal(byName(headers,'Referer'),'https://site.example/');
  assert.equal(byName(headers,'Origin'),'https://site.example');
  assert.equal(byName(headers,'Accept'),'*/*');
  assert.equal(byName(headers,'Accept-Language'),'ko-KR');
  assert.equal(byName(headers,'User-Agent'),'Browser UA');
});

test('transport inheritance never copies credential headers',()=>{
  const source={url:'https://cdn.example/a.m3u8',mainGroup:'g',headers:[]};
  const sibling={url:'https://cdn.example/b.m3u8',mainGroup:'g',networkVerified:true,
    verifiedAt:1,headers:[
      {name:'Cookie',value:'secret=1'},
      {name:'Authorization',value:'Bearer secret'},
      {name:'Origin',value:'https://site.example'}
    ]};
  const headers=trustedMediaHeaders(source,[source,sibling]);
  assert.equal(headers.some(x=>/cookie|authorization/i.test(x.name)),false);
  assert.equal(byName(headers,'Origin'),'https://site.example');
});
