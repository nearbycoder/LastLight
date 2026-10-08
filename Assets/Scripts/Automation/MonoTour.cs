using System.Collections;
using LastLight.Audio;
using LastLight.Core;
using LastLight.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LastLight.Automation
{
    /// <summary>
    /// The mono tour (-llScript mono, with -llFresh): plays the foghorn panned hard left, then hard
    /// right, and measures the left and right channels of what the listener puts out. With Settings
    /// ▸ Sound on Stereo the far side is nearly silent; switched to Mono with the keyboard, both
    /// sides carry the same signal. Music and the sea are turned down while it listens.
    /// </summary>
    public static class MonoTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["mono"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        /// <summary>The listener's output while a panned foghorn sounds: RMS of the left and right channels.</summary>
        static IEnumerator Measure(float pan, float[] result)
        {
            var left = new float[1024];
            var right = new float[1024];
            double l = 0, r = 0;
            int n = 0;
            Sfx.Play("foghorn", 1f, 1f, pan, Bus.Sfx, 0f);
            float until = Unscaled.Time + 1.2f;
            yield return Tour.Wait(0.15f);
            while (Unscaled.Time < until)
            {
                AudioListener.GetOutputData(left, 0);
                AudioListener.GetOutputData(right, 1);
                for (int i = 0; i < left.Length; i++) { l += left[i] * left[i]; r += right[i] * right[i]; }
                n += left.Length;
                yield return null;
            }
            result[0] = (float)System.Math.Sqrt(l / Mathf.Max(1, n));
            result[1] = (float)System.Math.Sqrt(r / Mathf.Max(1, n));
            yield return Tour.Wait(2.5f);   // let it ring out before the next
        }

        static IEnumerator Press(Key key)
        {
            TourScripts.Key(key, true);
            yield return null;
            yield return null;
            TourScripts.Key(key, false);
            yield return null;
            yield return null;
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var g = Game.Instance;
            var save = SaveData.Current;
            yield return Tour.Wait(4f);
            t.Log($"output: {AudioSettings.GetConfiguration().speakerMode}, {AudioSettings.outputSampleRate} Hz, listener volume {AudioListener.volume:0.00}");
            save.music = 0f;
            save.ambience = 0f;
            save.Apply(display: false);
            yield return Tour.Wait(2f);
            var m = new float[2];

            Check(t, !save.mono && !MonoMix.On, "a fresh save is on Stereo");
            yield return Measure(-1f, m);
            float stereoL = m[0], stereoR = m[1];
            Check(t, stereoL > 0.003f && stereoR < 0.1f * stereoL, $"Stereo: the foghorn panned left is in the left channel only (left {stereoL:0.0000}, right {stereoR:0.0000})");

            // Settings ▸ Sound, one step right with the keyboard: Mono.
            g.TourShowSettings();
            yield return Tour.Wait(1f);
            var s = (SettingsScreen)g.TourScreen("settings");
            EventSystem.current.SetSelectedGameObject(s.RowControl("Sound").gameObject);
            yield return null;
            yield return Press(Key.RightArrow);
            yield return Tour.Wait(0.4f);
            Check(t, save.mono && MonoMix.On && s.AboutShown == "Sound", $"→ on Sound chooses Mono (mono {save.mono}, described: {s.AboutShown})");
            yield return t.Shot("mono_setting");
            g.TourHideAll();
            g.TourTitle();
            yield return Tour.Wait(1f);

            yield return Measure(-1f, m);
            float monoL = m[0], monoR = m[1];
            Check(t, monoL > 0.0015f && Mathf.Abs(monoL - monoR) <= 0.01f * Mathf.Max(monoL, monoR),
                $"Mono: the same foghorn reaches both channels alike (left {monoL:0.0000}, right {monoR:0.0000})");
            yield return Measure(1f, m);
            Check(t, m[1] > 0.0015f && Mathf.Abs(m[0] - m[1]) <= 0.01f * Mathf.Max(m[0], m[1]),
                $"Mono: panned right, too (left {m[0]:0.0000}, right {m[1]:0.0000})");

            // And back to Stereo.
            save.mono = false;
            save.Apply(display: false);
            yield return Measure(1f, m);
            Check(t, m[1] > 0.003f && m[0] < 0.1f * m[1], $"back on Stereo, the foghorn panned right is in the right channel only (left {m[0]:0.0000}, right {m[1]:0.0000})");
            t.Log($"real save untouched: {Game.HasArg("-llFresh")}");
            t.Log($"mono {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
