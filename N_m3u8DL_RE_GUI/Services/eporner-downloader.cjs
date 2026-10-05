'use strict';
// Direct Eporner MP4 transport for networks resetting TLS with SNI.
// TLS chain AND expected-hostname validation always remain enabled.
// Usage: node eporner-downloader.cjs <page-url> <output-dir> <max-height|0> <threads> [--probe|--smoke] [--name <save-name>]
const https = require('node:https');
const dns = require('node:dns').promises;
const tls = require('node:tls');
const fs = require('node:fs/promises');
const path = require('node:path');
const HOST = 'www.eporner.com';
const UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/154.0.0.0 Safari/537.36';
const CHUNK_SIZE = 8 * 1024 * 1024;
const [pageUrl, outDir, heightArg, threadArg, ...extraArgs] = process.argv.slice(2);
const flag = extraArgs.find(v => v === '--probe' || v === '--smoke') || '';
const nameIndex = extraArgs.indexOf('--name');
const requestedName = nameIndex >= 0 ? String(extraArgs[nameIndex + 1] || '') : '';
const idMatch = /^https:\/\/(?:www\.)?eporner\.com\/(?:embed\/|video-)([a-zA-Z0-9]+)(?:[/?#]|$)/.exec(pageUrl || '');
if (!idMatch) { console.error('[error] Invalid Eporner URL'); process.exit(2); }
const videoId = idMatch[1];
const referer = 'https://' + HOST + '/embed/' + videoId + '/';

function decodeText(value) {
  return String(value || '')
    .replace(/&amp;/gi, '&').replace(/&quot;/gi, '"')
    .replace(/&#39;|&apos;/gi, "'").replace(/&lt;/gi, '<').replace(/&gt;/gi, '>')
    .replace(/\s+/g, ' ').trim();
}
function safeBaseName(value) {
  let name = decodeText(value).replace(/[<>:"/\\|?*\x00-\x1f]/g, '_').trim().replace(/[ .]+$/g, '');
  if (/\.mp4$/i.test(name)) name = name.slice(0, -4).trim();
  if (name.length > 180) name = name.slice(0, 180).trim();
  return name || ('video-' + videoId);
}

function checkHost(name) {
  if (name !== HOST && !/^[a-z0-9-]+(?:\.[a-z0-9-]+)*\.eporner\.com$/i.test(name))
    throw new Error('Unexpected media hostname');
}
async function get(url, headers = {}) {
  const target = new URL(url);
  if (target.protocol !== 'https:' || (target.port && target.port !== '443'))
    throw new Error('HTTPS is required');
  checkHost(target.hostname);
  const ip = (await dns.lookup(target.hostname, {family: 4})).address;
  return new Promise((resolve, reject) => {
    const request = https.get({
      hostname: ip, port: 443, servername: '', rejectUnauthorized: true, agent: false,
      checkServerIdentity: (_name, cert) => tls.checkServerIdentity(target.hostname, cert),
      path: target.pathname + target.search, timeout: 20000,
      headers: Object.assign({
        Host: target.hostname, 'User-Agent': UA, Referer: referer,
        'Accept-Encoding': 'identity'
      }, headers)
    }, res => {
      if (!res.socket.authorized) {
        res.destroy(); return reject(new Error('Untrusted TLS certificate chain'));
      }
      const error = tls.checkServerIdentity(target.hostname, res.socket.getPeerCertificate());
      if (error) { res.destroy(); return reject(error); }
      resolve(res);
    });
    request.on('timeout', () => request.destroy(new Error('Network timeout')));
    request.on('error', reject);
  });
}
async function readText(url) {
  const res = await get(url);
  if (res.statusCode !== 200) {
    res.destroy(); throw new Error('Metadata HTTP ' + res.statusCode);
  }
  const chunks = []; let bytes = 0;
  for await (const c of res) {
    bytes += c.length;
    if (bytes > 3 * 1024 * 1024) { res.destroy(); throw new Error('Metadata size limit'); }
    chunks.push(c);
  }
  return Buffer.concat(chunks).toString('utf8');
}
async function metadata(maxHeight) {
  const page = await readText(referer);
  const match = page.match(/hash\s*[:=]\s*["']([\da-f]{32})/i);
  if (!match) throw new Error('Video hash not found');
  const hash = Array.from({length: 4},
    (_, i) => parseInt(match[1].slice(i * 8, i * 8 + 8), 16).toString(36)).join('');
  const params = new URLSearchParams({
    hash: hash, device: 'generic', domain: HOST, fallback: 'false'
  });
  const meta = JSON.parse(await readText('https://' + HOST + '/xhr/video/' +
    videoId + '?' + params.toString()));
  if (meta.available !== true) throw new Error('No available video returned by API');
  const options = Object.entries(meta.sources && meta.sources.mp4 || {})
    .filter(([, v]) => v && typeof v.src === 'string')
    .map(([label, v]) => ({ height: parseInt(label, 10), url: v.src }))
    .filter(v => Number.isFinite(v.height) && v.height > 0)
    .sort((a, b) => b.height - a.height);
  const selected = options.find(v => maxHeight === 0 || v.height <= maxHeight);
  if (!selected) throw new Error('No MP4 format at or below selected resolution');
  const url = new URL(selected.url);
  if (url.protocol !== 'https:') throw new Error('Media is not HTTPS');
  checkHost(url.hostname);
  const ogTitle = page.match(/<meta[^>]+property=["']og:title["'][^>]+content=["']([^"']+)/i) ||
                  page.match(/<meta[^>]+content=["']([^"']+)["'][^>]+property=["']og:title["']/i);
  const htmlTitle = page.match(/<title[^>]*>([^<]+)<\/title>/i);
  const title = decodeText(meta.title || meta.name || (ogTitle && ogTitle[1]) || (htmlTitle && htmlTitle[1]) || '');
  const durationValue = Number(meta.duration || meta.length || meta.video_duration || 0);
  const duration = Number.isFinite(durationValue) && durationValue > 0 ? durationValue : null;
  return { selected, options, title, duration };
}
async function totalLength(url) {
  const res = await get(url, {Range: 'bytes=0-0'});
  const match = String(res.headers['content-range'] || '').match(/^bytes 0-0\/(\d+)$/);
  if (res.statusCode !== 206 || !match) {
    res.destroy(); throw new Error('Media server does not support verified ranges');
  }
  for await (const _ of res) { /* exactly one byte */ }
  const n = Number(match[1]);
  if (!Number.isSafeInteger(n) || n < 1) throw new Error('Invalid media length');
  return n;
}
async function fetchRange(url, from, to, handle) {
  const res = await get(url, {Range: 'bytes=' + from + '-' + to});
  const expected = 'bytes ' + from + '-' + to + '/';
  if (res.statusCode !== 206 ||
      !String(res.headers['content-range'] || '').startsWith(expected)) {
    res.destroy(); throw new Error('Unexpected range response HTTP ' + res.statusCode);
  }
  let position = from;
  for await (const buffer of res) {
    let offset = 0;
    while (offset < buffer.length) {
      const written = (await handle.write(buffer, offset,
        buffer.length - offset, position)).bytesWritten;
      if (!written) throw new Error('File write returned zero bytes');
      position += written; offset += written;
    }
    if (position > to + 1) throw new Error('Oversized response');
  }
  if (position !== to + 1) throw new Error('Incomplete media range');
}
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
(async () => {
  const height = Math.max(0, Math.min(2160, Number(heightArg) || 0));
  const threadCount = Math.min(256, Math.max(1, Math.trunc(Number(threadArg) || 128)));
  console.log('[config] concurrent range requests: ' + threadCount);
  const info = await metadata(height);
  const selected = info.selected;
  const total = await totalLength(selected.url);
  console.log('[media] ' + selected.height + 'p, ' +
    (total / 1048576).toFixed(1) + ' MiB; certificate and range verified');
  console.log('[size] ' + total);
  console.log('[probe-json] ' + JSON.stringify({
    title: info.title || null,
    duration: info.duration,
    heights: info.options.map(v => v.height),
    selectedHeight: selected.height,
    bytes: total,
    ext: 'mp4'
  }));
  if (flag === '--probe') { console.log('PROBE_OK'); return; }
  if (flag === '--smoke') {
    if (!outDir || !path.isAbsolute(outDir)) throw new Error('Smoke output path required');
    await fs.mkdir(outDir, {recursive: true});
    const sample = path.join(outDir, '.eporner-range-smoke.part');
    const sampleFile = await fs.open(sample, 'w+');
    try {
      await sampleFile.truncate(2048);
      await Promise.all([
        fetchRange(selected.url, 0, 1023, sampleFile),
        fetchRange(selected.url, 1024, 2047, sampleFile)
      ]);
      await sampleFile.sync();
      if ((await sampleFile.stat()).size !== 2048) throw new Error('Wrong smoke output length');
    } finally {
      await sampleFile.close();
      await fs.rm(sample, {force:true});
    }
    console.log('SMOKE_OK (two parallel verified media ranges)');
    return;
  }
  if (!outDir || !path.isAbsolute(outDir))
    throw new Error('Absolute output directory required');
  await fs.mkdir(outDir, {recursive: true});
  const baseName = requestedName ? safeBaseName(requestedName) :
    ('eporner-' + videoId + '-' + selected.height + 'p');
  const target = path.join(outDir, baseName + '.mp4');
  const temp = target + '.part';
  const stateFile = target + '.resume.json';
  const count = Math.ceil(total / CHUNK_SIZE);
  let completed = new Set(); let resume = false;
  try {
    const state = JSON.parse(await fs.readFile(stateFile, 'utf8'));
    const stat = await fs.stat(temp);
    if (state.id === videoId && state.height === selected.height &&
        state.total === total && state.chunk === CHUNK_SIZE &&
        stat.size === total && Array.isArray(state.completed)) {
      completed = new Set(state.completed.filter(i =>
        Number.isInteger(i) && i >= 0 && i < count));
      resume = true;
    }
  } catch { /* no compatible resume file */ }
  const handle = await fs.open(temp, resume ? 'r+' : 'w+');
  try {
    if (!resume) await handle.truncate(total);
    let next = 0, fatal = null, stateQueue = Promise.resolve();
    async function record(index) {
      completed.add(index);
      stateQueue = stateQueue.then(async () => {
        await fs.writeFile(stateFile + '.tmp', JSON.stringify({
          id: videoId, height: selected.height, total: total,
          chunk: CHUNK_SIZE, completed: [...completed].sort((a,b) => a-b)
        }));
        await fs.rename(stateFile + '.tmp', stateFile);
      });
      await stateQueue;
      const bytes = [...completed].reduce((n, i) =>
        n + Math.min(CHUNK_SIZE, total - CHUNK_SIZE * i), 0);
      console.log('[download] ' + (100 * bytes / total).toFixed(1) +
        '% (' + completed.size + '/' + count + ')');
    }
    async function worker() {
      while (!fatal) {
        const index = next++;
        if (index >= count) return;
        if (completed.has(index)) continue;
        const from = index * CHUNK_SIZE;
        const to = Math.min(total - 1, from + CHUNK_SIZE - 1);
        let ok = false;
        for (let attempt = 0; attempt < 3 && !ok; attempt++) {
          try {
            await fetchRange(selected.url, from, to, handle);
            await record(index);
            ok = true;
          } catch (error) {
            if (attempt === 2) fatal = error;
            else await sleep(800 * (attempt + 1));
          }
        }
      }
    }
    console.log('[media] ' + threadCount + ' concurrent range requests');
    await Promise.all(Array.from({length: threadCount}, worker));
    await stateQueue;
    if (fatal) throw fatal;
    if (completed.size !== count) throw new Error('Incomplete download');
    await handle.sync();
  } finally { await handle.close(); }
  await fs.rename(temp, target);
  await fs.rm(stateFile, {force: true});
  console.log('[download] 100.0%');
  console.log('[media] Saved ' + path.basename(target));
})().catch(e => { console.error('[error] ' + e.message); process.exitCode = 1; });
