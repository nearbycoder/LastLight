using System.Collections.Generic;
using LastLight.Sim;
using NUnit.Framework;

namespace LastLight.Tests
{
    /// <summary>
    /// Proofs about the game's content. Run with Tools/unity.sh test (batch) or Tools/validate.sh
    /// (in a running editor).
    /// </summary>
    public class ContentTests
    {
        static MapData map;
        static List<MissionDef> missions;

        [OneTimeSetUp]
        public void Load()
        {
            map = Validation.FreshMap();
            missions = Validation.FreshMissions();
        }

        [Test]
        public void MissionsReferenceValidMapData()
        {
            var errors = Validation.DataIntegrity(map, missions);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void EveryHazardAndBuoyIsReachableByTheBeam()
        {
            var errors = Validation.Reachability(map);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void EveryRouteIsSafeOnceItsHazardsAreCharted()
        {
            var errors = Validation.RouteSafety(map);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void EveryNightCanBeWon([Range(1, 12)] int night)
        {
            var r = Validation.PlayNight(map, missions[night - 1]);
            Assert.AreEqual(MissionOutcome.Won, r.Outcome, $"night {night} {r.Def.title}: {r.Arrivals}/{r.Total} home, {r.Wrecks} wrecked");
            Assert.GreaterOrEqual(r.Lamps, 1);
        }

        [Test]
        public void TheNightWatchIsWellFormed([Values(1, 2, 3)] int seed)
        {
            var def = NightWatch.Generate(map, seed);
            var errors = Validation.MissionErrors(map, def);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.IsTrue(def.endless);
            Assert.Greater(def.ships.Length, 200, "an hour of ships");
            for (int i = 1; i < def.ships.Length; i++)
                Assert.GreaterOrEqual(def.ships[i].t, def.ships[i - 1].t, "ships are scheduled in order");
        }

        [Test]
        public void TheBotKeepsTheNightWatchForTenMinutes([Values(1, 2, 3)] int seed)
        {
            var r = Validation.PlayWatch(map, seed);
            Assert.GreaterOrEqual(r.Time, 600f, $"watch {seed} ended at {r.Time:0}s with {r.Arrivals} home");
            Assert.GreaterOrEqual(r.Lamps, 1);
        }

        static SimWorld Play(MissionDef def, AutoKeeper bot, int seed = 7)
        {
            var w = new SimWorld(map, def, seed);
            for (int i = 0; i < 60 * 900 && w.Outcome == MissionOutcome.Running; i++)
                w.Step(1f / 60f, bot.Decide(w, 1f / 60f));
            return w;
        }

        [Test]
        public void TheDawnDebriefExplainsEveryWreck([Values(2, 4, 9)] int night)
        {
            // Neglect each ship in turn, as the trailer does to stage a wreck, and read the debrief.
            var def = missions[night - 1];
            int staged = 0;
            foreach (var spawn in def.ships)
            {
                var w = Play(def, new AutoKeeper { Ignore = s => s.Name == spawn.name });
                var lines = Debrief.Lines(w);
                int expected = 0;
                foreach (var s in w.Ships) if (s.State == ShipState.Wrecked || !s.SteadyHand) expected++;
                Assert.AreEqual(expected, lines.Count, $"night {night}, neglecting {spawn.name}: one line per ship that had a bad night");
                for (int i = 0; i < w.Wrecks; i++)
                {
                    Assert.AreEqual(Debrief.Kind.Wreck, lines[i].Kind, "wrecks come first");
                    StringAssert.IsMatch("struck|aground|ashore", lines[i].Text);
                }
                foreach (var l in lines) Assert.IsNotEmpty(l.Ship);
                if (w.Wrecks > 0)
                {
                    staged++;
                    UnityEngine.Debug.Log($"[Debrief] night {night}, neglecting {spawn.name}: {w.Outcome}, {w.Lamps} lamps\n  " + string.Join("\n  ", lines));
                }
            }
            Assert.Greater(staged, 0, $"neglecting a ship on night {night} wrecks someone");
        }

        [Test]
        public void TheDebriefSaysHowAShipWasWrecked()
        {
            var s = new SimShip { Name = "Little Auk", WreckCause = "the Merrow Teeth", WreckedWhile = ShipState.Sailing };
            Assert.AreEqual("struck the Merrow Teeth, uncharted", Debrief.WreckText(s));
            s.WreckCharted = true;
            Assert.AreEqual("struck the Merrow Teeth, charted too late to turn", Debrief.WreckText(s));
            s.WreckedWhile = ShipState.Lost;
            Assert.AreEqual("lost in the dark, struck the Merrow Teeth", Debrief.WreckText(s));
            var c = new SimShip { Name = "SS Calloway", WreckCause = "Long Sands", WreckShoal = true, WreckedWhile = ShipState.Lured, LuredAt = "Corley Cove" };
            Assert.AreEqual("lured by the false light at Corley Cove, ran aground on the Long Sands", Debrief.WreckText(c));
        }

        [Test]
        public void TheBeamFormulaMatchesTheDocumentedShape()
        {
            var beam = new SimBeam { Origin = UnityEngine.Vector2.zero, Height = 19f, Bearing = 0f };
            // Straight ahead, mid-range: lit. Behind: dark. Far beyond range: dark.
            Assert.Greater(SimBeam.Wedge(beam.Origin, beam.Direction, beam.HalfAngle, beam.Range, new UnityEngine.Vector2(0, 50)), SimBeam.LitThreshold);
            Assert.AreEqual(0f, SimBeam.Wedge(beam.Origin, beam.Direction, beam.HalfAngle, beam.Range, new UnityEngine.Vector2(0, -50)));
            Assert.AreEqual(0f, SimBeam.Wedge(beam.Origin, beam.Direction, beam.HalfAngle, beam.Range, new UnityEngine.Vector2(0, 300)));
        }
    }
}
