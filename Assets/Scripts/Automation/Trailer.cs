using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using LastLight.Audio;
using LastLight.Core;
using LastLight.Sim;
using LastLight.View;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// The trailer shoot (<c>-llScript trailer</c>) and the README stills (<c>-llScript stills</c>).
    /// Each shot plays a night with the AutoKeeper plus optional staging: a neglected ship, a forced
    /// foghorn, a held focus. The simulation is deterministic, so a headless twin of the night is
    /// played first, with the same staging, to find when the moment happens. The rendered night is
    /// then fast-forwarded off camera to just before it, framed by <see cref="TrailerCam"/> and
    /// recorded. clips.json gives each clip's frame range in video.mp4 and the events during it;
    /// Tools/make_trailer.py edits the clips into the trailer. <c>-llShots a,b</c> films a subset.
    /// </summary>
    public static class Trailer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Tour.Scripts["trailer"] = Shoot;
            Tour.Scripts["stills"] = Stills;
        }

        const int Fps = 30;

        /// <summary>Staging for one night, built afresh for the rendered night and for its twin.</summary>
        public delegate Func<SimWorld, KeeperInput, KeeperInput> Staging(SimWorld w, AutoKeeper bot);

        /// <summary>A camera pose for a world; t is seconds from the start of the roll.</summary>
        public delegate CameraRig.Pose Framing(SimWorld w, Vector2 anchor, float t);

        enum HudMode { Full, None, Radio }

        sealed class Shot
        {
            public string Name;
            public int Night;
            public Staging Stage;
            public Func<SimWorld, bool> Moment;    // null: Offset is a night time
            public Func<SimWorld, Vector2> Anchor;  // read from the twin at the moment
            public float Offset;                  // the roll (or still) starts this long after the moment
            public float Length;
            public Framing Cam;                   // null: the play view
            public HudMode Hud = HudMode.Full;
            public float Limit = 400f;
        }

        sealed class Clip
        {
            public string Name;
            public int Start, Frames, Night;
            public float At;
            public readonly StringBuilder Events = new StringBuilder();
        }

        sealed class Ctx
        {
            public Tour Tour;
            public Recorder Rec;
            public TrailerCam Cam;
            public bool Stills;
            public readonly List<Clip> Clips = new List<Clip>();
        }

        // ------------------------------------------------------------------ the shoot

        static IEnumerator Shoot(Tour t)
        {
            var g = Game.Instance;
            var c = Prepare(t, false);
            c.Rec = Recorder.Begin(t.OutDir, Fps);
            yield return Hold(2.8f);   // the title fades in from black
            t.Log($"capture clock: deltaTime {Time.deltaTime:0.0000}, unscaled {Time.unscaledDeltaTime:0.0000}");

            var only = new HashSet<string>(Game.ArgString("-llShots", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            bool Want(string name) => only.Count == 0 || only.Contains(name);

            if (Want("title")) yield return TitleShot(c, "title", TitleFraming);
            if (Want("endcard")) yield return TitleShot(c, "endcard", EndcardFraming);
            foreach (var s in Shots())
                if (Want(s.Name)) yield return Film(c, s);
            if (Want("logbook")) yield return LogbookShot(c);
            if (Want("results")) yield return ResultsShot(c, 2);
            // The ending last: it follows the results, and its credits keep running after the clip.
            if (Want("ending")) yield return EndingShot(c);

            c.Rec.Finish();
            WriteClips(c);
            t.Log($"trailer shoot: {c.Clips.Count} clips, {c.Rec.Frames} frames ({c.Rec.Frames / (float)Fps:0.0} s)");
        }

        static Ctx Prepare(Tour t, bool stills)
        {
            var g = Game.Instance;
            g.AutoPlay = true;
            var save = SaveData.Current;
            save.hints = false;
            save.music = 0f;            // the score is laid in the edit, under the game's own sound
            Sfx.MusicVolume = 0f;
            return new Ctx { Tour = t, Cam = TrailerCam.Create(g.Rig), Stills = stills };
        }

        /// <summary>Waits a span of captured time (frames, not wall-clock seconds).</summary>
        static IEnumerator Hold(float seconds)
        {
            int frames = Mathf.RoundToInt(seconds * Fps);
            for (int i = 0; i < frames; i++) yield return null;
        }

        static IEnumerator Roll(Ctx c, string name, float seconds, MissionRunner runner = null, int night = 0)
        {
            if (c.Stills)
            {
                yield return c.Tour.Shot(name);
                yield break;
            }
            var clip = new Clip { Name = name, Start = c.Rec.Frames, Night = night, At = runner != null ? runner.World.Time : 0f };
            Action<SimEvent> log = e =>
            {
                if (e.Type == SimEventType.ShipIncoming || e.Type == SimEventType.ShipSpawned) return;
                clip.Events.Append(clip.Events.Length > 0 ? ", " : "")
                    .Append($"[{c.Rec.Frames - clip.Start}, \"{e.Type}\", \"{e.Ship?.Name ?? ""}\"]");
            };
            if (runner != null) runner.OnEvent += log;
            c.Rec.Rolling = true;
            int frames = Mathf.RoundToInt(seconds * Fps);
            for (int i = 0; i < frames; i++) yield return null;
            c.Rec.Rolling = false;
            if (runner != null) runner.OnEvent -= log;
            clip.Frames = c.Rec.Frames - clip.Start;
            c.Clips.Add(clip);
            c.Tour.Log($"clip {name}: {clip.Frames} frames from {clip.Start}, night {night} t={clip.At:0.0}");
        }

        static void WriteClips(Ctx c)
        {
            var sb = new StringBuilder("{\n  \"fps\": " + Fps + ",\n  \"clips\": [\n");
            for (int i = 0; i < c.Clips.Count; i++)
            {
                var k = c.Clips[i];
                sb.Append(string.Format(CultureInfo.InvariantCulture,
                    "    {{\"name\": \"{0}\", \"start\": {1}, \"frames\": {2}, \"night\": {3}, \"at\": {4:0.00}, \"events\": [{5}]}}{6}\n",
                    k.Name, k.Start, k.Frames, k.Night, k.At, k.Events, i < c.Clips.Count - 1 ? "," : ""));
            }
            sb.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(c.Tour.OutDir, "clips.json"), sb.ToString());
        }

        // ------------------------------------------------------------------ filming a night

        static IEnumerator Film(Ctx c, Shot s)
        {
            var g = Game.Instance;
            c.Cam.Stop();
            g.TourHideAll();
            if (s.Night == NightWatch.Number) g.TourWatch(); else g.TourBriefing(s.Night);
            g.TourBegin();
            var r = g.Runner;
            // Nothing has stepped yet: the twin starts from the same state.
            float at = s.Offset, found = 0f;
            var anchor = Vector2.zero;
            var marks = new List<(int id, Vector2 pos)>();
            if (s.Moment != null)
            {
                var twin = Twin(r.World, s, out found);
                if (found < 0f)
                {
                    c.Tour.Log($"shot {s.Name}: the moment never came (night {s.Night}, twin at t={twin.Time:0})");
                    yield break;
                }
                at = found + s.Offset;
                if (s.Anchor != null) anchor = s.Anchor(twin);
                foreach (var ship in twin.Ships) if (ship.Active) marks.Add((ship.Id, ship.Pos));
            }
            r.InputFilter = s.Stage?.Invoke(r.World, r.Bot);
            g.Rig.Snap(CameraRig.PlayPose);

            // Off camera: run the night quickly up to just before the moment.
            r.TimeScale = 6f;
            while (r.World.Time < at - 1.6f && r.World.Outcome == MissionOutcome.Running) yield return null;
            r.TimeScale = 1f;
            if (s.Cam != null) c.Cam.Begin(s.Cam, r.World, anchor, at);
            int settle = 0;
            while (r.World.Time < at || settle < 36)
            {
                if (s.Moment != null && marks.Count > 0 && r.World.Time >= found)
                {
                    // The twin should have predicted the night exactly.
                    float worst = 0f;
                    foreach (var (id, pos) in marks)
                    {
                        var live = r.World.Ships.Find(x => x.Id == id);
                        if (live != null) worst = Mathf.Max(worst, Vector2.Distance(live.Pos, pos));
                    }
                    c.Tour.Log($"shot {s.Name}: moment at t={found:0.00}, twin vs live drift {worst:0.00} u");
                    marks.Clear();
                }
                settle++;
                yield return null;
            }
            switch (s.Hud)
            {
                case HudMode.Full: g.Hud.Show(true, 0f); break;
                case HudMode.None: g.Hud.Show(false, 0f); break;
                case HudMode.Radio: g.Hud.ShowRadioOnly(g.Radio); break;
            }
            if (c.Stills && s.Length > 0f) yield return Hold(s.Length);
            yield return Roll(c, s.Name, s.Length, r, s.Night);
            c.Cam.Stop();
        }

        static SimWorld Twin(SimWorld live, Shot s, out float found)
        {
            var w = new SimWorld(live.Map, live.Mission, 7, live.Hard ? Difficulty.Hard : Difficulty.Standard);
            w.Beam.Bearing = live.Beam.Bearing;
            w.Beam.AngularVelocity = live.Beam.AngularVelocity;
            w.Beam.Focus = live.Beam.Focus;
            var bot = new AutoKeeper();
            var filter = s.Stage?.Invoke(w, bot);
            found = -1f;
            while (w.Time < s.Limit && w.Outcome == MissionOutcome.Running)
            {
                var input = bot.Decide(w, MissionRunner.StepTime);
                if (filter != null) input = filter(w, input);
                w.Step(MissionRunner.StepTime, input);
                if (s.Moment(w))
                {
                    found = w.Time;
                    break;
                }
            }
            return w;
        }

        // ------------------------------------------------------------------ the shot list

        static IEnumerable<Shot> Shots()
        {
            // The light and the ships: a trawler left in the dark loses its way, then the beam finds it.
            yield return new Shot
            {
                Name = "sweep", Night = 1,
                Stage = (w, bot) =>
                {
                    bool free = false;
                    bot.Ignore = s => !free && s.Name == "Little Auk";
                    return (ww, input) =>
                    {
                        var s = Ship(ww, "Little Auk");
                        if (s != null && s.State == ShipState.Lost && s.StateTime > 1.4f) free = true;
                        return input;
                    };
                },
                Moment = w => Has(w, SimEventType.ShipLost, "Little Auk"),
                Offset = -3.8f, Length = 10f,
                Cam = Between("Little Auk", 0.55f, 160f, 52f, 30f, new Vector2(18f, 0f)),
            };

            // Focus: the wide beam falls short of a far ship; the focused beam reaches it.
            yield return new Shot
            {
                Name = "focus", Night = 4,
                Stage = (w, bot) =>
                {
                    float t0 = -1f;
                    SimShip target = null;
                    return (ww, input) =>
                    {
                        if (t0 < 0f && FarShip(ww) is SimShip far) { t0 = ww.Time; target = far; }
                        if (t0 < 0f || ww.Time > t0 + 7.5f || !target.Active) return input;
                        return new KeeperInput { HasTarget = true, TargetBearing = Geo.Bearing(target.Pos - ww.Beam.Origin), Focus = ww.Time - t0 > 2.6f };
                    };
                },
                Moment = w => FarShip(w) != null,
                Anchor = w => FarShip(w).Pos,
                Offset = -0.8f, Length = 7.6f,
                Cam = (w, a, t) => Orbit(Vector2.Lerp(w.Beam.Origin, a, 0.5f), 0f, 235f, 56f, 0f, 30f),
            };

            // Charting: the beam sweeps ahead of a ship and a hidden reef breaks white in time.
            yield return new Shot
            {
                Name = "chart", Night = 2,
                Moment = w => w.Time > 20f && ChartedAhead(w, 50f).reef != null,
                Anchor = w => { var (reef, ship) = ChartedAhead(w, 50f); return Vector2.Lerp(reef.Pos, ship.Pos, 0.5f); },
                Offset = -2.6f, Length = 8.5f, Hud = HudMode.None,
                Cam = (w, a, t) => Orbit(a, 0f, Mathf.Lerp(108f, 94f, Ease(t / 8.5f)), 48f, 0f, 30f),
            };

            // Buoys: the sweep lights a buoy, and it burns over its channel.
            yield return new Shot
            {
                Name = "buoy", Night = 3,
                Moment = w => w.Time > 8f && Has(w, SimEventType.BuoyLit),
                Anchor = w => w.Buoys[w.Events.Find(e => e.Type == SimEventType.BuoyLit).Index].Pos,
                Offset = -1.6f, Length = 6.5f, Hud = HudMode.None,
                Cam = Side(44f, 22f, 70f, 34f, 1.5f),
            };

            // Three hulls, close up.
            yield return new Shot
            {
                Name = "hull_trawler", Night = 2,
                Moment = w => w.Time > 14f && Ship(w, "Little Auk") is SimShip s && s.Inside && !s.Lit && s.TimeSinceLit > 1.5f && s.TimeSinceLit < 2f,
                Offset = 0f, Length = 4.5f, Hud = HudMode.None,
                Cam = Abeam("Little Auk", 32f, 15f, 34f),
            };
            yield return new Shot
            {
                Name = "hull_steamer", Night = 4,
                Moment = w => w.Time > 16f && Ship(w, "SS Calloway") is SimShip s && s.Inside && !s.Lit && BeamNear(w, s, 35f),
                Offset = 0f, Length = 4.5f, Hud = HudMode.None,
                Cam = Abeam("SS Calloway", 46f, 16f, 34f),
            };
            // The ferry, with Dot on the radio: "Everyone wave at the lighthouse!"
            yield return new Shot
            {
                Name = "hull_ferry", Night = 6,
                Offset = 29.6f, Length = 5.5f, Hud = HudMode.Radio,
                Cam = Abeam("Evening Star", 38f, 15f, 34f),
            };

            // Sea fret: the foghorn's shockwave steadies every ship in earshot.
            yield return new Shot
            {
                Name = "fog", Night = 5,
                Stage = (w, bot) =>
                {
                    bool done = false;
                    return (ww, input) =>
                    {
                        if (!done && ww.Time > 40f && ww.HornCooldown <= 0f && HornWorthy(ww)) { done = true; input.Horn = true; }
                        return input;
                    };
                },
                Moment = w => w.Time > 40f && Has(w, SimEventType.Horn),
                Offset = -2.6f, Length = 7.5f,
            };
            // A ship coming out of the fog into the beam.
            yield return new Shot
            {
                Name = "fog_emerge", Night = 5,
                Moment = w => w.Time > 20f && w.Ships.Exists(s => s.Active && s.InFog && s.Lit && s.EverInside),
                Anchor = w => w.Ships.Find(s => s.Active && s.InFog && s.Lit && s.EverInside).Pos,
                Offset = -1f, Length = 4.5f, Hud = HudMode.None,
                Cam = (w, a, t) => Orbit(a, 0f, Mathf.Lerp(128f, 118f, Ease(t / 4.5f)), 40f, 0f, 30f),
            };

            // Mayday: a damaged collier runs dark; its flare gives it away.
            yield return new Shot
            {
                Name = "mayday", Night = 7,
                Stage = (w, bot) =>
                {
                    bool free = false;
                    bot.Ignore = s => !free && s.Name == "SS Calloway";
                    float flareAt = -1f;
                    return (ww, input) =>
                    {
                        if (flareAt < 0f && ww.Time > 8f && Has(ww, SimEventType.ShipFlare, "SS Calloway")) flareAt = ww.Time;
                        if (flareAt > 0f && ww.Time > flareAt + 1.2f) free = true;
                        return input;
                    };
                },
                Moment = w => w.Time > 8f && Has(w, SimEventType.ShipFlare, "SS Calloway"),
                Anchor = w => Ship(w, "SS Calloway").Pos,
                Offset = -2.6f, Length = 8.5f,
                Cam = (w, a, t) => Orbit(Vector2.Lerp(w.Beam.Origin, a, 0.78f), 0f, 145f, 52f, 0f, 30f),
            };

            // Squall: lightning shows every reef in the bay.
            yield return new Shot
            {
                Name = "storm", Night = 8,
                Moment = w => w.Time > 30f && Has(w, SimEventType.Lightning),
                Offset = -2.8f, Length = 6.8f,
            };

            // False light: the keeper looks away, a ship is lured, then the beam douses the lantern.
            yield return new Shot
            {
                Name = "false_light", Night = 9,
                Stage = LookAwayThenDouse,
                Moment = w => w.Time > 30f && Has(w, SimEventType.ShipLured),
                Anchor = w => Vector2.Lerp(w.Ships.Find(s => s.State == ShipState.Lured).Pos, w.Wreckers[0].Site.Pos, 0.45f),
                Offset = -2.6f, Length = 10.5f,
                Cam = (w, a, t) => Orbit(a, 0f, 150f, 52f, -12f, 30f),
            };

            // The Mimic: a false light that copies the keeper's sweep.
            yield return new Shot { Name = "mimic", Night = 11, Offset = 27f, Length = 6.5f };

            // Wrecks: a neglected trawler meets the uncharted Teeth.
            foreach (var (name, night) in new[] { ("wreck", 2), ("wreck_fog", 5) })
            {
                yield return new Shot
                {
                    Name = name, Night = night,
                    Stage = (w, bot) => { bot.Ignore = s => s.Name == "Little Auk"; return null; },
                    Moment = w => Has(w, SimEventType.ShipWrecked),
                    Anchor = w => w.Events.Find(e => e.Type == SimEventType.ShipWrecked).Ship.Pos,
                    Offset = -3.4f, Length = 8f, Hud = HudMode.None,
                    Cam = (w, a, t) => Orbit(a, 0f, 50f, 27f, Geo.Bearing(a - w.Beam.Origin) * Mathf.Rad2Deg + 35f, 34f),
                };
            }

            // The finale and the endless watch.
            yield return new Shot { Name = "finale", Night = 12, Offset = 60f, Length = 6.5f };
            yield return new Shot
            {
                Name = "finale_lightning", Night = 12,
                Moment = w => w.Time > 44f && Has(w, SimEventType.Lightning),
                Offset = -1.4f, Length = 4.5f, Hud = HudMode.None,
                Cam = (w, a, t) => Orbit(new Vector2(18f, 48f), 4f, 160f, 22f, 0f, 38f),
            };
            yield return new Shot
            {
                Name = "douse", Night = 10,
                Moment = w => Has(w, SimEventType.WreckerDoused),
                Anchor = w => w.Wreckers[w.Events.Find(e => e.Type == SimEventType.WreckerDoused).Index].Site.Pos,
                Offset = -2.4f, Length = 5f, Hud = HudMode.None,
                Cam = Side(42f, 18f, 55f, 34f, 5f),
            };
            yield return new Shot { Name = "watch", Night = NightWatch.Number, Offset = 165f, Length = 6.5f };
        }

        /// <summary>
        /// Night 9: from t = 30 the keeper looks away to the north-west, so the wreckers' lantern
        /// lures a ship; a moment after the lure the beam focuses on the lantern and douses it.
        /// </summary>
        static readonly Staging LookAwayThenDouse = (w, bot) =>
        {
            float luredAt = -1f;
            return (ww, input) =>
            {
                var wr = ww.Wreckers.Count > 0 ? ww.Wreckers[0] : null;
                if (wr == null || ww.Time < 30f) return input;
                if (luredAt < 0f && ww.Ships.Exists(s => s.State == ShipState.Lured)) luredAt = ww.Time;
                bool busyElsewhere = luredAt < 0f || ww.Time < luredAt + 2.4f;
                if (busyElsewhere) return new KeeperInput { HasTarget = true, TargetBearing = Geo.Bearing(new Vector2(-70f, 70f) - ww.Beam.Origin) };
                if (wr.Burning) return new KeeperInput { HasTarget = true, TargetBearing = Geo.Bearing(wr.Site.Pos - ww.Beam.Origin), Focus = true };
                return input;
            };
        };

        // ------------------------------------------------------------------ custom shots

        static CameraRig.Pose TitleFraming(float t)
        {
            float k = Ease(t / 10f);
            return new CameraRig.Pose(Vector3.Lerp(new Vector3(-52f, 9f, -20f), new Vector3(-45f, 8.4f, -13f), k), new Vector3(0f, 17f, 30f), Mathf.Lerp(40f, 37f, k));
        }

        static CameraRig.Pose EndcardFraming(float t)
        {
            float k = Ease(t / 10f);
            // Tilted up at the sky: the tower stands low in the frame and the end card's text sits above it.
            return new CameraRig.Pose(Vector3.Lerp(new Vector3(-36f, 12f, 64f), new Vector3(-32f, 11.5f, 57f), k), Vector3.Lerp(new Vector3(2f, 29f, 0f), new Vector3(2f, 28f, 0f), k), 40f);
        }

        /// <summary>The title scene with its menu hidden: the lens sweeping over the tower.</summary>
        static IEnumerator TitleShot(Ctx c, string name, Func<float, CameraRig.Pose> framing)
        {
            var g = Game.Instance;
            if (g.Current != Game.State.Title) { g.TourTitle(); yield return Hold(0.2f); }
            g.TourHideAll();
            var r = g.Runner;
            c.Cam.Begin((w, a, t) => framing(t), r.World, Vector2.zero, r.World.Time + 1f);
            yield return Hold(1f);
            yield return Roll(c, name, 10f);
            c.Cam.Stop();
        }

        /// <summary>The logbook opening on a season under way (eight nights kept).</summary>
        static IEnumerator LogbookShot(Ctx c)
        {
            var g = Game.Instance;
            var save = SaveData.Current;
            save.unlocked = 9;
            save.lamps = new[] { 3, 3, 2, 3, 3, 2, 3, 2, 0, 0, 0, 0 };
            save.best = new[] { 610, 790, 880, 1210, 1150, 1560, 1700, 1490, 0, 0, 0, 0 };
            save.shipsHome = 54;
            c.Cam.Stop();
            g.TourTitle();
            g.Rig.Snap(CameraRig.TitlePose);
            yield return Hold(1.5f);
            g.TourHideAll();
            yield return Hold(0.3f);
            if (c.Stills) { g.TourShowLogbook(); yield return Hold(2f); yield return c.Tour.Shot("logbook"); yield break; }
            var roll = Roll(c, "logbook", 6f);
            roll.MoveNext();            // start rolling this frame, then open the book
            g.TourShowLogbook();
            while (roll.MoveNext()) yield return roll.Current;
        }

        /// <summary>A night played to its end off camera, then the dawn results and the lamps.</summary>
        static IEnumerator ResultsShot(Ctx c, int night)
        {
            var g = Game.Instance;
            c.Cam.Stop();
            g.TourHideAll();
            g.TourBriefing(night);
            g.TourBegin();
            g.Rig.Snap(CameraRig.PlayPose);
            g.Runner.TimeScale = 6f;
            while (!g.ShowingResults) yield return null;
            if (c.Stills) { yield return Hold(4.5f); yield return c.Tour.Shot("results"); yield break; }
            yield return Roll(c, "results", 7.5f);
        }

        /// <summary>The ending: dawn, the Calloway's thanks, and the light put out.</summary>
        static IEnumerator EndingShot(Ctx c)
        {
            var g = Game.Instance;
            c.Cam.Stop();
            g.TourEnding();
            yield return Hold(1.3f);
            yield return Roll(c, "ending", 52f);
        }

        // ------------------------------------------------------------------ the README stills

        /// <summary>Full-frame PNG screenshots of the game as a player sees it.</summary>
        static IEnumerator Stills(Tour t)
        {
            var g = Game.Instance;
            var c = Prepare(t, true);
            Time.captureFramerate = Fps;
            yield return Hold(3.5f);
            yield return t.Shot("title");
            var only = new HashSet<string>(Game.ArgString("-llShots", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            foreach (var s in StillList())
                if (only.Count == 0 || only.Contains(s.Name)) yield return Film(c, s);
            if (only.Count == 0 || only.Contains("results")) yield return ResultsShot(c, 2);
            if (only.Count == 0 || only.Contains("logbook")) yield return LogbookShot(c);
            if (only.Count == 0 || only.Contains("ending"))
            {
                g.TourEnding();
                yield return Hold(1.3f + 14.4f);  // the Calloway's thanks on the radio, fully typed
                yield return t.Shot("ending");
            }
            Time.captureFramerate = 0;
        }

        static IEnumerable<Shot> StillList()
        {
            // For stills the shot's Length is how long to wait after the moment before the capture.
            yield return new Shot { Name = "play", Night = 3, Offset = 52f, Length = 0f };
            yield return new Shot
            {
                Name = "chart", Night = 2,
                Moment = w => w.Time > 20f && ChartedAhead(w, 50f).reef != null,
                Offset = 0.8f, Length = 0f,
            };
            yield return new Shot
            {
                Name = "foghorn", Night = 5,
                Stage = (w, bot) =>
                {
                    bool done = false;
                    return (ww, input) =>
                    {
                        if (!done && ww.Time > 40f && ww.HornCooldown <= 0f && HornWorthy(ww)) { done = true; input.Horn = true; }
                        return input;
                    };
                },
                Moment = w => w.Time > 40f && Has(w, SimEventType.Horn),
                Offset = 0.9f, Length = 0f,
            };
            yield return new Shot
            {
                Name = "wreck", Night = 2,
                Stage = (w, bot) => { bot.Ignore = s => s.Name == "Little Auk"; return null; },
                Moment = w => Has(w, SimEventType.ShipWrecked),
                Offset = 1.3f, Length = 0f,
            };
            yield return new Shot
            {
                Name = "false_light", Night = 9, Stage = LookAwayThenDouse,
                Moment = w => w.Time > 30f && Has(w, SimEventType.ShipLured),
                Offset = 1.4f, Length = 0f,
            };
            yield return new Shot { Name = "storm", Night = 12, Moment = w => w.Time > 50f && Has(w, SimEventType.Lightning), Offset = 0.15f, Length = 0f };
            yield return new Shot { Name = "mimic", Night = 11, Offset = 96f, Length = 0f };
            yield return new Shot { Name = "night_watch", Night = NightWatch.Number, Offset = 170f, Length = 0f };
        }

        // ------------------------------------------------------------------ helpers

        static SimShip Ship(SimWorld w, string name) => w.Ships.Find(s => s.Name == name);

        static bool Has(SimWorld w, SimEventType type) => w.Events.Exists(e => e.Type == type);

        static bool Has(SimWorld w, SimEventType type, string ship) =>
            w.Events.Exists(e => e.Type == type && e.Ship != null && e.Ship.Name == ship);

        /// <summary>A ship out beyond the wide beam's reach but within the focused beam's.</summary>
        static SimShip FarShip(SimWorld w)
        {
            if (w.Time < 15f) return null;
            foreach (var s in w.Ships)
            {
                if (!s.Active || !s.EverInside) continue;
                float d = Vector2.Distance(s.Pos, w.Beam.Origin);
                if (d > 128f && d < 168f && w.Occlusion(w.Beam.Origin, s.Pos) > 0.9f) return s;
            }
            return null;
        }

        /// <summary>A reef charted this step with a ship close by and heading for it.</summary>
        static (SimReef reef, SimShip ship) ChartedAhead(SimWorld w, float within)
        {
            foreach (var e in w.Events)
            {
                if (e.Type != SimEventType.ReefCharted || e.Index < 0 || e.Index >= w.Reefs.Count) continue;
                var reef = w.Reefs[e.Index];
                foreach (var s in w.Ships)
                {
                    if (!s.Active) continue;
                    var to = reef.Pos - s.Pos;
                    if (to.magnitude < within && Vector2.Dot(to.normalized, s.Forward) > 0.5f) return (reef, s);
                }
            }
            return (null, null);
        }

        /// <summary>The beam is sweeping close by (within some degrees of the ship's bearing).</summary>
        static bool BeamNear(SimWorld w, SimShip s, float degrees) =>
            Mathf.Abs(Geo.DeltaAngle(w.Beam.Bearing, Geo.Bearing(s.Pos - w.Beam.Origin))) * Mathf.Rad2Deg < degrees;

        static bool HornWorthy(SimWorld w)
        {
            int near = 0;
            bool fog = false;
            foreach (var s in w.Ships)
            {
                if (!s.Active || !s.EverInside) continue;
                if (Vector2.Distance(s.Pos, w.Beam.Origin) < SimWorld.HornRange * 0.8f) near++;
                fog |= s.InFog;
            }
            return near >= 3 && fog;
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>A camera looking at a point on the sea from a bearing (yaw) and elevation (pitch).</summary>
        static CameraRig.Pose Orbit(Vector2 at, float height, float dist, float pitchDeg, float yawDeg, float fov)
        {
            float p = pitchDeg * Mathf.Deg2Rad, y = yawDeg * Mathf.Deg2Rad;
            var focus = new Vector3(at.x, height, at.y);
            var back = new Vector3(-Mathf.Sin(y) * Mathf.Cos(p), Mathf.Sin(p), -Mathf.Cos(y) * Mathf.Cos(p));
            return new CameraRig.Pose(focus + back * dist, focus, fov);
        }

        /// <summary>The anchor seen side-on to the beam, so the shaft crosses the frame.</summary>
        static Framing Side(float dist, float pitch, float side, float fov, float height) => (w, a, t) =>
            Orbit(a, height, dist, pitch, Geo.Bearing(a - w.Beam.Origin) * Mathf.Rad2Deg + side, fov);

        /// <summary>Between the lighthouse and a ship, looking north like the play view.</summary>
        static Framing Between(string ship, float k, float dist, float pitch, float fov, Vector2 shift = default) => (w, a, t) =>
        {
            var s = Ship(w, ship);
            return Orbit((s != null ? Vector2.Lerp(w.Beam.Origin, s.Pos, k) : a) + shift, 0f, dist, pitch, 0f, fov);
        };

        /// <summary>
        /// A ship seen side-on to the beam that lights it, so the camera stays out of the shaft's
        /// glare and the light crosses the frame. The side is the one that looks north, out to sea.
        /// </summary>
        static Framing Abeam(string ship, float dist, float pitch, float fov)
        {
            float side = float.NaN;
            return (w, a, t) =>
            {
                var s = Ship(w, ship);
                if (s == null) return Orbit(a, 1f, dist, pitch, 0f, fov);
                float beam = Geo.Bearing(s.Pos - w.Beam.Origin) * Mathf.Rad2Deg;
                if (float.IsNaN(side))
                {
                    // Prefer looking out to sea; avoid a sea stack between the camera and the ship.
                    side = Mathf.Abs(Mathf.DeltaAngle(beam + 90f, 0f)) < 90f ? 90f : -90f;
                    foreach (float c in new[] { side, -side, side * 0.6f, -side * 0.6f, side * 1.4f, -side * 1.4f })
                    {
                        var look = Geo.Dir((beam + c) * Mathf.Deg2Rad);
                        var cam = s.Pos - look * dist * Mathf.Cos(pitch * Mathf.Deg2Rad);
                        bool clear = true;
                        foreach (var st in w.Map.Stacks)
                            if (Geo.SegmentDistance(cam, s.Pos + look * 12f, st.Pos, out _) < st.Radius + 4f) clear = false;
                        if (clear) { side = c; break; }
                    }
                }
                return Orbit(s.Pos, 1f, dist, pitch, beam + side, fov);
            };
        }
    }
}
