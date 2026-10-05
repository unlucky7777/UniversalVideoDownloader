/*
 * UVD opt-in highest-quality selector.
 * Runs only inside the browser launched for media capture (or user-armed tab).
 * Uses visible, recognized video-player controls; never clicks page-wide text,
 * consent/login, subscription, age-confirmation, advertising or DRM controls.
 */
(() => {
  'use strict';
  if (window.__uvdQualityInjected) {
    window.__uvdQualityKick?.();
    return;
  }
  window.__uvdQualityInjected = true;
  const deadline = Date.now() + 15000;
  let timer = null;
  let observer = null;
  let selected = false;
  let opened = false;
  let lastOpen = 0;
  let pending = false;

  const menuSelectors = [
    '.mgp_qualitiesMenu', '.mgp_qualityMenu', '.mhp1138_qualityMenu',
    '.vjs-menu-content', '.jw-settings-submenu', '.plyr__menu__container',
    '.dplayer-quality-list', '.art-setting-panel',
    '[class*="qualities-menu" i]', '[class*="quality-menu" i]',
    '[class*="qualityMenu"]', '[class*="qualitiesMenu"]'
  ];
  const triggerSelectors = [
    '.mgp_qualityButton', '.mgp_qualityBtn', '.mgp_settingsButton',
    '.mgp_options > .mgp_optionsBtn', '.mgp_selector',
    '.mhp1138_qualityButton', '.mhp1138_settings',
    '.vjs-resolution-button', '.vjs-quality-selector', '.jw-icon-hd',
    '.jw-icon-settings', '[data-plyr="settings"]',
    '.dplayer-setting', '.art-control-setting',
    'button[aria-label*="quality" i]', 'button[title*="quality" i]',
    '[data-testid*="quality" i]'
  ];
  const itemSelectors = [
    'button', 'li', '[role="menuitem"]', '[role="option"]',
    '[data-quality]', '[data-resolution]',
    '.mgp_qualityItem', '.mhp1138_qualityItem', 'div', 'span'
  ].join(',');
  const qualityLabel = /^\s*(?:quality\s*:?\s*)?(2160|1440|1080|720|540|480|360|320|240)\s*p?\s*(?:HD|FHD|UHD|4K|QHD)?\s*$/i;

  function emit(code) {
    try { window.chrome?.webview?.postMessage('uvd-quality:' + code); } catch (_) {}
    try { window.dispatchEvent(new CustomEvent('uvd-quality-observation',{detail:code})); } catch (_) {}
    try {
      if (window.chrome?.runtime?.id)
        window.chrome.runtime.sendMessage({type:'UVD_QUALITY_EVENT',code});
    } catch (_) {}
  }
  function visible(el) {
    if (!el || el.disabled) return false;
    try {
      if (!el.getClientRects?.().length) return false;
      const style = window.getComputedStyle?.(el);
      return !style || (style.display !== 'none' && style.visibility !== 'hidden');
    } catch (_) { return false; }
  }
  function roots(root, depth = 0) {
    if (!root || depth > 2) return [];
    const result = [root];
    try {
      for (const el of root.querySelectorAll('*'))
        if (el.shadowRoot) result.push(...roots(el.shadowRoot, depth + 1));
    } catch (_) {}
    return result;
  }
  function parseQuality(el) {
    const values = [
      el?.getAttribute?.('data-quality'), el?.getAttribute?.('data-resolution'),
      el?.getAttribute?.('aria-label'), el?.textContent
    ];
    for (const value of values) {
      const match = qualityLabel.exec(String(value || '').trim().slice(0, 45));
      if (match) return Number(match[1]);
    }
    return 0;
  }
  function stop() {
    if (timer) clearInterval(timer);
    if (observer) observer.disconnect();
    // Permit an explicit later Scan to retry without a page refresh.
    if (window.__uvdQualityKick === attempt) {
      window.__uvdQualityInjected = false;
      window.__uvdQualityKick = null;
    }
  }
  function attempt() {
    if (selected || Date.now() >= deadline) {
      if (!selected) emit('unavailable');
      stop();
      return;
    }
    if (!document.documentElement) return;
    // Prefer public player APIs over version-fragile CSS menu selectors.
    try {
      const player=typeof window.jwplayer==='function'?window.jwplayer():null;
      if(player && typeof player.getQualityLevels==='function' &&
         typeof player.setCurrentQuality==='function') {
        const levels=player.getQualityLevels();
        if(Array.isArray(levels)&&levels.length) {
          const choices=levels.map((l,i)=>({
            i,h:Number(l.height)||Number(/(2160|1440|1080|720|540|480|360|320|240)/.exec(l.label||'')?.[1])||0
          })).sort((a,b)=>b.h-a.h);
          if(choices[0].h>0){
            player.setCurrentQuality(choices[0].i);
            emit('selected:'+choices[0].h);
            selected=true;stop();return;
          }
        }
      }
    } catch (_) {}
    try {
      const hls=window.hls;
      if(hls && Array.isArray(hls.levels) && hls.levels.length &&
         typeof hls.currentLevel==='number') {
        const choices=hls.levels.map((l,i)=>({i,h:Number(l.height)||0}))
          .sort((a,b)=>b.h-a.h);
        if(choices[0].h>0) {
          hls.currentLevel=choices[0].i;
          emit('selected:'+choices[0].h);
          selected=true;stop();return;
        }
      }
    } catch (_) {}
    let menus = [];
    for (const root of roots(document)) {
      for (const selector of menuSelectors) {
        try { menus.push(...root.querySelectorAll(selector)); } catch (_) {}
      }
    }
    const choices = [];
    for (const menu of menus) {
      if (!visible(menu)) continue;
      try {
        for (const el of menu.querySelectorAll(itemSelectors)) {
          const h = parseQuality(el);
          if (h > 0 && visible(el)) choices.push({el,h});
        }
      } catch (_) {}
    }
    // Some players render options directly inside their player menu rather than
    // a named quality menu. Only consider options inside known player containers.
    if (!choices.length && opened) {
      const containers = document.querySelectorAll(
        '.mgp_container, .mhp1138_player, .jwplayer, .video-js, .plyr, .dplayer, .art-video-player');
      for (const container of containers) {
        for (const el of container.querySelectorAll(itemSelectors)) {
          const h = parseQuality(el);
          if (h > 0 && visible(el)) choices.push({el,h});
        }
      }
    }
    if (choices.length) {
      choices.sort((a,b)=>b.h-a.h);
      const best = choices[0];
      // Report first so the capture layer knows to disregard previously seen
      // default-quality requests when the player's new request arrives.
      // The event is sent before clicking so the capture layer can timestamp
      // the quality switch before any new media network request is emitted.
      try {
        best.el.click();
        selected = true;
        emit('selected:' + best.h); // attempted; actual source still verified later
        stop();
        return;
      } catch (_) { emit('click-failed'); }
    }
    if (Date.now() - lastOpen < 1800) return;
    for (const root of roots(document)) {
      for (const selector of triggerSelectors) {
        const control = root.querySelector?.(selector);
        if (!visible(control)) continue;
        lastOpen = Date.now();
        opened = true;
        try { control.click(); emit('menu-opened'); } catch (_) {}
        return;
      }
    }
  }
  window.__uvdQualityKick = attempt;
  function start() {
    attempt();
    timer = setInterval(attempt, 800);
    observer = new MutationObserver(() => {
      if (pending || selected) return;
      pending = true;
      setTimeout(() => { pending=false; attempt(); }, 250);
    });
    if (document.documentElement)
      observer.observe(document.documentElement, {childList:true,subtree:true});
    setTimeout(stop, 15500);
  }
  if (document.readyState === 'loading')
    document.addEventListener('DOMContentLoaded', start, {once:true});
  else start();
})();
