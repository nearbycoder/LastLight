using System.Collections;
using LastLight.Core;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// The watchend tour (-llScript watchend, with -llFresh -llSeasonDone): keeps a Night Watch for a
    /// couple of minutes, ends it from the pause menu and checks it reached the dawn card and the
    /// table of best watches. Then checks a twelve-night pause menu still offers a restart.
    /// </summary>
    public static class WatchEndTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["watchend"] = Run;

        static IEnumerator Run(Tour t)
        {
            var g = Game.Instance;
            var save = SaveData.Current;
            int before = save.watches.Count;
            yield return Tour.Wait(5f);
            g.AutoPlay = true;
            g.TourWatch();
            yield return Tour.Wait(4f);
            g.TourBegin();
            g.Runner.TimeScale = 8f;
            while (g.Runner.World.Time < 150f) yield return null;
            g.Runner.TimeScale = 1f;
            var w = g.Runner.World;
            g.TourPause();
            yield return Tour.Wait(1.2f);
            yield return t.Shot("01_watch_pause");
            string label = g.TourPauseItem(1);
            t.Log($"{(label == "End the watch" ? "PASS" : "FAIL")} the watch's pause menu offers \"{label}\"");
            g.TourPauseChoose(1);
            yield return Tour.Wait(0.6f);
            yield return t.Shot("01b_watch_end_confirm");
            string answer = g.TourPauseConfirmLabel(1);
            t.Log($"{(g.TourPauseConfirming && answer == "End the watch" ? "PASS" : "FAIL")} ending the watch asks first: \"{g.TourPauseConfirmHeading}\" ({g.TourPauseConfirmLabel(0)} / {answer})");
            g.TourPauseConfirm(1);
            // The watch is recorded as it's stood down; the sea keeps moving behind the dawn card.
            int score = w.Score, ships = w.Arrivals, seconds = Mathf.RoundToInt(w.Time);
            WatchRecord kept = null;
            int rank = 0;
            for (int i = 0; i < save.watches.Count; i++)
                if (save.watches[i].seconds == seconds && save.watches[i].score == score && save.watches[i].ships == ships) { kept = save.watches[i]; rank = i + 1; break; }
            yield return Tour.Wait(4f);
            yield return t.Shot("02_watch_stood_down");
            bool ok = g.ShowingResults && w.StoodDown && save.watches.Count == Mathf.Min(SaveData.WatchTable, before + 1) && kept != null;
            t.Log($"{(ok ? "PASS" : "FAIL")} stood down at {seconds}s with {ships} ships home and {score} points: results {g.ShowingResults}, " +
                  $"kept {(kept != null ? $"as watch {rank} of {save.watches.Count} ({kept.score} points, {kept.ships} ships)" : "nowhere")}");
            t.Log($"real save untouched: {Game.HasArg("-llFresh")}");

            // The twelve nights still restart.
            g.TourHideAll();
            g.TourBriefing(2);
            yield return Tour.Wait(3f);
            g.TourBegin();
            yield return Tour.Wait(3f);
            g.TourPause();
            yield return Tour.Wait(1f);
            label = g.TourPauseItem(1);
            t.Log($"{(label == "Restart the night" ? "PASS" : "FAIL")} night II's pause menu offers \"{label}\"");
            yield return t.Shot("03_night_pause");
            g.TourResume();
        }
    }
}
