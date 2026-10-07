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

        static string Changes(NightLog.Track t)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < t.Points.Count; i++)
                if (i == 0 || t.Points[i].State != t.Points[i - 1].State) sb.Append(t.Points[i].State).Append('@').Append(t.Points[i].Pos).Append(' ');
            return sb.ToString();
        }
    }
}
