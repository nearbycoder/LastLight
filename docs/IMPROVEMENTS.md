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

## Round 4 scope

Rounds 1 to 3 worked through the ranked list. What's left on it is blocked: Windows (module
install), macOS signing (Developer ID), a real gamepad and a listening pass (hardware and people),
and Standard's balance (the owner). WebGL is still judged too big and risky. So this round's items
come from a fresh read of the code and of the running game at screen shapes other than 16:9.
None of them changes the simulation, so the pinned Standard scores and both difficulties stay as
they are.

Baseline, before any change: the `ui` tour in a 1280×1024 window (5:4). The Settings panel runs
off both sides of the screen, the play view crops the bay so the harbour, where ships are bound,
is mostly off screen at the bottom left, and the hint panel covers the HUD's "ONE WRECK ALLOWED"
row. The same tour's resolution-switch check printed FAIL at that window size (the window stayed
1280×1024). That one is followed up under item 1.

Every item must keep `Tools/unity.sh test` green and the `ui`, `input` and `ending` tours at 0
errors. Captures go to `docs/media/improvements/round4/`. Tour output and logs stay under
`Builds/round4/`. The real save directory (`~/.config/unity3d/Gannet Head/Last Light/`) is
checksummed before and after (`Builds/round4/save_before.txt`).

### 1. Every screen shape, not only 16:9
Steam Deck and many laptops are 16:10, some monitors are 4:3 or 5:4, and ultrawides are 21:9.
- Menus and HUD lay out in a 1920×1080 box that always fits on screen (the canvas expands
  instead of averaging width and height), so nothing runs off the edges at any shape, and 16:9
  looks exactly as it does now.
- The camera keeps the bay's full width in view on screens narrower than 16:9 by widening the
  vertical field of view. Wider screens show more of the coast at the sides, as now.
- **Verify:** a new `screens` tour visits the title, logbook, settings, briefing, a night with
  130% HUD text, pause with the radio log, and a results card. It checks that each panel lies
  inside the screen and that the HUD's corner blocks, manifest and hint don't overlap, and checks
  that the harbour and the far reefs project inside the view. It runs at 1280×800 (16:10),
  1280×1024 (5:4), 2560×1080 (21:9) and 1600×900 (16:9, which must be unchanged). I'll read
  the screenshots.

### 2. End a Night Watch and keep it
A watch runs until the third wreck, which can take half an hour or more. Today the only way to
stop is "Leave the lighthouse", which throws the watch away, so a player who has to stop loses
their best run.
- In a watch, the pause menu's "Restart the night" becomes **"End the watch"**. It goes
  straight to the dawn card, and the watch is recorded and ranked as if the last wreck had
  ended it. The card says the keeper stood the watch down, and "Keep watch again" starts a new
  one, so nothing is lost by removing Restart there.
- **Verify:** a `watchend` tour plays a watch with the AutoKeeper for a couple of minutes,
  pauses, ends the watch, and checks that the results card shows, that the watch was recorded at
  the right rank with its score, ships and time, and that the twelve nights' pause menu still
  says "Restart the night". It runs with `-llFresh -llSeasonDone`, so the real save is untouched.

### 3. Skip the ending, and the credits on any device
The ending runs about a minute before the credits, and it plays again every time night 12 is
replayed from the logbook. The credits can be skipped only with Esc, after 4 s, and nothing on
screen says so.
- Esc, Start or B during the ending shows "Press again to skip". A second press within a few
  seconds goes straight to the title, as the ending does when it finishes (the ending counts as
  seen). The prompt follows the device.
- **Verify:** the `ending` tour gains a skip run: it presses the simulated key once (checks the
  prompt shows and the ending keeps going), then twice (checks the title is back within a few
  seconds, with dawn and the lens reset), and the same with the simulated pad's Start.

### 4. Game speed (an accessibility assist)
Nights demand fast, continuous aiming, and there's no way to slow them down for players who need
it.
- Settings ▸ **Game speed**: 100% (the default), 85% or 70%. It slows the simulation only: the
  ships, the beam's weight, fog, storms and wreckers all run slower together, so outcomes for the
  same inputs are unchanged and only the player gets more time. Music, radio typing and menus
  keep their pace.
- The dawn card heading and the table of best watches say when the speed was below 100%, as they
  already do for Hard.
- **Verify:** a tour measures sim seconds per real second at each speed (expected 1.0, 0.85 and
  0.7, within 5%, with the load recorded) and checks the results heading. An EditMode test
  checks the watch table keeps the mark. Screenshot of the setting.

### 5. Controls in the pause menu
The controls appear on the title strip and in the one-time hints. Mid-night, a player who forgot
how to focus or sound the horn has nowhere to look.
- The pause menu shows a one-line control strip at the bottom, the same as the title's. It
  follows the device and the Focus setting.
- **Verify:** the `input` tour's pause step checks the strip's wording in mouse and pad modes,
  and I'll read the screenshots.

### Not in this round
- **Standard difficulty and the Night Watch ramp**: the owner's call.
- **Real gamepad, listening pass, playtests, Windows, macOS signing, WebGL**: as before.

## Round 4 results (2026-10-06)

All five items landed on `improvements-4`, one commit each. Verification was on the built Linux
player: 58/58 EditMode tests (57 before, plus one for standing a watch down), and the `input`,
`ui`, `status`, `radiolog`, `ending`, `endingskip`, `watchend` and `speed` tours, plus `screens` at
four window sizes, all with 0 errors and every check passing (110 checks in the final pass, `Builds/round4/final.log`). The load
average during that pass was 15 to 26, except `watchend` (80) and `speed` (50), whose checks run on
simulation time and on the game's own frame time. Captures are in
`docs/media/improvements/round4/`. The only simulation change is `SimWorld.StandDown`, which only
the pause menu calls and only in a watch, so the AutoKeeper's pinned Standard scores still pass and
balance is unchanged on both difficulties. The validation report wasn't re-run. The real save's
checksum was the same before and after. Its file was rewritten once with identical contents during
a batch editor build, and the test runner updated `TestResults.xml` beside it, as in round 3.

1. **Every screen shape.** As planned, plus a fix the new tour found at 16:9. The canvases now
   expand around the 1920×1080 layout (`ScreenMatchMode.Expand`), and screens narrower than 16:9
   widen the camera's vertical field of view (30° becomes 41.7° at 5:4 in the play view) so the
   bay keeps its 16:9 width. The `screens` tour was run on the old layout first: at 5:4 the
   harbour, two Widow's Ledge reefs, a Hen's Chicks reef and the fairway buoy were off screen,
   Settings and the radio log ran off the edge, and at 21:9 the logbook was taller than the
   screen. **At 16:9 with 130% HUD text,** night 11's twelve-ship manifest ran into the night's
   title and the hint panel covered the wreck allowance row ("ONE MORE WRECK ALLOWED"). Round 3's
   130% check had missed it because the row's text grows after a wreck. The HUD now lays its
   top bar out itself: the manifest stays centred while it fits, otherwise it moves into the
   space between the title and the score and shrinks if it must (to 77–80% here), and the hint
   drops below the night's block when they'd meet. At 16:9 with 100% text nothing moves, apart
   from the hint sitting 4 px lower to clear the manifest's marks. Afterwards every check passes
   at 1280×800, 1280×1024, 1600×900 and 2560×1080 (`screens_*` captures). Westpoint's false light
   and its rock sit just past the left edge at 16:9, as released, so the tour holds them to "no
   further out than 16:9" (within 6%) rather than reframing the camera, which would change every
   capture and the trailer. That's listed under known issues.
2. **End the watch.** As planned. The `watchend` tour kept a watch for 150 s of sim time, ended it
   from the pause menu, and found it on the dawn card ("You stood the watch down after 2:30, 5
   ships home.") and fourth in the table, with the same score, ships and time. Night II's pause
   menu still offers "Restart the night".
3. **Skip the ending.** As planned. The prompt went to the top right, because at the bottom it sat
   where the radio panel appears. A skipped ending also restarts the title's sea, because the
   ending's own scene (one steamer, the lens winding down) would otherwise linger behind the title.
   The full ending, watched without skipping, is unchanged. `endingskip` passes with Esc in the
   dawn scene, a pad's Start in the credits and a pad's B, each back at the title 1.5 s after the
   second press. One press alone lets the ending go on, and the prompt lapses after 3 s.
4. **Game speed.** As planned, with one change to the verification. The watch table's mark is
   checked by the `speed` tour (with a blank save) rather than an EditMode test, because recording
   a watch calls `Save()`, which in the editor writes the same prefs file the player uses. Measured
   at a load of about 25: 1.000, 0.851 and 0.701 sim seconds per real second at 100, 85 and 70%.
   The dawn heading reads "DAWN · NIGHT II · 70% SPEED", and an 85% watch is kept and listed as
   "300 (85%)". Settings gained a row, so the panel is 60 units taller and the `input` tour's
   d-pad walk takes ten steps down the right column instead of nine.
5. **Controls in the pause menu.** Changed from a strip to a card. A strip under the menu would
   have run into the HUD's radio panel at 130% text, and one at the top into a dropped hint, so
   the controls are a card left of the menu that mirrors the radio log. The `input` tour checks
   the mouse-and-keys and pad wordings, and `screens` checks the card fits.

**One process slip.** While committing item 1 on its own, I copied three backup files into the
shared /tmp as `Game.cs`, `Screens.cs` and `MoreScreens.cs` by mistake, and the same command then
deleted those three names. Had another session kept files with exactly those names there, they
would have been overwritten and removed. There's no way to tell now.

Still open: Standard difficulty and the Night Watch ramp (owner), whether slowed scores should be
kept apart from full-speed ones (owner), a real-gamepad pass, a listening pass, human playtests,
fullscreen and real-hardware checks of other screen shapes (a Steam Deck in particular), Windows
(module install) and macOS signing (Developer ID).

## Round 5 scope

What's left on the ranked list is still blocked (Windows module, macOS signing, a real gamepad,
a listening pass, playtests) or waiting on the owner (Standard's balance, the Night Watch ramp,
whether slowed runs share records). These items come from a fresh read of the code and of round
4's captures, and from the round 4 known issues. None of them changes the simulation, so the
pinned Standard scores and both difficulties stay as they are.

Every item must keep `Tools/unity.sh test` green and the `ui`, `input`, `watchend` and `screens`
tours at 0 errors. Captures go to `docs/media/improvements/round5/`. Tour output and logs stay
under `Builds/round5/`. The real save directory is checksummed before and after
(`Builds/round5/save_before.txt`).

### 1. Markers at the edge for what's out of frame
Round 4's `screens` tour found that Westpoint's false light and its rock sit just past the left
edge at 16:9, and Corley Cove's lantern (night 9, the night that teaches wreckers) just past the
right. A lit false light there, or a ship lured toward it, can be off screen with nothing to say so.
Reframing the camera would change every capture and the trailer, so instead:
- A burning false light whose lantern is outside the frame shows a pulsing lantern marker pinned
  inside the nearest edge, with a chevron pointing out toward it. It goes when the lantern is
  doused or comes into view.
- A lost ship's "?" and a lured ship's lantern stay inside the screen edge (with a chevron) when
  the ship itself is outside it.
- **Verify:** a new `offscreen` tour plays nights 9 and 10 at 16:9, waits for each off-frame
  lantern to burn, and checks its marker is showing, wholly on screen and on the lantern's side,
  and that it goes when the lantern is doused. It also checks a lured ship's glyph stays on screen.
  Screenshots read by me.

### 2. Ask before throwing a night away
In the pause menu, "Restart the night", "Keeper's logbook" and "Leave the lighthouse" abandon the
night at once, and in a Night Watch "Leave the lighthouse" throws away a watch that may be half an
hour long, one item away from "End the watch", which keeps it.
- Those items, and "End the watch", ask first on a small card: what will happen ("Tonight's watch
  won't be kept" or, in a watch, "This watch won't be kept. End the watch keeps it"), with **Stay**
  selected so a double press does nothing. Esc, B or Stay goes back to the menu.
- **Verify:** a new `confirm` tour checks each item with the mouse path, the keyboard and the
  simulated pad: the card shows, the night is still paused and unchanged, back returns to the
  menu, and confirming does what the item says. The `watchend` and `ui` tours confirm through the
  card.

### 3. Brightness
The game is dark by design, and on a bright laptop screen or in a lit room it can be hard to read
the water.
- Settings ▸ **Brightness**: five steps, with the middle one exactly as now. It changes the 3D
  scene's exposure only; menus and the HUD are unchanged.
- **Verify:** a tour measures the scene's mean brightness on a frozen frame at each step (it must
  rise with each step, and the middle step must match the frame without the setting) and saves the
  darkest and brightest. `screens` checks the taller Settings panel fits at all four shapes.

### 4. Rebindable keys
The keyboard controls are fixed (A/D or arrows, Shift/W/↑, Space). Remapping is one of the basic
accessibility guidelines, and it's what a player with one hand, a non-QWERTY keyboard or a
different habit needs.
- Settings ▸ **Keys**: turn left, turn right, focus and foghorn each have three key slots
  (defaults as now: A and ←, D and →, Shift, W and ↑, Space). Choose a slot and press a key to set
  it; Backspace clears it; Esc cancels. A key already used elsewhere moves to the new action. Esc
  and P stay as pause. "Reset keys" restores the defaults. The mouse buttons and the gamepad stay
  as they are.
- Every prompt that names a key (title strip, pause card, hints, briefing, the HUD's horn key, the
  ending) names the bound key, using the keyboard layout's own label for it.
- **Verify:** an EditMode test covers binding, moving a key between actions, clearing, the
  reserved keys and reset, without touching the save. A `keys` tour rebinds the horn and focus with
  the simulated keyboard through the Settings panel, checks the new keys work in a night and the
  old ones don't, checks the prompts name the new keys, and resets.

### 5. The resolution switch in a 1280×1024 window (time-boxed)
Round 4 saw the `ui` tour's switch to 1280×720 fail once when the game started in a 1280×1024
window. I'll try to reproduce it and fix it if the cause is in the game; if it's the compositor,
I'll say so.

### Not in this round
- **Standard difficulty, the Night Watch ramp and slowed records**: the owner's call.
- **Gamepad remapping**: no real pad to check it on.
- **Fullscreen at other shapes and a Steam Deck**: switching this shared machine's display to
  fullscreen would disturb the other sessions, and there's no Deck.

## Round 5 results (2026-10-07)

Four items landed on `improvements-5`, one commit each, and item 5 was investigated. Verification
was on the built Linux player: 64/64 EditMode tests (58 before, plus six for the key bindings),
and a final pass of 16 tour runs (`input`, `ui`, `status`, `radiolog`, `ending`, `endingskip`,
`watchend`, `speed`, the new `offscreen`, `confirm`, `brightness` and `keys`, and `screens` at
four window sizes with 130% HUD text). All had 0 errors: 177 checks passed and none failed
(`Builds/round5/final.log`), at load averages of 10 to 19. No simulation code changed, so the
pinned Standard scores still pass and balance is unchanged on both difficulties; the validation
report wasn't re-run. The real prefs file's checksum was the same before and after. Its timestamp
moved when a batch editor build rewrote it with identical contents, and the test runner updated
`TestResults.xml` beside it, as in rounds 3 and 4. Captures are in
`docs/media/improvements/round5/`.

1. **Edge markers.** As planned, with one addition: a lantern within 28 px of the edge counts as
   out of frame, because West Point's sits at viewport x 0.00 at 16:9, cut in half. The marker
   stays below the top bar and clear of the radio panel (Corley Cove's would otherwise sit on it).
   A lured ship heading for West Point's rock is out of frame for only a moment before it strikes,
   because the rock is just 5% outside, so the tour checks its mark on the first frame after it
   leaves the view. `offscreen` passes at 1600×900, 1280×1024 and 2560×1080. At 21:9 both
   lanterns are in frame and no marker shows, as intended. The tour turns screen shake off,
   because the camera's lean toward each wreck moved the frame in the first run.
2. **Ask before throwing a night away.** As planned. `confirm` checks the keyboard (Enter twice
   stays, Esc backs out to the menu, the night doesn't move), a click on Restart, and the simulated
   pad (A asks, B backs out, A then down and A opens the logbook). In a watch, Leave offers Stay,
   End the watch or Leave anyway, and Leave anyway keeps nothing. The card fits at all four shapes.
3. **Brightness.** As planned. The upper steps were widened after the first measurement, because
   +0.7 stop brightened the scene by only a third. The steps are −0.5, −0.25, 0, +0.5 and +1 stop
   on the graded exposure of 0.45. The scene's mean brightness on a held frame of night III was
   0.133, 0.148, 0.164, 0.202 and 0.246. Standard measured the same before and after the sweep.
   The Settings panel is 60 units taller.
4. **Rebindable keys.** As planned. Binding is keyboard-only: Enter (or a click) on a slot turns
   menu navigation off until the key is chosen, so arrows and Enter can be bound, and turns it back
   on a frame later so the key doesn't also move the selection. Esc and P back out of waiting and
   can't be bound. The `keys` tour rebinds the horn to H, focus to J and ↓ to turn left, moves W
   from focus to the horn, then plays night V: H sounds the horn and Space doesn't, J focuses and
   Shift doesn't, and ↓ turns the lens. The title strip, fog card, horn gauge and pause card name
   H and J. Reset puts the defaults back. The briefing's "press Space" and the ending's "hold
   Space" are menu-like and stay on Space, so their prompts are still right. Key names come from
   the keyboard layout through the Input System, but only a US layout was tried.
5. **The resolution switch at 5:4.** Not reproduced. The `ui` tour's switch from a 1280×1024
   window to 1280×720 passed three times (loads 22 to 43), and the window size changed within
   0.01 s. The check now waits up to 5 s and logs how long the resize took, so a slow compositor
   would show up as a number. Round 4's failure remains unexplained.

**Found on the way.** The `screens` tour's framing check failed twice at 5:4 (West Point's lantern
at −0.07, against its −0.06 limit) and passed on re-runs at −0.02 and −0.03. In play the camera
sways and leans toward the beam, and the check measured that live view. It now measures the play
view at rest (0.00 at 16:9, 16:10 and 5:4) and logs the live view beside it.

Still open: Standard difficulty and the Night Watch ramp (owner), whether slowed scores should be
kept apart (owner), a gamepad remap and a real-gamepad pass, a listening pass, human playtests,
fullscreen and real-hardware checks of other screen shapes (a Steam Deck in particular), non-US
keyboard layouts, Windows (module install) and macOS signing (Developer ID).

## Round 6 scope

The ranked list is still blocked (Windows module, macOS signing, a real gamepad, a listening pass,
playtests) or waiting on the owner (Standard's balance, the Night Watch ramp, slowed records).
These items come from reading the game as a first-time player would. The radio, the hints and the
dawn debrief all talk in place names ("Light the Hen Bell before the Auk gets there", "struck the
Hen's Chicks, uncharted"), but nothing on screen says which buoy is the Hen Bell, which hull is
the Auk or where the Hen's Chicks are. The rules are taught once per save by hints, and a
returning player has nowhere to read them again. The game always renders at the display's
refresh rate, which on a laptop or handheld means heat, fan noise and battery for no gain. None
of the items changes the simulation, so the pinned Standard scores and both difficulties stay as
they are.

Every item must keep `Tools/unity.sh test` green and the `ui`, `input`, `confirm`, `keys` and
`screens` tours at 0 errors. Captures go to `docs/media/improvements/round6/`. Tour output and logs
stay under `Builds/round6/`. The real save directory is checksummed before and after
(`Builds/round6/save_before.txt`).

### 1. Names on the water
- When a ship's captain or crew is on the radio and the ship is afloat, a small name tag sits under
  its hull for as long as the call is on screen. It stays inside the screen edge, with a chevron,
  when the ship is out of frame. A call that names a ship afloat ("before the Auk gets there")
  tags it the same way.
- When a call names a place you can see (a buoy such as the Hen Bell, a wrecker's cliff such as
  Corley Cove or West Point, a sea stack, or Porthkell harbour), a label appears on the water there
  for a few seconds, pinned at the edge if it's out of frame.
- Hidden hazards are never labelled before they're found: a reef group or sandbank gets its name
  over the white water the first time it's charted each night ("The Merrow Teeth"), so the
  debrief's place names are places the player has seen named. A call naming the Teeth before
  they're charted doesn't point at them.
- The trailer tour turns the labels off, so a re-shoot matches the released trailer.
- **Verify:** a new `names` tour. Night 1: Ianto's "at the harbour" labels Porthkell at the
  harbour. Night 3: the Hen Bell label sits on the buoy's projected position, and a call from the
  Little Auk tags the Little Auk. Night 2: no reef label before the first chart, then "The Merrow
  Teeth" at the charted group. Each check compares the label's canvas position with the projected
  world position, and checks pinned labels are on screen. Screenshots read by me, also at 5:4 and
  with 130% HUD text.

### 2. Keeper's notes
- A **Keeper's notes** screen, opened from the title menu and from the pause menu (it doesn't end
  the night). It lists the rules in short entries: the light and focus, confidence and lost ships,
  the lamps and the wreck allowance, then reefs and charting, breakers and full astern, buoys,
  steamers and sandbanks, fog and the foghorn, the ferry, ships running dark, storms, wreckers and
  lured ships, the mimic, and the Night Watch. An entry appears once its night is open, so the
  notes don't give the season away. The words name the keys and buttons the player has bound, for
  the device in use.
- Topics on the left page, the chosen entry on the right. Mouse, keys and pad all work; Esc or B
  closes it, back to wherever it was opened from.
- **Verify:** a `notes` tour opens the notes from the title with a fresh save (three entries) and
  a finished season (every entry), walks the topics with the simulated keyboard and pad, checks the
  pause menu's notes return to the paused night without changing it, and checks the wording follows
  the device and rebound keys. `screens` checks the screen fits at all four shapes. Screenshots.

### 3. Frame-rate limit
- Settings ▸ **Frame rate**: *Display* (as now: the display's refresh rate, at least 60), *60* or
  *30*. A laptop or handheld can save power, and the fog nights' cost is capped.
- **Verify:** a `framerate` tour measures frames per real second on night V at each setting, with
  the load average recorded: 30 must read 28 to 31, 60 must read 57 to 61 (when the machine can
  reach 60), and Display at least 60. `screens` checks the Settings panel still fits, and the
  `input` tour's d-pad walk is updated for the extra row.

### 4. Sound in the background (if time allows)
- Settings ▸ **Sound in background**: *On* (as now) or *Off*, which mutes the game while its window
  is out of focus. The night already pauses then.
- **Verify:** the tour drives the focus handler (tours can't lose real focus) and checks the
  listener is muted and comes back on focus.

### Not in this round
- **Standard difficulty, the Night Watch ramp and slowed records**: the owner's call.
- **Gamepad remapping, a real pad, fullscreen at other shapes, a Steam Deck, non-US layouts**: no
  hardware here to check them on, and switching this shared machine's display would disturb other
  sessions.
- **Re-cutting the trailer**: the owner's call.

## Round 6 results (2026-10-07)

All four items landed on `improvements-6`, one commit each. Verification was on the built Linux
player from the final commit: 71/71 EditMode tests (64 before, plus four for names on the water and
three for the keeper's notes), and a final pass of 20 tour runs: `input`, `ui`, `status`,
`radiolog`, `ending`, `endingskip`, `offscreen`, `brightness`, `keys`, the new `names`, `notes` and
`framerate`, `watchend`, `confirm`, `speed`, and `screens` at four window sizes with 130% HUD text
(`Builds/round6/final.log`). Every run had 0 errors. One check failed in the first pass: I had
started `speed` with `-llSeasonDone`, whose sample watches outrank the tour's short watch, so it
can't reach the briefing's top three; run as designed, on a blank save, it passed 6/6. `input`
and `ui` first ran at load averages of 48 and 62 (other sessions), so they were run again at 12;
both passed both times. The other runs were at loads of 10 to 30. No simulation code changed, so
the pinned Standard scores still pass and balance is unchanged on both difficulties; the
validation report wasn't re-run. The real prefs file's checksum was the same before and after
(its timestamp moved when a batch build rewrote it with identical contents, and the test runner
updated `TestResults.xml` beside it, as in rounds 3 to 5). Captures are in
`docs/media/improvements/round6/`.

1. **Names on the water.** As planned. Which places and ships a call names is decided by
   `PlaceNames`, a pure class with its own tests: whole words, the longest name first ("Teeth
   Bell" is the buoy, "Corleys" are people), and an uncharted reef group or sandbank claims its
   name without being shown. Reef labels sit just north of the group's furthest reef, because at
   the group's centre the label covered the white water. The `names` tour found a one-frame flash:
   a new tag was drawn at full strength in the canvas's corner until the HUD's next update, which
   also fooled its own fade-in check. New tags now start unseen. The tour checks Porthkell on
   night I, the Little Auk's tag under its hull, the Hen Bell on its buoy, and on night II that
   nothing points at the Teeth while Ianto names them uncharted, then that they're named when
   charted. It passes at 1600×900 and at 1280×1024 with 130% HUD text. The trailer and stills
   shoots turn names off.
2. **Keeper's notes.** As planned: 16 entries, five from the start (the light, lost ships, lamps
   and the allowance, names on the water, and the assists), the rest as nights open, and the
   Night Watch after the season. The pause menu gained an item between Settings and the logbook,
   so the `confirm` and `screens` tours choose by the new positions. Writing the tests found that
   asking the keyboard for a key's layout name crashes a batch editor (no display); key names fall
   back to the key's own name there. The book rewords itself if the keeper picks up the other
   device with it open.
3. **Frame rate.** As planned. For most of the session the shared GPU read 100% busy with other
   sessions' work and the game managed only about 30 fps even at 640×360, so the caps couldn't be
   told apart from that limit; a forced 20 fps target held exactly. In the final pass the GPU had
   freed up and night I (1280×720, 50% render scale, low fog) measured 120.0, 60.0 and 30.0 fps
   for Display (a 120 Hz screen), 60 and 30.
4. **Sound in background.** Done. *On* keeps today's behaviour. *Off* sets the listener's volume
   to zero while the window is out of focus (the night pauses as before) and back on return. The
   `input` tour checks both through the focus handler, since a tour can't lose real focus. The
   Settings panel is 60 units taller (1000) for the row; `screens` passes at all four shapes.

**Process notes.** I edited scripts while the first build was running, so that build held a mix
of item 1 and half of item 2; it was used only for the first `names` run, and everything was
rebuilt before any result was kept. Items 3 and 4 share four files; I committed item 3 by
removing item 4's lines and restoring them afterwards, so the item 3 commit on its own wasn't
built or toured (the next commit, with both, was). One `screens` loop in my shell split the sizes
wrongly and ran at 1280×900, 1600×900 and 2560×900; those runs were discarded and redone.

Still open: Standard difficulty and the Night Watch ramp (owner), whether slowed scores should be
kept apart (owner), a gamepad remap and a real-gamepad pass, a listening pass, human playtests
(now including whether the names help or clutter and whether the notes say enough), fullscreen
and real-hardware checks of other screen shapes (a Steam Deck in particular), the frame-rate caps'
effect on a real laptop's battery, non-US keyboard layouts, Windows (module install) and macOS
signing (Developer ID).

## Round 7 scope

The ranked list is still blocked (Windows module, macOS signing, a real gamepad, a listening pass,
playtests) or waiting on the owner (Standard's balance, the Night Watch ramp, slowed records).
These items come from playing the game through as a returning player would. The dawn card tells
you in words what went wrong ("struck the Hen's Chicks, uncharted"), but you never see the night
as a whole: where each ship went, where it lost its way, where the lure took it. The three lamps
are judged only at dawn, so during a night you can't tell that the steady-hand lamp went out ten
seconds in, and a replayed night's briefing doesn't say which lamp is still missing. And there's
no way to start the season again (for a second player on the same computer, or to play it fresh)
short of deleting files by hand. None of the items changes the simulation, so the pinned Standard
scores and both difficulties stay as they are.

Every item must keep `Tools/unity.sh test` green and the `ui`, `input`, `confirm`, `notes` and
`screens` tours at 0 errors. Captures go to `docs/media/improvements/round7/`. Tour output and logs
stay under `Builds/round7/`. The real save directory is checksummed before and after
(`Builds/round7/save_before.txt`).

### 1. The night's chart
- The dawn card gains a **Chart** button. It opens a chart of Merrow Bay drawn like the logbook's
  paper: the coast, the harbour, the light, the sea stacks, and every ship's track that night.
  A track is inked solid while the captain was steering, dotted red with a **?** where they lost
  their way, and dashed amber with a lantern where a false light had them, so the states differ by
  pattern and mark as well as colour. A wreck is a cross with the ship's name. Reefs and sandbanks
  you charted that night are drawn and named, as are the false lights that burned; reefs never
  charted stay hidden, as in play.
- The tracks come from a `NightLog` that reads the simulation after each step and never changes
  it. It keeps a point when a ship has moved a little or changed state, so a 30-minute watch stays
  small.
- Esc, B or "Back to dawn" returns to the card.
- **Verify:** EditMode tests: on a night where the bot neglects a ship, every wrecked ship's track
  ends at its wreck, a ship that was lost has a lost stretch, and a generated Night Watch's log
  stays under its cap. A `chart` tour stages a wreck on night II and a lure on night X, opens the
  chart with the mouse, keys and the simulated pad, checks that each wreck cross sits where the
  sim's wreck position projects on the chart, and checks back returns to the card. `screens` checks
  the chart fits at all four shapes. Screenshots read by me, and the lost and lured stretches put
  through `Tools/cvd_sim.py`.

### 2. Lamps at stake
- During a night (not the Night Watch, which has its own strip), three small lamps sit under the
  score. The third goes out the moment any ship is first lost or lured, with a short line under
  them naming the ship ("Steady hand: the Kittiwake lost its way"); the second goes out at the
  first wreck. So the player knows what's still in play.
- The briefing of a night already kept says what's left: "Kept with two lamps, best 880. Still to
  earn: a steady hand, no ship ever lost or lured." Or, with all three, just the best score. Its
  prompt also says how to go back (Esc or B).
- **Verify:** a `lamps` tour plays night II with a neglected ship and checks every frame that the
  HUD's lamps match the simulation (the third out exactly when the first ship is lost or lured, the
  second at the first wreck), and that at dawn they match the card's lamps. It reads the briefing
  line on a sample save for a three-lamp night, a two-lamp night and an unplayed one. `screens`
  checks the taller score block stays clear of the manifest at 130% HUD text at all four shapes.

### 3. A new season
- The logbook gains **Start a new season**. It asks first, with **Stay** selected: the nights,
  lamps, scores, ships brought home, the ending and the Night Watch records are cleared and the
  hints come back; settings and keys stay. The season being cleared is kept in the save file under
  a second key, so an owner or support can put it back by hand.
- A save that can't be read is kept aside under its own key rather than being overwritten by the
  next save.
- **Verify:** an EditMode test of the reset itself (what's cleared and what's kept), without
  touching the save. A `season` tour runs the built player against a throwaway config directory
  under `Builds/round7/` seeded with a finished season: it checks Stay changes nothing, then that
  confirming leaves the title on "Begin the watch" without the Night Watch, the logbook with only
  night I open, and the old season under the backup key. A second run seeded with a damaged save
  checks the game starts fresh and the damaged text is kept aside. The real save's checksum must
  match before and after.

### Not in this round
- **Standard difficulty, the Night Watch ramp and slowed records**: the owner's call.
- **Gamepad remapping, a real pad, fullscreen at other shapes, a Steam Deck, non-US layouts**: no
  hardware here to check them on.
- **Re-cutting the trailer**: the owner's call.

## Round 7 results (2026-10-07)

All three items landed on `improvements-7`, one commit each, plus a small follow-up so the trailer
shoot hides the new HUD lamps. Verification was on the built Linux player from the final commit:
79/79 EditMode tests (71 before, plus five for the night's log and three for the season and the
save file), and a final pass of 24 tour runs: `input`, `ui`, `status`, `radiolog`, `ending`,
`endingskip`, `offscreen`, `brightness`, `keys`, `names`, `notes`, the new `chart` and `lamps`,
`watchend`, `confirm`, `speed`, `screens` at four window sizes with 130% HUD text, `framerate`,
and the new `season` tour three ways. 289 checks passed, none failed, and every run had 0 errors
(`Builds/round7/final.log`). The final pass waited for the load average to drop below 24 before
each tour, and ran at loads of 9 to 23. No simulation code changed (the night's log only reads
the simulation, and a test shows a logged night scores exactly as an unlogged one), so the pinned
Standard scores still pass and balance is unchanged on both difficulties; the validation report
wasn't re-run. Captures are in `docs/media/improvements/round7/`.

1. **The night's chart.** As planned. `NightLog` keeps a point when a ship has moved 2.5 units or
   changed state; a 30-minute watch kept 16,594 points, under the 20,000 at which it thins itself
   (a test forces the thinning with a smaller cap and checks every change of state survives). The
   chart is drawn as vector ink (`ChartInk`, one UI mesh), so it's sharp at any size. The `chart`
   tour checks each wreck's cross against the wreck's position projected independently onto the
   chart's frame on screen (within 3 px; all six crosses were exact), that the "?" and lantern
   marks match the log and the ships' own counts (a doused lure can leave a ship lost, which gets a
   "?" on the chart but isn't a "lost its way" in the debrief), and that a click, the arrows and
   Enter, and the simulated pad's A open it while Esc, B and "Back to dawn" return. The dawn card's
   buttons had no left and right navigation before (they were laid out in a row with vertical-only
   navigation); they now walk with the arrows and the d-pad. `chart_cvd.jpg` shows the lost and
   lured stretches staying apart by pattern and mark under deuteranopia and protanopia, though the
   red and amber themselves converge. The chart fits at all four screen shapes.
2. **Lamps at stake.** As planned, except where the note goes: under the lamps in two short lines
   ("STEADY HAND GONE / the Little Auk lost its way"), because a single line beside them would meet
   the hint panel at 130% HUD text. The foghorn gauge moved down to make room (it stays where it
   was in a Night Watch and in trailer shoots). The `lamps` tour compared the HUD's lamps with the
   simulation every frame: at most one frame behind (they update in turn), and equal to the dawn
   card's lamps on night II (a wreck: three to one) and night III (a lost ship: three to two). The
   briefing's record line reads, for example, "Two lamps, best 880. Still to earn: a steady hand."
   The keeper's notes mention both, and the longest entry still fits (514 of 640).
3. **A new season, and the save moved.** The logbook's "Start a new season" works as planned. Its
   tour found something bigger: **the built Linux player keeps its PlayerPrefs, and so the whole
   save, in `~/.config/unity3d/unknown/unknown/prefs`**, a file every Unity player with the same
   fault shares, not in `Gannet Head/Last Light/prefs` (that file is the editor's). Other sessions'
   games write that shared file on this machine (its timestamp moved while no tour of mine was
   running), so another game could overwrite or clear Last Light's progress. The save now lives in
   `save.json` in the game's data folder, written to a temporary file and swapped in whole; the
   first launch carries an older build's PlayerPrefs save over; a damaged save is kept as
   `save.unreadable.json`; and the cleared season is kept as `save.previous.json`. The `season`
   tour runs the player against a throwaway config directory (`Tools/season_tour.sh`, which sets
   `XDG_CONFIG_HOME`; Unity honours it) and refuses to run anywhere else. It checks the reset with
   keys and the pad, a 120-character damaged save, and the migration from PlayerPrefs.

**Process notes.** Two consequences of the PlayerPrefs finding. First, rounds 3 to 6 checksummed
`Gannet Head/Last Light/prefs` as "the real save"; it never held the player's save. Tours run with
`-llFresh`, which never reads or writes the save, so no progress was at risk, but Unity itself
wrote its window-size keys into the shared `unknown/unknown/prefs` at the end of every tour in
every round. From this round `Tools/tour.sh` gives the player a config directory of its own
(`Builds/tour-config`). Second, this round's "real save" check is the game's whole data folder:
before and after, it held the editor's `prefs` (same checksum), the test runner's
`TestResults.xml` (rewritten by each test run, as in earlier rounds) and no `save.json`. Also,
waiting loops in my shell were cut short twice, so I waited on the final pass through a monitor
instead; nothing was run twice because of it.

Still open: Standard difficulty and the Night Watch ramp (owner), whether slowed scores should be
kept apart (owner), a gamepad remap and a real-gamepad pass, a listening pass, human playtests
(now including whether the chart and the lamps at stake help), fullscreen and real-hardware checks
of other screen shapes (a Steam Deck in particular), non-US keyboard layouts, the save's location
on macOS and Windows (untested), Windows (module install) and macOS signing (Developer ID).

## Round 8 scope

The ranked list is still blocked (Windows module, macOS signing, a real gamepad, a listening pass,
playtests) or waiting on the owner (Standard's balance, the Night Watch ramp, slowed records).
These items come from reading round 7's captures and the save code as a player would meet them.
On night III the Hen Bell's label and the Hen's Chicks' label are drawn on top of each other, over
a lost ship, and on the dawn chart wreck names sit across reef marks and place names. The save
moved to its own file in round 7, but when it can't be read or written the game only writes a line
to its log: a keeper whose save failed finds out next time they start the game, with an empty
logbook, and a save that merely couldn't be *opened* (a permissions slip, a file held by another
program) is treated as no save at all and overwritten by the next one. The dawn card gives a
score with no account of where it came from, and nothing during a night says what score is worth
chasing, which is the whole point of a Night Watch. None of the items changes the simulation, so
the pinned Standard scores and both difficulties stay as they are.

Every item must keep `Tools/unity.sh test` green and the `ui`, `input`, `names`, `chart`,
`lamps` and `screens` tours at 0 errors. Captures go to `docs/media/improvements/round8/`. Tour
output and logs stay under `Builds/round8/`. The real save directory is checksummed before and
after (`Builds/round8/save_before.txt`).

### 1. Names that don't sit on each other
- On the water, a place's label moves up or down, out of the way of a label already placed, a
  ship's name tag, or a lost or lured ship's mark, rather than covering it. It stays as close to
  its place as it can and keeps pointing at it. Ship tags, which follow their hulls, keep their
  place.
- On the dawn chart, a wreck's name and the names of places are placed so they don't cover each
  other, or a wreck's cross.
- **Verify:** the `names` tour checks every frame of night III that no two labels on screen
  overlap (it reproduces the Hen Bell and Hen's Chicks), and that each label is still within reach
  of what it names. The `chart` tour checks that no two of the chart's names overlap on night IX
  (five wrecks). Screenshots read by me, also at 5:4 with 130% HUD text.

### 2. Tell the keeper when the save is in trouble
- A save that can't be **opened** (as opposed to read and found damaged) is never written over:
  the game plays on without saving and says so.
- When the save can't be read, the title says so once, in words: what happened, that the damaged
  file was kept as `save.unreadable.json`, and where it is.
- When the save can't be **written** (a full disk, a read-only folder), the dawn card and the
  title say that progress isn't being saved, and where the save should be.
- **Verify:** EditMode tests for the decisions (an unopenable save isn't overwritten; a failed
  write is reported) using folders under `Temp/`. The `season` tour gains two runs in a throwaway
  config: `damaged` checks the title's notice, and a new `readonly` run makes the throwaway save
  folder read-only, keeps a night, and checks the dawn card's and the title's notices and that
  the seeded save is unchanged. My own save folder is checksummed before and after.

### 3. The score explained at dawn
- Under the dawn card's score, one line says where it came from, for example "4 ships home 600 ·
  steady hands +200", with ships running dark (which count double) given their own part. The parts
  always add up to the score.
- **Verify:** an EditMode test plays every night with the AutoKeeper (and a neglected ship on
  some) and checks the parts add up to the sim's score. The `ui` tour's staged dawn cards show the
  line; screenshots read by me.

### 4. The score to beat, during the night
- Under the HUD's score, "best 880" when the night (or a watch) has a best to beat; it changes to
  "NEW BEST" in brass the moment the score passes it. Nothing shows the first time a night is
  played.
- **Verify:** a `best` tour plays night III on a sample save (best 880) and a Night Watch on a
  finished season with the AutoKeeper, and checks every frame that the line matches the sim's score
  against the best, and that it turns exactly when the score passes. `screens` checks the score
  block stays clear of the manifest at 130% HUD text at all four shapes.

### 5. An offer of help after repeated failures (if time allows)
- After a night fails twice in a row, the dawn card adds a line pointing to the assists that
  already exist (Game speed, and Difficulty if it's on Hard), with where to find them. It doesn't
  change anything by itself, and doesn't appear if the assists are already in use.
- **Verify:** the `ui` tour stages two failures on night II and checks the line, and that it's
  absent after one failure and at 70% game speed.

### Not in this round
- **Standard difficulty, the Night Watch ramp and slowed records**: the owner's call.
- **Gamepad remapping, a real pad, fullscreen at other shapes, a Steam Deck, non-US layouts, the
  save on macOS and Windows**: no hardware or platforms here to check them on.
- **Re-cutting the trailer**: the owner's call.

## Round 8 results (2026-10-07)

All four planned items and the optional fifth landed on `improvements-8`, one commit each.
Verification was on the built Linux player from the final commit: 93/93 EditMode tests (79
before, plus five for the label placer, four for save trouble, three for the score's parts and two
for the offer of help), and a final pass of 28 tour runs (`Builds/round8/final.log`): `input`,
`ui`, `status`, `radiolog`, `ending`, `endingskip`, `offscreen`, `brightness`, `keys`, `names`,
`notes`, `chart`, `lamps`, `watchend`, `confirm`, `speed`, the new `best` and `help`, `screens` at
four window sizes with 130% HUD text, `framerate`, and the `season` tour five ways (two of them
new). Every run had 0 errors; 305 checks passed and one failed (see below). The pass ran at load
averages of 2 to 13. Captures are in `docs/media/improvements/round8/`.

The only simulation change is that a ship's points are now worked out in `ScoreParts`, with the
same formula. The test that pins the AutoKeeper's Standard scores on all twelve nights still
passes, so balance is unchanged on both difficulties. The validation report wasn't re-run.

1. **Names that don't sit on each other.** As planned. `LabelPlacer` is a small pure class (five
   tests). On the water a name keeps its place while that's clear, so it doesn't flick between
   places as a mark bobs. The `names` tour leaves the Little Auk in the dark on night III and checks
   all 5,400 frames: no name covers another or a ship's mark. The Hen's Chicks stepped up a line
   at 5.5 s, where it used to land on the Hen Bell, and the Black Hen stepped down at 12.3 s. Run
   with `-llNamesOverlap` (names as round 7 drew them), the same check fails at 5.5 s on exactly
   that pair (`names_before_after_night3.jpg`). On the chart the old drawing failed with 11 clashes
   on night IX. The worst was the Dunlin's name, hidden under the Evening Star's where both wrecked
   at the same rock. The first new version still left two wreck names touching their own crosses:
   they were set 26 px under a 32 px cross. They now sit 31 px under, and the check passes on
   nights II and IX (`chart_names_before_after.jpg`).
2. **Save trouble told.** As planned, plus one fix found on the way. A path that exists but isn't a
   readable file (here a folder named `save.json`) used to fall through to the PlayerPrefs
   migration. It now counts as a save that can't be opened. The title's notice says what happened
   and where; the dawn card says when a kept night or watch wasn't saved. A damaged save that can't
   even be copied aside also stops saving rather than risk the only copy. The `season` tour's
   `readonly` run (the throwaway save folder made read-only for the run) and `unopenable` run
   keep night I and check the dawn card, the title and that the seeded save is byte-for-byte
   unchanged. The reasons ("permission was refused", "the disk is full") come from the exception,
   and a full disk wasn't tried.
3. **The score explained.** As planned, worded "6 ships home 750 · 6 steady hands +300" (a ship
   that ran dark adds "1 ran dark, double +150"). The test plays all twelve nights twice, kept and
   with a shaky keeper who neglects a ship, plus a ten-minute watch: the parts always add up, and
   both the dark-ship and wavering cases occur.
4. **The score to beat.** As planned, with one wording change. A night's best counts only if the
   night is kept, so passing it on one of the twelve nights reads "PAST YOUR BEST"; in a watch,
   which is always kept, it reads "NEW BEST". The `best` tour compared the line with the
   simulation every frame (6,838 on night III, 1,440 in the watch) with no mismatched frame. It
   turned at 1,000 against 880 and at 750 against 600, and the lamps and horn stay clear of it.
   With no best (night IV, never kept) nothing shows and the lamps stay where they were.
5. **An offer of help.** Done. After the same night fails twice running, the dawn card points to
   Settings ▸ Game speed, or to Difficulty on Hard, and says nothing once both are as easy as they
   go. The `help` tour passes 7/7: the first failure is silent, the second offers game speed, 70%
   on Standard offers nothing, Hard offers Difficulty, and a kept night or a different night starts
   the count again. The wording and the threshold of two are guesses for a playtest to settle.

**The one failed check.** In the final pass, the `ui` tour's resolution switch (a 1600×900 window
to 1280×720) failed. The window reached 1280×720 within 0.01 s but was back at 1600×900 half a
second later. Round 8 doesn't touch that code. The same tour passed earlier this session and in
two reruns straight after the pass (load about 1), each resizing within 0.01 s. Round 4 saw one
such failure too. It looks like the compositor occasionally undoing the resize, but that's
unproven.

**Process notes.** Items 2 to 5 share `Game.cs` and the dawn card's code. They were built and
toured together, then committed one at a time by taking each shared file back to its state for
that item. So only the last commit (all items) was built and toured on its own. The first `help`
run hit `tour.sh`'s 600 s limit two checks before the end while the shared GPU was fully busy. It
was re-run with a longer limit and passed, and the final pass gives `help` 1,500 s. My own save
folder held the editor's `prefs` (same checksum; its timestamp moved during a batch build) and the
test runner's `TestResults.xml` (rewritten by each test run) before and after, and no `save.json`,
as in round 7.

Still open: Standard difficulty and the Night Watch ramp (owner), whether slowed scores should be
kept apart (owner), a gamepad remap and a real-gamepad pass, a listening pass, human playtests
(now including the score to beat, the score's parts and the offer of help), fullscreen and
real-hardware checks of other screen shapes (a Steam Deck in particular), non-US keyboard layouts,
the save's location and its trouble notices on macOS and Windows, a full disk, Windows (module
install) and macOS signing (Developer ID).

## Round 9 scope

The ranked list is still blocked (Windows module, macOS signing, a real gamepad, a listening pass,
playtests) or waiting on the owner (Standard's balance, the Night Watch ramp, slowed records).
These items come from reading round 7 and 8's captures and the code as a returning player would
meet them. The dawn chart shows every track at once: on a busy night the lines are unlabelled and
overlap, and nothing says *when* anything happened or where the light was at the time, which is
what a keeper needs to learn from a wreck. Settings has 22 rows with one- or two-word labels
("Render scale", "Focus", "Game speed") and nothing says what any of them does. And a Night Watch
can run half an hour, but closing the window during one (the window's close button, Alt+F4, a
logout) throws it away; only "End the watch" in the pause menu keeps it. None of the items changes
the simulation, so the pinned Standard scores and both difficulties stay as they are.

Every item must keep `Tools/unity.sh test` green and the `ui`, `input`, `chart`, `confirm`, `keys`
and `screens` tours at 0 errors. Captures go to `docs/media/improvements/round9/`. Tour output
and logs stay under `Builds/round9/`. My own save folder is checksummed before and after
(`Builds/round9/save_before.txt`). Where I can, test windows run in a private nested KWin
(`kwin_wayland --virtual` under its own D-Bus session) rather than on the shared desktop.

### 1. Replay the night on the dawn chart
- The chart gains **Replay**: the night plays back on the paper at several times its speed. Each
  ship afloat is a small mark at its place, with its name, coloured and marked by its state
  (on course, lost with a "?", lured with a lantern); the light's beam sweeps from Gannet Head as
  it did, wide or focused; a false light glows while it burned; a wreck's cross appears when it
  happens. The full tracks stay, faint, underneath.
- A timeline under the key shows the time ("2:14 of 4:05"). ←/→ or the d-pad step it back and
  forth, a click or drag on the timeline jumps, Space, Enter or A plays and pauses. It starts
  paused at the start; Back still returns to dawn.
- The log gains a time for each point, the beam's bearing and focus a few times a second, and
  when each false light burned. It still only reads the simulation.
- **Verify:** EditMode tests: the log's beam samples match the beam a bot actually swept, a
  ship's interpolated position at a logged point's time is that point, the replay's state for a
  wrecked ship turns to wrecked at its wreck time, and a 30-minute watch's beam log stays capped.
  The `chart` tour opens the replay with the mouse, keys and the simulated pad, steps and drags
  the timeline, and checks at several times that each ship's mark sits where the log puts it (in
  screen space, within 3 px) and that the beam's wedge points at the logged bearing; at the end,
  every wrecked ship's mark is on its cross. `screens` checks the replay fits at all four shapes.
  Screenshots read by me.

### 2. Settings say what they do
- Under the right-hand column, a short description of the setting that's selected or under the
  pointer: what it changes and when it applies ("Slows the whole night together: ships, the lens,
  fog, storms and wreckers. Only you gain time. The dawn card notes a slowed night.").
- **Verify:** the `input` tour's d-pad walk checks the description follows the selection through
  every row, and that hovering a row with the mouse shows that row's. `screens` checks the
  description fits inside the panel at all four shapes and at its longest. Screenshots read by me.

### 3. Closing the game keeps a Night Watch
- Quitting the game while a watch is under way (playing or paused) stands the watch down and keeps
  it, as "End the watch" does, before the game closes. The twelve nights are unchanged: closing
  mid-night keeps nothing, as now. "Leave the lighthouse ▸ Leave anyway" still keeps nothing.
  The keeper's notes on the Night Watch say so.
- **Verify:** a `quitwatch` run of `Tools/season_tour.sh` (a throwaway config with a finished
  season) plays a watch with the AutoKeeper for a couple of minutes and quits the way a window's
  close button does; the script then checks `save.json` holds the watch with its score, ships and
  time. A second run sends the player SIGTERM (as a logout does) and checks the same; a third
  quits mid-night and checks the save is unchanged. My own save folder is checksummed before and
  after.

### 4. Button names for PlayStation and Nintendo pads (if time allows)
- Settings ▸ **Pad buttons**: *Auto* (from the pad's name where Unity reports one), *Xbox* (A, B,
  RT, Start, as now), *PlayStation* (Cross, Circle, R2, Options) or *Nintendo* (B, A, ZR, +, by
  position). Every prompt that names a pad button follows it.
- **Verify:** an EditMode test of the names for each style; the `input` tour switches styles and
  checks the title strip, a hint, the pause card and the briefing prompt. Only simulated pads;
  whether Unity on Linux reports a real DualShock's name isn't known.

### Not in this round
- **Standard difficulty, the Night Watch ramp and slowed records**: the owner's call.
- **Gamepad remapping, a real pad, fullscreen at other shapes, a Steam Deck, non-US layouts, the
  save on macOS and Windows, a full disk**: no hardware or platforms here to check them on.
- **Re-cutting the trailer**: the owner's call.
