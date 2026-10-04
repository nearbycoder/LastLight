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
