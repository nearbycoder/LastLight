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
| Focus: a narrow, long, bright beam that turns slower | Hold the **left mouse button**, **Shift**, **W** or **↑** | Hold either trigger |
| Sound the foghorn (14 s cooldown) | **Space** or the **right mouse button** | **A** |
| Pause, or back out of a menu | **Esc** or **P** | **Start** (and **B** in menus) |
| Move through and choose menu items | Mouse, or **arrow keys / Tab** and **Enter** | D-pad or stick, **A** to choose |

The game starts fullscreen. Settings has volumes for master, music, effects, radio and ambience,
along with text speed, fog quality, screen shake, hints, lens turn speed, windowed or fullscreen,
and resolution.

### The rules in brief

- **Ships follow the light.** Each ship has a confidence ring. It refills in your beam and drains
  in the dark. At zero the captain is **Lost**: the ship slows, wanders and drifts toward the shore
  until you light it again.
- **Reefs are hidden.** Captains' routes run straight over rocks they can't see. Light a reef
  briefly to **chart** it. White water breaks over it and captains steer around it. A chart fades
  about 22 seconds after the light leaves it, so time your sweeps to stay just ahead of each ship.
- A night ends when every scheduled ship has made port or been lost, and fails at once if the wrecks
  exceed that night's allowance. You earn a lamp for keeping the light through the night, a second
  for losing no ship, and a third for a **steady hand**: no ship ever Lost or Lured.

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
or light the ship to break the spell. On night 11, one false light copies your every sweep.

### Every ship counts
<img src="docs/media/wreck.jpg" width="100%" alt="A trawler left in the dark strikes an uncharted reef and burns, its ring turned red">

Miss a reef and the hull strikes, burns and sinks, and the crew's lifeboat drifts clear. The
captains talk you through it all on the radio. Ianto the harbourmaster, cheeky Maren on the
*Little Auk*, gruff Captain Pryce on the *SS Calloway* and Dot on the ferry *Evening Star* all
grumble, joke, panic and thank you in typed text over synthesized gibberish voices.

### Twelve nights, three lamps each, and the Night Watch
<img src="docs/media/results.jpg" width="49%" alt="Dawn results for night II: three lamps lit, five ships home, none wrecked"> <img src="docs/media/logbook.jpg" width="49%" alt="The keeper's logbook: nights kept with their lamps, later nights still sealed with wax">

Dawn tallies every night with up to three lamps, and the keeper's logbook keeps your best for each
night so you can go back for a cleaner watch. Finish the season and the **Night Watch** opens: an
endless score attack, generated fresh each time, with every hazard out and no end to the ships.

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
| ∞ | Night Watch | Unlocked after the season: endless, every hazard at once, the third wreck ends the watch |

| Hull | Character |
|---|---|
| Trawler | Small and quick, turns tightly, loses confidence fast |
| Collier steamer | Big and slow, turns wide, runs aground on sandbanks that trawlers sail over |
| Passenger ferry | Rows of lit windows, and worth the most |

Around the nights you'll find a live title scene, briefing cards, the radio, pause and settings
menus, dawn results, an ending, and credits that name every ship you brought home. Progress and
settings are saved locally.

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
There's no Windows or macOS build yet.

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
Tools/play.sh                  # run the build (LL_W=1920 LL_H=1080 for a window size)
Tools/package.sh 0.1.0         # zip the build for a release, into Builds/release
```

`Tools/unity.sh` expects the editor at `~/Unity/Hub/Editor/6000.6.2f1` (set `UNITY` to change
that). On Arch-based distributions the editor needs the legacy `libxml2.so.2`
(`sudo pacman -S libxml2-legacy`). Alternatively, set `LASTLIGHT_UNITY_LIBS` to a folder that
contains a copy of it.

### Tests and validation

- `Tools/unity.sh test` runs the EditMode tests (22 of them). They check that every mission
  references valid map data, that every reef, buoy and wrecker lantern is reachable by the beam,
  that every route is safe for every hull once its hazards are charted, that the **AutoKeeper**
  bot wins all twelve nights in the pure simulation, and that the bot keeps a generated Night
  Watch for at least ten minutes.
- `Tools/validate.sh` prints the same checks as a report from a resident editor
  (`Tools/unity.sh serve`). `Tools/tour.sh report <dir>` produces the report from the built player.
  The report also plays every night with a **novice keeper**, which is slow to react, has a shaky
  hand and doesn't know where the reefs are.
- The latest report, from the v0.1.0 build: the AutoKeeper wins all twelve nights, with three lamps
  on eleven of them. On night 9 it chases the wreckers' lantern and loses a ship, so it gets one
  lamp. The novice wins every night in three runs each. Its lamps fall from 9/9 on nights 1 to 3
  to 3/9 on nights 7 and 8 (the first fog with dark ships, then the storm), then recover to 5 to
  8 on nights 9 to 12. The bot keeps the Night Watch for 16 to 25 minutes (57 to 117 ships home).
- `Tools/tour.sh <ui|nights|ending|input|watch> <dir>` plays the built game with scripted input
  and saves screenshots. The `input` tour drives the real mouse and keyboard path, then a
  simulated gamepad.
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
an existing shoot.

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
  on real controllers are unverified.
- **Linux only.** There are no Windows, macOS or web builds yet.
- In the Night Watch, steamers threading the narrow water between Widow's Ledge and the Teeth can
  run onto a reef that was charted late. That ends most of the bot's watches. It's fair (chart
  ahead of the slow ships), but it can feel abrupt.
- Fog nights on the High quality setting can drop below 60 fps on weaker GPUs. Medium, or a lower
  fullscreen resolution, is the safer choice there.
- On the development machine, the X11/XWayland window path hung at startup. Use `LastLight.sh`
  under Wayland.
- **No license has been chosen yet.** Until one is added, all rights are reserved. The bundled
  fonts keep their own open licenses.
