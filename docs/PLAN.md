# Last Light: Design and Technical Plan

> **Pitch:** You keep the last lighthouse on a wrecking coast. Sweep its beam through the night to
> guide ships home, chart the hidden reefs ahead of them, and expose the false lights trying to
> lure them in.

The lighthouse is being decommissioned at the end of the season. You have twelve nights left.

---

## 1. Design pillars

1. **The beam is the game.** You do one thing: point the light. Pointing it has to feel heavy,
   warm and alive, with a brass lens turning on its bearing, a shaft cutting through mist, and
   silhouettes rising out of the dark. Every system reads through the beam: lit things are
   safe, known and visible, and dark things are risky.
2. **Calm surface, tense decisions.** The scene is quiet, with moonlit swell, soft music and a
   slow sweep. The pressure comes from attention. There are three ships, one beam, and a reef
   about to be met. Every decision is readable at a glance: who is losing their bearings, what
   lies uncharted ahead, and where the false light is.
3. **Short nights, one more try.** Each mission (a "night") takes 3 to 5 minutes and is rated with
   three lamps. Failure is quick and legible ("the *Calloway* never saw the Teeth"), so the
   player always knows what to do better.
4. **Voices on the radio.** Captains have personalities. They grumble, joke, panic and thank you.
   The coast feels inhabited, so the final switch-off lands emotionally.

---

## 2. Core loop

**Second to second:** look for whichever ship is fading. Swing the beam to it, using the shaft
to sweep the water ahead of it and chart reefs. Then swing away to the next ship before the
first one runs out of confidence.

**Night (mission) loop:**
1. **Briefing card:** night number, name and the harbourmaster's radio line. A new element is
   shown as a picture, not a paragraph.
2. **Watch:** ships enter from the edges on a schedule. Keep them confident, chart hazards ahead
   of them, light buoys to look after the channel, sound the foghorn in fog, and douse false lights.
3. **Dawn:** the night ends when every scheduled ship has either made port or been lost. Results
   tally ships saved, wrecks and lamps (stars).

**Meta loop:** 12 nights unlock in sequence. A logbook stores the best lamps and score for each
night. Finishing night 12 plays the ending and unlocks **Night Watch**, an endless score-attack
mode (stretch goal, see the milestones).

---

## 3. Mechanics in detail

All gameplay runs in a pure C# simulation (`SimWorld`) on the XZ plane (units ≈ 10 m). Views
read from it. Numbers below are starting values that get tuned in the prototype.

### 3.1 The beam
- The lens sits in the lantern room at `L = (0, 18, 0)` on Gannet Head.
- **Aim:** the mouse ray hits the sea plane, which gives a target bearing θ*. The lens follows
  with a critically damped spring, `ω_max = 200°/s` wide and `110°/s` focused, and angular
  acceleration capped so the swing has weight. Keyboard A/D or the arrow keys rotate the lens
  directly, and a gamepad right stick sets the bearing.
- **Wide beam (default):** cone half-angle 13° with a soft edge, range 95 u. The hot core lights
  ships strongly. The fringe (edge 35%) lights them weakly, with partial confidence regen.
- **Focus (hold LMB, Shift or RT):** the lens narrows. Half-angle 4.5°, range 175 u, 2× charting
  speed, penetrates fog, and douses false lights 2× faster. It turns slower. The trade-off is
  reach and power versus coverage.
- **Lit test:** `I(p) = coneFalloff(angle) · rangeFalloff(dist) · occlusion · fogAttenuation`.
  - Occlusion: 2D line of sight from the lens to p against *tall stacks* (Black Hen, the
    Sentinels) and land polygons. Rocks cast real beam shadows, and buoys exist to cover those.
  - Fog: the distance travelled inside fog banks attenuates the wide beam strongly
    (`exp(-0.06·d)`) and the focus beam weakly (`exp(-0.015·d)`).
- A point is **lit** when `I > 0.25`.

### 3.2 Ships and confidence
Each ship has `confidence ∈ [0,1]`, shown as a thin ring arc around the hull.
- **Lit:** confidence refills (`+2.2/s`) and the ship sails at full speed toward its next waypoint.
- **Dark:** confidence drains at the type rate (×1.6 in fog, ×1.5 if damaged). The ship keeps
  going at 70% speed, "feeling its way".
- **Lost (confidence = 0):** the captain loses the route. Speed drops to 45%, the heading
  wanders, and the tide current pulls the ship toward the shore. The ring turns red and pulses,
  and a "?" glyph appears. Lighting it again restores *Sailing* immediately. The ship then turns
  back toward its route within its turning radius, so late rescues can still hit something.
- **Arrived:** reaching the harbour mouth or the far map edge on its route. Score, radio thanks,
  horn.
- **Wrecked:** touching a reef, stack, shoal (steamers only) or land. Crunch, sink animation,
  debris, radio mayday. Lifeboat lights drift away, so the crew survives and the tone stays humane.

### 3.3 Hidden reefs and charting
- **Stacks** (tall rocks, islets) are always known. They're visible silhouettes that captains
  avoid on their own.
- **Reefs** are low, awash and invisible in the dark. Captains don't know about them, and each
  captain's intended route runs straight across some of them. Those routes are the "hidden routes".
- A reef is **charted** once it has been lit for 0.35 s total (0.18 s focused). A charted reef
  shows white breaking foam and a faint chart glyph, and captains steer around it. The chart
  fades after **22 s** without light, and the reef goes back to dark and unknown.
- Captains use look-ahead avoidance. Steamers turn wide and need ~25 u of warning, trawlers
  ~10 u. **Charting ahead, at the right moment,** is the skill.
- **Shoals** (the Long Sands) are large, shallow sandbars that are only dangerous to deep-draught
  steamers. They're charted the same way.

### 3.4 Buoys
- Lateral channel buoys (red can, green cone, a bell buoy) sit dark at the start.
- Sweeping the beam over a buoy **charges** it fully, with a bell chime. Its lamp burns for
  **28 s** and dims over the last 8 s.
- A burning buoy projects a **guidance aura** (radius 20 u). Ships inside it don't lose
  confidence and slowly regain it. Buoys let the keeper hand a channel to the buoy for a while.
  They're the main tool against beam shadows and multi-ship pressure.

### 3.5 Fog (sea fret)
- Soft-edged fog banks drift with the wind and are rendered volumetrically.
- Inside fog, ships drain 1.6× faster, their running lights shrink to faint halos, and the wide
  beam barely reaches through. Focus cuts through.
- **Foghorn (Space, A button):** a diaphone blast with a 14 s cooldown. Every ship within 130 u
  gets +0.45 confidence. Lost ships recover to *Sailing* with 0.35, and their positions ping
  briefly on the water. It's a pressure valve, with a visible shockwave across the sea.

### 3.6 Damaged vessels
- They have no running lights, so they're invisible in the dark until the beam finds them.
- They move at 70% speed and drain 1.5× faster.
- When they enter, they radio a rough bearing ("somewhere off the Teeth!"), and a bearing tick
  appears on the HUD compass ring. A red **distress flare** goes up every 18 s and lights its
  surroundings briefly.
- Each one is worth double points.

### 3.7 False lights (wreckers)
- Wreckers on cliff tops and islets show a lantern that **imitates your light**: a warmer,
  flickering beam that sweeps a sector.
- A ship caught in a false beam while **not** lit by yours becomes **Lured**. It steers
  confidently toward the false light, which leads it straight onto the rocks beneath. Its ring
  turns sickly amber.
- **Douse it:** hold your beam on the false lantern for 1.4 s (0.6 s focused). The lantern
  gutters out with sparks and a startled shout on the radio. The wreckers relight at another
  site after a delay.
- Lighting a lured ship with your beam breaks the spell immediately.
- In night 11, a false light copies *your* sweep pattern, and ships can't tell the two apart
  unless you're on them.

### 3.8 Storms
- A **current** vector pushes every ship, and Lost ships twice as hard. Rain cuts beam range by 15%.
- **Lightning** strikes every 9 to 16 s. It flashes the whole bay for 0.25 s, briefly charting
  every reef, then the thunder follows. You can use the storm.

### 3.9 Scoring and lamps (stars)
- Ship arrived: trawler 100, steamer 150, ferry 250. ×2 if damaged. +50 "steady hand" if it
  never went Lost or Lured.
- A night **fails** immediately when the wrecks exceed its allowance (usually 1 to 2).
- 🜂 **Light kept:** the night was survived.
- 🜂🜂 **No wrecks.**
- 🜂🜂🜂 **Steady hand:** no wrecks, and no ship ever went Lost or Lured.

---

## 4. Vessel types

| | Trawler (*Little Auk*, etc.) | Collier steamer (*SS Calloway*, etc.) | Passenger ferry (*Evening Star*) |
|---|---|---|---|
| Length / radius | 6 u / 2 | 13 u / 3.6 | 10 u / 2.8 |
| Speed | 7.5 u/s | 4.6 u/s | 6.0 u/s |
| Turn rate | 70°/s | 22°/s | 40°/s |
| Confidence drain | full → 0 in 7 s | 14 s | 10 s |
| Look-ahead | 12 u | 28 u | 18 u |
| Hazards | reefs, stacks, land | + **shoals** (deep draught) | reefs, stacks, land |
| Points | 100 | 150 | 250 |
| Read | small, skittish, in groups | big, slow, wide turns, smoke | rows of lit windows, precious |

Each type has a different hull silhouette, horn, wake and radio voice.

---

## 5. The map: Merrow Bay

One coastal map. The camera looks north out to sea from above the south shore.

```
 z=110  ...........................open sea / northern lane...........................
        W1→                         N2↓                         N3↓              →E1 exit
                Black Hen ▲(stack, casts beam shadow)
                                                    ≈≈ The Merrow Teeth (reefs) ≈≈
   W2→        ~~Long Sands~~ (shoal)
                   o buoys (channel)             ▲Sentinels         ≈ Widow's Ledge ≈  ←E2
   Porthkell ⌂⌂⌂ harbour mouth          ◉ GANNET HEAD LIGHT
 z=-45 ═══ shore / cliffs ══════════╗   (headland)   ╔═══════════ east cliffs, Corley Cove ═══
```

| Feature | Approx. position | Purpose |
|---|---|---|
| Gannet Head Light | (0, 0), lantern y=18 | the player. 360° beam, headland blocks due south |
| Porthkell harbour mouth | (-92, -22) | main destination, behind a breakwater with a small harbour light |
| Long Sands | (-62, 8), 30×10 ellipse | steamer-only shoal guarding the west approach |
| Channel buoys | (-70,24), (-52,-4), (-78,-8), (-40,30) | the bell buoy and lateral pairs mark the deep channel into Porthkell |
| Black Hen | (-48, 52) | a tall islet that throws a long beam shadow to the north-west. Wrecker site |
| The Merrow Teeth | (40, 45) to (70, 70) | a reef cluster crossing the direct N→harbour and E→W routes |
| The Sentinels | (32, 12) | two tall stacks near the light. Beam shadow to the east |
| Widow's Ledge | (95, 18) | reefs along the coastal route from the east |
| Corley Cove | (112, -30) | cliff cove. Wrecker site 2 |
| West Point | (-128, 2) | wrecker site 3 |
| Entry points | N1(-70,118), N2(0,118), N3(75,118), E1(140,70), E2(140,22), W1(-140,72), W2(-140,30) | |
| Exits | east edge, west edge, north edge | through traffic |

Routes are authored polylines (captain intentions) stored in `Resources/Data/merrow_bay.json`.
They deliberately pass over reefs and shoals. Blender reads the same JSON to build the coast,
rocks and buoys, so geometry and gameplay can't drift apart.

---

## 6. The twelve nights

| # | Name | New element / purpose | Traffic | Hazards active | Wrecks allowed |
|---|---|---|---|---|---|
| 1 | **First Watch** | Aiming. Ships follow the light, and confidence drains in the dark | 3 trawlers, spaced | none | 1 |
| 2 | **The Teeth** | Hidden reefs. Chart ahead of a ship. Focus introduced for reach | 4 trawlers + 1 steamer | Merrow Teeth | 1 |
| 3 | **Lantern Row** | Buoys. Charge the channel while you tend others | 6 (2 steamers) | Teeth, buoys | 1 |
| 4 | **Deep Water** | Steamers and shoals. Different hulls, different dangers | 7 (3 steamers) | Teeth, Long Sands, buoys | 1 |
| 5 | **Sea Fret** | Fog and the foghorn. Silhouettes out of the murk | 7 | + fog banks | 1 |
| 6 | **The Evening Star** | The ferry, the precious cargo. The first big multitask | 8 (ferry ×2) | Teeth, Sands, Widow's Ledge | 1 |
| 7 | **Mayday** | Damaged vessels. Find a dark ship from its radio bearing and flares | 8 (2 damaged) | all reefs, light fog | 1 |
| 8 | **Squall** | Storm current, rain and lightning that charts reefs | 9 | all + current | 2 |
| 9 | **False Light** | Wreckers. Douse the imitation light | 9 | + wrecker (Corley Cove) | 1 |
| 10 | **Two Lights** | Wreckers moving between sites, with fog | 10 | + 2 wrecker sites, fog | 2 |
| 11 | **The Mimic** | A false light that copies your sweep. Heaviest traffic | 12 | everything | 2 |
| 12 | **Last Light** | Finale: a storm, every named captain, a damaged ferry, the wreckers' last try. Dawn comes, the captain thanks you, and the light is switched off | 12 | everything | 2 |

The difficulty curve introduces one new tool or threat per night through night 9. Nights 10
and 11 combine them. Night 12 is a set piece. Concurrent ships rise from 1–2 (N1) to 5–6 (N11).
Each night opens with a 10 to 20 s calm stretch before the pressure builds, and a breather comes
after every peak.

### Onboarding (no walls of text)
- **N1:** a ghost mouse icon pulses: "Move to turn the light." The first trawler enters with a
  ring and the hint "Keep ships in the light." When its ring first drains below half, a gentle
  arrow appears. The radio does the rest, in the harbourmaster's voice.
- **N2:** the first reef gets charted by a scripted lightning flash (a free demonstration), then
  the hint "Sweep ahead of ships to chart reefs." When the first steamer is far away: "Hold to
  focus."
- Each later night shows one picture card in its briefing (icon + 6 words) and a one-time
  in-world highlight the first time the element appears.

---

## 7. Narrative and characters

**Setting:** Merrow Bay, a fictional rocky coast in about 1950. **Gannet Head Light** is being
replaced by a radio beacon. A letter from *the Board of Lights* (fictional) arrives on night 1:
"discontinued at the close of the season."

| Voice | Ship | Personality | Voice profile |
|---|---|---|---|
| **Ianto Rees**, harbourmaster | Porthkell harbour | warm, dry, briefs each night | mid pitch, measured |
| **Maren Holt** | trawler *Little Auk* | quick, cheeky, saving for a new boat | high, fast |
| **Capt. Edwin Pryce** | collier *SS Calloway* | gruff, superstitious, 40 years on this coast, distrusts beacons | low, slow |
| **"Dot" Okafor** | ferry *Evening Star* | sunny, chatty, narrates for passengers | mid-high, bouncy |
| **The Corleys** | wreckers | menacing whispers on an open channel (from night 9) | low, raspy, distorted |
| **The Board** | — | clipped officialdom (letters and the final call) | flat, nasal |

**Beats:** N1 the letter. N3 Maren jokes about the light being "the only one awake round here".
N6 Dot's passengers wave at the light. N7 Pryce's *Calloway* is damaged, and he's reluctantly
grateful. N9 the wreckers show up and Ianto warns about the Corleys. N11 the false light mimics
yours, and the Board says the beacon is "nearly commissioned". N12 a storm, everyone at sea,
and the Corleys' last attempt. At dawn the *Calloway* comes in last. Pryce: *"Gannet Head,
Calloway. That's us home. ...Thank you for the light, keeper."* The Board: *"Gannet Head, you
may extinguish."* The player **holds to extinguish**. The lens slows and stops, the lamp fades,
and the sun rises. Credits list every ship you brought home over the campaign.

Radio is stylized text (typewriter reveal) with a portrait chip, played over synthesized
gibberish voice, radio squelch and static.

---

## 8. Art direction

- **Mood:** a nocturne. Deep blue-black sea, cold moonlight, one warm column of light.
  Everything you care about is warm, and everything you fear is cold or sickly.
- **Palette:**
  - Night sea `#06121C` → `#0E2A3A`, moon rim `#9FB7D0`, sky zenith `#04070F`, horizon haze `#1B2D44`
  - Beam / lantern `#FFE4A8` (core) → `#FFC870` (fringe)
  - Ship lamps: masthead white `#FFF3D6`, port red `#FF4A3A`, starboard green `#46E08A`, windows `#FFB760`
  - Danger: charted reef foam `#E8F4FF`, Lost ring `#FF5A4A`, lured ring `#FF9A2E`, wrecker light `#FF7A3A`, flicker
  - UI: ink `#0B1118`, paper `#E9DFC7`, brass `#C9A35A`, muted `#7E8A96`
- **Shapes:** chunky, slightly stylized low-poly with clean silhouettes and bevelled edges.
  Readability comes first. Ships are recognizable from their silhouette alone.
- **Lighting:** a dim blue moon (directional), a **spot light** for the beam (shadows on, so
  rocks cast shadows across the sea), point lights for ship lamps, buoys and harbour windows
  (URP Forward+). Ambient is near-black, so unlit things genuinely disappear.
- **Camera:** fixed three-quarter view (pitch ≈ 52°, FOV 32°) framing the whole bay, with the
  horizon and moon at the top. A gentle idle drift, a slight push toward the beam, and shakes on
  wrecks, thunder and the foghorn. The title screen uses a low cinematic angle near the tower,
  then swoops down to the play view.
- **VFX:** volumetric beam shaft, drifting fog banks, a lens flare and glow at the lantern, ship
  wakes, reef foam, splash and debris on wrecks, distress flares, wrecker lantern sparks, rain
  sheets and lightning, foghorn shockwave ring, chart glyphs, buoy lamp halos.
- **Post:** bloom (a high-quality, wide halo around lamps), ACES tonemapping, cool-shadow and
  warm-highlight split toning, vignette, light film grain, slight chromatic aberration at edges.

## 9. Rendering tech

- **Water (`LL/Water`):** a dense grid with 4 Gerstner waves in the vertex shader, analytic
  normals plus scrolling procedural detail normals, Blinn-Phong for the moon and *all additional
  lights* (the beam spot and ship lamps make glittering paths on the swell), a fresnel sky
  reflection with a moon glitter path, depth-based shore and rock intersection foam, a far-distance
  fade to horizon haze, and a global "foam" texture written by the sim (charted reefs and
  wakes).
- **Volumetrics (`LL/Atmosphere`, a full-screen pass):** a raymarched height-limited haze and
  fog-bank volume (16–32 steps, blue-noise jittered). Single scattering from the beam is computed
  analytically per sample (cone test, soft edge, range falloff, flicker), plus moonlight
  in-scatter and fog bank density from global uniforms. The same pass draws the false lights'
  beams. This produces the shafts, the glow in fog, and silhouettes against lit mist.
- **Beam extras:** an additive near-field cone mesh, a lantern glare sprite, and a rotating
  Fresnel lens mesh with emissive panels.
- **Sky (`LL/Sky`):** a gradient, a hash-based star field with twinkle, a moon disc and halo,
  thin moving clouds, and a dawn variant for the ending.
- **Lit props (`LL/Lit`):** a simple stylized lit shader (vertex colour × base colour, wrap
  lighting, a rim term from additional lights so silhouettes pop when the beam passes). URP Lit
  is the fallback.

## 10. Audio direction

All audio is synthesized by Python and numpy scripts in `ArtSource/audio/` (rendered to WAV).
None of it is copyrighted.
- **Ambience:** the sea (layered filtered noise with slow swell envelopes and occasional wave
  crashes), wind, distant bell buoy, rain and thunder for storms, harbour murmurs.
- **The lens:** a looping mechanical whirr and gear-tick bed. Its pitch and volume follow angular
  speed. A carbon-arc sizzle rises during focus.
- **SFX:** foghorn diaphone ("BEEEE-ohh"), three ship horns (trawler toot, steamer deep chord,
  ferry two-tone), reef charted (soft glass chime and foam hiss), buoy bell, ship arrived (harbour
  bell and rising chord), ship lost (a dissonant low swell), lured, wreck (timber crunch, metal
  groan, splash), flare (whoosh and pop), false light doused (hiss and gutter), lightning crack and
  rolling thunder, UI clicks, page turns, typewriter ticks, lamp stars on the results.
- **Radio:** a squelch in and out, a static bed, and **gibberish voices** built from formant
  synthesis syllables (vowel formant tables, consonant noise bursts), pitch-contoured per
  character and band-passed to 300–3000 Hz with light distortion. Every character has a distinct
  pitch, rate and timbre.
- **Music:** an original score built from synthesized pads, a concertina-like reed lead and a
  bell/celesta FM motif. Tracks are *Keeper's Waltz* (title, 3/4, D minor and F major), *Night
  Watch* (an in-mission ambient bed with a tension layer that crossfades in as ships drift Lost),
  *Dawn* (results), and *The Light Goes Out* (the ending, the waltz slowed onto a music box).
- **Mix:** music −14 LUFS-ish under SFX. Radio ducks the music. The foghorn and wreck duck
  everything briefly. There are separate volume sliders.

## 11. UI/UX and controls

| Input | Action |
|---|---|
| Mouse move | Aim the beam (the lens follows with weight) |
| Hold LMB / Shift | Focus the beam |
| Space / RMB | Foghorn (once unlocked) |
| A/D, ←/→ | Turn the lens (keyboard play) |
| Esc / P | Pause |
| Gamepad | Right stick aims, RT focuses, A sounds the foghorn, Start pauses |

**Screens:**
- **Title:** a live 3D scene (a low angle at the tower with the beam sweeping overhead) with
  "LAST LIGHT" in an elegant serif. Begin / Continue, Logbook, Settings, Quit.
- **Logbook (night select):** a keeper's log page with 12 dated entries, three lamp icons each,
  best score, and locked entries shown in faded ink.
- **Briefing:** a night card with the date, name, the harbourmaster's line and one diagram card
  for the new element. Press to begin.
- **HUD:** top left, the night name. Top centre, the ship manifest (an icon per scheduled ship:
  waiting, at sea, home, lost). Bottom left, a radio panel with a portrait chip, name and
  typewriter text. Bottom right, the foghorn dial. Around the lighthouse, a subtle compass ring
  with incoming-ship ticks and distress bearings. In-world: confidence rings, an "incoming"
  chevron at the edge 4 s before a ship arrives, and a destination glow at the harbour mouth.
- **Pause:** Resume, Restart night, Settings, Logbook, Title.
- **Settings:** master, music, SFX and radio volumes, fullscreen, resolution, quality (volumetric
  steps), screen shake, beam sensitivity, text speed. Saved with PlayerPrefs.
- **Results ("Dawn"):** ships home, wrecks, steady-hand count and score. Lamps light one by one
  with chimes. Next / Retry / Logbook.
- **Save:** JSON in PlayerPrefs: unlocked night, best lamps and score per night, total ships
  saved, and settings.

## 12. Game feel and juice list

- The lens swing uses inertia: the shaft drags slightly behind fast swings, and the whirr pitch
  follows the speed.
- The beam has a hot core with a soft feathered edge, and the light dances slightly with the
  flame flicker.
- A ship catching the light gets a rim flash, a tiny "ping" chime (pitched per type), and its
  ring refills with a sparkle.
- Silhouettes emerge from fog as the shaft passes behind them.
- Charting a reef makes foam bloom outward in a ring, a chart glyph inks in, and a glassy chime plays.
- A buoy lighting up makes its lamp flare, the bell rings, and an aura ring expands.
- A Lost ship's ring blinks red, a dissonant swell plays, and a "?" bobs over the ship.
- Arrivals play a horn, harbour windows brighten, and a +points text floats up and flies to the
  score.
- Wrecks bring a camera shake, a splash column, debris, the ship tilting and sinking, lifeboat
  lights, a mayday on the radio, and a 0.2 s hit-pause in slow motion.
- The foghorn sends a shockwave ring across the water, the mist parts slightly, and a low camera
  rumble plays.
- Dousing a false light makes it sputter, throw sparks and go dark, followed by a radio shout.
- Lightning gives a full-bay flash, briefly reveals everything, and thunder follows the delay.
- On results, the stars light one at a time with rising chimes.
- Screen transitions dip to black with an iris, and the music crossfades.
- The menu is alive, with the beam always sweeping behind it.

## 13. Code architecture

```
Assets/
  Scripts/
    Core/        Game (state machine), Bootstrap, SaveSystem, Settings, Events, Missions loader
    Sim/         SimWorld, SimShip, SimReef, SimBuoy, SimFog, SimWrecker, SimBeam, MapData,
                 MissionData, Steering, Geometry2D (polygon, LOS)   <- pure C#, no MonoBehaviour
    View/        WorldView, ShipView, ReefView, BuoyView, BeamView, LighthouseView, FogView,
                 WreckerView, CameraRig, Fx, MaterialLibrary, ModelLibrary
    Audio/       AudioDirector (pooled SFX), MusicDirector (layers), RadioVoice, Ambience
    UI/          UiKit, Hud, Menus (Title, Logbook, Briefing, Pause, Settings, Results, Ending)
    Automation/  AutoKeeper (bot policy), AutoPilot (screenshot tour), Showcase
  Shaders/       Water, Atmosphere, Sky, Lit, BeamCone, Ring, Foam, Glow
  Resources/     Data/merrow_bay.json, Data/missions.json, Models/*.fbx, Audio/*.wav, Fonts
  Editor/        ProjectSetup, BuildScript, ImportSettings, Validation menu
  Tests/EditMode Mission solvability (bot), route safety, sim invariants
ArtSource/       Blender scripts (+ .blend), audio synth scripts
Tools/           unity.sh, play.sh, build_models.sh, build_audio.sh, autopilot.sh
```

- **Sim/view split:** `SimWorld.Step(dt, input)` is deterministic for a given seed and doesn't
  depend on rendering. The view layer interpolates and decorates. That lets EditMode tests run
  every night at 20× speed with a bot.
- **Data-driven:** the map JSON (coast polygons, stacks, reefs, shoals, buoys, routes, wrecker
  sites, entries) and the missions JSON (schedule, enabled hazards, fog, storm, wreckers, radio
  script by time or event, allowances).
- **Bootstrap:** a `RuntimeInitializeOnLoadMethod` builds the world from code. The single
  `Main` scene holds only the camera, moon light and post volume.
- **Events:** C# events from the sim (ShipLit, ShipLost, ReefCharted, BuoyCharged, Wreck,
  Arrived, Doused, Lightning, and so on) drive audio, FX, radio and UI.
- **Input:** Input System package (mouse, keyboard, gamepad).

## 14. Asset list (Blender, `ArtSource/`)

| Asset | Notes |
|---|---|
| `lighthouse.fbx` | tower (white with red band), gallery rail, lantern room glazing bars, a separate **Lens** object (rotates), keeper's cottage, oil store, walls |
| `merrow_bay.fbx` | coast terrain from the map JSON: cliffs (noise-displaced extrusions), beaches, grass tops, rocks at the cliff foot, Porthkell town (houses, chapel, quay, breakwater, harbour light), wrecker huts, a path to the light |
| `stacks.fbx` | Black Hen islet, Sentinels ×2, coastal stacks (noise-displaced) |
| `reefs.fbx` | 5 low reef rock variants (awash, flat-topped, jagged) |
| `buoys.fbx` | red can, green cone, bell buoy (cage and bell), each with a lamp anchor |
| `trawler.fbx` | hull, wheelhouse, mast, gantry, nets, lamp anchors (empties) |
| `steamer.fbx` | long hull, superstructure, funnel (smoke anchor), masts, lamp anchors |
| `ferry.fbx` | two decks, rows of window strips (emissive), funnel, lamp anchors |
| `debris.fbx` | planks, crates, barrel, lifeboat |
| `wrecker_lantern.fbx` | pole lantern and brazier |

Materials follow a naming scheme (`col_RRGGBB`, `glow_RRGGBB`, `glass_RRGGBB`, `metal_RRGGBB`).
Unity maps them onto shared project materials at load. Each model is render-checked in Blender
(contact sheet) before import.

## 15. Validation

- **EditMode tests (`unity test`):**
  1. *Route safety:* every route followed by a fully guided ship, with all hazards charted,
     reaches its destination without a collision.
  2. *Naive danger:* every reef that a mission enables is actually crossed by some route there,
     so no hazard is decorative.
  3. *Solvability:* the `AutoKeeper` bot (a greedy attention policy that lights the
     most-urgent ship, charts ahead, charges buoys, horns and douses) plays every night in the
     pure sim and must earn at least one lamp. Its lamps and score are logged as a difficulty
     report.
  4. *Data integrity:* the missions reference valid routes, entries and buoys.
- **Play verification:** an `-llAutopilot` flag runs a scripted tour in the player (title,
  briefing, mid-night action, fog, wreckers, results, ending) and saves screenshots. Console
  errors fail the run.

## 16. Milestones

1. **M0 Setup:** Unity URP project, git, .gitignore, tool scripts, the play-and-capture loop working.
2. **M1 Core prototype:** placeholder geometry. Water, sky, beam (spot and volumetric), ships with
   confidence, reefs and charting. Iterate on the *feel* with screenshots until the sweep is a
   pleasure. **Commit.**
3. **M2 Art pass 1:** Blender lighthouse, coast, ships, rocks and buoys, imported and lit. **Commit.**
4. **M3 Systems:** buoys, fog and foghorn, damaged ships, wreckers, storm, scoring, missions
   JSON for all 12 nights, the bot, EditMode tests. **Commit.**
5. **M4 Audio:** SFX, ambience, radio voices, music, mixing. **Commit.**
6. **M5 Flow and UI:** title, logbook, briefing, HUD, pause, settings, results, save, onboarding,
   ending sequence. **Commit.**
7. **M6 Polish:** VFX, post, transitions, camera, juice, balance via the bot and manual play, perf.
   **Commit.**
8. **M7 Ship:** README, Linux build in `Builds/`, final screenshot tour. **Commit.**
9. *(Stretch)* Night Watch endless mode.

## 17. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Volumetric raymarch is too slow on a shared iGPU | step count in the quality setting, half-res option, analytic beam (no shadow-map sampling) |
| URP 17.6 RenderGraph API churn | use the stock `FullScreenPassRendererFeature` with a material instead of custom passes |
| Unfocused editor freezes in Play mode, headless capture issues | `set_autotick` and `runInBackground`, with a fallback of building the player and running an autopilot screenshot tour |
| Gibberish voices sound bad | heavy radio band-pass, short syllables, rely on the text and the squelch |
| Steering AI does odd things (spinning, clipping coast) | circle obstacles, look-ahead tangents, route waypoints kept clear of land, EditMode route-safety tests |
| Balance is too hard or too easy | bot difficulty report per night, a tunable drain multiplier per mission |
| Scope | content is data-driven, so the 12 nights are JSON. Endless mode is optional |
| Shared machine | batch-mode Unity, headless Blender with capped threads, close editors when done |

## 18. The 5-minute prototype test

A new player gets five minutes: night 1 and most of night 2. To pass:
- **Within 10 seconds,** moving the mouse and watching the shaft swing through the mist feels
  good on its own, with no ships needed. The lens has weight, there's a whirr, and silhouettes
  catch the light.
- **By minute 2,** the player has felt the tug of two ships fading at once, and the relief of
  the ring refilling.
- **In night 2,** a steamer heading for the Teeth gets the reefs charted just in time, foam
  blooming and the ship swinging wide. Either that's a thrill, or the wreck makes it obvious why
  it happened.
- The results screen shows two lamps and names the missing one ("Steady hand: the *Little Auk*
  got lost once"). The player's reaction should be: *"I can do that clean. One more."*

If the sweep isn't satisfying with an empty sea, nothing else matters. That gets fixed first.
