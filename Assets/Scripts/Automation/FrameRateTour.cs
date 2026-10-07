using System.Collections;
using LastLight.Core;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// The frame-rate tour (-llScript framerate, with -llFresh): night V (fog, the costly night)
    /// with the AutoKeeper, measured at each Settings ▸ Frame rate choice. 30 and 60 must hold to
    /// their cap; Display must not be held below 60. Frames are counted over real time, so a loaded
    /// machine can only make the Display figure lower, never the caps higher. -llFrameNight N plays
    /// another night instead.
    /// </summary>
    public static class FrameRateTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["framerate"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        static IEnumerator Measure(float seconds, float[] fps)
        {
            int frames = 0;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds) { frames++; yield return null; }
            fps[0] = frames / (Time.realtimeSinceStartup - start);
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            var g = Game.Instance;
            var save = SaveData.Current;
            yield return Tour.Wait(3f);
            int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            t.Log($"display {Screen.currentResolution.width}x{Screen.currentResolution.height} at {hz} Hz, vSync {QualitySettings.vSyncCount}");
            g.AutoPlay = true;
            // -llFrameNight 1 (with a low render scale) when the GPU is shared and night V can't reach 60.
            g.TourBriefing(Game.Arg("-llFrameNight", 5));
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            yield return Tour.Wait(4f);
            var fps = new float[1];
            float display = 0f;
            foreach (int cap in new[] { 0, 60, 30, 0 })
            {
                save.frameCap = cap;
                save.Apply(display: false);
                yield return Tour.Wait(1f);
                yield return Measure(5f, fps);
                string name = cap == 0 ? "Display" : cap.ToString();
                t.Log($"frame rate {name}: target {Application.targetFrameRate}, measured {fps[0]:0.0} fps (sim {g.Runner.World.Time:0} s)");
                if (cap != 0) Check(t, Application.targetFrameRate == cap, $"{name} sets the target to {cap} ({Application.targetFrameRate})");
                if (cap == 30) Check(t, fps[0] >= 28f && fps[0] <= 31f, $"30 holds the night to 30 fps ({fps[0]:0.0})");
                else if (cap == 60) Check(t, fps[0] <= 61f && (fps[0] >= 57f || display < 57f), $"60 holds the night to 60 fps ({fps[0]:0.0}{(display < 57f ? ", the machine can't reach 60 now" : "")})");
                else
                {
                    Check(t, Application.targetFrameRate == Mathf.Max(60, hz), $"Display aims for the display's rate, at least 60 (target {Application.targetFrameRate}, display {hz} Hz)");
                    display = Mathf.Max(display, fps[0]);
                }
            }
            t.Log($"framerate {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
