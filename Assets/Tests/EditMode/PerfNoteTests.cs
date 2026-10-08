using LastLight.Core;
using NUnit.Framework;

namespace LastLight.Tests
{
    /// <summary>The dawn card's word after a night that ran slowly: what to lower, in order.</summary>
    public class PerfNoteTests
    {
        [Test]
        public void ANightAtFullPaceSaysNothing()
        {
            var save = new SaveData();
            Assert.IsNull(Game.PerfNote(60f, 60, save));
            Assert.IsNull(Game.PerfNote(52f, 60, save), "a little short isn't worth a word");
            Assert.IsNull(Game.PerfNote(46f, 120, save), "45 and up is smooth enough, whatever the display");
            Assert.IsNull(Game.PerfNote(29.5f, 30, new SaveData { frameCap = 30 }), "a 30 cap that holds is what was asked for");
            Assert.IsNull(Game.PerfNote(0f, 60, save), "nothing measured, nothing said");
        }

        [Test]
        public void ASlowNightNamesTheNextStepsDown()
        {
            string note = Game.PerfNote(33.6f, 60, new SaveData());
            StringAssert.Contains("about 34 frames a second", note);
            StringAssert.Contains("Settings ▸ Graphics fidelity Medium or Render scale 70%", note);
            StringAssert.Contains("Graphics fidelity High or Render scale 70%", Game.PerfNote(30f, 60, new SaveData { quality = 3 }), "Ultra steps down to High first");
            StringAssert.Contains("Graphics fidelity Low or Render scale 50%", Game.PerfNote(30f, 60, new SaveData { renderScale = 0.7f, quality = 1 }));
            StringAssert.Contains("Render scale 70% or Frame rate 30", Game.PerfNote(30f, 60, new SaveData { renderScale = 0.85f, quality = 0 }));
            StringAssert.Contains("Graphics fidelity Medium or Frame rate 30", Game.PerfNote(30f, 60, new SaveData { renderScale = 0.5f }));
            StringAssert.Contains("Frame rate 30", Game.PerfNote(30f, 60, new SaveData { renderScale = 0.5f, quality = 0 }));
            StringAssert.Contains("Render scale 70%", Game.PerfNote(20f, 30, new SaveData { frameCap = 30 }), "a 30 cap that can't hold");
        }

        [Test]
        public void NothingLeftToLowerSaysNothing()
        {
            Assert.IsNull(Game.PerfNote(30f, 60, new SaveData { renderScale = 0.5f, quality = 0, frameCap = 30 }));
            Assert.IsNull(Game.PerfNote(20f, 60, new SaveData { renderScale = 0.5f, quality = 0 }), "below 25 a 30 cap wouldn't steady it");
        }
    }
}
