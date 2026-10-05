import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';
const mod=await import(pathToFileURL('D:\\UniversalVideoDownloader\\release\\UVD-Source-Capture\\site-router.js'));
const d=mod.describePage;
for(const link of [
 'https://www.youtube.com/watch?v=jNQXAC9IVRw',
 'https://m.youtube.com/watch?v=jNQXAC9IVRw',
 'https://music.youtube.com/watch?v=jNQXAC9IVRw',
 'https://www.youtube.com/shorts/jNQXAC9IVRw',
 'https://youtu.be/jNQXAC9IVRw',
 'https://www.youtube.com/live/jNQXAC9IVRw'
]) assert.equal(d(link)?.kind,'youtube',link);
for(const link of [
 'https://vimeo.com/123456789','https://www.dailymotion.com/video/x1234',
 'https://www.tiktok.com/@demo/video/123456789','https://www.reddit.com/r/videos/comments/12345/',
 'https://www.youtube.com/results?search_query=test','https://www.instagram.com/reel/demo/'
]) assert.equal(d(link)?.kind,'website',link);
for(const link of ['file:///tmp/demo.mp4','chrome://extensions','javascript:alert(1)',
 'https://user:pass@youtube.com/watch?v=jNQXAC9IVRw','http://127.0.0.1/private',''])assert.equal(d(link),null);
console.log('SITE_ROUTER=PASS YouTube, Shorts, Vimeo/Dailymotion/TikTok/Reddit generic page + credential rejection');
