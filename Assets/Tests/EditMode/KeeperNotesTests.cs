using System.Linq;
using LastLight.Core;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace LastLight.Tests
{
    /// <summary>The keeper's notes: entries open with the season, and the words follow the device,
    /// the keys, the Focus setting and the difficulty. Works on its own SaveData, never the save.</summary>
    public class KeeperNotesTests
    {
        static string Body(SaveData save, string id, bool pad = false) => KeeperNotes.For(save, pad).First(e => e.Id == id).Body;

        [Test]
        public void EntriesOpenWithTheSeason()
        {
            var save = new SaveData();
            CollectionAssert.AreEqual(new[] { "light", "ships", "lamps", "names", "assists" }, KeeperNotes.For(save, false).Select(e => e.Id).ToArray());
            Assert.AreEqual(11, KeeperNotes.Sealed(save));

            save.unlocked = 5;
            var ids = KeeperNotes.For(save, false).Select(e => e.Id).ToArray();
            CollectionAssert.IsSubsetOf(new[] { "reefs", "breakers", "buoys", "steamers", "fog" }, ids);
            CollectionAssert.DoesNotContain(ids, "wreckers", "night IX's idea stays sealed on night V");
            CollectionAssert.DoesNotContain(ids, "watch");

            save.unlocked = 12;
            Assert.AreEqual(15, KeeperNotes.For(save, false).Count, "the Night Watch waits for the season's end");
            save.endingSeen = true;
            Assert.AreEqual(16, KeeperNotes.For(save, false).Count);
            Assert.AreEqual(0, KeeperNotes.Sealed(save));
        }

        [Test]
        public void WordsFollowDeviceKeysAndSettings()
        {
            var save = new SaveData { unlocked = 12 };
            StringAssert.Contains("Move the mouse", Body(save, "light"));
            StringAssert.Contains("Hold the left button", Body(save, "light"));
            StringAssert.Contains("Point the right stick", Body(save, "light", pad: true));
            StringAssert.Contains("Hold the right trigger", Body(save, "light", pad: true));
            StringAssert.Contains("Press A", Body(save, "fog", pad: true));

            save.focusToggle = true;
            StringAssert.Contains("Click the left button", Body(save, "light"));
            StringAssert.Contains("again to widen", Body(save, "light"));

            save.keys.Bind(KeeperAction.Horn, 0, Key.H, out _);
            StringAssert.Contains("Press H or click the right button", Body(save, "fog"));
            save.keys.Clear(KeeperAction.Horn, 0);
            StringAssert.StartsWith("Click the right button", Body(save, "fog").Split('\n')[2]);
        }

        [Test]
        public void HardChangesTheNumbers()
        {
            var save = new SaveData { unlocked = 12 };
            StringAssert.Contains("22 seconds", Body(save, "reefs"));
            StringAssert.Contains("28 seconds", Body(save, "buoys"));
            save.difficulty = 1;
            StringAssert.Contains("15 seconds", Body(save, "reefs"));
            StringAssert.Contains("20 seconds", Body(save, "buoys"));
            StringAssert.StartsWith("On Hard the crew give no warning", Body(save, "breakers"));
        }
    }
}
