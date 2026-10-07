using LastLight.Core;
using NUnit.Framework;

namespace LastLight.Tests
{
    /// <summary>The dawn card's offer of help after the same night fails twice running.</summary>
    public class HelpNoteTests
    {
        [Test]
        public void OneFailureSaysNothing()
        {
            Assert.IsNull(Game.HelpNote(0, new SaveData()));
            Assert.IsNull(Game.HelpNote(1, new SaveData()));
        }

        [Test]
        public void TwoFailuresPointToTheAssistsNotInUse()
        {
            StringAssert.Contains("Game speed", Game.HelpNote(2, new SaveData()));
            StringAssert.Contains("85% or 70%", Game.HelpNote(3, new SaveData()));
            StringAssert.Contains("(70%)", Game.HelpNote(2, new SaveData { gameSpeed = 0.85f }));
            Assert.IsNull(Game.HelpNote(2, new SaveData { gameSpeed = 0.7f }), "both assists as easy as they go: nothing to offer");
            StringAssert.Contains("Difficulty", Game.HelpNote(2, new SaveData { difficulty = 1 }));
            StringAssert.Contains("Difficulty", Game.HelpNote(2, new SaveData { difficulty = 1, gameSpeed = 0.7f }));
        }
    }
}
