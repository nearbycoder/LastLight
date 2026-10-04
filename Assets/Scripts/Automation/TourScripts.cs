using System.Collections;
using LastLight.Core;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>The screenshot tours (selected with -llScript name).</summary>
    public static class TourScripts
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Tour.Scripts["ui"] = Ui;
            Tour.Scripts["night"] = Night;
        }

        public static IEnumerator Default(Tour t)
        {
            yield return Tour.Wait(3f);
            yield return t.Shot("00_boot");
            int night = Game.Arg("-llNight", 1);
            float at = Game.Arg("-llShotAt", 20);
            yield return Tour.Wait(at);
            yield return t.Shot($"night{night:00}");
        }

        /// <summary>Menus, a briefing, a night played by the AutoKeeper, pause and results.</summary>
        static IEnumerator Ui(Tour t)
        {
            var g = Game.Instance;
            yield return Tour.Wait(6f);
            yield return t.Shot("01_title");
            g.TourShowLogbook();
            yield return Tour.Wait(1.6f);
            yield return t.Shot("02_logbook");
            g.TourHideAll();
            g.TourShowSettings();
            yield return Tour.Wait(1.2f);
            yield return t.Shot("03_settings");
            g.TourHideAll();
            g.AutoPlay = true;
            int night = Game.Arg("-llNight2", 2);
            g.TourBriefing(night);
            yield return Tour.Wait(5f);
            yield return t.Shot("04_briefing");
            g.TourBegin();
            yield return Tour.Wait(14f);
            yield return t.Shot("05_play");
            g.TourPause();
            yield return Tour.Wait(1.2f);
            yield return t.Shot("06_pause");
            g.TourResume();
            g.Runner.TimeScale = 5f;
            float waited = 0f;
            while (!g.ShowingResults && waited < 120f) { waited += Time.unscaledDeltaTime; yield return null; }
            yield return Tour.Wait(4f);
            yield return t.Shot("07_results");
        }

        /// <summary>One night played by the AutoKeeper with a shot every N seconds.</summary>
        static IEnumerator Night(Tour t)
        {
            var g = Game.Instance;
            int night = Game.Arg("-llNight2", 1);
            float every = Game.Arg("-llEvery", 20);
            g.AutoPlay = true;
            g.TourBriefing(night);
            yield return Tour.Wait(4f);
            g.TourBegin();
            int i = 0;
            while (!g.ShowingResults && i < 30)
            {
                yield return Tour.Wait(every);
                yield return t.Shot($"n{night:00}_{i++:00}");
            }
            yield return Tour.Wait(4f);
            yield return t.Shot($"n{night:00}_results");
        }
    }
}
