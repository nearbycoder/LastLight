#!/usr/bin/env node
// Checks the browser build on phones and tablets: headless WebKit as an iPhone or an iPad, headless Chromium as an
// Android phone, with real (trusted) touch events, and desktop Chromium and Firefox to see that the touch controls
// stay hidden there.
//
//   node Tools/check-mobile.mjs --serve                                   Builds/Pages served at /LastLight/, the default devices
//   node Tools/check-mobile.mjs --serve --play --device=iphone,ipad,android
//   node Tools/check-mobile.mjs --serve --play --device=desktop-chromium,desktop-firefox
//   node Tools/check-mobile.mjs https://nearbycoder.github.io/LastLight/  the live site
//
// Devices: iphone (iPhone 15, landscape), iphone-portrait, iphone-se (the smallest: 568x320), ipad (iPad Pro 11,
// landscape), ipad-portrait, android (Pixel 7, landscape), android-portrait, desktop-chromium, desktop-firefox.
// Options: --serve[=port] (default 8657, 127.0.0.1 only; the server stops with the script), --device=a,b (default
// iphone,ipad,android), --play (play a short night by touch, or by mouse on a desktop: on a touch device, night V from a
// seeded save, for the foghorn), --rotate (with --play: turn to portrait mid-night), --chart[=seconds] (a fresh night I
// played out unkept, at most that long, default 420, then the dawn chart's timeline by touch), --stopped (a visit that
// stopped while showing: the next one says so and starts lighter), --timeout=seconds to reach the title (default 300),
// --out=dir (default Logs/mobile-check), --notch (emulate an iPhone's safe area: the notch and home indicator). --real-sound keeps WebKit's own sound (it crashes this headless WebKit).
//
// For every device it records, in <out>/<device>.json and .log: whether the title came up, the download, the memory
// (the wasm heap; textures, buffers and render buffers WebGL was given, estimated from their sizes and formats; audio
// decoded by Web Audio; and the resident memory of the browser's processes, sampled twice a second, the largest of them
// being the page's content process that a phone's per-tab limit applies to), the frame rate of the night, and the touch
// controls: whether they show, and whether the beam turns, focuses and the horn sounds through them. Screenshots go to
// <out>/<device>-*.png. Headless WebKit on Linux doesn't enforce iOS's memory limit; the numbers are for comparing.
//
// Touches are the browser's own (trusted, like a finger's): Chromium's Input.dispatchTouchEvent over CDP, and WebKit's
// Input.dispatchTouchEvent through Playwright's in-process connection. Exits 0 only when every check passed.
//
// Needs playwright-core 1.63 (PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core) and its WebKit (WEBKIT_PATH,
// default ~/.cache/webkit-libs/webkit-2359/pw_run.sh); Chromium and Firefox as in Tools/check-pages.mjs.
import { createRequire } from "node:module";
import { existsSync, mkdirSync, readdirSync, readFileSync, statSync, writeFileSync, mkdtempSync, rmSync } from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";

const require = createRequire(import.meta.url);
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..");

// ------------------------------------------------------------------ options
const opts = { devices: ["iphone", "ipad", "android"], play: false, chart: false, chartWait: 420, rotate: false, stopped: false, realSound: false, serve: 0, timeout: 300, out: path.join(root, "Logs", "mobile-check"), url: null };
for (const a of process.argv.slice(2)) {
  const [k, v] = a.split(/=(.*)/s);
  if (k === "--serve") opts.serve = v ? Number(v) : 8657;
  else if (k === "--device") opts.devices = v.split(",").filter(Boolean);
  else if (k === "--play") opts.play = true;
  else if (k === "--real-sound") opts.realSound = true;
  else if (k === "--chart") { opts.chart = true; if (v) opts.chartWait = Number(v); }
  else if (k === "--rotate") opts.rotate = true;
  else if (k === "--stopped") opts.stopped = true;
  else if (k === "--notch") opts.notch = true;
  else if (k === "--timeout") opts.timeout = Number(v);
  else if (k === "--out") opts.out = path.resolve(v);
  else if (k === "--help" || k === "-h") { console.log(readFileSync(new URL(import.meta.url), "utf8").split("\nimport")[0]); process.exit(0); }
  else if (!k.startsWith("--")) opts.url = a;
  else { console.error(`unknown option ${a}`); process.exit(2); }
}
if (!opts.url && !opts.serve) { console.error("usage: node Tools/check-mobile.mjs <url> | --serve [--play] [--device=iphone,ipad,android]"); process.exit(2); }
mkdirSync(opts.out, { recursive: true });

let pw;
try { pw = require(process.env.PLAYWRIGHT_CORE || "playwright-core"); }
catch (e) { console.error("[mobile] playwright-core not found: set PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core"); process.exit(2); }

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const MB = (b) => +(b / 1048576).toFixed(1);

const devices = {
  "iphone": { engine: "webkit", profile: "iPhone 15 landscape", touch: true },
  "iphone-portrait": { engine: "webkit", profile: "iPhone 15", touch: true },
  "iphone-se": { engine: "webkit", profile: "iPhone SE landscape", touch: true },
  "ipad": { engine: "webkit", profile: "iPad Pro 11 landscape", touch: true },
  "ipad-portrait": { engine: "webkit", profile: "iPad Pro 11", touch: true },
  "android": { engine: "chromium", profile: "Pixel 7 landscape", touch: true },
  "android-portrait": { engine: "chromium", profile: "Pixel 7", touch: true },
  "desktop-chromium": { engine: "chromium", viewport: { width: 1600, height: 900 }, touch: false },
  "desktop-firefox": { engine: "firefox", viewport: { width: 1600, height: 900 }, touch: false },
};

// ------------------------------------------------------------------ a static server like GitHub Pages (as check-pages)
const types = { ".html": "text/html; charset=utf-8", ".js": "application/javascript", ".wasm": "application/wasm",
  ".jpg": "image/jpeg", ".png": "image/png", ".css": "text/css", ".json": "application/json" };
function serve(port) {
  const dir = path.join(root, "Builds", "Pages");
  if (!existsSync(path.join(dir, "LastLight", "index.html"))) { console.error(`[mobile] no build in ${dir}/LastLight: run Tools/build-pages.sh`); process.exit(2); }
  const server = http.createServer((req, res) => {
    let p = decodeURIComponent(new URL(req.url, "http://x").pathname);
    if (p === "/LastLight") { res.writeHead(301, { Location: "/LastLight/" }); return res.end(); }
    if (p.endsWith("/")) p += "index.html";
    const file = path.join(dir, path.normalize(p));
    if (!file.startsWith(dir + path.sep) || !existsSync(file) || !statSync(file).isFile()) { res.writeHead(404); return res.end("not found"); }
    const body = readFileSync(file);
    res.writeHead(200, { "Content-Type": types[path.extname(file)] || "application/octet-stream", "Content-Length": body.length, "Cache-Control": "max-age=600" });
    res.end(body);
  });
  return new Promise((resolve, reject) => { server.on("error", reject); server.listen(port, "127.0.0.1", () => resolve(server)); });
}

// ------------------------------------------------------------------ in-page probes (before the page's scripts)
const probes = () => {
  // The wasm heap: the memory the module is given or exports, read as it grows.
  const mems = [];
  const NativeMemory = WebAssembly.Memory;
  WebAssembly.Memory = function (d) { const m = new NativeMemory(d); mems.push(m); return m; };
  WebAssembly.Memory.prototype = NativeMemory.prototype;
  const grab = (r) => {
    const inst = r && (r.instance || r);
    try { for (const v of Object.values(inst.exports || {})) if (v instanceof NativeMemory && !mems.includes(v)) mems.push(v); } catch (e) { }
    return r;
  };
  for (const k of ["instantiate", "instantiateStreaming"]) {
    const f = WebAssembly[k];
    if (f) WebAssembly[k] = function (...a) { return f.apply(this, a).then(grab); };
  }
  let heapPeak = 0;
  const heap = () => { let n = 0; for (const m of mems) n += m.buffer.byteLength; heapPeak = Math.max(heapPeak, n); return n; };

  // What WebGL was given: textures, buffers and render buffers, by their sizes and formats (an estimate: drivers pad).
  const bpp = { 0x8058: 4, 0x8051: 3, 0x1908: 4, 0x1907: 3, 0x8229: 1, 0x822B: 2, 0x1909: 1, 0x190A: 2, 0x1906: 1, 0x881A: 8, 0x8814: 16,
    0x822D: 2, 0x822F: 4, 0x822E: 4, 0x8230: 8, 0x8C3A: 4, 0x8C43: 4, 0x8059: 4, 0x88F0: 4, 0x81A6: 4, 0x81A5: 2, 0x8CAC: 4, 0x8CAD: 8,
    0x8D48: 1, 0x81A7: 4, 0x8C3D: 4, 0x8815: 12, 0x881B: 6, 0x8D62: 2, 0x8056: 2, 0x8057: 2 };
  const typeBytes = { 0x1401: 1, 0x140B: 2, 0x8D61: 2, 0x1406: 4, 0x1405: 4, 0x8033: 2, 0x8034: 2, 0x8363: 2 };
  const compsOf = { 0x1908: 4, 0x1907: 3, 0x1909: 1, 0x190A: 2, 0x1906: 1, 0x1903: 1, 0x8227: 2 };
  const gl = { tex: new Map(), buf: new Map(), rb: new Map(), peak: 0 };
  const bound = { tex: new Map(), buf: new Map(), rb: null, unit: 0 };
  const total = () => { let n = 0; for (const m of gl.tex.values()) for (const v of m.values()) n += v; for (const v of gl.buf.values()) n += v; for (const v of gl.rb.values()) n += v; return n; };
  const note = () => { gl.peak = Math.max(gl.peak, total()); };
  const P = window.WebGL2RenderingContext && WebGL2RenderingContext.prototype;
  if (P) {
    const wrap = (name, after) => { const f = P[name]; P[name] = function (...a) { const r = f.apply(this, a); try { after.call(this, a, r); } catch (e) { } return r; }; };
    const texTarget = (t) => (t >= 0x8515 && t <= 0x851A) ? 0x8513 : t;   // cube faces belong to the cube map
    const texOf = (t) => bound.tex.get(bound.unit + ":" + texTarget(t));
    const setTex = (t, key, bytes) => { const tex = texOf(t); if (!tex) return; if (!gl.tex.has(tex)) gl.tex.set(tex, new Map()); gl.tex.get(tex).set(key, bytes); note(); };
    wrap("activeTexture", (a) => { bound.unit = a[0]; });
    wrap("bindTexture", (a) => { bound.tex.set(bound.unit + ":" + a[0], a[1]); });
    wrap("deleteTexture", (a) => { gl.tex.delete(a[0]); });
    wrap("texStorage2D", (a) => { const [t, levels, fmt, w, h] = a; const b = (bpp[fmt] || 4) * w * h * (levels > 1 ? 4 / 3 : 1) * (t === 0x8513 ? 6 : 1); setTex(t, "s", b); });
    wrap("texStorage3D", (a) => { const [t, levels, fmt, w, h, d] = a; setTex(t, "s", (bpp[fmt] || 4) * w * h * d * (levels > 1 ? 4 / 3 : 1)); });
    wrap("texImage2D", (a) => {
      if (a.length < 8) return;   // the image-source form: size unknown here
      const [t, level, fmt, w, h, , format, type] = a;
      const b = bpp[fmt] && fmt !== 0x1908 && fmt !== 0x1907 ? bpp[fmt] : (compsOf[format] || 4) * (typeBytes[type] || 1);
      setTex(t, `${t}:${level}`, b * w * h);
    });
    wrap("texImage3D", (a) => { const [t, level, fmt, w, h, d] = a; setTex(t, `${t}:${level}`, (bpp[fmt] || 4) * w * h * d); });
    wrap("compressedTexImage2D", (a) => { const [t, level, , w, h, , data] = a; setTex(t, `${t}:${level}`, a.length >= 9 && a[8] ? a[8] : data && data.byteLength && data.byteLength < 64 * w * h ? data.byteLength : w * h); });
    wrap("bindBuffer", (a) => { bound.buf.set(a[0], a[1]); });
    wrap("deleteBuffer", (a) => { gl.buf.delete(a[0]); });
    wrap("bufferData", (a) => { const b = bound.buf.get(a[0]); if (!b) return; gl.buf.set(b, typeof a[1] === "number" ? a[1] : a.length >= 5 && a[4] ? a[4] * (a[1].BYTES_PER_ELEMENT || 1) : a.length >= 4 && a[1] ? a[1].byteLength - a[3] * (a[1].BYTES_PER_ELEMENT || 1) : (a[1] ? a[1].byteLength : 0)); note(); });
    wrap("bindRenderbuffer", (a) => { bound.rb = a[1]; });
    wrap("deleteRenderbuffer", (a) => { gl.rb.delete(a[0]); });
    wrap("renderbufferStorage", (a) => { if (bound.rb) { gl.rb.set(bound.rb, (bpp[a[1]] || 4) * a[2] * a[3]); note(); } });
    wrap("renderbufferStorageMultisample", (a) => { if (bound.rb) { gl.rb.set(bound.rb, (bpp[a[2]] || 4) * a[3] * a[4] * Math.max(1, a[1])); note(); } });
  }

  // Sound Web Audio decoded into memory (PCM, four bytes a sample), counted as it's made.
  let audioBytes = 0;
  const B = window.BaseAudioContext && BaseAudioContext.prototype;
  if (B) {
    const dec = B.decodeAudioData;
    B.decodeAudioData = function (data, ok, bad) {
      const count = (buf) => { try { audioBytes += buf.length * buf.numberOfChannels * 4; } catch (e) { } return buf; };
      const p = dec.call(this, data, ok ? (b) => ok(count(b)) : undefined, bad);
      return p && p.then && !ok ? p.then(count) : p;
    };
    const cb = B.createBuffer;
    B.createBuffer = function (ch, len, rate) { audioBytes += ch * len * 4; return cb.call(this, ch, len, rate); };
  }
  // Every AudioContext, for the sound check: is it running, is anything reaching the speakers?
  const contexts = [], analysers = [];
  const Native = window.AudioContext || window.webkitAudioContext;
  if (Native) {
    const Wrapped = function (...a) { const c = new Native(...a); contexts.push(c); return c; };
    Wrapped.prototype = Native.prototype;
    window.AudioContext = Wrapped;
    if (window.webkitAudioContext) window.webkitAudioContext = Wrapped;
  }
  const connect = AudioNode.prototype.connect;
  AudioNode.prototype.connect = function (dest, ...rest) {
    const r = connect.call(this, dest, ...rest);
    try {
      if (dest instanceof AudioDestinationNode) {
        const ctx = dest.context;
        if (!ctx.__probe) {
          ctx.__probe = ctx.createAnalyser(); ctx.__probe.fftSize = 2048;
          const mute = ctx.createGain(); mute.gain.value = 0;
          connect.call(ctx.__probe, mute); connect.call(mute, ctx.destination);
          analysers.push(ctx.__probe);
        }
        connect.call(this, ctx.__probe);
      }
    } catch (e) { }
    return r;
  };
  let frames = 0;
  const tick = () => { frames++; requestAnimationFrame(tick); };
  requestAnimationFrame(tick);
  window.__mobile = () => {
    let peak = 0;
    const buf = new Float32Array(2048);
    for (const a of analysers) { a.getFloatTimeDomainData(buf); for (let i = 0; i < buf.length; i++) peak = Math.max(peak, Math.abs(buf[i])); }
    const c = document.getElementById("unity-canvas");
    return {
      heap: heap(), heapPeak, gl: total(), glPeak: gl.peak, textures: gl.tex.size, audioBytes,
      jsHeap: performance.memory ? performance.memory.usedJSHeapSize : null,
      canvas: c ? [c.width, c.height] : null, frames,
      sound: { peak, states: contexts.map((x) => x.state) },
    };
  };
};

// This headless WebKit's GStreamer (Linux) has no MP4 demuxer or audio sink, and Unity's web build encodes its sound as
// AAC in MP4: decoding it there fails inside GStreamer and takes the tab down with it, which a phone's WebKit wouldn't.
// For WebKit the sound is replaced before the page loads: decodeAudioData gives a short silent buffer, and audio
// elements get no source. (--real-sound keeps WebKit's own; the sound itself is checked in Chromium.)
const quietMedia = () => {
  const B = window.BaseAudioContext && BaseAudioContext.prototype;
  if (B) B.decodeAudioData = function (data, ok) {
    const buf = this.createBuffer(2, Math.round(this.sampleRate * 0.25), this.sampleRate);
    if (ok) { setTimeout(() => ok(buf), 0); return Promise.resolve(buf); }
    return Promise.resolve(buf);
  };
  const desc = Object.getOwnPropertyDescriptor(HTMLMediaElement.prototype, "src");
  Object.defineProperty(HTMLMediaElement.prototype, "src", { configurable: true, get() { return this.__src || ""; }, set(v) { this.__src = v; } });
  HTMLMediaElement.prototype.play = function () { return Promise.resolve(); };
  HTMLMediaElement.prototype.load = function () { };
  window.__quietMedia = true;
};

// ------------------------------------------------------------------ the browser's processes: resident memory
function descendants(pid) {
  const kids = new Map();
  for (const d of readdirSync("/proc")) {
    if (!/^\d+$/.test(d)) continue;
    try {
      const stat = readFileSync(`/proc/${d}/stat`, "utf8");
      const ppid = Number(stat.slice(stat.lastIndexOf(")") + 2).split(" ")[1]);
      if (!kids.has(ppid)) kids.set(ppid, []);
      kids.get(ppid).push(Number(d));
    } catch (e) { }
  }
  const out = [];
  const walk = (p) => { for (const k of kids.get(p) || []) { out.push(k); walk(k); } };
  walk(pid);
  return out;
}
function memorySample() {
  const procs = [];
  for (const p of descendants(process.pid)) {
    try {
      const status = readFileSync(`/proc/${p}/status`, "utf8");
      const rss = Number((status.match(/VmRSS:\s+(\d+)/) || [])[1] || 0) * 1024;
      if (!rss) continue;
      const name = (status.match(/Name:\s+(.*)/) || [])[1] || "?";
      const cmd = readFileSync(`/proc/${p}/cmdline`, "utf8").split("\0");
      const type = (cmd.find((c) => c.startsWith("--type=")) || "").slice(7);
      procs.push({ pid: p, name: type ? `${name}:${type}` : name, rss });
    } catch (e) { }
  }
  return procs;
}

// ------------------------------------------------------------------ touches, as a finger makes them
class Fingers {
  constructor(engine, page, cdp) { this.engine = engine; this.page = page; this.cdp = cdp; this.active = new Map(); }
  static async attach(engine, page, context) {
    if (engine === "chromium") return new Fingers(engine, page, await context.newCDPSession(page));
    if (engine === "webkit") {
      const impl = page._connection.toImpl(page);   // Playwright's own page, in this process
      return new Fingers(engine, page, impl.delegate._pageProxySession);
    }
    return null;
  }
  async send(type, points) {
    if (this.engine === "chromium") {
      // CDP takes every finger still down and works out what changed; the last one up is a touchEnd with none.
      const all = [...this.active.entries()].map(([id, p]) => ({ x: p.x, y: p.y, id, radiusX: 6, radiusY: 6, force: 1 }));
      if (all.length === 0) await this.cdp.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
      else await this.cdp.send("Input.dispatchTouchEvent", { type: type === "touchEnd" ? "touchMove" : type, touchPoints: all });
    } else {
      // WebKit's command reports the points it's given as all changing; give it just those.
      await this.cdp.send("Input.dispatchTouchEvent", { type, touchPoints: points.map(([id, p]) => ({ x: Math.round(p.x), y: Math.round(p.y), id })) });
    }
  }
  async down(id, x, y) { this.active.set(id, { x, y }); await this.send("touchStart", [[id, { x, y }]]); }
  async move(id, x, y) { this.active.set(id, { x, y }); await this.send("touchMove", [[id, { x, y }]]); }
  async up(id) { const p = this.active.get(id); this.active.delete(id); if (p) await this.send("touchEnd", [[id, p]]); }
  async tap(x, y, id = 9) { await this.down(id, x, y); await sleep(70); await this.up(id); }
  async drag(id, from, to, ms = 600, steps = 12) {
    await this.down(id, from[0], from[1]);
    for (let i = 1; i <= steps; i++) {
      await sleep(ms / steps);
      await this.move(id, from[0] + (to[0] - from[0]) * i / steps, from[1] + (to[1] - from[1]) * i / steps);
    }
    await this.up(id);
  }
}

// ------------------------------------------------------------------ browsers
function cachedChromium() {
  const cache = path.join(os.homedir(), ".cache", "ms-playwright");
  if (!existsSync(cache)) return null;
  const rev = (d) => Number(d.split("-").pop()) || 0;
  const dirs = readdirSync(cache).sort((a, b) => rev(b) - rev(a));
  for (const d of dirs) {
    const shell = path.join(cache, d, "chrome-headless-shell-linux64", "chrome-headless-shell");
    if (d.startsWith("chromium_headless_shell-") && existsSync(shell)) return shell;
  }
  return null;
}
async function launch(engine, tmp) {
  if (engine === "webkit") return pw.webkit.launch({ headless: true, executablePath: process.env.WEBKIT_PATH || path.join(os.homedir(), ".cache/webkit-libs/webkit-2359/pw_run.sh") });
  if (engine === "chromium") {
    const o = { headless: true, ignoreDefaultArgs: ["--autoplay-policy=no-user-gesture-required"],
      args: ["--use-angle=vulkan", "--enable-features=Vulkan", "--ignore-gpu-blocklist", "--autoplay-policy=user-gesture-required"] };
    if (process.env.CHROMIUM_PATH) return pw.chromium.launch({ ...o, executablePath: process.env.CHROMIUM_PATH });
    try { return await pw.chromium.launch(o); }
    catch (e) { const exe = cachedChromium(); if (!exe) throw e; return pw.chromium.launch({ ...o, executablePath: exe }); }
  }
  if (engine === "firefox") {
    process.env.TMPDIR = tmp;
    return pw.firefox.launch({ headless: true, channel: "moz-firefox", executablePath: process.env.FIREFOX_PATH || "/usr/bin/firefox",
      firefoxUserPrefs: { "media.autoplay.default": 1, "media.autoplay.block-webaudio": true, "media.autoplay.blocking_policy": 0 } });
  }
  throw new Error(`unknown browser ${engine}`);
}

// ------------------------------------------------------------------ one device's run
async function run(name, url) {
  const dev = devices[name];
  if (!dev) { console.error(`[mobile] unknown device ${name}`); return false; }
  const results = [];
  const check = (ok, what) => { results.push({ ok: !!ok, what }); console.log(`[mobile] ${name}: ${ok ? "PASS" : "FAIL"} ${what}`); return ok; };
  const note = (what) => { results.push({ note: what }); console.log(`[mobile] ${name}: NOTE ${what}`); };
  const log = [], problems = [];
  const tmp = mkdtempSync(path.join(opts.out, `tmp-${name}-`));
  const info = { device: name, engine: dev.engine, profile: dev.profile || "desktop", url };
  const samples = [];
  let peakTotal = 0, peakLargest = { rss: 0 }, sampler = null, browser;
  try {
    browser = await launch(dev.engine, tmp);
    info.browser = `${dev.engine} ${browser.version()}`;
    const ctxOpts = dev.profile ? { ...pw.devices[dev.profile] } : { viewport: dev.viewport };
    delete ctxOpts.defaultBrowserType;
    if (dev.engine === "firefox") delete ctxOpts.isMobile;
    const context = await browser.newContext(ctxOpts);
    await context.addInitScript(probes);
    // --stopped: the page's "showing" mark, as a tab stopped while showing leaves it (set before the page reads it).
    await context.addInitScript(() => { try { if (sessionStorage.getItem("ll-check-stopped") === "1") { sessionStorage.removeItem("ll-check-stopped"); localStorage.setItem("lastLight.showing", "1"); } } catch (e) { /* about:blank has no storage */ } });
    if (opts.notch && dev.touch) {
      // Headless browsers report no safe area; an iPhone 15 held landscape has the notch's 59 pt on one side and the home
      // indicator's 21 pt at the bottom. The page reads them through these variables.
      await context.addInitScript(() => document.addEventListener("DOMContentLoaded", () => {
        const r = document.documentElement.style;
        r.setProperty("--ll-inset-left", "59px"); r.setProperty("--ll-inset-right", "59px"); r.setProperty("--ll-inset-bottom", "21px");
      }));
      note("safe area emulated: 59 px left and right, 21 px at the bottom (an iPhone 15 in landscape)");
    }
    if (dev.engine === "webkit" && !opts.realSound) { await context.addInitScript(quietMedia); note("WebKit's sound is replaced by silence (its GStreamer here can't decode the game's AAC); sound is checked in Chromium"); }
    const page = await context.newPage();
    const W = page.viewportSize().width, H = page.viewportSize().height;
    info.viewport = `${W}x${H}`;
    page.on("console", (m) => { const line = `[${m.type()}] ${m.text()}`; log.push(line); if (m.type() === "error") problems.push(line); });
    page.on("pageerror", (e) => { log.push(`[pageerror] ${e}`); problems.push(`page error: ${e}`); });
    page.on("requestfailed", (r) => { log.push(`[requestfailed] ${r.url()} ${r.failure()?.errorText}`); if (!r.url().startsWith("blob:")) problems.push(`request failed: ${r.url()}`); });
    page.on("response", (r) => { if (r.status() >= 400) { log.push(`[http ${r.status()}] ${r.url()}`); problems.push(`HTTP ${r.status()}: ${r.url()}`); } });
    page.on("crash", () => { log.push("[crash] the page crashed"); problems.push("the page crashed"); });
    sampler = setInterval(() => {
      const procs = memorySample();
      const sum = procs.reduce((n, p) => n + p.rss, 0);
      peakTotal = Math.max(peakTotal, sum);
      for (const p of procs) if (p.rss > peakLargest.rss) peakLargest = { ...p };
      samples.push({ t: Date.now(), sum, largest: Math.max(0, ...procs.map((p) => p.rss)) });
    }, 500);

    const shot = (label) => page.screenshot({ path: path.join(opts.out, `${name}-${label}.png`) }).catch(() => { });
    const has = (text, after = 0) => log.slice(after).some((l) => l.includes(text));
    const waitLog = async (text, ms, after = 0) => { for (const end = Date.now() + ms; Date.now() < end; await sleep(200)) if (has(text, after)) return true; return false; };
    const probe = () => page.evaluate(() => window.__mobile ? window.__mobile() : null).catch(() => null);
    const game = () => page.evaluate(() => window.lastLightState ? { ...window.lastLightState } : null).catch(() => null);
    const touchUi = () => page.evaluate(() => {
      const el = document.getElementById("ll-touch");
      if (!el) return { present: false, shown: false };
      const vis = (id) => { const e = document.getElementById(id); if (!e) return null; const r = e.getBoundingClientRect(); const cs = getComputedStyle(e);
        return { shown: cs.display !== "none" && cs.visibility !== "hidden" && +cs.opacity > 0.05 && r.width > 0, x: r.x + r.width / 2, y: r.y + r.height / 2, w: r.width, h: r.height, rect: [r.left, r.top, r.right, r.bottom] }; };
      return { present: true, shown: document.documentElement.classList.contains("ll-touch-mode"), focus: vis("ll-focus"), horn: vis("ll-horn"), pause: vis("ll-pause"), rotate: vis("ll-rotate") };
    }).catch(() => ({ present: false, shown: false }));
    const fingers = dev.touch ? await Fingers.attach(dev.engine, page, context) : null;
    // The game lays its screens out on 1920x1080, grown to fit the page (CanvasScaler Expand): a point anchored at the
    // middle of the left edge, (x, dy) canvas units from it, is here. dy counts down from the middle.
    // The canvas may be narrower than the page (kept clear of a notch); positions are the canvas's.
    let cv = { x: 0, y: 0, w: W, h: H };
    const canvasBox = async () => { const r = await page.evaluate(() => { const b = document.getElementById("unity-canvas").getBoundingClientRect(); return { x: b.left, y: b.top, w: b.width, h: b.height }; }).catch(() => null); if (r && r.w > 0) cv = r; };
    let s = Math.min(W / 1920, H / 1080);
    const leftMid = (x, dy) => [cv.x + x * s, cv.y + cv.h / 2 + dy * s];
    const press = async (x, y) => { if (fingers) await fingers.tap(x, y); else await page.mouse.click(x, y); };

    // ---- 1. load to the title
    const t0 = Date.now();
    await page.goto(url, { waitUntil: "domcontentloaded", timeout: 120000 });
    await page.waitForFunction(() => window.lastLightReady === true || !!window.lastLightError, null, { timeout: opts.timeout * 1000 }).catch(() => { });
    info.loadSeconds = +((Date.now() - t0) / 1000).toFixed(1);
    const ready = await page.evaluate(() => window.lastLightReady === true).catch(() => false);
    info.pageError = await page.evaluate(() => window.lastLightError || null).catch((e) => String(e));
    await sleep(3000);
    info.downloadMB = +((await page.evaluate(() => performance.getEntriesByType("resource").concat(performance.getEntriesByType("navigation"))
      .reduce((n, e) => n + (e.encodedBodySize || e.transferSize || 0), 0)).catch(() => 0)) / 1e6).toFixed(1);
    info.media = await page.evaluate(() => ({ coarse: matchMedia("(pointer: coarse)").matches, fine: matchMedia("(any-pointer: fine)").matches,
      hoverNone: matchMedia("(hover: none)").matches, dpr: devicePixelRatio, webgpu: !!navigator.gpu, maxTouchPoints: navigator.maxTouchPoints,
      renderer: (() => { const g = document.createElement("canvas").getContext("webgl2"); if (!g) return "no WebGL 2"; const e = g.getExtension("WEBGL_debug_renderer_info"); return e ? g.getParameter(e.UNMASKED_RENDERER_WEBGL) : g.getParameter(g.RENDERER); })() }));
    info.atTitle = await probe();
    console.log(`[mobile] ${name}: ${info.browser}, ${info.viewport} at ${info.media.dpr}x, ${info.media.renderer}; title after ${info.loadSeconds} s, ${info.downloadMB} MB`);
    await shot("01-title");
    check(ready && has("[Game] the title is up"), `reached the title in ${info.loadSeconds} s${info.pageError ? ` (page error: ${info.pageError.slice(0, 200)})` : ""}`);
    const ui0 = await touchUi();
    info.touchUiAtTitle = ui0;
    if (dev.touch) {
      if (name.endsWith("portrait")) check(ui0.rotate && ui0.rotate.shown, "in portrait the page asks to turn the device to landscape");
      else check(ui0.present && ui0.shown, `the touch controls are switched on (${ui0.present ? "html.ll-touch-mode " + ui0.shown : "no #ll-touch"})`);
    } else check(!ui0.shown && !(ui0.focus && ui0.focus.shown) && !(ui0.horn && ui0.horn.shown) && !(ui0.pause && ui0.pause.shown), "no touch controls on a desktop browser");

    await canvasBox();
    s = Math.min(cv.w / 1920, cv.h / 1080);
    info.canvasBox = cv;
    const centre = (x, dy) => [cv.x + cv.w / 2 + x * s, cv.y + cv.h / 2 + dy * s];   // anchored at the middle of the canvas; dy counts down
    const reachTitle = async () => {
      await page.waitForFunction(() => window.lastLightReady === true || !!window.lastLightError, null, { timeout: opts.timeout * 1000 }).catch(() => { });
      await sleep(2500);
      return page.evaluate(() => window.lastLightReady === true).catch(() => false);
    };
    // The save as the game wrote it to IndexedDB (Unity's IDBFS), changed by `change` and written back. The game writes
    // its save when the page is hidden (LastLightWeb.jslib), so the page is made to look hidden first.
    const editSave = async (change) => {
      await page.evaluate(() => { Object.defineProperty(document, "visibilityState", { configurable: true, get: () => "hidden" }); document.dispatchEvent(new Event("visibilitychange")); });
      await sleep(2500);
      return page.evaluate((change) => new Promise((resolve) => {
        const open = indexedDB.open("/idbfs");
        open.onerror = () => resolve(false);
        open.onsuccess = () => {
          const db = open.result;
          if (!db.objectStoreNames.contains("FILE_DATA")) return resolve(false);
          const req = db.transaction("FILE_DATA", "readwrite").objectStore("FILE_DATA").openCursor();
          req.onsuccess = () => {
            const c = req.result;
            if (!c) return resolve(false);
            if (String(c.key).endsWith("/save.json") && c.value.contents) {
              const save = JSON.parse(new TextDecoder().decode(c.value.contents));
              new Function("save", change)(save);
              c.update({ ...c.value, timestamp: new Date(), contents: new TextEncoder().encode(JSON.stringify(save)) }).onsuccess = () => resolve(true);
              return;
            }
            c.continue();
          };
          req.onerror = () => resolve(false);
        };
      }), change);
    };

    if (opts.play && ready && !name.endsWith("portrait")) {
      // ---- 2. the first touch (or click) unlocks sound
      await press(W * 0.8, H * 0.18);   // the sky over the bay: no menu there
      let snd = null;
      for (let i = 0; i < 20; i++) { await sleep(250); snd = (await probe())?.sound; if (snd && snd.states.includes("running") && snd.peak > 0.001) break; }
      if (dev.engine === "webkit" && !opts.realSound) note(`audio context after the first touch: ${snd ? snd.states.join(",") : "?"}`);
      else check(snd && snd.states.includes("running"), `sound starts after the first ${dev.touch ? "touch" : "click"} (context ${snd ? snd.states.join(",") : "?"}, peak ${snd ? snd.peak.toFixed(3) : "?"})`);
      if (!dev.touch) {
        await page.mouse.move(W * 0.5, H * 0.5); await sleep(300); await page.mouse.move(W * 0.6, H * 0.4); await sleep(500);
        const ui = await touchUi();
        check(!ui.shown, "the touch controls stay hidden after the mouse is used");
      }

      // ---- 3. a save with nights I to IV kept, so the night played is V (Sea Fret: fog, and the foghorn)
      if (dev.touch) {
        const seeded = await editSave("save.unlocked = 5; for (let i = 0; i < 4; i++) { save.lamps[i] = 3; save.best[i] = 900; } save.tutorialSeen = true;");
        check(seeded, "a save with nights I to IV kept was written to IndexedDB");
        await page.reload({ waitUntil: "domcontentloaded" });
        await reachTitle();
        await fingers.tap(W * 0.8, H * 0.18);
        await sleep(500);
      }

      // ---- 4. Begin the watch (the title's first item), and the briefing's Back and Begin buttons
      let mark = log.length;
      await press(...leftMid(300, 12));
      await sleep(2500);
      await shot("02-briefing");
      if (dev.touch) {
        const ui = await touchUi();
        check(ui.pause && ui.pause.shown && (await game())?.corner === 2, "the briefing shows a Back button at the top left");
        await fingers.tap(ui.pause.x, ui.pause.y);
        await sleep(1500);
        const back = await page.evaluate(() => window.lastLightState.corner).catch(() => -1);
        await shot("02b-back-to-title");
        check(back === 0, "Back on the briefing returns to the title");
        await press(...leftMid(300, 12));
        await sleep(2500);
      }
      mark = log.length;
      await press(...leftMid(300, 278));   // the briefing's Begin the watch button
      const begun = await waitLog("under way", 15000, mark);
      info.night = (log.slice(mark).find((l) => l.includes("under way")) || "").replace(/.*\[Game\] /, "");
      check(begun, `Begin the watch and the briefing start a night by ${dev.touch ? "touch" : "mouse"} (${info.night || "none"})`);
      if (begun) {
        await sleep(3000);
        const ui = await touchUi();
        info.touchUiPlaying = ui;
        if (dev.touch) {
          const big = (b) => b && b.shown && b.w >= 44 && b.h >= 44;
          check(big(ui.focus) && big(ui.horn) && big(ui.pause), `Focus, Foghorn and Pause buttons show, each at least 44 pt (${["focus", "horn", "pause"].map((k) => ui[k] ? `${k} ${Math.round(ui[k].w)}x${Math.round(ui[k].h)}` : `${k} missing`).join(", ")})`);
          const inside = (b) => b && b.rect[0] >= 0 && b.rect[1] >= 0 && b.rect[2] <= W && b.rect[3] <= H;
          check(inside(ui.focus) && inside(ui.horn) && inside(ui.pause), "the buttons sit inside the page");
          const layout = await page.evaluate(() => window.lastLightTouch.layout);
          info.layout = layout;
          check(layout[0] > 0 && layout[1] > 0, `the HUD is told the room the buttons take (${layout.map((v) => v.toFixed(3)).join(", ")})`);
        }
        await shot("03-night");
        // ---- 5. play: drag to aim, hold Focus with a second finger, the foghorn, measured
        const g0 = await game();
        const p0 = await probe();
        const s0 = Date.now();
        let focused = false, horned = false, still = true;
        const bearings = [];
        if (dev.touch && ui.focus && ui.focus.shown) {
          // A finger on Focus alone is the button's, not the game's: the light doesn't turn towards it.
          const b0 = (await game()).bearing;
          await fingers.down(4, ui.focus.x, ui.focus.y); await sleep(1200);
          const gf = await game();
          await fingers.up(4); await sleep(600);
          still = Math.abs(gf.bearing - b0) < 3;
          if (gf && gf.focus) focused = true;
          // Drag along the bottom of the sea, left and right: the beam follows the drag, not the finger's spot.
          for (let round = 0; round < 3; round++) {
            const y = H * 0.75;
            await fingers.down(1, W * 0.3, y);
            for (let i = 1; i <= 16; i++) { await sleep(60); await fingers.move(1, W * 0.3 + (W * 0.35) * i / 16, y - H * 0.05 * Math.sin(i / 16 * Math.PI)); }
            if (round === 1) {   // a second finger holds Focus while the first one is still down
              await fingers.down(2, ui.focus.x, ui.focus.y);
              await sleep(900);
              const g = await game();
              if (!(g && g.focus)) focused = false;
              await shot("04-focus");
              await fingers.up(2);
              await sleep(300);
              const g2 = await game();
              if (g2 && g2.focus && !(await page.evaluate(() => window.lastLightTouch.focusHeld))) note("focus stayed on after the button was let go (Settings ▸ Focus is Toggle?)");
            }
            await fingers.up(1);
            await sleep(1200);
            const g = await game(); if (g) bearings.push(g.bearing);
            await fingers.down(1, W * 0.65, y);
            for (let i = 1; i <= 16; i++) { await sleep(60); await fingers.move(1, W * 0.65 - (W * 0.35) * i / 16, y); }
            await fingers.up(1);
            await sleep(1200);
            const g3 = await game(); if (g3) bearings.push(g3.bearing);
          }
          const before = await game();
          if (ui.horn && ui.horn.shown) {
            await fingers.tap(ui.horn.x, ui.horn.y);
            await sleep(500);
          }
          const after = await game();
          horned = !!(after && before && before.hornReady >= 1 && after.hornReady < 1);
          info.horn = { before, after };
          await shot("05-horn");
          // A long press anywhere brings up no callout or selection, and the page hasn't scrolled or zoomed.
          await fingers.down(3, W * 0.5, H * 0.4); await sleep(900); await fingers.up(3);
          const page0 = await page.evaluate(() => ({ sx: scrollX, sy: scrollY, scale: visualViewport ? visualViewport.scale : 1, sel: String(getSelection()) }));
          check(page0.sx === 0 && page0.sy === 0 && page0.scale === 1 && page0.sel === "", `no scroll, zoom or selection after drags and a long press (${JSON.stringify(page0)})`);
        } else if (!dev.touch) {
          for (let i = 0; i < 100; i++) {
            const a = i / 100 * Math.PI * 4;
            await page.mouse.move(W / 2 + Math.cos(a) * W * 0.35, H * 0.45 + Math.sin(a) * H * 0.25, { steps: 2 });
            if (i % 30 === 10) await page.mouse.down();
            if (i % 30 === 20) await page.mouse.up();
            if (i === 60) await page.mouse.click(W / 2, H / 2, { button: "right" });
            await sleep(200);
          }
          const ui = await touchUi();
          check(!ui.shown, "the touch controls stay hidden while playing with a mouse");
        }
        const p1 = await probe();
        const fps = p0 && p1 ? (p1.frames - p0.frames) / ((Date.now() - s0) / 1000) : null;
        info.nightFps = fps ? +fps.toFixed(1) : null;
        if (dev.touch) {
          const spread = bearings.length ? Math.max(...bearings) - Math.min(...bearings) : 0;
          info.bearings = bearings.map((b) => Math.round(b));
          check(spread > 10, `dragging on the sea turns the beam (bearings ${info.bearings.join(", ") || "unknown"}; from ${g0 ? Math.round(g0.bearing) : "?"})`);
          check(still, "a finger on the Focus button alone doesn't turn the light");
          check(focused, "holding Focus, alone and with a second finger while one drags, focuses the beam");
          check(horned, `the Foghorn button sounds the horn and starts its cooldown (ready ${info.horn.before ? info.horn.before.hornReady : "?"} → ${info.horn.after ? info.horn.after.hornReady : "?"})`);
        }
        check(fps !== null, `the night ran at about ${fps ? fps.toFixed(0) : "?"} fps (headless, on a shared machine)`);
        // ---- 6. pause and resume
        const pauseMark = log.length;
        if (dev.touch) { const u = await touchUi(); if (u.pause) await fingers.tap(u.pause.x, u.pause.y); }
        else await page.keyboard.press("Escape");
        const paused = await waitLog("[Game] paused", 5000, pauseMark);
        await sleep(900);
        await shot("06-paused");
        const ui2 = await touchUi();
        if (dev.touch) check(paused && !(ui2.focus && ui2.focus.shown) && !(ui2.horn && ui2.horn.shown) && !(ui2.pause && ui2.pause.shown), `the Pause button pauses the night, and the buttons step aside for the menu (${paused ? "paused" : "not paused"})`);
        if (dev.touch) await fingers.tap(...centre(0, -60));   // the pause menu's Resume
        else await page.keyboard.press("Escape");
        const resumed = await waitLog("[Game] resumed", 5000, pauseMark);
        check(resumed, `${dev.touch ? "tapping Resume" : "Esc"} resumes the night`);
        await sleep(1200);
        await shot("07-resumed");
        if (dev.touch) {
          // A key, then a mouse, put the controls away; a finger brings them back (the page also hears of a pad from the
          // game, which headless browsers can't plug in: its own half is checked by calling it).
          const shownNow = async () => (await touchUi()).shown;
          await page.keyboard.press("F2"); await sleep(700);
          const afterKey = await shownNow();
          await fingers.tap(W * 0.5, H * 0.2); await sleep(1300);
          const afterTouch = await shownNow();
          await page.mouse.move(W * 0.4, H * 0.3); await page.mouse.move(W * 0.45, H * 0.35, { steps: 4 }); await sleep(700);
          const afterMouse = await shownNow();
          await shot("07b-mouse-used");
          await fingers.tap(W * 0.5, H * 0.2); await sleep(700);
          const afterTouch2 = await shownNow();
          await page.evaluate(() => window.lastLightTouch.padUsed()); await sleep(500);
          const afterPad = await shownNow();
          await fingers.tap(W * 0.5, H * 0.2); await sleep(700);
          const back = await shownNow();
          check(!afterKey && afterTouch && !afterMouse && afterTouch2 && !afterPad && back,
            `the controls step aside for a key, a mouse and a pad, and come back with a touch (key ${afterKey}, touch ${afterTouch}, mouse ${afterMouse}, touch ${afterTouch2}, pad ${afterPad}, touch ${back})`);
        }
        if (dev.touch && opts.rotate) {
          // Turned to portrait mid-night: the page asks to turn back, and the night pauses.
          const rmark = log.length;
          await page.setViewportSize({ width: H, height: W });
          const rp = await waitLog("[Game] paused", 5000, rmark);
          await sleep(800);
          const ur = await touchUi();
          await shot("08-portrait");
          check(rp && ur.rotate && ur.rotate.shown, `turned to portrait mid-night: the night pauses and the page asks to turn back (${rp ? "paused" : "not paused"})`);
          await page.setViewportSize({ width: W, height: H });
          await sleep(1200);
          await fingers.tap(...centre(0, -60));
          await waitLog("[Game] resumed", 5000, rmark);
        }
      }
    }

    if (opts.chart && ready && dev.touch && !name.endsWith("portrait")) {
      // ---- 7. a whole night, then the dawn chart: the Chart button, the timeline dragged and tapped, Replay
      const fresh = await editSave("save.unlocked = 1; for (let i = 0; i < 12; i++) { save.lamps[i] = 0; save.best[i] = 0; } save.tutorialSeen = true;");
      await page.reload({ waitUntil: "domcontentloaded" });
      await reachTitle();
      await fingers.tap(W * 0.8, H * 0.18);
      await sleep(500);
      let mark = log.length;
      await fingers.tap(...leftMid(300, 12)); await sleep(2500);
      await fingers.tap(...leftMid(300, 278));
      const begun = await waitLog("under way", 15000, mark);
      check(fresh && begun, "a fresh save's night I is under way, for the chart");
      // The night plays itself out unkept: ships lose their way and the Board ends it, or they come home.
      const t0 = Date.now();
      let done = false;
      while (begun && Date.now() - t0 < opts.chartWait * 1000) {
        await sleep(2000);
        const g = await game();
        if (g && !g.playing && g.corner === 0 && Date.now() - t0 > 10000) { done = true; break; }
      }
      await waitLog("[Game] the night ran at", 15000, mark);   // said as dawn's card comes up
      await sleep(2500);
      await shot("09-dawn");
      check(done, `the night ended (after ${Math.round((Date.now() - t0) / 1000)} s)`);
      if (done) {
        // The dawn card's Chart button: the middle one after a night lost, low on the card, which grows with its notes
        // (down that middle line there's only text above it).
        let chart = null;
        for (let dy = 370; dy <= 450 && !chart; dy += 16) {
          await fingers.tap(...centre(0, dy));
          for (let i = 0; i < 6 && !chart; i++) { await sleep(250); chart = await page.evaluate(() => window.lastLightChart || null); }
        }
        await sleep(800);
        await shot("10-chart");
        check(chart && chart.duration > 0, `the Chart button opens the dawn chart (the night ran ${chart ? Math.round(chart.duration) : "?"} s)`);
        if (chart) {
          const tl = (f) => centre(710 - 133 + 266 * f, 190);
          const read = async () => { await sleep(400); return page.evaluate(() => window.lastLightChart); };
          await fingers.drag(5, tl(0.15), tl(0.7), 700, 14);
          const c1 = await read();
          check(c1 && Math.abs(c1.time / c1.duration - 0.7) < 0.08, `dragging the timeline scrubs the replay (to ${c1 ? (100 * c1.time / c1.duration).toFixed(0) : "?"}% of the night, finger at 70%)`);
          // Under the line, where only the touch area reaches.
          const below = tl(0.3); below[1] += 28 * s;
          await fingers.tap(...below);
          const c2 = await read();
          await shot("11-chart-scrubbed");
          check(c2 && Math.abs(c2.time / c2.duration - 0.3) < 0.08 || (c2 && c2.time < c1.time), `a tap just under the timeline (its touch area) moves the replay (to ${c2 ? (100 * c2.time / c2.duration).toFixed(0) : "?"}%)`);
          await fingers.tap(...centre(710, 140));   // Replay / Play on
          await sleep(1500);
          const c3 = await page.evaluate(() => window.lastLightChart);
          check(c3 && (c3.playing || c3.time > c2.time + 0.5), `the Replay button plays the replay (${c3 ? c3.time.toFixed(1) : "?"} s, ${c3 && c3.playing ? "playing" : "paused"})`);
        }
      }
    }

    if (opts.stopped && ready && !name.endsWith("portrait")) {
      // ---- 8. a visit that stops while showing (as a phone's browser does when the tab runs short of memory): the
      // next visit says so and starts lighter. The page's mark is set again before the reloaded page reads it.
      const smark = log.length;
      await page.evaluate(() => sessionStorage.setItem("ll-check-stopped", "1"));
      await page.reload({ waitUntil: "domcontentloaded" });
      await sleep(400);
      const noteShown = await page.evaluate(() => { const n = document.getElementById("ll-note"); return n && getComputedStyle(n).display !== "none" ? n.textContent : null; }).catch(() => null);
      await reachTitle();
      check(noteShown && has("[Game] the last visit stopped", smark), `after a visit that stopped, the page says so (${noteShown ? "\"" + noteShown.slice(0, 60) + "…\"" : "no note"}) and the game starts lighter`);
    }
    info.end = await probe();
    check(!problems.some((p) => p.includes("crashed")), "the page didn't crash");
    check(problems.length === 0, `no console errors, page errors or failed requests${problems.length ? ": " + problems.slice(0, 3).join(" | ") : ""}`);
    await context.close();
  } catch (e) {
    check(false, `the run itself failed: ${e && e.message ? e.message.split("\n")[0] : e}`);
  } finally {
    if (sampler) clearInterval(sampler);
    if (browser) await browser.close().catch(() => { });
    rmSync(tmp, { recursive: true, force: true });
  }
  const m = info.end || info.atTitle || {};
  info.memory = {
    wasmHeapMB: m.heap != null ? MB(m.heap) : null, wasmHeapPeakMB: m.heapPeak != null ? MB(m.heapPeak) : null,
    webglMB: m.gl != null ? MB(m.gl) : null, webglPeakMB: m.glPeak != null ? MB(m.glPeak) : null,
    decodedAudioMB: m.audioBytes != null ? MB(m.audioBytes) : null, jsHeapMB: m.jsHeap ? MB(m.jsHeap) : null, canvas: m.canvas,
    browserProcessesPeakMB: MB(peakTotal), largestProcessPeakMB: MB(peakLargest.rss), largestProcess: peakLargest.name,
  };
  console.log(`[mobile] ${name}: memory ${JSON.stringify(info.memory)}`);
  info.results = results; info.problems = problems;
  writeFileSync(path.join(opts.out, `${name}.log`), log.join("\n").slice(-2_000_000) + "\n");
  writeFileSync(path.join(opts.out, `${name}.json`), JSON.stringify({ ...info, samples: samples.filter((_, i) => i % 4 === 0) }, null, 2) + "\n");
  return results.some((r) => "ok" in r) && results.every((r) => !("ok" in r) || r.ok);
}

// ------------------------------------------------------------------ main
let server = null, url = opts.url;
if (opts.serve) {
  server = await serve(opts.serve);
  url = url || `http://127.0.0.1:${opts.serve}/LastLight/`;
  console.log(`[mobile] serving ${path.join(root, "Builds", "Pages")} at http://127.0.0.1:${opts.serve}/ (pid ${process.pid})`);
}
let ok = true;
for (const d of opts.devices) ok = (await run(d, url)) && ok;
if (server) server.close();
console.log(`[mobile] ${ok ? "PASS" : "FAIL"} (${opts.devices.join(", ")} at ${url}); logs in ${opts.out}`);
process.exit(ok ? 0 : 1);
