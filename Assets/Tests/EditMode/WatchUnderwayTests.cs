using System.IO;
using LastLight.Core;
using NUnit.Framework;
using UnityEngine;

namespace LastLight.Tests
{
    /// <summary>A Night Watch that outlives a crash: where a watch under way stands is kept in the
    /// save, and a watch still under way when the game starts is kept as it stood. Nothing here
    /// touches the real save: the file test writes under the project's Temp folder, and nothing
    /// calls Save().</summary>
    public class WatchUnderwayTests
    {
        static SaveData WithThreeWatches()
        {
            var s = new SaveData { unlocked = 12, endingSeen = true, shipsHome = 97, watchBest = 9600, watchShips = 57, watchSeconds = 954 };
            s.watches.Add(new WatchRecord { score = 9600, ships = 57, seconds = 954 });
            s.watches.Add(new WatchRecord { score = 7450, ships = 44, seconds = 781 });
            s.watches.Add(new WatchRecord { score = 5100, ships = 31, seconds = 602 });
            return s;
        }

        [Test]
        public void ACheckpointIsKeptInTheSaveFile()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "WatchUnderwayTests");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            SaveData.ClearTrouble();
            try
            {
                var s = WithThreeWatches();
                s.difficulty = 1;
                s.CheckpointWatch(5450, 33, 640.4f, new[] { "Little Auk", "Kittiwake" }, 85);
                Assert.IsTrue(s.SaveTo(dir));
                var back = SaveData.Load(dir, out var problem, out bool blocked, out _);
                Assert.IsNull(problem);
                Assert.IsFalse(blocked);
                var u = back.watchUnderway;
                Assert.IsTrue(u.active);
                Assert.AreEqual(5450, u.score);
                Assert.AreEqual(33, u.ships);
                Assert.AreEqual(640, u.seconds);
                Assert.IsTrue(u.hard, "kept on Hard");
                Assert.AreEqual(85, u.speed);
                CollectionAssert.AreEqual(new[] { "Little Auk", "Kittiwake" }, u.names);
            }
            finally
            {
                SaveData.ClearTrouble();
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void AWatchCutShortIsKeptOnceAsItStood()
        {
            var s = WithThreeWatches();
            s.CheckpointWatch(5450, 33, 640f, new[] { "Little Auk", "Saint Brannoc" });
            s.difficulty = 1;   // changed since: the watch keeps the difficulty it was played at
            var kept = s.RecoverWatch(out int rank);
            Assert.IsNotNull(kept);
            Assert.AreEqual(3, rank, "5,450 sits between 7,450 and 5,100");
            Assert.AreEqual(4, s.watches.Count);
            Assert.AreEqual(5450, s.watches[2].score);
            Assert.AreEqual(33, s.watches[2].ships);
            Assert.AreEqual(640, s.watches[2].seconds);
            Assert.IsFalse(s.watches[2].hard);
            Assert.AreEqual(99, s.shipsHome, "the ships it brought home count");
            Assert.Contains("Saint Brannoc", s.homeNames);
            Assert.IsFalse(s.watchUnderway.active, "the entry is cleared");
            Assert.IsNull(s.RecoverWatch(out _), "and kept only once");
            Assert.AreEqual(4, s.watches.Count);

            // A short watch still goes in, below the best, while there's room.
            var t = WithThreeWatches();
            t.CheckpointWatch(300, 2, 95f, new string[0]);
            Assert.IsNotNull(t.RecoverWatch(out rank));
            Assert.AreEqual(4, rank);
        }

        [Test]
        public void EndingAWatchClearsTheCheckpoint()
        {
            var s = WithThreeWatches();
            s.CheckpointWatch(1200, 8, 200f, new[] { "Kittiwake" });
            s.AddWatch(1500, 9, 230f, new[] { "Kittiwake" }, false);
            Assert.IsFalse(s.watchUnderway.active, "recording the watch clears it");
            Assert.IsNull(s.RecoverWatch(out _));

            s.CheckpointWatch(1200, 8, 200f, new string[0]);
            s.DropWatchUnderway();
            Assert.IsFalse(s.watchUnderway.active, "throwing the watch away clears it");

            s.CheckpointWatch(1200, 8, 200f, new string[0]);
            s.ClearSeason();
            Assert.IsFalse(s.watchUnderway.active, "a new season clears it");
        }

        [Test]
        public void AnOlderSaveHasNoWatchUnderway()
        {
            // A save written before this round has no watchUnderway at all.
            string json = JsonUtility.ToJson(WithThreeWatches());
            int at = json.IndexOf(",\"watchUnderway\"");
            Assert.Greater(at, 0);
            int end = json.IndexOf('}', json.IndexOf("\"names\"", at)) + 1;
            string older = json.Substring(0, at) + json.Substring(end);
            StringAssert.DoesNotContain("watchUnderway", older);
            var s = new SaveData();
            Assert.IsTrue(SaveStore.TryParse(older, s, out _));
            Assert.IsNotNull(s.watchUnderway);
            Assert.IsFalse(s.watchUnderway.active);
            Assert.IsNull(s.RecoverWatch(out _));
            Assert.AreEqual(3, s.watches.Count);
        }
    }
}
