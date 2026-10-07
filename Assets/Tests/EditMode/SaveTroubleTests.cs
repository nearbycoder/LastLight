using System.IO;
using LastLight.Core;
using NUnit.Framework;
using UnityEngine;

namespace LastLight.Tests
{
    /// <summary>Trouble with the save is told to the keeper, and a save that can't be opened is
    /// never written over. Everything here happens under the project's Temp folder.</summary>
    public class SaveTroubleTests
    {
        static string dir;

        [SetUp]
        public void Make()
        {
            dir = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "SaveTroubleTests");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            SaveData.ClearTrouble();
        }

        [TearDown]
        public void Clear()
        {
            SaveData.ClearTrouble();
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        [Test]
        public void AGoodSaveLoadsQuietly()
        {
            File.WriteAllText(Path.Combine(dir, SaveStore.FileName), "{\"version\":1,\"unlocked\":5}");
            var s = SaveData.Load(dir, out var problem, out var blocked, out var migrate);
            Assert.AreEqual(5, s.unlocked);
            Assert.IsNull(problem);
            Assert.IsFalse(blocked);
            Assert.IsFalse(migrate);
        }

        [Test]
        public void ADamagedSaveIsKeptAsideAndSaid()
        {
            File.WriteAllText(Path.Combine(dir, SaveStore.FileName), "{\"version\":1,\"unlocked\":");
            var s = SaveData.Load(dir, out var problem, out var blocked, out _);
            Assert.AreEqual(1, s.unlocked, "a fresh season");
            Assert.IsFalse(blocked, "the damaged text is kept aside, so saving may go on");
            StringAssert.Contains(SaveStore.UnreadableName, problem);
            StringAssert.Contains(dir, problem);
            Assert.AreEqual("{\"version\":1,\"unlocked\":", SaveStore.ReadFile(dir, SaveStore.UnreadableName));
        }

        [Test]
        public void ASaveThatCantBeOpenedIsNeverWrittenOver()
        {
            // Something at save.json that can't be read as a file.
            string path = Path.Combine(dir, SaveStore.FileName);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "inside.txt"), "untouched");
            var s = SaveData.Load(dir, out var problem, out var blocked, out _);
            Assert.IsTrue(blocked);
            StringAssert.Contains("couldn't be opened", problem);
            StringAssert.Contains("won't be kept", problem);
            // This session doesn't write.
            SaveData.ClearTrouble(blocked: true);
            s.unlocked = 4;
            Assert.IsFalse(s.SaveTo(dir));
            Assert.IsTrue(Directory.Exists(path), "the save is left as it was");
            Assert.AreEqual("untouched", File.ReadAllText(Path.Combine(path, "inside.txt")));
            Assert.IsFalse(File.Exists(path + ".tmp"));
            StringAssert.Contains("wasn't saved", SaveData.Unsaved);
        }

        [Test]
        public void AFailedWriteIsReportedAndClearsWhenOneWorks()
        {
            // The save folder's place is taken by a file, so nothing can be written there.
            string blockedDir = Path.Combine(dir, "not-a-folder");
            File.WriteAllText(blockedDir, "a file");
            var s = new SaveData { unlocked = 3 };
            Assert.IsFalse(s.SaveTo(blockedDir));
            StringAssert.Contains("isn't being saved", SaveData.WriteProblem);
            StringAssert.Contains(blockedDir, SaveData.WriteProblem);
            StringAssert.Contains("couldn't be saved", SaveData.Unsaved);
            Assert.AreEqual(SaveData.WriteProblem, SaveData.Trouble);
            // A later save that works clears it.
            Assert.IsTrue(s.SaveTo(dir));
            Assert.IsNull(SaveData.WriteProblem);
            Assert.IsNull(SaveData.Unsaved);
            Assert.IsNull(SaveData.Trouble);
            var back = new SaveData();
            Assert.IsTrue(SaveStore.TryParse(SaveStore.ReadFile(dir, SaveStore.FileName), back, out _));
            Assert.AreEqual(3, back.unlocked);
        }
    }
}
