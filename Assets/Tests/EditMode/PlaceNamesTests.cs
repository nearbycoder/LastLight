using System.Collections.Generic;
using System.Linq;
using LastLight.Sim;
using NUnit.Framework;

namespace LastLight.Tests
{
    /// <summary>Names on the water: which places and ships a radio call points at, and that a
    /// hidden reef or sandbank is never pointed at before it's charted.</summary>
    public class PlaceNamesTests
    {
        static MapData map;

        [OneTimeSetUp]
        public void Load() => map = Validation.FreshMap();

        static SimWorld World(string[] reefs = null, string[] shoals = null, string[] buoys = null)
        {
            var def = new MissionDef { id = "names", night = 1, title = "", reefGroups = reefs ?? new string[0], shoals = shoals ?? new string[0], buoys = buoys ?? new string[0] };
            return new SimWorld(map, def, 7);
        }

        static string[] Places(SimWorld w, string text)
        {
            var list = new List<PlaceNames.Place>();
            PlaceNames.FindPlaces(w, text, list);
            return list.Select(p => p.Label).ToArray();
        }

        static string[] Ships(SimWorld w, string text)
        {
            var list = new List<SimShip>();
            PlaceNames.FindShips(w, text, list);
            return list.Select(s => s.Name).ToArray();
        }

        [Test]
        public void ACallNamesTheBuoyAndTheShip()
        {
            var w = World(buoys: new[] { "hen_bell" });
            w.Ships.Add(new SimShip { Id = 1, Name = "Little Auk", Captain = "maren" });
            w.Ships.Add(new SimShip { Id = 2, Name = "SS Calloway", Captain = "pryce" });
            CollectionAssert.AreEqual(new[] { "Hen Bell" }, Places(w, "Light the Hen Bell before the Auk gets there."));
            CollectionAssert.AreEqual(new[] { "Little Auk" }, Ships(w, "Light the Hen Bell before the Auk gets there."));
            CollectionAssert.AreEqual(new[] { "SS Calloway" }, Ships(w, "Gannet Head, Calloway. We've lost the marks."));
            CollectionAssert.AreEqual(new[] { "Little Auk" }, Ships(w, "Trawler Little Auk"), "a captain's role names their ship");
            // A ship that's home or wrecked isn't tagged.
            w.Ships[0].State = ShipState.Arrived;
            CollectionAssert.IsEmpty(Ships(w, "Tell that to the Auk."));
        }

        [Test]
        public void WholeWordsOnly()
        {
            var w = World(buoys: new[] { "fairway" });
            CollectionAssert.AreEqual(new[] { "Corley Cove" }, Places(w, "There! A false light at Corley. Put your beam on it!"));
            CollectionAssert.IsEmpty(Places(w, "The Corleys went home empty-handed."), "the Corleys are people");
            CollectionAssert.IsEmpty(Places(w, "Harbourmaster here."), "harbourmaster isn't the harbour");
            CollectionAssert.AreEqual(new[] { "Porthkell" }, Places(w, "Evening, keeper. Ianto at the harbour."));
            CollectionAssert.AreEqual(new[] { "Porthkell Fairway" }, Places(w, "Mind the Porthkell Fairway."), "the longer name wins");
            CollectionAssert.AreEqual(new[] { "Corley Needle" }, Places(w, "Off the Corley Needle."));
            CollectionAssert.AreEqual(new[] { "West Point" }, Places(w, "Now West Point. They've a boat to row between them."));
        }

        [Test]
        public void HiddenHazardsAreNamedOnlyOnceCharted()
        {
            var w = World(reefs: new[] { "teeth" }, shoals: new[] { "longsands" }, buoys: new[] { "teeth_bell", "sands_n" });
            CollectionAssert.IsEmpty(Places(w, "The Teeth are out there, keeper."));
            CollectionAssert.IsEmpty(Places(w, "Long Sands are shifting again."));
            CollectionAssert.AreEqual(new[] { "Teeth Bell" }, Places(w, "Light the Teeth Bell."), "the buoy, not the reef");
            CollectionAssert.AreEqual(new[] { "Long Sands North" }, Places(w, "Long Sands North is lit."), "the buoy, not the bank");

            w.Reefs.First(r => r.Group == "teeth").ChartTimer = 5f;
            w.Shoals[0].ChartTimer = 5f;
            CollectionAssert.AreEqual(new[] { "The Merrow Teeth" }, Places(w, "The Teeth are out there, keeper."));
            CollectionAssert.AreEqual(new[] { "Long Sands" }, Places(w, "Long Sands lit. Good."));
            var centre = PlaceNames.GroupCentre(w, "teeth");
            Assert.Greater(centre.sqrMagnitude, 1f, "the label sits on the Teeth, not at the origin");
        }

        [Test]
        public void EveryNamedPlaceIsOnTheMap()
        {
            // Every buoy, cliff and stack name resolves to a place with a position.
            var w = World(buoys: map.Buoys.Select(b => b.id).ToArray());
            foreach (var name in map.Buoys.Select(b => b.name).Concat(map.WreckerSites.Values.Select(s => s.Name)).Concat(map.Stacks.Select(s => s.Name)))
            {
                var list = new List<PlaceNames.Place>();
                PlaceNames.FindPlaces(w, $"Watch {name}, keeper.", list);
                Assert.AreEqual(1, list.Count, name);
                Assert.AreEqual(name, list[0].Label);
            }
        }
    }
}
