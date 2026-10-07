using System.Collections;
using LastLight.Core;
using LastLight.UI;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// The speed tour (-llScript speed, with -llFresh): measures simulated seconds per
    /// real second at each game speed, then checks the dawn card and the table of best watches say
    /// when a night or a watch was played slowed.
    /// </summary>
    public static class SpeedTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["speed"] = Run;

        /// <summary>The heading without its letter spacing.</summary>
        static string Squeeze(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"\s", "");

        static IEnumerator Run(Tour t)
        {
            var g = Game.Instance;
            var save = SaveData.Current;
            yield return Tour.Wait(4f);
            g.TourShowSettings();
            yield return Tour.Wait(1.2f);
            yield return t.Shot("01_settings");
            g.TourHideAll();
            g.AutoPlay = true;

            foreach (float speed in new[] { 1f, 0.85f, 0.7f })
            {
                save.gameSpeed = speed;
                g.TourBriefing(2);
                yield return Tour.Wait(3f);
                g.TourBegin();
                yield return Tour.Wait(2f);
                // Against the frame time the game itself counts (frames are capped at 0.1 s), so a
                // slow frame on a busy machine doesn't skew the ratio.
                float sim0 = g.Runner.World.Time, real = 0f, wall = 0f;
                while (wall < 10f)
                {
                    yield return null;
                    wall += Time.unscaledDeltaTime;
                    real += Mathf.Min(Time.deltaTime, 0.1f);
                }
                float ratio = (g.Runner.World.Time - sim0) / real;
                bool ok = Mathf.Abs(ratio - speed) <= 0.05f * speed;
                t.Log($"{(ok ? "PASS" : "FAIL")} game speed {speed * 100f:0}%: {ratio:0.000} sim seconds per real second over {wall:0.0}s");
                g.TourHideAll();
            }

            // A slowed night says so at dawn.
            save.gameSpeed = 0.7f;
            g.TourBriefing(2);
            yield return Tour.Wait(3f);
            g.TourBegin();
            g.Runner.TimeScale = 10f;
            float waited = 0f;
            while (!g.ShowingResults && waited < 120f) { waited += Time.unscaledDeltaTime; yield return null; }
            yield return Tour.Wait(2.5f);
            yield return t.Shot("02_results_70");
            string heading = Squeeze(((ResultsScreen)g.TourScreen("results")).Heading);
            t.Log($"{(heading.Contains("70%SPEED") ? "PASS" : "FAIL")} night II at 70%: dawn heading \"{heading}\"");

            // So does a slowed watch, on its card and in the table of best watches.
            g.TourHideAll();
            save.gameSpeed = 0.85f;
            g.TourWatch();
            yield return Tour.Wait(3f);
            g.TourBegin();
            g.Runner.TimeScale = 8f;
            while (g.Runner.World.Time < 90f) yield return null;
            g.TourPause();
            yield return Tour.Wait(0.5f);
            g.TourPauseChoose(1);
            int score = g.Runner.World.Score;
            WatchRecord kept = null;
            foreach (var r in save.watches) if (r.score == score && r.speed == 85) kept = r;
            yield return Tour.Wait(2.5f);
            heading = Squeeze(((ResultsScreen)g.TourScreen("results")).Heading);
            t.Log($"{(kept != null && heading.Contains("85%SPEED") ? "PASS" : "FAIL")} a watch at 85% is kept as {(kept != null ? kept.speed + "%" : "nothing")}, dawn heading \"{heading}\"");
            g.TourHideAll();
            save.gameSpeed = 1f;
            g.TourWatch();
            yield return Tour.Wait(3f);
            string records = ((BriefingScreen)g.TourScreen("briefing")).DateLine;
            bool listed = records.Contains("(85%)");
            t.Log($"{(listed ? "PASS" : "FAIL")} the watch briefing's records: \"{records}\"");
            yield return t.Shot("03_watch_records");
        }
    }
}
