// No browser/network is opened: verify bundled autoplay logic against synthetic DOMs.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const file = path.join(__dirname, '..', 'N_m3u8DL_RE_GUI', 'Services', 'browser-autoplay.js');
const src = fs.readFileSync(file, 'utf8');

function run(dom) {
  const timers = [];
  const messages = [];
  const sandbox = {
    window: { chrome: { webview: { postMessage(message) { messages.push(message); } } } },
    document: {
      readyState: 'complete',
      documentElement: {},
      querySelectorAll: dom.querySelectorAll,
      querySelector: dom.querySelector,
      addEventListener() { throw Error('Not expected after document complete'); }
    },
    setTimeout(fn) { timers.push(fn); return timers.length; },
    setInterval(fn) { timers.push(fn); return timers.length; },
    clearInterval() {},
    MutationObserver: class {
      constructor(fn) { this.fn = fn; }
      observe() {}
      disconnect() {}
    }
  };
  vm.runInNewContext(src, sandbox, { timeout: 1000 });
  return { ...sandbox, messages, timers };
}

let playCalls = 0;
const video = {
  muted: false, defaultMuted: false, autoplay: false, playsInline: false,
  paused: true, ended: false,
  setAttribute() {},
  play() { playCalls++; this.paused = false; return Promise.resolve(); }
};
const tested = run({
  querySelectorAll: s => s === 'video' ? [video] : [],
  querySelector: () => null
});
assert.equal(playCalls, 1);
assert.equal(video.muted, true);
assert.equal(video.defaultMuted, true);
assert.equal(video.autoplay, true);
assert.equal(video.playsInline, true);
assert.equal(tested.window.__uvdAutoplayInjected, true);
assert.equal(typeof tested.window.__uvdAutoplayKick, 'function');
assert(tested.messages.some(x => x === 'uvd-autoplay:attempt'));

let clickCount = 0;
const overlay = { getClientRects: () => [1], disabled: false, click() { clickCount++; } };
run({
  querySelectorAll: () => [],
  querySelector: s => s === '.vjs-big-play-button' ? overlay : null
});
assert.equal(clickCount, 1);
let iframeAllowed = '';
const frame = {
  getClientRects: () => [1], disabled: false,
  getAttribute(name) { return name === 'src' ? 'https://luluvdo.com/e/abc' : ''; },
  setAttribute(name, value) { if (name === 'allow') iframeAllowed = value; }
};
run({ querySelectorAll: s => s === 'iframe' ? [frame] : [], querySelector: () => null });
assert.equal(iframeAllowed, 'autoplay');

console.log('AUTOPLAY_DOM_TEST=PASS muted video.play + overlay click + iframe allow + bridge telemetry');
