using System.Linq;
using LastLight.Core;
using NUnit.Framework;

namespace LastLight.Tests
{
    /// <summary>Settings ▸ Pad buttons: each style's names, the guess Auto makes from a pad's name,
    /// and the keeper's notes in those names. Works on its own SaveData, never the save.</summary>
    public class PadButtonsTests
    {
        [Test]
        public void EachStyleNamesItsButtons()
        {
            Assert.AreEqual(("A", "B", "RT", "Start"), PadButtons.For(PadStyle.Xbox));
            Assert.AreEqual(("Cross", "Circle", "R2", "Options"), PadButtons.For(PadStyle.PlayStation));
            // Nintendo by position: the bottom button (the one Xbox calls A) is B.
            Assert.AreEqual(("B", "A", "ZR", "+"), PadButtons.For(PadStyle.Nintendo));
            Assert.AreEqual(("A", "B", "RT", "Start"), PadButtons.For(PadStyle.Auto), "Auto with no pad reads as Xbox");
        }

        [TestCase("Sony Interactive Entertainment Wireless Controller", PadStyle.PlayStation)]
        [TestCase("DualSense Wireless Controller", PadStyle.PlayStation)]
        [TestCase("PS4 Controller", PadStyle.PlayStation)]
        [TestCase("Nintendo Switch Pro Controller", PadStyle.Nintendo)]
        [TestCase("Pro Controller", PadStyle.Nintendo)]
        [TestCase("Xbox Wireless Controller", PadStyle.Xbox)]
        [TestCase("Microsoft X-Box 360 pad", PadStyle.Xbox)]
        [TestCase("8BitDo Ultimate 2C Wireless", PadStyle.Xbox)]
        [TestCase("", PadStyle.Xbox)]
        public void AutoGuessesFromThePadsName(string product, PadStyle expected)
        {
            Assert.AreEqual(expected, PadButtons.Detect("Gamepad", product));
        }

        [Test]
        public void TheNotesUseTheChosenNames()
        {
            string Body(SaveData s, string id, bool pad = true) => KeeperNotes.For(s, pad).First(e => e.Id == id).Body;
            var save = new SaveData { unlocked = 12, padStyle = (int)PadStyle.PlayStation };
            StringAssert.Contains("Press Cross to sound the foghorn", Body(save, "fog"));
            StringAssert.Contains("Options pauses", Body(save, "light"));
            save.padStyle = (int)PadStyle.Nintendo;
            StringAssert.Contains("Press B to sound the foghorn", Body(save, "fog"));
            StringAssert.Contains("+ pauses", Body(save, "light"));
            save.padStyle = (int)PadStyle.Xbox;
            StringAssert.Contains("Press A to sound the foghorn", Body(save, "fog"));
            StringAssert.Contains("Start pauses", Body(save, "light"));
            StringAssert.Contains("Esc pauses", Body(save, "light", pad: false), "the mouse and keys keep their words");
        }
    }
}
