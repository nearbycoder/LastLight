using System.Collections.Generic;
using System.IO;
using LastLight.Core;
using NUnit.Framework;
using UnityEngine;

namespace LastLight.Tests
{
    /// <summary>Starting a new season, and the save file. Nothing here touches the real save: the
    /// file tests write under the project's Temp folder, and nothing calls Save().</summary>
    public class SeasonTests
    {
        static SaveData Finished()
        {
            var s = new SaveData
            {
                unlocked = 12, lamps = new[] { 3, 3, 3, 3, 2, 3, 3, 2, 1, 3, 2, 2 }, best = new[] { 610, 790, 1000, 1210, 1150, 1600, 1700, 1650, 1150, 1800, 1900, 2250 },
                shipsHome = 97, endingSeen = true, tutorialSeen = true, watchBest = 9600, watchShips = 57, watchSeconds = 954,
                hudScale = 1.3f, difficulty = 1, gameSpeed = 0.85f, focusToggle = true, textSpeed = 1.5f, master = 0.4f, brightness = 2,
            };
            s.homeNames.Add("Little Auk");
            s.hintsSeen.Add("aim");
            s.watches.Add(new WatchRecord { score = 9600, ships = 57, seconds = 954 });
            s.keys.Bind(KeeperAction.Horn, 1, UnityEngine.InputSystem.Key.H, out _);
            return s;
        }

        [Test]
        public void ANewSeasonClearsProgressAndKeepsSettings()
        {
            var s = Finished();
            Assert.IsTrue(s.HasProgress);
            s.ClearSeason();
            Assert.AreEqual(1, s.unlocked);
            Assert.AreEqual(0, s.TotalLamps);
            CollectionAssert.AreEqual(new int[12], s.best);
            Assert.AreEqual(0, s.shipsHome);
            Assert.IsEmpty(s.homeNames);
            Assert.IsFalse(s.endingSeen);
            Assert.IsFalse(s.WatchUnlocked);
            Assert.AreEqual(0, s.watchBest);
            Assert.IsEmpty(s.watches);
            Assert.IsEmpty(s.hintsSeen, "the hints come back");
            Assert.IsFalse(s.HasProgress, "nothing left to clear");
            // Settings and keys stay.
            Assert.AreEqual(1.3f, s.hudScale);
            Assert.AreEqual(1, s.difficulty);
            Assert.AreEqual(0.85f, s.gameSpeed);
            Assert.IsTrue(s.focusToggle);
            Assert.AreEqual(1.5f, s.textSpeed);
            Assert.AreEqual(0.4f, s.master);
            Assert.AreEqual(2, s.brightness);
            Assert.AreEqual(UnityEngine.InputSystem.Key.H, s.keys.Get(KeeperAction.Horn, 1));
            Assert.IsFalse(new SaveData().HasProgress, "a blank save has nothing to clear");
        }

        [Test]
        public void ASaveThatCantBeReadIsReported()
        {
            string whole = JsonUtility.ToJson(Finished());
            var into = new SaveData();
            Assert.IsTrue(SaveStore.TryParse(whole, into, out _));
            Assert.AreEqual(12, into.unlocked);
            Assert.IsTrue(SaveStore.TryParse("", new SaveData(), out _), "no save at all is a new keeper, not a damaged save");
            foreach (var damaged in new[] { whole.Substring(0, 120), "not a save", "{\"unlocked\":" })
                Assert.IsFalse(SaveStore.TryParse(damaged, new SaveData(), out var error), $"\"{damaged}\" is reported as unreadable ({error})");
        }

        [Test]
        public void TheSaveFileIsReplacedWhole()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "SeasonTests");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            try
            {
                SaveStore.Write(dir, SaveStore.FileName, "{\"unlocked\":3}");
                SaveStore.Write(dir, SaveStore.FileName, "{\"unlocked\":4}");
                Assert.AreEqual("{\"unlocked\":4}", SaveStore.ReadFile(dir, SaveStore.FileName));
                Assert.AreEqual("{\"unlocked\":4}", SaveStore.Read(dir, out var from));
                Assert.AreEqual("file", from);
                Assert.IsFalse(File.Exists(Path.Combine(dir, SaveStore.FileName + ".tmp")), "no temporary file is left behind");
                Assert.IsNull(SaveStore.ReadFile(dir, SaveStore.PreviousName));
                var names = new List<string>();
                foreach (var f in Directory.GetFiles(dir)) names.Add(Path.GetFileName(f));
                CollectionAssert.AreEquivalent(new[] { SaveStore.FileName }, names);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
