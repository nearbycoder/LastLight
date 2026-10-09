#!/usr/bin/env node
// Checks the browser build as GitHub Pages serves it, in headless Chromium and/or Firefox.
//
//   node Tools/check-pages.mjs https://nearbycoder.github.io/LastLight/      the live site: loads to the title?
//   node Tools/check-pages.mjs --serve                                       Builds/Pages served at /LastLight/
//   node Tools/check-pages.mjs --serve --play --browser=chromium,firefox     plus a short night, a setting kept
//                                                                            over a reload, and sound after a click
// Options: --serve[=port] (default 8642, 127.0.0.1 only; the server stops with the script), --browser=chromium|firefox
// (comma list, default chromium), --play, --steps=N how far --play moves Graphics fidelity (default 1: from the web's
// Medium to High, so the night is played on High; negative steps down), --timeout=seconds to reach the title (default 240), --out=dir for the logs and
// screenshots (default Logs/pages-check).
//
// Exits 0 only when every browser reached the title (the game's "[Game] the title is up", window.lastLightReady) with
// no console errors, page errors or failed requests, and, with --play, every play check passed. Each browser's console
// goes to <out>/<browser>.log and its results to <out>/<browser>.json.
//
// Needs playwright-core 1.63 or later (PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core, or one Node can resolve).
// Chromium: CHROMIUM_PATH, else Playwright's own, else the newest headless shell or Chromium in ~/.cache/ms-playwright.
// Firefox is the system one (FIREFOX_PATH, default /usr/bin/firefox) over WebDriver BiDi; no Playwright Firefox needed.
// Every browser runs headless with a fresh profile, so nothing touches a real browser's storage.
import { createRequire } from "node:module";
import { existsSync, mkdirSync, readdirSync, readFileSync, statSync, writeFileSync, mkdtempSync, rmSync } from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";

const require = createRequire(import.meta.url);
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..");

// ------------------------------------------------------------------ options
const opts = { browsers: ["chromium"], play: false, steps: 1, serve: 0, timeout: 240, out: path.join(root, "Logs", "pages-check"), url: null };
for (const a of process.argv.slice(2)) {
  const [k, v] = a.split(/=(.*)/s);
  if (k === "--serve") opts.serve = v ? Number(v) : 8642;
  else if (k === "--browser") opts.browsers = v.split(",").filter(Boolean);
  else if (k === "--play") opts.play = true;
  else if (k === "--steps") opts.steps = Number(v);
  else if (k === "--timeout") opts.timeout = Number(v);
  else if (k === "--out") opts.out = path.resolve(v);
  else if (k === "--help" || k === "-h") { console.log(readFileSync(new URL(import.meta.url), "utf8").split("\nimport")[0]); process.exit(0); }
  else if (!k.startsWith("--")) opts.url = a;
  else { console.error(`unknown option ${a}`); process.exit(2); }
}
if (!opts.url && !opts.serve) { console.error("usage: node Tools/check-pages.mjs <url> | --serve [--play] [--browser=chromium,firefox]"); process.exit(2); }
mkdirSync(opts.out, { recursive: true });

let pw;
try { pw = require(process.env.PLAYWRIGHT_CORE || "playwright-core"); }
catch (e) { console.error("[check] playwright-core not found: set PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core"); process.exit(2); }

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ------------------------------------------------------------------ a static server like GitHub Pages: files only,
// no Content-Encoding, case-sensitive paths, the site under /LastLight/
const types = { ".html": "text/html; charset=utf-8", ".js": "application/javascript", ".wasm": "application/wasm",
  ".jpg": "image/jpeg", ".png": "image/png", ".ico": "image/x-icon", ".css": "text/css", ".json": "application/json",
  ".data": "application/octet-stream", ".unityweb": "application/octet-stream", ".br": "application/octet-stream" };
function serve(port) {
  const dir = path.join(root, "Builds", "Pages");
  if (!existsSync(path.join(dir, "LastLight", "index.html"))) { console.error(`[check] no build in ${dir}/LastLight: run Tools/build-pages.sh`); process.exit(2); }
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
  return new Promise((resolve, reject) => {
    server.on("error", reject);
    server.listen(port, "127.0.0.1", () => resolve(server));
  });
}

// ------------------------------------------------------------------ in-page probes (added before the page's scripts)
// Every AudioContext the game makes, and an analyser on what reaches its speakers: is it running, and is there sound?
const audioProbe = () => {
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
          ctx.__probe = ctx.createAnalyser();
          ctx.__probe.fftSize = 2048;
          const mute = ctx.createGain();
          mute.gain.value = 0;
          connect.call(ctx.__probe, mute);
          connect.call(mute, ctx.destination);
          analysers.push(ctx.__probe);
        }
        connect.call(this, ctx.__probe);
      }
    } catch (e) { /* never break the game's sound */ }
    return r;
  };
  window.__llAudio = () => {
    let peak = 0;
    const buf = new Float32Array(2048);
    for (const a of analysers) {
      a.getFloatTimeDomainData(buf);
      for (let i = 0; i < buf.length; i++) peak = Math.max(peak, Math.abs(buf[i]));
    }
    return { peak, states: contexts.map((c) => c.state), channels: contexts.map((c) => c.destination.channelCount) };
  };
  // Frames the page draws (Unity draws on requestAnimationFrame): the frame rate over a stretch.
  let frames = 0;
  const tick = () => { frames++; requestAnimationFrame(tick); };
  requestAnimationFrame(tick);
  window.__llFrames = () => frames;
};

// The save as the game last wrote it to IndexedDB (Unity's IDBFS), parsed, or null.
const readSave = () => new Promise((resolve) => {
  const open = indexedDB.open("/idbfs");
  open.onerror = () => resolve(null);
  open.onsuccess = () => {
    const db = open.result;
    if (!db.objectStoreNames.contains("FILE_DATA")) return resolve(null);
    const req = db.transaction("FILE_DATA").objectStore("FILE_DATA").openCursor();
    req.onsuccess = () => {
      const c = req.result;
      if (!c) return resolve(null);
      if (String(c.key).endsWith("/save.json") && c.value.contents) {
        try { return resolve(JSON.parse(new TextDecoder().decode(c.value.contents))); } catch (e) { return resolve(null); }
      }
      c.continue();
    };
    req.onerror = () => resolve(null);
  };
});

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
  for (const d of dirs) {
    const chrome = path.join(cache, d, "chrome-linux64", "chrome");
    if (d.startsWith("chromium-") && existsSync(chrome)) return chrome;
  }
  return null;
}

async function launch(engine, tmp) {
  if (engine === "chromium") {
    const o = {
      headless: true,
      ignoreDefaultArgs: ["--autoplay-policy=no-user-gesture-required"],   // Playwright's default lets sound start unasked
      // The real GPU through ANGLE/Vulkan rather than SwiftShader, and sound held until a click, as in a desktop browser.
      args: ["--use-angle=vulkan", "--enable-features=Vulkan", "--ignore-gpu-blocklist", "--autoplay-policy=user-gesture-required"],
    };
    if (process.env.CHROMIUM_PATH) return pw.chromium.launch({ ...o, executablePath: process.env.CHROMIUM_PATH });
    try { return await pw.chromium.launch(o); }
    catch (e) {
      const exe = cachedChromium();
      if (!exe) throw e;
      return pw.chromium.launch({ ...o, executablePath: exe });
    }
  }
  if (engine === "firefox") {
    process.env.TMPDIR = tmp;   // its profile goes here, not the shared /tmp
    return pw.firefox.launch({
      headless: true, channel: "moz-firefox", executablePath: process.env.FIREFOX_PATH || "/usr/bin/firefox",
      // Firefox's own defaults for a desktop user: audible media and Web Audio wait for a click or key.
      firefoxUserPrefs: { "media.autoplay.default": 1, "media.autoplay.block-webaudio": true, "media.autoplay.blocking_policy": 0 },
    });
  }
  throw new Error(`unknown browser ${engine}`);
}

// ------------------------------------------------------------------ one browser's run
async function run(engine, url) {
  const results = [];
  const check = (ok, what) => { results.push({ ok: !!ok, what }); console.log(`[check] ${engine}: ${ok ? "PASS" : "FAIL"} ${what}`); return ok; };
  const log = [];
  const problems = [];   // console errors, page errors, failed requests
  const tmp = mkdtempSync(path.join(opts.out, `tmp-${engine}-`));
  const W = 1600, H = 900;
  // The game lays its screens out on 1920x1080; positions here are in those units.
  const at = (x, y) => [x * W / 1920, y * H / 1080];
  let browser, bytes = 0;
  const info = { engine, url };
  try {
    browser = await launch(engine, tmp);
    info.browser = `${engine} ${browser.version()}`;
    const context = await browser.newContext({ viewport: { width: W, height: H } });
    await context.addInitScript(audioProbe);
    const page = await context.newPage();
    page.on("console", (m) => {
      const line = `[${m.type()}] ${m.text()}`;
      log.push(line);
      if (m.type() === "error") problems.push(line);
    });
    page.on("pageerror", (e) => { log.push(`[pageerror] ${e}`); problems.push(`page error: ${e}`); });
    page.on("requestfailed", (r) => {
      log.push(`[requestfailed] ${r.url()} ${r.failure()?.errorText}`);
      // The loader hands its scripts over as blob: URLs and drops them once read; that isn't a download failing.
      if (!r.url().startsWith("blob:")) problems.push(`request failed: ${r.url()}`);
    });
    page.on("response", (r) => {
      if (r.status() >= 400) { log.push(`[http ${r.status()}] ${r.url()}`); problems.push(`HTTP ${r.status()}: ${r.url()}`); }
    });
    const shot = (name) => page.screenshot({ path: path.join(opts.out, `${engine}-${name}.png`) }).catch(() => { });
    const has = (text) => log.some((l) => l.includes(text));
    const waitLog = async (text, ms, after = 0) => {
      for (const end = Date.now() + ms; Date.now() < end; await sleep(200))
        if (log.slice(after).some((l) => l.includes(text))) return true;
      return false;
    };
    // Up to the title: the game says so, or the page shows an error.
    const reachTitle = async () => {
      const t0 = Date.now();
      await page.waitForFunction(() => window.lastLightReady === true || !!window.lastLightError, null, { timeout: opts.timeout * 1000 }).catch(() => { });
      const seconds = (Date.now() - t0) / 1000;
      const error = await page.evaluate(() => window.lastLightError || null).catch((e) => String(e));
      const ready = await page.evaluate(() => window.lastLightReady === true).catch(() => false);
      return { ready, error, seconds };
    };

    // ---- 1. load to the title
    const t0 = Date.now();
    await page.goto(url, { waitUntil: "domcontentloaded", timeout: 120000 });
    const first = await reachTitle();
    info.loadSeconds = +((Date.now() - t0) / 1000).toFixed(1);
    await sleep(3000);   // the title's first seconds: anything that goes wrong early shows up in the console
    // What came over the network for the page (resource timing's encoded sizes, as the browser counts them).
    bytes = await page.evaluate(() => performance.getEntriesByType("resource").concat(performance.getEntriesByType("navigation"))
      .reduce((n, e) => n + (e.encodedBodySize || 0), 0)).catch(() => 0);
    info.downloadMB = +(bytes / 1e6).toFixed(1);
    info.renderer = await page.evaluate(() => {
      const gl = document.createElement("canvas").getContext("webgl2");
      if (!gl) return "no WebGL 2";
      const ext = gl.getExtension("WEBGL_debug_renderer_info");
      return ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
    }).catch(() => "?");
    console.log(`[check] ${engine}: ${info.browser}, renderer ${info.renderer}; title after ${info.loadSeconds} s, ${info.downloadMB} MB downloaded`);
    await shot("01-title");
    check(first.ready && has("[Game] the title is up"), `reached the title in ${first.seconds.toFixed(1)} s${first.error ? ` (page error: ${first.error})` : ""}`);
    check(problems.length === 0, `no console errors, page errors or failed requests at load${problems.length ? ": " + problems.slice(0, 3).join(" | ") : ""}`);

    if (opts.play && first.ready) {
      // ---- 2. sound waits for a click, then plays
      const before = await page.evaluate(() => window.__llAudio());
      check(before.states.length > 0 && before.states.every((s) => s !== "running"), `sound held before any input (audio context ${before.states.join(",") || "none"})`);
      await page.mouse.click(...at(1300, 300));   // the sky over the bay: no menu there
      let after = before;
      for (let i = 0; i < 20; i++) {
        await sleep(250);
        after = await page.evaluate(() => window.__llAudio());
        if (after.states.includes("running") && after.peak > 0.001) break;
      }
      info.audioPeak = +after.peak.toFixed(4);
      check(after.states.includes("running") && after.peak > 0.001, `sound starts after a click (context ${after.states.join(",")}, peak ${after.peak.toFixed(3)})`);

      // ---- 3. a setting kept over a reload: Settings ▸ Graphics fidelity --steps steps, Done
      const saveBefore = await page.evaluate(readSave);
      const fidelityBefore = saveBefore ? saveBefore.quality : 1;   // a new save: the web's default, Medium
      await page.mouse.click(...at(300, 762));   // title ▸ Settings (fourth item while the Night Watch is locked)
      await sleep(1500);
      await shot("02-settings");
      // The panel is 1660x1000 in the middle; rows sit 236 + 52 a row from the top, the right column's controls
      // centred at x 1580. In a browser Graphics fidelity is that column's tenth row (no Resolution row); the left
      // half of a stepper steps back, the right half on.
      for (let i = 0; i < Math.abs(opts.steps); i++) {
        await page.mouse.click(...at(opts.steps < 0 ? 1450 : 1710, 236 + 9 * 52));   // Graphics fidelity ‹ or ›
        await sleep(600);
      }
      await shot("03-settings-changed");
      await page.mouse.click(...at(960, 974));            // Done
      await sleep(1500);
      const saveAfter = await page.evaluate(readSave);
      const fidelityAfter = saveAfter ? saveAfter.quality : null;
      const fidelityWant = (((fidelityBefore + opts.steps) % 4) + 4) % 4;
      check(saveAfter && fidelityAfter === fidelityWant, `Settings ▸ Graphics fidelity changed and saved to IndexedDB (${fidelityBefore} → ${fidelityAfter})`);
      const mark = log.length;
      await page.reload({ waitUntil: "domcontentloaded" });
      const second = await reachTitle();
      await sleep(1500);
      const saveReloaded = await page.evaluate(readSave);
      const names = ["Low", "Medium", "High", "Ultra"];
      check(second.ready && saveReloaded && saveReloaded.quality === fidelityAfter && log.slice(mark).some((l) => l.includes(`Graphics fidelity ${names[fidelityAfter]}`)),
        `the setting survives a reload (save has ${saveReloaded?.quality}, the game started on ${names[fidelityAfter] ?? "?"})`);
      await shot("04-reloaded");

      // ---- 4. a short night: Begin the watch, the briefing, turn and focus the light, foghorn, pause and resume
      await page.mouse.click(...at(1300, 300));   // a click for sound in the reloaded page
      await sleep(500);
      const beginMark = log.length;
      await page.mouse.click(...at(300, 552));    // title ▸ Begin the watch
      await sleep(2500);
      await shot("05-briefing");
      await page.keyboard.press("Space");         // the briefing: "click, or press Space"
      const begun = await waitLog("[Game] night 1 under way", 15000, beginMark);
      check(begun, "Begin the watch and the briefing start night I");
      if (begun) {
        await sleep(2000);
        const f0 = await page.evaluate(() => window.__llFrames());
        const s0 = Date.now();
        for (let i = 0; i < 120; i++) {   // about 24 s of sweeping the bay, focusing now and then
          const a = i / 120 * Math.PI * 4;
          await page.mouse.move(W / 2 + Math.cos(a) * W * 0.35, H * 0.45 + Math.sin(a) * H * 0.25, { steps: 2 });
          if (i % 30 === 10) await page.mouse.down();
          if (i % 30 === 20) await page.mouse.up();
          if (i === 60) await page.mouse.click(W / 2, H / 2, { button: "right" });   // the foghorn
          await sleep(200);
        }
        const fps = ((await page.evaluate(() => window.__llFrames())) - f0) / ((Date.now() - s0) / 1000);
        info.nightFps = +fps.toFixed(1);
        await shot("06-night");
        const pauseMark = log.length;
        await page.keyboard.press("Escape");
        const paused = await waitLog("[Game] paused", 5000, pauseMark);
        await sleep(800);
        await shot("07-paused");
        await page.keyboard.press("Escape");
        const resumed = await waitLog("[Game] resumed", 5000, pauseMark);
        check(paused && resumed, `the night pauses and resumes (Esc); the night ran at about ${fps.toFixed(0)} fps`);
        await sleep(1500);
      }
      check(problems.length === 0, `no console errors, page errors or failed requests while playing${problems.length ? ": " + problems.slice(0, 3).join(" | ") : ""}`);
    }
    await context.close();
  } catch (e) {
    check(false, `the run itself failed: ${e && e.message ? e.message.split("\n")[0] : e}`);
  } finally {
    if (browser) await browser.close().catch(() => { });
    rmSync(tmp, { recursive: true, force: true });
  }
  info.results = results;
  info.problems = problems;
  writeFileSync(path.join(opts.out, `${engine}.log`), log.join("\n").slice(-4_000_000) + "\n");
  writeFileSync(path.join(opts.out, `${engine}.json`), JSON.stringify(info, null, 2) + "\n");
  return results.length > 0 && results.every((r) => r.ok);
}

// ------------------------------------------------------------------ main
let server = null;
let url = opts.url;
if (opts.serve) {
  server = await serve(opts.serve);
  url = url || `http://127.0.0.1:${opts.serve}/LastLight/`;
  console.log(`[check] serving ${path.join(root, "Builds", "Pages")} at http://127.0.0.1:${opts.serve}/ (pid ${process.pid})`);
}
let ok = true;
for (const engine of opts.browsers) ok = (await run(engine, url)) && ok;
if (server) server.close();
console.log(`[check] ${ok ? "PASS" : "FAIL"} (${opts.browsers.join(", ")} at ${url}); logs in ${opts.out}`);
process.exit(ok ? 0 : 1);
