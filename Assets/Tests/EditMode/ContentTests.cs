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
