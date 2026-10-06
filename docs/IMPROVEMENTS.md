# Improvements: round 1 assessment

Written on 2026-10-06 against v0.1.0 (`cc2debc`), on the `improvements` branch. It covers what
the baseline checks show, what a real player would run into, a ranked list of candidates, and
the scope proposed for this round.

## Baseline (what was run, and what it showed)

| Check | Result |
|---|---|
| `Tools/unity.sh test` (EditMode, batch) | **22/22 pass** in 22 s: data integrity, reachability, route safety, the AutoKeeper wins all 12 nights, and it keeps three generated Night Watches for 10 minutes or more |
| `Tools/unity.sh build-linux` | **Succeeds**: 111 MB, 0 errors |
| `Tools/tour.sh report` (built player) | Identical to the README. The AutoKeeper wins 12/12 (three lamps on 11; one lamp on night 9). The novice wins every night 3/3, with lamps 9,9,9,8,7,6,**3,3**,8,5,5,6 out of 9. Night Watches lasted 24.7, 20.2 and 15.9 minutes |
| `Tools/tour.sh ui` | Passes. Title, logbook, settings (the 1280×720 resolution switch applies and is restored), briefing, play, pause and results screenshots all look right |
| `Tools/tour.sh nights` | Passes with 0 errors. At 1600×900 on High: night 5 (fog) ran at **38 to 42 fps**, and the other nights at 59 to 115 fps. The machine's load average was about 38 during the run (other sessions), so the absolute numbers are pessimistic, but fog remains the costly case |
| `Tools/tour.sh input` | Passes. Mouse aim lands within 0.1°, and focus, horn and pause work. The simulated pad covers the d-pad, A, right stick, trigger and Start/B |

Not run: `Tools/audio_check.py` (the audio is unchanged since release, and it passed then), the
`watch`/`ending` tours, and the trailer (out of scope for this phase). There was no real
gamepad, no listening test and no human playtest.

## What a real player hits (findings from the code and the running game)

1. **Hints repeat every night, and on every retry.** `Feedback.hintSeen` and `fired` live on the
   per-night `Feedback` instance, so the shoal, horn and similar hints fire again each night their
   trigger occurs. In the night 9 capture, *"Steamers run aground on sandbanks…"* sits at the top
   of the screen on the night that is supposed to teach wreckers. A player retrying night 2 sees the
   chart hint again every time.
2. **Prompts assume a mouse and keyboard.** The hint text ("Move the mouse…", "Press SPACE…",
   "Hold the left button…"), the title footer and the briefing's "click, or press Space" ignore
   the gamepad, even though the game otherwise fully supports one.
3. **Dawn says *that* you failed, never *why*.** `ResultsScreen.Setup` prints "Too many ships were
   lost. The Board will hear of it." The sim already records `WreckCause` (reef group, shoal,
   stack, shore), `EverLost` and `EverLured` for each ship, and none of it reaches the player.
   PLAN §18's own test ("Steady hand: the *Little Auk* got lost once") isn't met. The results
   card also has an empty band of about 150 px between the stats and the score, where this
   information belongs.
4. **Wrecks on late-charted reefs feel abrupt.** This is the README's Night Watch known issue,
   and it applies in the campaign too. `SimEventType.ShipDanger` is declared but **never
   emitted**, so a warning beat was planned and never built. Once a reef is charted inside a
   steamer's turning circle, the captain simply turns too late.
5. **The game keeps running when the window loses focus.** `runInBackground: 1`, and nothing
   handles `OnApplicationFocus`. Alt-tabbing mid-night lets ships drift Lost and wreck. The same
   happens when a pad disconnects.
6. **Fog is the frame-rate risk.** `Atmosphere.shader` raymarches at full resolution, and the only
   control is the step count (10/16/24). PLAN §17 listed "half-res option" as a mitigation, but it
   wasn't built. The URP asset already exposes `m_RenderScale`.
7. **Photosensitivity.** Storm nights flash the whole bay (`_LLFlash`) every 9 to 16 seconds,
   and there's no way to soften it.
8. **The novice curve has a hole at nights 7 and 8** (3/9 lamps and 3 wrecks each, then night 9
   recovers to 8/9). This comes from a bot model, not people, so it's a lead rather than a fact.
9. **The Night Watch is the same night each time, apart from the ship order.** It has fixed fog
   positions, no storm (the README says "every hazard out"), and wreckers always arrive at
   4 and 12 minutes. Its only records are best score, ships and time.
10. **Platform reach.** The blog lists Windows · macOS · Linux, but the release is Linux-only.
    This editor has the Mac and WebGL modules but not Windows. `applicationIdentifier.Standalone`
    is still the URP template's `com.Unity-Technologies.com.unity.template.urp-blank`, which
    becomes the macOS bundle id and the name of the macOS PlayerPrefs file.

## Ranked candidates

Impact is for a real player. Effort: S = under half a day, M = about a day, L = several days.

| # | Improvement | Impact | Effort | Risk |
|---|---|---|---|---|
| 1 | **Dawn debrief**: a short per-ship account on the results card (what wrecked whom, who got Lost or Lured, and which lamp was missed and why) | High: turns a fail into "I see, one more" | S–M | Low (UI only, data already exists) |
| 2 | **Danger telegraph and late-chart reprieve**: emit `ShipDanger` when a ship is a few seconds from an uncharted hazard on its course (the ring flickers, with a sound and a captain's line), and let a captain who sees a reef charted inside his turning circle go full astern for a near miss | High: fixes the known abrupt wreck and makes the core skill readable | M | Medium (touches steering and balance; must keep the proofs green) |
| 3 | **Hints shown once per save, and prompts that match the device** (pad or mouse and keyboard), with a "Reset hints" option | High for the first five minutes | S | Low |
| 4 | **Auto-pause on focus loss and pad disconnect** | Medium–high (prevents unfair losses) | S | Low |
| 5 | **macOS build and package** (Mono, Intel and Apple Silicon, unsigned), a proper bundle id, and README instructions | High for reach (the blog promises it) | M | Medium (can't be run on a Mac here) |
| 6 | **Render scale setting** (100/85/70%) for the 3D camera only, so the UI stays sharp | Medium (fog frame rate on iGPUs) | S | Low |
| 7 | **Reduce flashing option** (scales lightning and flare flashes) | Medium (accessibility) | S | Low |
| 8 | **Windows build**: a `BuildWindows` entry point and a package script | High for reach | S once unblocked | **Blocked**: the owner has to install Windows Build Support (Mono) in Unity Hub |
| 9 | **Night Watch variety**: weather fronts (squalls, fog thickening and moving), randomized wrecker timing, a local top-five table | Medium (replayability) | M | Medium (bot watch-length test needs retuning) |
| 10 | **Smooth the night 7–8 spike** (stagger the first dark ship, ease Squall's current) | Medium, speculative without playtests | S | Low–medium |
| 11 | **Real gamepad pass** (dead zones, glyphs, rumble on wreck and horn) | Medium | S–M | Blocked on hardware |
| 12 | **WebGL build** for a browser demo | Medium (reach) | L | High: the full-res raymarch, audio pitch and loops on web, untested browsers |
| 13 | **Audio listening pass** and **human playtest** | High | — | Needs people, not code |
| 14 | XWayland startup hang | Low (the launcher already avoids it) | ? | Unknown root cause |

## Proposed scope for this round

These five items do the most for a first-time player's five minutes and for reach, without
reopening the art or the content. Every item must leave `Tools/unity.sh test` at 22/22 (or more,
with new tests) and the `ui`, `nights` and `input` tours at 0 errors.

### 1. Dawn debrief (candidate 1)
- The results card shows up to four lines in the empty band, most severe first, for example:
  *"Little Auk: struck the Merrow Teeth (uncharted)"*, *"SS Calloway: aground on the Long Sands"*,
  *"Kittiwake: lured toward Corley Cove, then saved"*, *"Curlew lost its way twice"*. When more
  happened than fits, a final line reads "+N more".
- The verdict names the missed lamp (*"Steady hand: the Little Auk lost its way once"*), as in PLAN §18.
- A failed night names the wreck that ended it.
- The sim records, for each ship, its state at the moment of the wreck (Sailing, Lost or Lured),
  whether the hazard was charted, and its Lost and Lured counts. These are plain fields with no
  change to behaviour.
- **Verify:** a new EditMode test plays a staged night (the AutoKeeper with one ship ignored, as
  the trailer does) and checks the debrief lines. The `ui` tour gains a results shot after a
  staged wreck, and I'll read the screenshot.

### 2. Danger telegraph and late-chart reprieve (candidate 2)
- `ShipDanger` fires once per hazard approach when a ship's projected track at its current
  heading meets an uncharted reef or shoal within about 4 s (steamers) or 2.5 s (trawlers). The
  ring flickers amber-white, a short "breakers" cue plays, and named captains have a radio line.
  The warning arrives too late to reach the hazard from far away, so reading the sea ahead still
  matters.
- Reprieve: when a hazard is charted while already inside a ship's turning envelope, the ship
  goes full astern (speed falls to about 25% over about 1 s) while it turns. That turns some
  late charts into visible near misses. A steamer at full speed with no chart still wrecks.
- **Verify:** the EditMode tests stay green, and a new test stages a late chart on the
  Widow's Ledge–Teeth gap that wrecked before the change and doesn't after. The report is
  re-run: AutoKeeper Night Watch lengths should rise, and the novice lamps per night must not drop
  below today's. The README's known-issue entry is updated to match what's measured.

### 3. Hints once per save, and device-aware prompts (candidate 3)
- Seen hint ids persist in `SaveData` (a new list field; old saves load with it empty). A hint
  shows again only after Settings ▸ "Reset hints".
- Hint text, the title footer and the briefing prompt switch between mouse and keyboard wording
  and gamepad wording (right stick, RT, A, Start), based on the last device used.
- **Verify:** an EditMode or tour check that a hint id seen on night 2 doesn't show on night 4.
  The `input` tour captures the hint and title footer in both modes, and I'll read those
  screenshots.

### 4. Comfort and performance settings (candidates 4, 6, 7)
- The game pauses (the normal pause menu) when the window loses focus during a night, or when the
  active gamepad disconnects. It stays paused until the player resumes.
- A new "Render scale" setting (100%, 85%, 70%) drives the URP render scale, while the UI canvases
  stay at native resolution.
- A new "Reduce flashing" setting scales `_LLFlash` and flare flashes to about 30%.
- **Verify:** the `input` tour simulates focus loss and checks the state is Paused. The `nights`
  tour runs night 5 at 100% and at 70%, and I'll record both fps figures under similar load. A
  screenshot during a lightning strike with the setting on and off.

### 5. macOS build (candidate 5), with Windows prepared
- `BuildScript.BuildMac` (Mono, universal Intel and Apple Silicon) and `Tools/unity.sh build-mac`.
  `Tools/package.sh` gains a `mac` target that zips the `.app` with its executable bit kept, plus
  the licenses and a README.txt explaining the Gatekeeper right-click ▸ Open step for an
  unsigned app.
- `applicationIdentifier.Standalone` becomes `com.nearbycoder.lastlight`. On Linux the save path
  comes from the company and product names, which stay unchanged, so existing Linux saves are
  unaffected.
- `BuildWindows` and a Windows package target are added, and fail with a clear message while the
  module is missing.
- **Verify:** the build succeeds. `file` reports a universal Mach-O binary. The zip round-trips
  with the executable bit intact. The Linux build and tours still pass afterwards (switching the
  build target reimports assets, so the Linux build is rebuilt and re-toured last). The README says
  plainly that the macOS build hasn't been run on a Mac.

### Not in this round, and why
Night Watch variety (9) and the night 7–8 rebalance (10) wait for human feedback, or for item 2's
effect on the curve. WebGL (12) is a large, risky port that doesn't fit this game's GPU budget.
The real-pad pass, the listening pass and playtesting need hardware or people.

## Decisions for the owner
- **Windows:** install *Windows Build Support (Mono)* for 6000.6.2f1 in Unity Hub if a Windows
  build is wanted. Everything else for it can be prepared in advance.
- **macOS:** do you want an unsigned, untested-on-hardware macOS zip published in a release, or
  only built and kept locally until someone can run it on a Mac? Signing and notarization need an
  Apple Developer account.
- **WebGL:** not recommended for this round. Say so if a browser demo matters more than the items
  above.
