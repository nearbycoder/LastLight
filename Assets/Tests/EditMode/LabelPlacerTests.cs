using System.Collections.Generic;
using LastLight.UI;
using NUnit.Framework;
using UnityEngine;

namespace LastLight.Tests
{
    /// <summary>Names that don't sit on each other: the placer behind the names on the water and
    /// the dawn chart's names.</summary>
    public class LabelPlacerTests
    {
        static readonly Rect Screen = new Rect(0, 0, 1920, 1080);

        [Test]
        public void ANameWithRoomStaysPut()
        {
            var placed = new List<Rect> { new Rect(100, 100, 120, 34) };
            var at = LabelPlacer.Place(new Rect(400, 100, 120, 34), LabelPlacer.Vertical(38, 2), placed, Screen);
            Assert.AreEqual(Vector2.zero, at);
            Assert.AreEqual(2, placed.Count);
        }

        [Test]
        public void TwoNamesOnOneSpotEndUpApart()
        {
            // The Hen Bell and the Hen's Chicks, as round 7's capture had them.
            var placed = new List<Rect>();
            var steps = LabelPlacer.Vertical(38, 2);
            var a = new Rect(310, 280, 90, 34);
            var b = new Rect(345, 285, 130, 34);
            LabelPlacer.Place(a, steps, placed, Screen);
            var moved = LabelPlacer.Place(b, steps, placed, Screen);
            Assert.AreEqual(new Vector2(0, 38), moved, "the second name steps up a line");
            Assert.IsFalse(LabelPlacer.Overlaps(placed[0], placed[1]));
        }

        [Test]
        public void ANameStaysOnScreen()
        {
            // At the top edge, up isn't allowed, so it steps down.
            var placed = new List<Rect> { new Rect(500, 1040, 200, 34) };
            var at = LabelPlacer.Place(new Rect(520, 1040, 120, 34), LabelPlacer.Vertical(38, 2), placed, Screen);
            Assert.AreEqual(new Vector2(0, -38), at);
        }

        [Test]
        public void WithNoClearPlaceTheLeastCoveredWins()
        {
            // Hemmed in above and below, with a sliver free two lines down.
            var placed = new List<Rect> { new Rect(0, 0, 1920, 1080 - 400) };
            placed.Add(new Rect(0, 700, 1920, 380));
            var at = LabelPlacer.Place(new Rect(800, 640, 100, 34), LabelPlacer.Vertical(38, 2), placed, Screen);
            // 640..674 covers 34 rows of the lower block's top (0..680); +38 -> 678..712 covers 2 + 12;
            // the least covered is +38.
            Assert.AreEqual(new Vector2(0, 38), at);
        }

        [Test]
        public void StepsGoNearestFirst()
        {
            var up = LabelPlacer.Vertical(10, 2);
            CollectionAssert.AreEqual(new[] { Vector2.zero, new Vector2(0, 10), new Vector2(0, -10), new Vector2(0, 20), new Vector2(0, -20) }, up);
            var down = LabelPlacer.Vertical(10, 1, upFirst: false);
            CollectionAssert.AreEqual(new[] { Vector2.zero, new Vector2(0, -10), new Vector2(0, 10) }, down);
        }
    }
}
