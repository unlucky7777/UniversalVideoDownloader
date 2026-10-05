/*
 * Embedded browser autoplay (explicit GO / Auto detect only).
 * Designed for video embedded inside dynamically created cross-origin iframes.
 * WebView2 executes this script in the top document and its child frames.
 * No login/CAPTCHA/age-confirmation/ad overlays are clicked.
 */
(() => {
  'use strict';
  if (window.__uvdAutoplayInjected) {
    window.__uvdAutoplayKick?.();
    return;
  }
  window.__uvdAutoplayInjected = true;

  let deadline = Date.now() + 45000;
  const clicked = new WeakMap();
  const lastPlayedAt = new WeakMap();
  const playControls = [
    '.vjs-big-play-button', '.video-js .vjs-play-control',
    '.plyr__control--overlaid', '[data-plyr="play"]',
    '.jw-display-icon-container', '.jw-icon-display', '.jw-icon-playback',
    '.dplayer-play-icon', '.dplayer-play-btn',
    '.art-video-player .art-control-play',
    '.xgplayer-start', '.xgplayer-play', '.fp-play',
    '.clappr-player .play-wrapper',
    'button[aria-label="Play"]', 'button[title="Play"]'
  ];
  const tryPlayerIframe = /(?:^|\/)(?:e|embed|player|video)(?:\/|[?#]|$)|luluvdo|lulu|dood|stream/i;
  let timer, observer;
  let pending = false;
  let started = false;
  let played = false;
  let blockedReported = false;
  let noVideoReported = false;
  let lastSdkAttempt = 0;

  function feedback(code) {
    try {
      window.chrome?.webview?.postMessage('uvd-autoplay:' + code);
    } catch (_) {}
  }
  function stop() {
    if (timer) clearInterval(timer);
    if (observer) observer.disconnect();
    timer = null; observer = null; started = false;
  }
  function playVideo(video) {
    if (!video || video.ended) return;
    try {
      video.muted = true;
      video.defaultMuted = true;
      video.autoplay = true;
      video.playsInline = true;
      video.setAttribute('muted', '');
      video.setAttribute('playsinline', '');
      video.setAttribute('autoplay', '');
      if (!video.paused) {
        if (!played) { played = true; feedback('playing'); }
        return;
      }
      const now = Date.now();
      if (now - (lastPlayedAt.get(video) || 0) < 2000) return;
      lastPlayedAt.set(video, now);
      feedback('attempt');
      const result = video.play();
      if (result && typeof result.then === 'function') {
        result.then(() => {
          if (!played && !video.paused) {
            played = true; feedback('playing');
          }
        }).catch(error => {
          if (!blockedReported) {
            blockedReported = true;
            const category = String(error?.name || 'unknown').replace(/[^A-Za-z]/g, '').slice(0, 30);
            feedback(category === 'NotAllowedError' ? 'blocked' : 'play-failed:' + category);
          }
        });
      }
    } catch (_) {}
  }
  function visible(node) {
    try { return !!node?.getClientRects()?.length && !node.disabled; }
    catch (_) { return false; }
  }
  function allowVideoIframes(root) {
    for (const frame of root.querySelectorAll('iframe')) {
      try {
        const src = frame.getAttribute('src') || '';
        if (!src || !tryPlayerIframe.test(src) || !visible(frame)) continue;
        const permission = frame.getAttribute('allow') || '';
        if (!/(?:^|;)\s*autoplay(?:\s*;|$)/i.test(permission))
          frame.setAttribute('allow', (permission ? permission + '; ' : '') + 'autoplay');
      } catch (_) {}
    }
  }
  function collectRoots(root, depth = 0) {
    if (!root || depth > 2) return [];
    const roots = [root];
    try {
      for (const element of root.querySelectorAll('*')) {
        if (element.shadowRoot) roots.push(...collectRoots(element.shadowRoot, depth + 1));
      }
    } catch (_) {}
    return roots;
  }
  function attempt() {
    if (Date.now() >= deadline) { stop(); return; }
    if (!document.documentElement) return;
    const roots = collectRoots(document);
    let seenVideos = 0;
    for (const root of roots) {
      allowVideoIframes(root);
      for (const video of root.querySelectorAll('video')) {
        seenVideos++;
        playVideo(video);
      }
    }
    if (played && seenVideos > 0) return;
    if (!noVideoReported && Date.now() > deadline - 35000 && seenVideos === 0) {
      noVideoReported = true; feedback('no-video');
    }

    // Some players expose HTML5 video only after their own play overlay is used.
    // Do not touch generic links, permissions, logins or age verification widgets.
    for (const root of roots) {
      for (const selector of playControls) {
        const control = root.querySelector(selector);
        if (!control || !visible(control)) continue;
        const record = clicked.get(control) || { count: 0, last: 0 };
        if (record.count >= 3 || Date.now() - record.last < 4000) continue;
        clicked.set(control, { count: record.count + 1, last: Date.now() });
        feedback('player-control');
        try { control.click(); } catch (_) {}
        return;
      }
    }
    try {
      // The recognized JW player has an API even when it wraps or replaces video.
      if (typeof window.jwplayer === 'function' && seenVideos === 0 &&
          Date.now() - lastSdkAttempt > 4000) {
        lastSdkAttempt = Date.now();
        const player = window.jwplayer();
        if (player && typeof player.play === 'function') {
          player.setMute?.(true);
          player.play(true);
        }
      }
    } catch (_) {}
  }

  window.__uvdAutoplayKick = attempt;
  window.__uvdAutoplayReset = () => {
    deadline = Date.now() + 45000;
    played = false;
    blockedReported = false;
    noVideoReported = false;
    start();
    attempt();
  };
  function start() {
    if (started) return;
    started = true;
    feedback('ready');
    attempt();
    timer = setInterval(attempt, 1300);
    observer = new MutationObserver(() => {
      if (pending) return;
      pending = true;
      setTimeout(() => { pending = false; attempt(); }, 300);
    });
    observer.observe(document.documentElement, { childList: true, subtree: true });
    setTimeout(() => { if (Date.now() >= deadline) stop(); }, 46000);
  }

  if (document.readyState === 'loading')
    document.addEventListener('DOMContentLoaded', start, { once: true });
  else start();
})();
