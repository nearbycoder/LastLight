using System.Collections;
using LastLight.Core;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// The brightness tour (-llScript brightness, with -llFresh): the scene's mean brightness at
    /// each Brightness step, with the HUD hidden and the night held still. Each step must be
    /// brighter than the last, Standard must give the graded exposure, and the same frame must
    /// measure the same before and after the sweep.
    /// </summary>
    public static class BrightnessTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["brightness"] = Run;

        static IEnumerator Run(Tour t)
        {
            var g = Game.Instance;
            var save = SaveData.Current;
            yield return Tour.Wait(4f);
            g.TourShowSettings();
            yield return Tour.Wait(1.2f);
            yield return t.Shot("brightness_settings");
            g.TourHideAll();
            g.AutoPlay = true;
            g.TourBriefing(Game.Arg("-llNight2", 3));
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            yield return Tour.Wait(12f);
            g.Runner.TimeScale = 0f;
            g.Hud.Show(false, 0f);
            yield return Tour.Wait(1f);

            bool ok = Mathf.Approximately(Stage.Exposure(0), 0.45f);
            t.Log($"{(ok ? "PASS" : "FAIL")} Standard is the graded exposure ({Stage.Exposure(0):0.00})");
            float before = 0f, last = -1f;
            var values = new float[5];
            bool rising = true;
            for (int step = -3; step <= 2; step++)
            {
                // The first reading is Standard, before the sweep; then -2 up to +2.
                int s = step == -3 ? 0 : step;
                save.brightness = s;
                save.Apply(display: false);
                yield return null;
                yield return null;
                yield return new WaitForEndOfFrame();
                bool keep = s == -2 || s == 2 || step == -3;
                float b = TourScripts.MeanBrightness(keep ? System.IO.Path.Combine(t.OutDir, $"brightness_{(step == -3 ? "standard" : s < 0 ? "minus" + -s : "plus" + s)}.png") : null);
                if (step == -3) { before = b; continue; }
                values[s + 2] = b;
                if (last >= 0f && b <= last) rising = false;
                last = b;
            }
            t.Log($"scene brightness by step: -2 {values[0]:0.0000}, -1 {values[1]:0.0000}, Standard {values[2]:0.0000} (before the sweep {before:0.0000}), +1 {values[3]:0.0000}, +2 {values[4]:0.0000}");
            t.Log($"{(rising ? "PASS" : "FAIL")} each step is brighter than the last");
            bool same = Mathf.Abs(values[2] - before) <= 0.02f * before;
            t.Log($"{(same ? "PASS" : "FAIL")} Standard measures the same as before the sweep (within 2%)");
            save.brightness = 0;
            save.Apply(display: false);
            g.Hud.Show(true, 0f);
            g.Runner.TimeScale = 1f;
            t.Log($"brightness {(ok && rising && same ? "PASS" : "FAIL")}");
        }
    }
}
