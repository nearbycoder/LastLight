<p align="center">
  <img src="docs/media/teaser.webp" width="960" alt="The lighthouse beam sweeping across Merrow Bay at night, narrowing to reach a far ship">
</p>

<h1 align="center">Last Light</h1>

<p align="center">
  <em>You keep the last lighthouse on a wrecking coast. Sweep its beam through the night to guide ships home,<br>
  chart the hidden reefs ahead of them, and expose the false lights trying to lure them in.</em>
</p>

<p align="center">
  <img alt="Unity 6000.6.2f1" src="https://img.shields.io/badge/Unity-6000.6.2f1-222c37?logo=unity&logoColor=white">
  <img alt="Universal Render Pipeline" src="https://img.shields.io/badge/render-URP%2017.6-3d4f63">
  <img alt="Platform: Linux" src="https://img.shields.io/badge/platform-Linux%20x86__64-c9a35a?logo=linux&logoColor=white">
  <img alt="Input: mouse, keyboard, gamepad" src="https://img.shields.io/badge/input-mouse%20%C2%B7%20keyboard%20%C2%B7%20gamepad-5b6b7a">
  <img alt="Art: Blender 4.5" src="https://img.shields.io/badge/art-Blender%204.5-e87d0d?logo=blender&logoColor=white">
  <img alt="Version 0.1.0" src="https://img.shields.io/badge/version-0.1.0-e9dfc7">
</p>

<p align="center">
  <a href="https://github.com/nearbycoder/LastLight/releases/latest"><b>Download for Linux</b></a> ·
  <a href="docs/media/LastLight_trailer.mp4"><b>Watch the trailer</b></a> ·
  <a href="#how-to-play">How to play</a> ·
  <a href="#build-from-source">Build from source</a>
</p>

## Trailer

[![Last Light trailer: click to play (1:50, 1080p, with sound)](docs/media/trailer_poster.jpg)](docs/media/LastLight_trailer.mp4)

<sub>1 minute 50 seconds, 1920×1080, with the game's own music and sound. Every shot is the game
running, recorded from scripted nights (see <a href="#the-trailer">how the trailer is made</a>).</sub>

## About

Gannet Head Light is being switched off at the end of the season. A radio beacon will replace it.
You have twelve nights left.

Out on Merrow Bay, trawlers, coal steamers and the passenger ferry steer by your light. Keep a
ship in the beam and its captain sails on with confidence. Leave it in the dark and the captain
loses the way, drifting toward rocks nobody can see. There's one beam and there are many ships,
so every night is a quiet juggling act. The sea looks calm, but every decision is tense.

Each night adds one new idea: hidden reefs, buoys, fog, damaged ships with no lights, storms, and
wreckers on the cliffs with lanterns that imitate yours. Nights take two to five minutes and are
rated with up to three lamps, so a night you nearly kept is always worth one more try. Finish the
season and an endless **Night Watch** opens.

Every model was built by script in Blender, and every sound, radio voice and note of music was
synthesized in Python. The game uses no stock or sampled assets.

## How to play

Point the light. That's the whole interface. The rest is deciding where to point it.

| Action | Mouse and keyboard | Gamepad |
|---|---|---|
| Aim the beam | Move the mouse (the lens follows with weight), or turn it with **A / D** or **← / →** | Right or left stick |
| Focus: a narrow, long, bright beam that turns slower | Hold the **left mouse button**, **Shift**, **W** or **↑** (or press to switch, with Focus set to Toggle) | Hold either trigger (or press, with Toggle) |
| Sound the foghorn (14 s cooldown) | **Space** or the **right mouse button** | **A** |
| Pause, or back out of a menu | **Esc** or **P** | **Start** (and **B** in menus) |
| Move through and choose menu items | Mouse, or **arrow keys / Tab** and **Enter** | D-pad or stick, **A** to choose |

The keyboard keys for turning, focus and the foghorn can be changed in **Settings ▸ Keys** (three
keys each; Esc and P always pause). The mouse buttons and the gamepad keep theirs.

The game starts fullscreen. Settings has volumes for master, music, effects, radio and ambience,
along with text speed, hints, screen shake, **focus** (hold the button, or toggle: press once to
focus and again to widen), lens turn speed, windowed or fullscreen, resolution,
**frame rate** (the display's refresh rate, or a cap of 60 or 30 to save power and heat on a
laptop or handheld), fog quality, **render scale** (the 3D scene at 100, 85, 70 or 50% while the text stays sharp),
**brightness** (five steps for the 3D scene, from half a stop darker to a stop brighter; the
menus and HUD are unchanged), **reduce flashing** (the storm's lightning lights the bay at about
a tenth of its strength), HUD text size, difficulty, **game speed** and the keyboard **keys**.
A night pauses itself when the game window loses focus or the gamepad you're using is unplugged.
With **Sound in background** off, the game also falls silent while its window is out of focus.
The pause menu shows the night's latest radio calls, so a call you missed can be read again, and
the controls for the device you're using. **Keeper's notes**, on the title menu and in the pause
menu (the night waits), keep the rules in short entries: the light, lost ships, the lamps and the
wreck allowance, then each night's new idea as the season reaches it, so nothing is given away
early. From the pause menu the book opens on tonight's idea, and the entries name the keys and
buttons you use. Restarting, leaving or opening the logbook from it asks
first, with **Stay** selected, because they throw the night away; in a Night Watch the question
offers **End the watch**, which keeps it. The mouse pointer hides while you play with a gamepad.
Menus and the HUD fit screens of other shapes (checked at 16:10, 5:4 and 21:9 as well as 16:9),
and on screens narrower than 16:9 the camera widens its view so the whole bay stays in sight.

**Game speed** (100, 85 or 70%) is an assist that slows the whole night together: ships, the
lens, fog, storms and wreckers. Only you gain time. Music, the radio's typing and the menus keep
their pace. The dawn card and the table of best watches say when a night or watch was slowed.

Press Esc, Start or B during the ending and a second press skips to the title (the ending still
counts as seen), which helps when replaying night XII.

**HUD text size** (100, 115 or 130%) enlarges everything drawn during a night: the radio, hints,
manifest, score, foghorn and markers. The menus keep their size.

**Difficulty** is Standard (the game as tuned) or **Hard**. On Hard, ships lose heart about 30%
faster, a chart fades after 15 seconds instead of 22, a lit buoy burns 20 seconds instead of 28,
the crew give no breakers warning, and Night Watch ships come about 15% closer together. The
change takes effect from the next night. Records are shared, and the dawn card, the HUD and the
table of best watches say when it was Hard.

**Names on the water.** When a ship is on the radio, or a call names one ("before the Auk gets
there"), its name shows under its hull for the length of the call. A call that names a place you
can see (a buoy such as the Hen Bell, a wreckers' cliff, a sea stack or Porthkell harbour) labels
it on the water for a few seconds, and a reef group or sandbank gets its name the first time it's
charted each night, so the debrief's "struck the Hen's Chicks" is a place you've seen named. A
hidden hazard is never pointed at before it's charted. Names out of frame stay at the screen's
edge with a chevron, and a name whose spot is taken (by another name, or a lost or lured ship's
mark) steps a line up or down rather than covering it.

Each onboarding hint shows once per save (**Settings ▸ Show hints again** brings them back).
Hints, the title's control strip, the briefing prompt and the HUD's foghorn key follow the device
you last touched, so a gamepad player reads "Hold RT" and "A" rather than mouse buttons and Space.
They also follow the Focus setting, so in Toggle they say "press" or "click" rather than "hold",
and name the keys you've chosen in Settings ▸ Keys.

### The rules in brief

- **Ships follow the light.** Each ship has a confidence ring. It refills in your beam and drains
  in the dark. At zero the captain is **Lost**: the ship slows, wanders and drifts toward the shore
  until you light it again. A lost ship shows a red ring and a bobbing **?**. A lured ship shows an
  amber ring, a small **lantern** and a dashed tether to the false light that has it, so the two
  read apart by shape as well as colour. The ship icons in the top bar carry the same marks (a
  **?** or a lantern), with a **cross** once a ship is wrecked and a **tick** once it's home.
- **Reefs are hidden.** Captains' routes run straight over rocks they can't see. Light a reef
  briefly to **chart** it. White water breaks over it and captains steer around it. A chart fades
  about 22 seconds after the light leaves it, so time your sweeps to stay just ahead of each ship.
- **Breakers ahead.** A few seconds before a ship reaches an uncharted reef (or a steamer an
  uncharted sandbank), its crew hear the surf. Its ring flickers white, a pale ring pulses round
  it and the captain may call out. If you chart the rock too close for the captain to turn, they
  ring for **full astern**, with three short blasts, and the ship slows hard while it swings
  clear. A chart in the last moment can still be a near miss.
- A night ends when every scheduled ship has made port or been lost, and fails at once if the wrecks
  exceed that night's allowance: one wreck on most nights, two on nights 8, 10, 11 and 12. The
  briefing says how many, and hulls under the night's title count them down, ending on "next
  wreck ends the night". You earn a lamp for keeping the light through the night, a second
  for losing no ship, and a third for a **steady hand**: no ship ever Lost or Lured. The three
  lamps under the score go out as they're lost, so you can see what's still in play.

## Features

### A beam with weight
<img src="docs/media/play.jpg" width="100%" alt="Night III, Lantern Row: the beam sweeps across the bay toward a ship while a lit buoy keeps the channel">

The lens is a heavy mass on a damped spring. It builds up speed, overshoots slightly and settles,
and the shaft cuts through moonlit haze, lighting the swell. **Hold to focus** for a narrow, long
beam that reaches the far lanes and pierces fog, at the cost of a slower, smaller sweep.

### Chart the hidden reefs
<img src="docs/media/chart.jpg" width="100%" alt="Night II, The Teeth: the beam passes over a reef cluster and white water breaks over the rocks it has charted">

The Merrow Teeth, Widow's Ledge and the Long Sands lie right across the captains' routes. Sweep
the water ahead of a ship and the surf breaks white over each reef you reveal, and the captain
steers around it. Steamers turn wide and need warning early, while trawlers can dodge late.

### Buoys, fog and the foghorn
<img src="docs/media/foghorn.jpg" width="100%" alt="Night V, Sea Fret: the foghorn's shockwave rings spread across the bay through drifting fog banks">

A sweep lights a channel buoy, and a burning buoy keeps nearby ships steady for about 28 seconds
while you tend to others. Sea fret rolls in on later nights: fog drains ships faster and swallows
the wide beam. Focus punches through it, and the **foghorn** sends a shockwave across the water
that steadies every ship in earshot.

### Ships running dark, and storms
<img src="docs/media/storm.jpg" width="100%" alt="Night XII, Last Light: rain and a lightning flash light the whole bay during the finale">

**Damaged vessels** carry no lights. Find them from their radio bearing and the red distress
flares they send up. **Storms** push ships off course with a current and shorten your beam, but
every lightning strike lights the whole bay and charts every reef for a moment.

### Wreckers and false lights
<img src="docs/media/false_light.jpg" width="100%" alt="Night IX, False Light: a wrecker's orange lantern on the cliffs lures a steamer toward the rocks">

The Corleys sweep lanterns from the cliffs that imitate your light. A ship caught in a false beam
while you're elsewhere is **Lured** toward the rocks. Hold your beam on the lantern to douse it,
or light the ship to break the spell. On night 11, one false light copies your every sweep. Two of
the lanterns, Corley Cove and West Point, sit just past the edges of a 16:9 screen; while one burns
there, a lantern marker at the edge points to it, and a lured ship out of frame keeps its mark on
screen the same way.

### Every ship counts
<img src="docs/media/wreck.jpg" width="100%" alt="A trawler left in the dark strikes an uncharted reef and burns, its ring turned red">

Miss a reef and the hull strikes, burns and sinks, and the crew's lifeboat drifts clear. The
captains talk you through it all on the radio. Ianto the harbourmaster, cheeky Maren on the
*Little Auk*, gruff Captain Pryce on the *SS Calloway* and Dot on the ferry *Evening Star* all
grumble, joke, panic and thank you in typed text over synthesized gibberish voices.

### Twelve nights, three lamps each, and the Night Watch
<img src="docs/media/results.jpg" width="49%" alt="Dawn results for night II: three lamps lit, five ships home, none wrecked"> <img src="docs/media/logbook.jpg" width="49%" alt="The keeper's logbook: nights kept with their lamps, later nights still sealed with wax">

Dawn tallies every night with up to three lamps and a short debrief of every ship that had a bad
night: which reef it struck and whether that reef was uncharted or charted too late, who was lured
and by which false light, and who lost their way. **Chart** on the dawn card opens the night's
chart, Merrow Bay drawn on paper with every ship's track: solid while the captain was on course,
dotted red with a **?** where they lost their way, dashed amber with a lantern where a false light
had them, and a cross at each wreck, with the reefs, sandbanks and false lights you saw that night.
The chart's names are set clear of each other and of the crosses, most important first. Under
the score, the dawn card says where it came from ("6 ships home 750 · 6 steady hands +300"; a
ship that ran dark counts double). If the same night fails twice running, the card points to
Settings ▸ Game speed (or Difficulty, on Hard); it changes nothing by itself.
During a night, three small lamps under the score show what's still in play: the third goes out
the moment a ship first loses its way or is lured, the second at the first wreck, with a note
naming the ship. On a night you've kept before, and in a Night Watch, "BEST 880" sits under the
score, and turns to "PAST YOUR BEST" (or "NEW BEST" in a watch) the moment you pass it. The
keeper's logbook keeps your best for each night so you can go back for a
cleaner watch, and a kept night's briefing says which lamp is still to earn. **Start a new season**
in the logbook clears the nights, lamps, scores and watch records (it asks first, and keeps your
settings and keys). Finish the season and the **Night Watch** opens: an
endless score attack with every reef, sandbank and buoy out and no end to the ships. Each watch
brings its own weather: one to three fog banks in different places, squalls that blow through
every few minutes with current, rain and lightning (stronger as the night wears on), and wreckers
lighting up at their own times, with the mimic light late in a long watch. Ianto gives the forecast
in the briefing, and the game keeps your five best watches. The briefing shows the top three, and
the dawn card tells you where a watch ranks. To stop, choose **End the watch** in the pause menu:
dawn comes at once, and the watch is kept and ranked as if the last wreck had ended it.

## Content overview

One coast (Merrow Bay), three hulls and twelve nights. Each night introduces one idea through
night 9, and the later nights combine them:

| Night | Name | What's new |
|---|---|---|
| I | First Watch | Aiming: ships follow the light |
| II | The Teeth | Hidden reefs and charting, and focus for reach |
| III | Lantern Row | Buoys |
| IV | Deep Water | Collier steamers and the Long Sands shoal |
| V | Sea Fret | Fog banks and the foghorn |
| VI | The Evening Star | The passenger ferry |
| VII | Mayday | Damaged vessels without lights |
| VIII | Squall | Storm current, rain and lightning |
| IX | False Light | Wreckers |
| X | Two Lights | Wreckers moving between sites, in fog |
| XI | The Mimic | A false light that copies your sweep |
| XII | Last Light | The finale |
| ∞ | Night Watch | Unlocked after the season: endless, with every hazard out and its own fog, squalls and wreckers each time; the third wreck ends the watch |

| Hull | Character |
|---|---|
| Trawler | Small and quick, turns tightly, loses confidence fast |
| Collier steamer | Big and slow, turns wide, runs aground on sandbanks that trawlers sail over |
| Passenger ferry | Rows of lit windows, and worth the most |

Around the nights you'll find a live title scene, briefing cards, the radio, pause and settings
menus, dawn results, an ending, and credits that name every ship you brought home. Progress and
settings are saved locally, in `save.json` in the game's data folder (on Linux
`~/.config/unity3d/Gannet Head/Last Light/`). If the save can't be read, the title says so and
where the damaged copy was kept (`save.unreadable.json`); if it can't be written (a full disk, a
read-only folder), the dawn card and the title say progress isn't being kept, and where the save
should be. A save that's there but can't be opened is never written over.

## Screenshots

| | |
|---|---|
| ![The title screen: the lighthouse at night, its beam sweeping overhead](docs/media/title.jpg) | ![Night II: charting the Teeth ahead of a trawler](docs/media/chart.jpg) |
| ![Night V: the foghorn's shockwave through the sea fret](docs/media/foghorn.jpg) | ![Night IX: a ship lured by the wreckers' lantern](docs/media/false_light.jpg) |
| ![Night XII: lightning over the finale](docs/media/storm.jpg) | ![The Night Watch, with its tally, clock and wreck allowance](docs/media/night_watch.jpg) |
| ![A wreck on an uncharted reef](docs/media/wreck.jpg) | ![Night III: a lit buoy holding the channel](docs/media/play.jpg) |
| ![Dawn results: three lamps](docs/media/results.jpg) | ![The keeper's logbook](docs/media/logbook.jpg) |

## Play it

1. Download `LastLight-v0.1.0-linux-x86_64.zip` from the
   [latest release](https://github.com/nearbycoder/LastLight/releases/latest).
2. Unzip it and run `./LastLight.sh`, or run `./LastLight.x86_64` directly.

You'll need 64-bit Linux and a GPU with OpenGL 4.5. The game was developed on CachyOS (Arch) with
an AMD Radeon integrated GPU under Wayland. On Wayland, `LastLight.sh` starts Unity's native
Wayland backend, because the X11/XWayland path hung at startup on the development machine.
There's no published Windows or macOS build yet. A macOS build can be made from source (see below),
but it's unsigned and hasn't been run on a Mac.

## Build from source

**Requirements:** Unity **6000.6.2f1** (URP 17.6, Input System 1.20; the packages resolve from
`Packages/manifest.json`), **Blender 4.5 LTS** for the models, Python 3 with the packages in
`Tools/requirements.txt` for audio and tooling, and FFmpeg for captures and the trailer.

```sh
python3 -m venv Tools/.venv && Tools/.venv/bin/pip install -r Tools/requirements.txt

# Data: the map and the missions (JSON, the single source of truth for positions)
python3 ArtSource/map/merrow_bay.py
python3 ArtSource/map/missions.py
Tools/.venv/bin/python Tools/chart.py                    # a top-down chart PNG for review

# Models: FBX into Assets/Resources/Models, preview renders into ArtSource/_renders
blender -b --threads 4 -P ArtSource/blender/build_assets.py -- --preview
blender -b --threads 4 -P ArtSource/blender/build_assets.py -- steamer lighthouse   # just some

# Audio: effects, radio voices and music, synthesized to Assets/Resources/Audio
Tools/.venv/bin/python ArtSource/audio/build_audio.py
Tools/.venv/bin/python ArtSource/audio/build_audio.py music                        # just the music

# Unity
Tools/unity.sh                 # open the editor
Tools/unity.sh build-linux     # batch-build Builds/Linux/LastLight.x86_64
Tools/unity.sh build-mac       # batch-build Builds/Mac/LastLight.app (universal Intel + Apple Silicon)
Tools/unity.sh build-windows   # batch-build Builds/Windows/LastLight.exe (needs the Windows module)
Tools/play.sh                  # run the build (LL_W=1920 LL_H=1080 for a window size)
Tools/package.sh 0.1.0 linux   # zip a build for a release, into Builds/release (or mac, windows)
```

`Tools/unity.sh` expects the editor at `~/Unity/Hub/Editor/6000.6.2f1` (set `UNITY` to change
that). On Arch-based distributions the editor needs the legacy `libxml2.so.2`
(`sudo pacman -S libxml2-legacy`). Alternatively, set `LASTLIGHT_UNITY_LIBS` to a folder that
contains a copy of it.

### Tests and validation

- `Tools/unity.sh test` runs the EditMode tests (93 of them). They check that every mission
  references valid map data, that every reef, buoy and wrecker lantern is reachable by the beam,
  that every route is safe for every hull once its hazards are charted, that the **AutoKeeper**
  bot wins all twelve nights in the pure simulation, that the bot keeps a generated Night
  Watch for at least ten minutes, and that the dawn debrief accounts for every wreck when the
  bot is made to neglect each ship in turn. Two more stage a steamer at Widow's Ledge. One checks
  that the crew warn of breakers a few seconds before an uncharted strike. The other charts the
  reef at distances from 2 to 44 units, with and without full astern, and checks that full astern
  turns some late charts into near misses and never causes a wreck. Others check that ten Night
  Watch seeds give ten different nights and that a squall eases in and out. On difficulty, they
  check that the AutoKeeper wins all twelve nights on Hard, that its Standard scores match the
  tuned game exactly, and that Hard really is harder: shorter charts and buoys, no warning, closer
  watch ships. Two check the pause menu's radio log: calls kept in order, capped, and cleared each
  night. One checks that only a Night Watch can be stood down, and that its score stands. Six cover the
  rebindable keys: the defaults, moving a key between actions, clearing, Esc and P staying as
  pause, reset, repairing a damaged save, and the save format (none of them touch the real save).
  Four check what a radio call points at for names on the water (whole words, the longest name
  first, and no hidden reef or sandbank before it's charted), and three check that the keeper's
  notes open with the season and name the device, keys and difficulty in play. Five check the
  night's log behind the dawn chart: every track ends where its ship ended, every time a ship lost
  its way or was lured is on its track, the log doesn't change the night, and a long watch's log
  thins itself and keeps every change of state. Three check starting a new season (what's cleared
  and what's kept), that a damaged save is reported rather than read, and that the save file is
  replaced whole; they write only under the project's `Temp/` folder. Four more, also under
  `Temp/`, check what the keeper is told about the save: a good save loads quietly, a damaged one
  is kept aside and said, one that can't be opened is never written over, and a failed write is
  reported and clears once a write works. Five check the label placer behind names on the water
  and the chart, three that the dawn card's score parts add up to the score on all twelve nights
  (kept and shaky) and a watch, and two the offer of help after repeated failures.
- `Tools/validate.sh` prints the same checks as a report from a resident editor
  (`Tools/unity.sh serve`). `Tools/tour.sh report <dir>` produces the report from the built player.
  The report also plays every night with a **novice keeper**, which is slow to react, has a shaky
  hand and doesn't know where the reefs are.
- The latest report, from the current build: the AutoKeeper wins all twelve nights with three lamps
  each. The novice wins every night in three runs each, with lamps of 9, 9, 8, 8, 8, 7, 6, 9, 7,
  9, 6 and 7 out of 9 (93 of 108) and no wrecks. The bot keeps every generated Night Watch for the
  full 30 minutes the report runs, with no wrecks, and so does the novice, with 1 or 2 wrecks,
  even with the squalls. On **Hard** the AutoKeeper still wins all twelve nights (two lamps on night
  6, three on the rest), and the novice drops to 74 of 108 lamps with 7 wrecks, close to v0.1.0's
  78 and 9. On Hard the bot keeps two of three watches for 30 minutes, and the novice's watches end
  after 24 to 28 minutes. For comparison, v0.1.0 gave the AutoKeeper
  one lamp on night 9, gave the novice 78 of 108 lamps (3/9 on nights 7 and 8) with 9 wrecks, and
  the bot's watches ended after 16 to 25 minutes. Nearly all of those wrecks were hulls striking
  reefs that had already been charted, and full astern now prevents those (see Status and known
  issues).
- `Tools/tour.sh <ui|nights|ending|input|watch|flash|breakers|status|radiolog|screens|watchend|endingskip|speed|offscreen|confirm|brightness|keys|names|notes|framerate|chart|lamps|best|help> <dir> -llFresh` plays the built
  game with scripted input and saves screenshots. `-llFresh` keeps the tour away from your save,
  and tours run with a config directory of their own (`Builds/tour-config`, or `LL_TOUR_CONFIG`),
  so Unity's window settings don't land in your `~/.config/unity3d` either.
  The `ui` tour shoots a briefing's wreck allowance, checks the HUD's allowance row after each
  staged wreck, and ends on two staged dawn debriefs. The `input` tour drives the real mouse and
  keyboard path, then a simulated gamepad (menus, aim, focus, horn and pause). It also checks that
  prompts follow the device, that a hint seen once stays away, that losing focus or unplugging the
  pad pauses the night, and that the d-pad walks both settings columns. It switches Focus to Toggle
and checks each control both ways, checks the pointer hides for pad play, and logs the input
devices Unity sees. `radiolog` pauses night 6 late on to read back the radio log. `screens` checks,
  at whatever window size the player starts with (`LL_W` and `LL_H`), that every menu panel is on
  screen, that the HUD's blocks don't overlap (night 11, the longest top bar), and that the harbour and
  every hazard are in view (judged from the play view at rest; the live view, which leans toward
  the beam, is logged beside it). `watchend` ends a Night Watch from the pause menu and checks it was kept.
  `offscreen` waits for Corley Cove's and West Point's lanterns to burn (nights 9 and 10) and checks
  their edge markers and a lured ship's mark stay on screen. `confirm` checks the pause menu's
  questions with keys, the simulated pad and clicks. `brightness` measures the scene at each
  Brightness step. `keys` rebinds keys through Settings with the simulated keyboard and plays a
  night with them. `names` checks that Porthkell, a ship on the radio, the Hen Bell and the Teeth
  are labelled where they are (and the Teeth not before they're charted). `notes` opens the
  keeper's notes from the title with a new and a finished season, walks them with keys and a
  pad, checks every entry fits, and opens them from the pause menu on night V. `framerate`
  measures a night at each Frame rate choice (`-llFrameNight 1` plays a cheaper night). `chart`
  stages a wreck on night II and lost and lured ships on night IX, opens the dawn chart with a
  click, the arrows and the simulated pad, and checks each wreck's cross sits where the wreck lies.
  `lamps` (with `-llSampleSave`) checks the HUD's lamps against the simulation every frame and
  against the dawn card, and reads the briefing's record line. `names` also leaves the Little Auk
  in the dark on night III and checks every frame that no name covers another or a ship's mark,
  and `chart` that none of the chart's names covers another or a cross (`-llNamesOverlap` draws
  names as before round 8, to show the checks catch it). `best` stages bests of 880 on night III
  and 600 for a watch and checks every frame that the line under the score matches the
  simulation. `help` fails night II again and again from the dawn card and checks the offer of
  help at each step.
  `endingskip` skips the ending with Esc, a pad's Start and a pad's B. `speed` measures each game
  speed (sim seconds per real second) and checks the dawn card and watch table marks. `flash` measures screen
  brightness on a lightning strike with Reduce flashing off and on. `breakers` captures a breakers
  warning and a full-astern call. `watch` (with `-llSeasonDone`) adds a squall at full blow.
  `-llRenderScale 70`, `-llReduceFlashing`, `-llHard` and `-llHudScale 130` set those options for
  any tour. Give `<dir>` as an absolute path, because the player doesn't resolve relative ones. No real gamepad has been tested, only Unity's simulated device.
- `Tools/season_tour.sh <reset|damaged|migrate|readonly|unopenable> <dir>` runs the `season`
  tour, which uses a real save, against a throwaway config directory under `<dir>` seeded with
  one: a finished season cleared from the logbook, a damaged save, an older build's save carried
  over, a save folder that can't be written (made read-only for the run), or something at
  `save.json` that can't be opened. It refuses to
  run anywhere else, and checks your own save folder is unchanged.
- `Tools/.venv/bin/python Tools/cvd_sim.py OUT.jpg "Label=shot.png:x,y,w,h" ...` shows screenshot crops
  as seen with deuteranopia and protanopia (Machado 2009), for checking that states read without
  colour. `Tools/tour.sh status` captures a lost ship and a lured ship for it, and checks that each
  top-bar mark (lost, lured, wrecked, home) matches its ship.
- `Tools/.venv/bin/python Tools/audio_check.py` measures every synthesized clip: clipping, true
  peak, EBU R128 loudness, DC offset, clicks, and the seams of looped clips.
- `Tools/record.sh` records a four-minute gameplay reel with sound.

### The trailer

`Tools/make_trailer.sh` rebuilds everything in `docs/media` from the built game. The `trailer`
tour (`Assets/Scripts/Automation/Trailer.cs`) plays each shot as a scripted night. The AutoKeeper
does the playing, with a little staging: a neglected trawler, a forced foghorn, a held focus. The
simulation is deterministic, so a headless twin of each night runs first to find the frame where
the moment happens. The real night is then fast-forwarded off camera and recorded at a fixed 30
fps, with the game's audio mix and music muted. `Tools/make_trailer.py` cuts the clips to the bars
of the title waltz, which it re-synthesizes from the game's own music generator. It also animates
the captions in the game's fonts, ducks the music under the game's sound, normalizes to −16 LUFS
and encodes the trailer, the poster and the teaser loop. `Tools/make_trailer.sh --edit` re-edits
an existing shoot. The shoot turns off names on the water, the lamps at stake and the score to
beat, which came after the released trailer, so a re-shoot matches it.

## Project structure

```
Assets/
  Scripts/Sim/         Pure C# simulation: map, beam, ships, reefs, buoys, fog, wreckers, storm,
                       scoring, the Night Watch generator, the AutoKeeper bot, content validation
  Scripts/View/        World, ship and effect views, camera rig, materials, model loading
  Scripts/Core/        Game flow, mission runner, input, radio, save data, the ending
  Scripts/UI/          HUD and menu screens, built in code
  Scripts/Audio/       Pooled SFX buses and the layered music director
  Scripts/Automation/  Screenshot tours, the gameplay recorder and the trailer shoot
  Shaders/             Water, volumetric atmosphere, sky, lit props, glows, rings, reef foam
  Resources/Data/      merrow_bay.json (the map) and missions.json (the twelve nights)
  Resources/Models/    FBX exported from Blender
  Resources/Audio/     Synthesized OGG effects, voices and music
  Resources/Fonts/     Cormorant Garamond, Alegreya Sans, Special Elite, with their licenses
  Editor/              Project setup, build script, import settings
  Tests/EditMode/      Content proofs (see Tests and validation)
ArtSource/
  blender/             bpy model generators (build_assets.py is the entry point) and .blend scenes
  audio/               numpy/scipy synthesis of effects, voices and music
  map/                 merrow_bay.py and missions.py, which write the JSON data
Tools/                 Editor, build, play, tour, validation, chart, audio-check and trailer scripts
docs/                  BRIEF.md (the original brief), PLAN.md (the design and technical plan), media/
```

## Tech highlights

- **A simulation you can prove things about.** All gameplay lives in a pure C# `SimWorld`,
  stepped at a fixed 60 Hz with a seeded RNG. The view layer only reads it and interpolates. The
  same code runs in the game, in the EditMode tests and under the AutoKeeper bot, which plays
  through the same input struct as a player, so the lens's speed limits apply to it too.
- **What looks lit is lit.** The beam test (cone falloff, range, the shadows of sea stacks and fog
  extinction) is written once in C# and mirrored in HLSL (`LLCommon.hlsl`). The glow you see on
  the water is the same function the ships respond to.
- **Volumetric night.** A full-screen raymarch (`Atmosphere.shader`) computes single scattering
  from the beam analytically per sample, together with moonlit haze and drifting fog banks. The
  fog quality setting is the step count. Water uses Gerstner waves, a moon glitter path, and a foam
  texture the simulation writes for charted reefs and wakes.
- **One source of truth for the coast.** `merrow_bay.json` drives the simulation and Blender's
  generators for the cliffs, rocks and buoys, so geometry and gameplay can't drift apart. The
  generators can render a preview of each model for review (`--preview`).
- **Synthesized sound.** Radio voices are formant-synthesis gibberish, pitched and timed per
  character and band-passed like a wireless. The score (a waltz, a night bed with a tension layer
  that rises as ships get lost, dawn, and a music-box ending) comes from the same Python
  generators, and a measurement pass guards against clicks, loop seams and loudness drift.
- **Balance by bot.** The AutoKeeper wins every night, and a deliberately sloppy novice model gives
  a rough difficulty curve across the season.

## Credits and tooling

Made by [nearbycoder](https://github.com/nearbycoder). The original brief and the full design
plan are in [`docs/BRIEF.md`](docs/BRIEF.md) and [`docs/PLAN.md`](docs/PLAN.md).

- **Engine:** Unity 6000.6.2f1 with the Universal Render Pipeline and the Input System.
- **Models:** generated with Blender 4.5's Python API (`ArtSource/blender`).
- **Sound and music:** synthesized with numpy and scipy (`ArtSource/audio`).
- **Fonts:** [Cormorant Garamond](https://github.com/CatharsisFonts/Cormorant) and
  [Alegreya Sans](https://github.com/huertatipografica/Alegreya-Sans) under the SIL Open Font
  License 1.1, and Special Elite by Astigmatic under the Apache License 2.0.
- **Trailer and captures:** FFmpeg, numpy and Pillow.

See [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) for the full list of third-party
components and their licenses.

## Status and known issues

Version 0.1.0 is the complete first version: twelve nights, the ending and the Night Watch, on
Linux. Here's what is still unproven or rough:

- **Not yet playtested by a person.** Balance comes from the AutoKeeper and the novice model, and
  feel was judged from screenshots, video and scripted input. Drain rates, ship schedules and the
  Night Watch's ramp may need tuning once people play it.
- **The audio has only been measured.** Every clip passes `Tools/audio_check.py`, but no one has
  yet judged whether the music and gibberish voices are pleasant to listen to.
- **Gamepad support is tested only with a simulated device.** Button layouts and stick dead zones
  on real controllers are unverified. An 8BitDo receiver is plugged into the development machine,
  but Unity listed only the mouse, keyboard and touchscreen (the pad was presumably off), and no
  one pressed its buttons.
- **Linux only, as published.** `Tools/unity.sh build-mac` makes a universal macOS app (Mono, macOS
  12 or later) and `Tools/package.sh <version> mac` zips it with instructions for opening an
  unsigned app. The build succeeds on Linux, and both architectures of the executable carry
  Unity's ad-hoc signature, but it isn't signed with a Developer ID or notarized, and **it has
  never been run on a Mac**. Signing needs an Apple Developer account. `build-windows` is ready
  but needs Unity's *Windows Build Support (Mono)* module, which isn't installed on the
  development machine; without it the command stops with a clear message. There's no web build.
- **Balance shifted after v0.1.0, and that shift hasn't been tested by people.** The wrecks that ended
  the bot's Night Watches turned out not to be late charts. Steamers and ferries threading Widow's
  Ledge struck reefs that had been charted well ahead, because their avoidance couldn't make the
  turn. Captains now ring for full astern when a charted reef or sandbank is closer than they can
  turn away from, so the wrecks left are uncharted rocks, lost ships and lured ships. The bot and
  the novice model now rarely or never wreck (see the latest report under Tests and validation),
  so the season and the Night Watch may now be too forgiving for strong players. The report shows
  even the novice keeping a Night Watch for 30 minutes. **Hard** (Settings ▸ Difficulty) is the
  tougher choice. It leaves Standard exactly as it was, and a test pins the AutoKeeper's Standard
  scores on all twelve nights. If that's the
  case, tighten them with deliberate levers (drain rates, schedules, the Night Watch's ramp)
  rather than steering faults.
- Fog nights on the High quality setting can drop below 60 fps on weaker GPUs. Medium, or a
  render scale of 70%, is the safer choice there. On the development machine's Radeon 8060S at
  1600×900, night 5 averaged about 50 fps at 100% and about 74 fps at 70%. Those runs were made
  while the machine was heavily loaded by other work, so treat the numbers as rough.
- On the development machine, the X11/XWayland window path hung at startup. Use `LastLight.sh`
  under Wayland.
- **Screen shapes were checked in windows, on one machine.** The `screens` tour passes at
  1280×800, 1280×1024, 1600×900 and 2560×1080 windows on KDE Wayland. Fullscreen at those shapes, real
  4:3 or 16:10 monitors, and a Steam Deck haven't been tried. West Point's false light and the rock it
  lures ships onto sit just past the left edge of the frame at 16:9 and narrower (Corley Cove's
  lantern just past the right), as released. The camera wasn't reframed, because that would change
  every capture and the trailer; instead a marker at the edge points to a burning lantern there, and
  a lured ship's mark stays on screen, though the ship itself still wrecks a little off screen.
  Once, in round 4, the `ui` tour's switch from a 1280×1024 window to 1280×720 didn't take. It
  didn't happen again in three runs in round 5 (the resize took effect within 0.01 s), so the cause
  is unknown.
- **Rebinding covers the keyboard only.** The gamepad's buttons are fixed (there's no real pad to
  check a remap on). Key names come from the keyboard layout, so an AZERTY keyboard should show "Q"
  for the default turn-left key, but only a US layout has been tried.
- **Game speed shares records.** A night kept at 70% counts in the logbook like any other, and only
  the dawn card and the watch table mark it. Whether slowed scores should be kept apart is open.
- **Names on the water and the keeper's notes are judged by tours, not people.** Whether the
  labels help or clutter, and whether the notes say enough, needs a playtest. Only places a
  call names, and reef groups the first time they're charted, get labels; a call that names
  nothing on the map ("the Hen's side") points at nothing.
- **Frame rate was measured in a window, at low settings, on one machine.** For much of round 6
  the shared development machine's GPU was fully busy with other work and the game reached only
  about 30 fps even at 640×360. When it freed up, night I at 1280×720 (50% render scale, low fog)
  measured 120.0, 60.0 and 30.0 fps for Display (a 120 Hz screen), 60 and 30. Battery and heat
  savings on a real laptop or handheld haven't been measured.
- **The save moved in round 7.** The built Linux player turned out to keep its PlayerPrefs, and
  so the whole save, in `~/.config/unity3d/unknown/unknown/prefs`, a file that every Unity player
  with the same fault shares (on the development machine other games write to it too), where
  another game could overwrite or clear it. The save is now `save.json` in the game's own data
  folder (`~/.config/unity3d/Gannet Head/Last Light/` on Linux), written to a temporary file and
  swapped in whole. The first launch of this build carries an older save over from PlayerPrefs
  and leaves the old entry where it was. A save that can't be read is kept as
  `save.unreadable.json`, and "Start a new season" keeps the season it clears as
  `save.previous.json`; neither can be restored from inside the game, only by renaming the file.
  This was checked on Linux only; macOS and Windows use Unity's usual data folder, untested.
- **The dawn chart and the lamps at stake are judged by tours, not people.** Whether the chart
  is worth opening, whether a busy night's tracks read, and whether the lamps under the score
  help or distract need a playtest. The same goes for round 8's score to beat, the score's parts
  on the dawn card and the offer of help after two failures (its wording, and whether two is the
  right number, are guesses).
- **Save trouble was checked on Linux only.** The notices for a save that can't be read, opened
  or written were checked with a read-only folder and an unopenable `save.json` in a throwaway
  config. A full disk wasn't tried, and the wording of the reasons comes from the exception, so on
  macOS and Windows it may read differently.
- **Names step aside, but only a line or two.** On a crowded spot (two wrecks at the same rock on
  the chart) the names stack up beside each other; when no clear place is near, a name takes the
  least covered one.
- **No license has been chosen yet.** Until one is added, all rights are reserved. The bundled
  fonts keep their own open licenses.
