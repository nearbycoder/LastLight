using LastLight.Sim;
using NUnit.Framework;

namespace LastLight.Tests
{
    /// <summary>The dawn card's account of the score: its parts always add up to the score.</summary>
    public class ScorePartsTests
    {
        static SimWorld Play(MissionDef def, AutoKeeper bot, int seconds = 900, MapData map = null)
        {
            var w = new SimWorld(map ?? Validation.FreshMap(), def, 7);
            for (int i = 0; i < 60 * seconds && w.Outcome == MissionOutcome.Running; i++) w.Step(1f / 60f, bot.Decide(w, 1f / 60f));
            return w;
        }

        static void CheckParts(SimWorld w, string what)
        {
            var p = ScoreParts.Of(w);
            Assert.AreEqual(w.Score, p.Total, $"{what}: the parts ({p.Line()}) add up to the score");
            Assert.AreEqual(w.Arrivals, p.Ships, $"{what}: every ship home is counted");
            Assert.AreEqual(w.SteadyArrivals, p.Steady, $"{what}: every steady hand is counted");
        }

        [Test]
        public void ThePartsAddUpOnEveryNight()
        {
            var missions = Validation.FreshMissions();
            int dark = 0, wavered = 0;
            for (int n = 1; n <= 12; n++)
            {
                var def = missions[n - 1];
                var kept = Play(def, new AutoKeeper());
                CheckParts(kept, $"night {n}, kept");
                dark += ScoreParts.Of(kept).Dark;
                // A shakier keeper, so some ships waver and miss the steady-hand bonus.
                var shaky = Play(def, new AutoKeeper { AimError = 9f, Hesitation = 1.2f, Ignore = s => s.Name == def.ships[0].name });
                CheckParts(shaky, $"night {n}, shaky");
                wavered += shaky.Arrivals - shaky.SteadyArrivals;
            }
            Assert.Greater(dark, 0, "ships running dark came home and were counted double");
            Assert.Greater(wavered, 0, "some ships came home without a steady hand");
        }

        [Test]
        public void ThePartsAddUpOnAWatch()
        {
            var map = Validation.FreshMap();
            var def = NightWatch.Generate(map, 4242);
            var w = Play(def, new AutoKeeper(), 600, map);
            Assert.Greater(w.Arrivals, 0);
            CheckParts(w, "a ten-minute watch");
        }

        [Test]
        public void TheLineReadsInWords()
        {
            Assert.AreEqual("", ScoreParts.Of(new SimWorld(Validation.FreshMap(), Validation.FreshMissions()[0], 7)).Line(), "no ships home, nothing to say");
        }
    }
}
