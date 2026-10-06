using LastLight.Core;
using NUnit.Framework;

namespace LastLight.Tests
{
    /// <summary>The pause menu's radio log: the night's calls in order, capped, and cleared with the radio.</summary>
    public class RadioLogTests
    {
        [Test]
        public void KeepsCallsInOrderAndDropsTheOldestPastCapacity()
        {
            var log = new RadioLog();
            for (int i = 0; i < RadioLog.Capacity + 5; i++) log.Add("Ianto Rees", "Harbourmaster", "call " + i);
            Assert.AreEqual(RadioLog.Capacity, log.Count);
            Assert.AreEqual("call 5", log.Entries[0].Text, "the oldest calls go first");
            Assert.AreEqual("call " + (RadioLog.Capacity + 4), log.Entries[log.Count - 1].Text, "the newest call is last");
            for (int i = 1; i < log.Count; i++)
                Assert.AreEqual("call " + (i + 5), log.Entries[i].Text);
        }

        [Test]
        public void ClearingTheRadioClearsTheLog()
        {
            var radio = new Radio();
            radio.Log.Add("Maren Holt", "Trawler Little Auk", "Thanks, keeper!");
            Assert.AreEqual(1, radio.Log.Count);
            radio.Clear();
            Assert.AreEqual(0, radio.Log.Count, "each night starts with an empty log");
        }
    }
}
