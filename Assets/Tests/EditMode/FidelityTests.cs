using LastLight.Core;
using LastLight.View;
using NUnit.Framework;
using UnityEngine;

namespace LastLight.Tests
{
    /// <summary>Settings ▸ Graphics fidelity grew out of Fog and haze quality: an older save's
    /// choice reads as the same step, and the steps below Ultra keep the raymarch they had.</summary>
    public class FidelityTests
    {
        [Test]
        public void AnOlderSavesFogQualityIsTheSameStep()
        {
            Assert.AreEqual(Fidelity.High, new SaveData().quality, "a new save starts on High, the game as graded");
            Assert.AreEqual(Fidelity.High, JsonUtility.FromJson<SaveData>("{\"master\":0.5}").quality, "a save with no choice is High");
            for (int old = 0; old <= 2; old++)
                Assert.AreEqual(old, JsonUtility.FromJson<SaveData>($"{{\"quality\":{old}}}").quality);
            Assert.AreEqual(new[] { "Low", "Medium", "High" }, new[] { Fidelity.Names[0], Fidelity.Names[1], Fidelity.Names[2] }, "the old names keep their places");
            var copy = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData { quality = Fidelity.Ultra }));
            Assert.AreEqual(Fidelity.Ultra, copy.quality, "Ultra is kept");
        }

        [Test]
        public void TheStepsBelowUltraKeepTheirRaymarch()
        {
            Assert.AreEqual(10, Fidelity.StepsFor(Fidelity.Low));
            Assert.AreEqual(16, Fidelity.StepsFor(Fidelity.Medium));
            Assert.AreEqual(24, Fidelity.StepsFor(Fidelity.High));
            Assert.Greater(Fidelity.StepsFor(Fidelity.Ultra), 24);
            foreach (int level in new[] { Fidelity.Low, Fidelity.Medium, Fidelity.High, Fidelity.Ultra })
                StringAssert.StartsWith(Fidelity.Names[level] + ":", Fidelity.About(level));
        }
    }
}
