using System.Collections.Generic;
using LastLight.Sim;
using NUnit.Framework;
using UnityEngine;

namespace LastLight.Tests
{
    /// <summary>The night's log behind the dawn chart: it follows every ship faithfully, never
    /// changes the night, and stays small on a long watch.</summary>
    public class NightLogTests
    {
        static (SimWorld w, NightLog log) Play(MissionDef def, AutoKeeper bot, int seconds = 900, int maxPoints = NightLog.DefaultMaxPoints)
        {
            var map = Validation.FreshMap();
            var w = new SimWorld(map, def, 7);
            var log = new NightLog(maxPoints);
            for (int i = 0; i < 60 * seconds && w.Outcome == MissionOutcome.Running; i++)
            {
                w.Step(1f / 60f, bot.Decide(w, 1f / 60f));
                log.Record(w);
            }
            return (w, log);
        }

        [Test]
        public void TracksEndWhereShipsEnded([Values(2, 4, 9)] int night)
        {
            var missions = Validation.FreshMissions();
            var def = missions[night - 1];
            int wrecks = 0;
            foreach (var spawn in def.ships)
            {
                var (w, log) = Play(def, new AutoKeeper { Ignore = s => s.Name == spawn.name });
                Assert.AreEqual(w.Ships.Count, log.Tracks.Count, "one track per ship that sailed");
                foreach (var t in log.Tracks)
                {
                    var s = t.Ship;
                    var end = t.Points[t.Points.Count - 1];
                    if (s.Resolved)
                    {
                        Assert.AreEqual(s.Pos, end.Pos, $"night {night}: the {s.Name}'s track ends where it ended");
                        Assert.AreEqual(s.State, end.State, $"night {night}: the {s.Name}'s track ends {s.State}");
                    }
                    int lost = 0, lured = 0;
                    for (int i = 1; i < t.Points.Count; i++)
                    {
                        if (t.Points[i].State == t.Points[i - 1].State) continue;
                        // A doused lure can leave its ship lost; the debrief counts only losing the light.
                        if (t.Points[i].State == ShipState.Lost && t.Points[i - 1].State != ShipState.Lured) lost++;
                        if (t.Points[i].State == ShipState.Lured) lured++;
                    }
                    Assert.AreEqual(s.LostCount, lost, $"night {night}: every time the {s.Name} lost its way is on its track");
                    Assert.AreEqual(s.LuredCount, lured, $"night {night}: every time the {s.Name} was lured is on its track");
                    // No stretch is longer than the spacing allows plus a step's travel, so the chart
                    // never cuts a corner across a reef.
                    for (int i = 1; i < t.Points.Count; i++)
                        Assert.LessOrEqual(Vector2.Distance(t.Points[i - 1].Pos, t.Points[i].Pos), log.Spacing + 1f, $"night {night}: the {s.Name}'s track has no long jumps");
                }
                if (w.Wrecks > 0) wrecks++;
            }
            Assert.Greater(wrecks, 0, $"neglecting a ship on night {night} wrecks someone, so a wreck was logged");
        }

        [Test]
        public void TheLogDoesNotChangeTheNight()
        {
            var def = Validation.FreshMissions()[3];
            var map = Validation.FreshMap();
            var plain = new SimWorld(map, def, 7);
            var bot = new AutoKeeper();
            for (int i = 0; i < 60 * 900 && plain.Outcome == MissionOutcome.Running; i++) plain.Step(1f / 60f, bot.Decide(plain, 1f / 60f));
            var (logged, _) = Play(def, new AutoKeeper());
            Assert.AreEqual(plain.Score, logged.Score);
            Assert.AreEqual(plain.Time, logged.Time);
            Assert.AreEqual(plain.Arrivals, logged.Arrivals);
        }

        [Test]
        public void AWatchLogStaysSmall()
        {
            // A 30-minute watch fits at the starting spacing; a cap a quarter that size makes the
            // log thin itself twice or more on the way.
            var map = Validation.FreshMap();
            var (w, full) = Play(NightWatch.Generate(map, 3), new AutoKeeper(), 1800);
            Assert.LessOrEqual(full.PointCount, NightLog.DefaultMaxPoints);
            Assert.LessOrEqual(full.Beam.Count, NightLog.MaxBeamSamples, "the light's log stays capped on a long watch");
            Assert.AreEqual(w.Time, full.Duration, 1e-3f, "the log knows how long the watch ran");
            var (_, log) = Play(NightWatch.Generate(map, 3), new AutoKeeper(), 1800, 4000);
            Debug.Log($"[NightLog] a {w.Time:0} s watch: {full.Tracks.Count} tracks, {full.PointCount} points at spacing {full.Spacing}; capped at 4000: {log.PointCount} points at spacing {log.Spacing}");
            Assert.LessOrEqual(log.PointCount, 4000, "the log thins itself on a long watch");
            Assert.GreaterOrEqual(log.Spacing, 10f, "and keeps points further apart afterwards");
            Assert.AreEqual(full.Tracks.Count, log.Tracks.Count);
            for (int k = 0; k < log.Tracks.Count; k++)
            {
                var t = log.Tracks[k];
                if (t.Ship.Resolved) Assert.AreEqual(t.Ship.Pos, t.Points[t.Points.Count - 1].Pos, "thinning keeps where each ship ended");
                Assert.AreEqual(Changes(full.Tracks[k]), Changes(t), $"thinning keeps every change of state for the {t.Ship.Name}");
            }
        }

        [Test]
        public void TheReplayFollowsTheNight([Values(2, 9)] int night)
        {
            // Play a night with a neglected ship, noting where every ship and the light really were
            // at each step, then ask the log for the same moments.
            var def = Validation.FreshMissions()[night - 1];
            var map = Validation.FreshMap();
            var w = new SimWorld(map, def, 7);
            var log = new NightLog();
            var bot = new AutoKeeper { Ignore = s => s.Name == def.ships[0].name };
            var truth = new List<(float time, float bearing, float focus, List<(SimShip ship, Vector2 pos, ShipState state)> ships)>();
            for (int i = 0; i < 60 * 900 && w.Outcome == MissionOutcome.Running; i++)
            {
                w.Step(1f / 60f, bot.Decide(w, 1f / 60f));
                log.Record(w);
                if (i % 7 != 0) continue;
                var ships = new List<(SimShip, Vector2, ShipState)>();
                foreach (var s in w.Ships) ships.Add((s, s.Pos, s.State));
                truth.Add((w.Time, w.Beam.Bearing, w.Beam.Focus, ships));
            }
            Assert.AreEqual(w.Time, log.Duration, 1e-3f);
            float worstBeam = 0f, worstShip = 0f;
            int stateMisses = 0, checkedShips = 0;
            foreach (var (time, bearing, focus, ships) in truth)
            {
                var b = log.BeamAt(time);
                worstBeam = Mathf.Max(worstBeam, Mathf.Abs(Geo.DeltaAngle(bearing, b.Bearing)) * Mathf.Rad2Deg);
                Assert.AreEqual(focus, b.Focus, 0.6f, "focus follows the light");
                foreach (var (ship, pos, state) in ships)
                {
                    var track = log.Tracks.Find(t => t.Ship == ship);
                    bool shown = NightLog.ShipAt(track, time, out var at, out var st);
                    if (state == ShipState.Arrived && ship.Resolved && time >= track.Points[track.Points.Count - 1].Time)
                    {
                        Assert.IsFalse(shown, $"the {ship.Name} is off the chart once home");
                        continue;
                    }
                    Assert.IsTrue(shown, $"night {night}: the {ship.Name} is on the chart at {time:0.0} s");
                    worstShip = Mathf.Max(worstShip, Vector2.Distance(pos, at));
                    if (st != state) stateMisses++;
                    checkedShips++;
                }
            }
            Debug.Log($"[NightLog] night {night} replay: beam within {worstBeam:0.00}°, ships within {worstShip:0.00} units, {stateMisses} of {checkedShips} states a sample late");
            Assert.LessOrEqual(worstBeam, 6f, "the replay's light is where the light was");
            Assert.LessOrEqual(worstShip, log.Spacing, "the replay's ships are where the ships were");
            Assert.LessOrEqual(stateMisses, checkedShips / 200 + 2, "a ship's state on the replay is its state then (changes land on the next point)");

            // Before a ship sailed it isn't drawn; a wreck stays on its rock from the moment it struck.
            int wrecked = 0;
            foreach (var t in log.Tracks)
            {
                Assert.IsFalse(NightLog.ShipAt(t, t.Points[0].Time - 0.5f, out _, out _), $"the {t.Ship.Name} isn't on the chart before it sailed");
                if (t.Ship.State != ShipState.Wrecked) continue;
                wrecked++;
                var end = t.Points[t.Points.Count - 1];
                Assert.AreEqual(t.Ship.WreckTime, end.Time, 1e-3f, $"the {t.Ship.Name}'s wreck is logged when it happened");
                Assert.IsTrue(NightLog.ShipAt(t, log.Duration, out var p, out var st));
                Assert.AreEqual(ShipState.Wrecked, st);
                Assert.AreEqual(t.Ship.Pos, p);
                NightLog.ShipAt(t, end.Time - 0.2f, out _, out var before);
                Assert.AreNotEqual(ShipState.Wrecked, before, $"the {t.Ship.Name} is afloat just before it struck");
            }
            Assert.Greater(wrecked, 0, "the neglected ship wrecked someone, so a wreck was replayed");
            if (night == 9)
            {
                Assert.Greater(log.Burns.Count, 0, "the wreckers' lanterns burned on night IX");
                foreach (var burn in log.Burns)
                {
                    Assert.IsTrue(log.BurningAt(burn.Site, burn.Start + 0.01f));
                    Assert.IsFalse(log.BurningAt(burn.Site, burn.Start - 0.5f) && !log.Burns.Exists(o => o != burn && o.Site == burn.Site && o.End >= burn.Start - 0.5f));
                }
            }
        }

        [Test]
        public void TheNightsMomentsAreEachTurnOnce([Values(2, 9)] int night)
        {
            // The replay jumps between these: every wreck, every time a ship was lured and every
            // time it lost its way, once each and in order.
            // Nobody keeps the light for the first stretch, so ships are lost (and on night IX lured);
            // then the bot keeps it, but leaves the first ship to its fate.
            var def = Validation.FreshMissions()[night - 1];
            def.allowedWrecks = 99;
            var w = new SimWorld(Validation.FreshMap(), def, 7);
            var log = new NightLog();
            var bot = new AutoKeeper { Ignore = s => s.Name == def.ships[0].name };
            for (int i = 0; i < 60 * 900 && w.Outcome == MissionOutcome.Running; i++)
            {
                var input = bot.Decide(w, 1f / 60f);
                w.Step(1f / 60f, w.Time < 100f ? new KeeperInput { TargetBearing = input.TargetBearing } : input);
                log.Record(w);
            }
            var moments = log.Moments();
            for (int i = 1; i < moments.Count; i++)
                Assert.LessOrEqual(moments[i - 1].Time, moments[i].Time, "in the order they happened");
            int lostSeen = 0, luredSeen = 0;
            foreach (var s in w.Ships)
            {
                int wrecked = 0, lured = 0, lost = 0;
                foreach (var m in moments)
                {
                    if (m.Ship != s) continue;
                    if (m.Kind == NightLog.MomentKind.Wrecked) { wrecked++; Assert.AreEqual(s.WreckTime, m.Time, 1e-3f, $"the {s.Name}'s wreck is when it struck"); }
                    if (m.Kind == NightLog.MomentKind.Lured) lured++;
                    if (m.Kind == NightLog.MomentKind.Lost && !m.AfterLure) lost++;
                }
                Assert.AreEqual(s.State == ShipState.Wrecked ? 1 : 0, wrecked, $"night {night}: the {s.Name} is wrecked once, if at all");
                Assert.AreEqual(s.LuredCount, lured, $"night {night}: each time the {s.Name} was lured");
                Assert.AreEqual(s.LostCount, lost, $"night {night}: each time the {s.Name} lost its way");
                lostSeen += lost;
                luredSeen += lured;
            }
            Debug.Log($"[NightLog] night {night}: {moments.Count} moments ({w.Wrecks} wrecks, {lostSeen} lost, {luredSeen} lured)");
            Assert.Greater(w.Wrecks, 0, "a neglected ship wrecks someone");
            Assert.Greater(lostSeen, 0, "and some ship lost its way");
            if (night == 9) Assert.Greater(luredSeen, 0, "and on night IX some ship was lured");
        }

        static string Changes(NightLog.Track t)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < t.Points.Count; i++)
                if (i == 0 || t.Points[i].State != t.Points[i - 1].State) sb.Append(t.Points[i].State).Append('@').Append(t.Points[i].Pos).Append(' ');
            return sb.ToString();
        }
    }
}
