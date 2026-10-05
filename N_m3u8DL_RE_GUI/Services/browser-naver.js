/*
 * Naver SmartEditor multi-video discovery. Runs only inside a user-opened
 * Naver page/frame; it reads published video module attributes, not cookies.
 * The temporary inkey stays in WebView2/native memory and is never logged.
 */
(() => {
  'use strict';
  if (!/^(?:cafe|m\.cafe|blog|m\.blog)\.naver\.com$/i.test(location.hostname)) return;
  if (window.__uvdNaverInstalled) { window.__uvdNaverScan?.(); return; }
  window.__uvdNaverInstalled = true;
  const seen = new Set();
  let pending = false;
  let delivered = 0;
  function extract(value, depth = 0) {
    if (!value || typeof value !== 'object' || depth > 3) return null;
    const data = value.data && typeof value.data === 'object' ? value.data : value;
    const vid = data.vid || data.videoId;
    const key = data.inkey || data.inKey || data.key;
    if (typeof vid === 'string' && typeof key === 'string' &&
        /^[A-Za-z0-9_-]{8,96}$/.test(vid) && /^[A-Za-z0-9_-]{8,512}$/.test(key))
      return { vid, key };
    for (const k of ['data', 'module', 'video', 'content']) {
      if (value[k] && value[k] !== value) {
        const found = extract(value[k], depth + 1);
        if (found) return found;
      }
    }
    return null;
  }
  function scan() {
    if (!document.documentElement || delivered >= 10) return;
    const nodes = document.querySelectorAll('[data-module]');
    for (const node of nodes) {
      if (delivered >= 10) break;
      const raw = node.getAttribute('data-module') || '';
      if (raw.length < 15 || raw.length > 8192 ||
          !/(?:vid|videoId)/.test(raw) || !/(?:inkey|inKey)/.test(raw)) continue;
      try {
        const found = extract(JSON.parse(raw));
        if (!found) continue;
        const id = found.vid + ':' + found.key;
        if (seen.has(id)) continue;
        seen.add(id); delivered++;
        window.chrome?.webview?.postMessage('uvd-naver-video:' + JSON.stringify(found));
      } catch (_) {}
    }
  }
  window.__uvdNaverScan = scan;
  const start = () => {
    scan();
    const observer = new MutationObserver(() => {
      if (pending) return;
      pending = true;
      setTimeout(() => { pending = false; scan(); }, 250);
    });
    observer.observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-module'] });
    setTimeout(() => observer.disconnect(), 120000);
  };
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, { once: true });
  else start();
})();
