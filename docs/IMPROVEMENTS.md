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

## Outcome of round 1 (2026-10-06)

All five items landed on `improvements`, one commit each. Verification was on the built Linux
player: 28/28 EditMode tests, the validation report, the `ui`, `input`, `nights`, `flash` and
`breakers` tours, and the audio check for the new clip. Screenshots are in
`docs/media/improvements/`.

1. **Dawn debrief.** As planned. The ui tour shows a one-wreck night and a failed night with
   named causes.
2. **Breakers ahead and full astern.** This was bigger than planned. The diagnosis showed that the
   wrecks ending the bot's Night Watches (and its night 9 lamp) were not late charts. Steamers and
   ferries struck Widow's Ledge reefs that had been charted well ahead, because their avoidance
   couldn't make the turn. Full astern therefore applies to any charted reef or sandbank the ship
   can't turn away from, not only late charts. Measured effect: the AutoKeeper earns three lamps
   on all 12 nights, the novice goes from 78 to 93 lamps out of 108 with 0 wrecks (was 9), and
   the bot now keeps every Night Watch the full 30 minutes. That's a real easing of difficulty. The
   README flags it, and a human playtest should decide whether the season needs tightening.
3. **Hints once per save and device-aware prompts.** As planned, plus the HUD's horn key. Settings
   moved to two columns with explicit pad navigation.
4. **Comfort and performance.** As planned. Reduce flashing went from 0.3 to 0.12 because 0.3
   still tripled screen brightness at a strike: peak brightness is now 0.27, against 0.72 with the
   setting off. Fog-night fps at render scale 70% versus 100% was measured on a heavily loaded
   shared machine, so the figures are rough (about 74 against 50 fps).
5. **macOS build.** It builds as a universal Mono app with Unity's ad-hoc signature and
   `com.nearbycoder.lastlight`. It's packaged with instructions for opening an unsigned app, has
   never been run on a Mac, and isn't published. `build-windows` is ready and stops with a clear
   message while the module is missing.

Still open: a real-gamepad pass, a listening pass, human playtests (now including the difficulty
change from item 2), Night Watch variety, Windows (module install), and macOS signing and
notarization (needs a Developer ID).

## Round 2 scope

Chosen from the open items above, plus what round 1 turned up. Round 1 left the Night Watch
easy, because the bot no longer loses a watch in 30 minutes, and it is the same night every time
apart from the ship order. Lost and lured ships differ only by colour, red against amber, and
PLAN's "?" for a lost captain was never built. The owner hasn't ruled on round 1's easier
tuning, so it stays the default. A Hard option adds a tougher choice without touching it.

Every item must keep `Tools/unity.sh test` green, keep the `ui`, `input` and `nights` tours at 0
errors, and keep the AutoKeeper winning all twelve nights on the default difficulty. Captures go
to `docs/media/improvements/round2/`. Tour output and logs stay under `Builds/` and `Logs/`.

### 1. Night Watch weather and records (it was "the same night every time")
- Every watch generates its own conditions from its seed:
  - one to three fog banks in different places, drifting in different directions
  - passing **squalls**, the first after five to nine minutes and then every four to seven,
    each lasting one to two minutes. A squall brings a current, rain and lightning, and the
    later ones are stronger.
  - wreckers arriving at random times and sites, with a mimic light late in long watches
- In the sim, a squall ramps in and out. The view follows it: rain, wind, swell, moonlight and
  the music's storm mood. The twelve nights keep their fixed storms, unchanged.
- The briefing gives a **forecast** in Ianto's voice, for example: fret off Black Hen, squalls
  from the north-west after about six minutes, the Corleys out early.
- The save keeps the **five best watches**. The briefing lists them, and the dawn card names the
  rank of the watch just kept.
- **Verify:** new tests check that ten seeds give different fog and squall layouts, that every
  generated watch passes the mission checks, that squall windows behave (the current is zero
  outside them and the ramp is smooth), and that the bot still keeps a watch for at least ten
  minutes. The report records how long watches last now. Screenshots: a squall in a watch, the
  forecast briefing, and the dawn card with a rank.

### 2. A Hard difficulty (today's tuning stays the default)
- Settings ▸ Difficulty: **Standard** (exactly today's game) or **Hard**. On Hard, ships lose
  heart about 30% faster, a chart fades after 15 s instead of 22, a lit buoy burns 20 s instead
  of 28, the crew give no breakers warning, and Night Watch ships come about 15% closer together.
- Records are shared. The results card and the watch table say when a night or watch was kept on
  Hard.
- **Verify:** a test proves Standard is unchanged (same outcomes and scores as today). A test
  shows the AutoKeeper still wins all twelve nights on Hard. The report adds a novice run on
  Hard. Screenshots: settings, and a Hard results card.

### 3. Telling lost and lured ships apart without colour
- A lost ship shows a bobbing **"?"** over its hull, as PLAN §12 intended. A lured ship shows a
  small **lantern** mark and a faint dashed **tether** to the false light that has it.
- **Verify:** a tour shot with a lost ship and a lured ship. The same crops are put through
  deuteranopia and protanopia simulation (Machado matrices) and set side by side, and I check the
  two states stay distinct by shape.

### 4. HUD and radio text size
- Settings ▸ Text size: 100%, 115% or 130%. It scales the in-night HUD: radio, hints, manifest,
  score, horn and markers. The menus are already large and are laid out at a fixed size.
- **Verify:** screenshots at 130% at both 1280×720 and 1920×1080, with no overlaps or clipping.

## Round 2 results (2026-10-06)

All four items landed on `improvements-2`, one commit each. Verification was on the built Linux
player: 55/55 EditMode tests, the validation report, and the `ui`, `input`, `watch`, `status`,
`breakers` and `nights` tours, all with 0 errors. Captures are in
`docs/media/improvements/round2/`.

1. **Night Watch weather and records.** As planned, except "the music's storm mood": the music
   was left alone, and rain, wind, swell and moonlight follow the squall. Tests show ten seeds give
   ten different nights, and a squall eases in and out with no jumps. **Finding:** the variety
   doesn't make the watch harder for the models. Both the bot and the novice keep all three
   watches the full 30 minutes on Standard, the novice with 1 or 2 wrecks. Whether Standard's
   Night Watch should get tougher (a faster ramp, or squalls that start sooner) is the owner's
   call. Hard below is the tougher option for now.
2. **Hard difficulty.** As planned. Standard is pinned by a test to the AutoKeeper's tuned scores
   on all twelve nights. On Hard the AutoKeeper still wins all twelve. The novice drops to 74 of
   108 lamps with 7 wrecks (Standard: 93 and 0; v0.1.0: 78 and 9), and its watches end after 24 to
   28 minutes.
3. **Lost and lured by shape.** A "?" over lost ships, and a lantern plus a dashed tether to the
   false light over lured ones. `Tools/cvd_sim.py` shows the red and amber rings converging under
   deuteranopia and protanopia while the shapes stay distinct (`lost_lured_cvd.jpg`). The
   manifest icons at the top still differ by colour only.
4. **HUD text size.** As planned: 100, 115 or 130%. Checked at 1280×720 and 1920×1080, and in a
   Hard Night Watch squall at 720p.

Still open: Standard Night Watch tuning (owner), the round 1 difficulty decision (owner), a
real-gamepad pass, a listening pass, human playtests, Windows (module), and macOS signing
(Developer ID).

## Round 3 scope

Chosen from what's still open after round 2 and from a read of the code and the running game
for things a first-time player runs into. The owner hasn't ruled on Standard's difficulty, so
nothing here changes balance on either difficulty. A real-gamepad pass, a listening pass and
playtests still need hardware or people.

Every item must keep `Tools/unity.sh test` green, keep the `ui`, `input` and `status` tours at
0 errors, and leave the AutoKeeper's pinned Standard scores alone. Captures go to
`docs/media/improvements/round3/`. Tour output and logs stay under `Builds/round3/`. The real
save (`~/.config/unity3d/Gannet Head/Last Light/prefs`) is checksummed before and after.

### 1. Manifest icons that read without colour
Round 2 left the ship icons in the top bar telling their states apart by colour only (white,
red, amber, gold). Under deuteranopia the red and amber pulses converge.
- Each icon gets a small mark under it drawn as a shape: a **?** while the ship is Lost, a
  **lantern** while it's Lured, a **cross** once it's wrecked, and a **tick** once it's home.
  Sailing ships have no mark.
- **Verify:** the `status` tour logs which mark each manifest icon shows when a lost ship and a
  lured ship are on screen, and fails if they don't match the ships' states. The manifest crops
  go through `Tools/cvd_sim.py`, and I read the result.

### 2. Say how many wrecks the night allows
Nights allow one or two wrecks (nights 8, 10, 11 and 12 allow two), and one more ends the night
at once. Nothing tells the player that: the briefing doesn't mention it, and only the Night
Watch shows its allowance on the HUD.
- The briefing card says it in a line under the date, for example *"The Board allows one wreck
  tonight. A second ends the night."*
- During the night, a small row of hulls beside the manifest shows the wrecks the Board allows,
  as the Night Watch strip already does. Each wreck greys one out, and the last one pulses so
  the next wreck is clearly the one that ends the night.
- **Verify:** the `ui` tour shoots a night 8 briefing (two allowed), and a staged wreck on night 2
  shows the strip with the allowance spent. The tour logs the strip's state against the sim's
  wreck count. Checked at 1280×720 and at 130% HUD text size for overlaps with the manifest.

### 3. A radio log in the pause menu
The radio shows one call at a time, and calls carry instructions ("Light the Hen Bell before the
Auk gets there"). A call that typed out while the player was busy is gone.
- The radio keeps the night's calls. The pause menu shows the latest six beside the menu, newest
  at the bottom, with the speaker's name. It is cleared at the start of each night.
- **Verify:** a new EditMode test checks that the log keeps calls in order, caps its length and
  clears. The `input` tour pauses after the opening calls, checks the log matches the radio's
  history, and saves a screenshot I'll read at 1280×720 and 1920×1080.

### 4. Focus: hold or toggle, and two control fixes
Focus means holding a button for most of a night. That's tiring, and impossible for some
players.
- Settings ▸ **Focus**: *Hold* (as now) or *Toggle*. In Toggle, a press of the left button,
  Shift, W, ↑ or a trigger switches focus on or off. Focus switches off when a night starts or
  restarts. Hints, the title strip and the fog card say "press" or "click" instead of "hold".
- The ending's "Hold the left button, or Space" prompt follows the device ("Hold A" on a pad).
- The mouse pointer hides while a gamepad is in use during a night and returns with the mouse.
- **Verify:** the `input` tour switches to Toggle and checks with the simulated mouse,
  keyboard and pad that one press focuses and stays focused after release, a second press
  unfocuses, and Hold mode still works. It also checks the pointer hides on pad input and comes
  back on mouse movement. Screenshot of the setting.

### Not in this round
- **Standard difficulty and the Night Watch ramp**: the owner's call.
- **Real gamepad**: an 8BitDo receiver is plugged into the development machine, but nobody can
  press its buttons in this session. The input tour will log which devices Unity sees, and
  that's all that can be claimed.
- **Listening pass, playtests, Windows, macOS signing**: need people, a module install or an
  Apple account.

## Round 3 results (2026-10-06)

All four items landed on `improvements-3`, one commit each. Verification was on the built Linux
player: 57/57 EditMode tests (55 before, plus two for the radio log), and the `ui`, `input`,
`status`, `radiolog` and `ending` tours, all with 0 errors and every check passing. The real save
file's checksum was the same before and after every run. Captures are in
`docs/media/improvements/round3/`. No simulation code changed, and the test that pins the
AutoKeeper's Standard scores still passes, so balance on both difficulties is unchanged. The
validation report wasn't re-run for that reason.

1. **Manifest marks by shape.** As planned. The line-art lantern was too thin to read at 20 px,
   so the mark uses a solid lantern silhouette. The `status` tour checks all four marks against
   their ships, and `manifest_cvd.jpg` shows the ?, lantern, cross and tick staying distinct under
   deuteranopia and protanopia. Two pieces of tour staging: night 10's wreck allowance is lifted
   while the tour waits for a lure, because the night sometimes failed first and made round 2's
   tour flaky, and home and wrecked are staged on a fresh night 2.
2. **Wreck allowance.** As planned, except where the briefing puts the line. It sits just above
   "Begin the watch", because there was no room under the date. The HUD row under the night's
   title shows a hull per allowed wreck and words ("ONE WRECK ALLOWED", then "NEXT WRECK ENDS THE
   NIGHT", which pulses). The `ui` tour checks it after each staged wreck. Checked on night 11
   (12 ships) at 1280×720 with 130% HUD text: no overlaps.
3. **Radio log.** As planned. It shows up to six calls, as many as fit in the panel. The panel
   sizes itself to its calls and stops above the HUD's radio panel even at 130% HUD text
   (checked at 1280×720 and 1920×1080).
4. **Focus hold or toggle.** As planned. The `input` tour checks the left button, Shift and the
   right trigger in Toggle (press focuses, it stays focused after release, a second press widens),
   that a click in the pause menu doesn't switch it, that Hold still works, and that the pointer
   hides on pad input and returns with the mouse. The ending's prompt follows the device in code,
   but the pad wording was only checked by reading the code, because the `ending` tour holds the
   prompt on its own and doesn't use a pad.

**Real gamepad.** The `input` tour now logs the devices Unity sees. With an 8BitDo receiver
plugged in, Unity listed only the mouse, keyboard and touchscreen, so the controller was probably
switched off. A real-pad pass still needs someone to hold it.

Still open: Standard difficulty and the Night Watch ramp (owner), a real-gamepad pass, a
listening pass, human playtests, Windows (module install) and macOS signing (Developer ID).
