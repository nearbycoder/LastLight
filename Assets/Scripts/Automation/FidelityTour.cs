using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LastLight.Core;
using LastLight.UI;
using LastLight.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastLight.Automation
{
    /// <summary>
    /// The fidelity tour (-llScript fidelity, with -llFresh, and -llFps 1000 so frames aren't held
    /// to the display): Settings ▸ Graphics fidelity. A fresh save is on High, the game as released.
    /// The stepper is walked with the keys, the simulated pad and clicks, and each step's camera,
    /// post-processing and moon are checked. Then the title, night V (fog, the costly night) and
    /// night XII (storm and rain) are each held still at one moment and shot at every step, the
    /// same frame each time, and each step's frame time is measured on that still frame and while
    /// the night plays (the AutoKeeper at the helm). The load average and the GPU's busy share are
    /// logged with every measurement, since the machine is shared.
    /// </summary>
    public static class FidelityTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["fidelity"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        static string Load()
        {
            string load = "?", gpu = "?";
            try { load = System.IO.File.ReadAllText("/proc/loadavg").Split(' ')[0]; } catch { }
            try { gpu = System.IO.File.ReadAllText("/sys/class/drm/card1/device/gpu_busy_percent").Trim() + "%"; } catch { }
            return $"load {load}, GPU busy {gpu}";
        }

        /// <summary>Frame times over <paramref name="seconds"/> of real time: the average and the
        /// mean of the slowest 1%, in milliseconds.</summary>
        static IEnumerator Measure(float seconds, float[] result)
        {
            var times = new List<float>();
            float start = Time.realtimeSinceStartup, last = start;
            yield return null;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                float now = Time.realtimeSinceStartup;
                times.Add((now - last) * 1000f);
                last = now;
                yield return null;
            }
            times.Sort();
            int worst = Mathf.Max(1, times.Count / 100);
            result[0] = times.Count > 0 ? times.Average() : 0f;
            result[1] = times.Count > 0 ? times.Skip(times.Count - worst).Average() : 0f;
        }

        static void SetLevel(int level)
        {
            var save = SaveData.Current;
            save.quality = level;
            save.Apply(display: false);
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
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

        static IEnumerator Click(Vector2 at)
        {
            TourScripts.MouseTo(at);
            yield return null;
            TourScripts.MouseTo(at, left: true);
            yield return null;
            yield return null;
            TourScripts.MouseTo(at);
            yield return null;
            yield return null;
        }

        /// <summary>What each step puts on the camera, the post-processing and the moon.</summary>
        static string Describe()
        {
            var cam = Stage.CameraData;
            string aa = cam.antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing ? $"SMAA {cam.antialiasingQuality}" : cam.antialiasing.ToString();
            return $"{Fidelity.Names[Fidelity.Level]}: {ShaderGlobals.Steps} steps, {aa}, bloom {(Stage.Bloom.active ? "on" : "off")}, moon shadows {Stage.Moon.shadows}, particles ×{Fidelity.Particles:0.##}, " +
                   $"keywords {(Shader.IsKeywordEnabled("LL_FIDELITY_LOW") ? "LOW" : "")}{(Shader.IsKeywordEnabled("LL_FIDELITY_ULTRA") ? "ULTRA" : "")}";
        }

        static bool Matches(int level)
        {
            var cam = Stage.CameraData;
            bool aa = level switch
            {
                Fidelity.Low => cam.antialiasing == AntialiasingMode.FastApproximateAntialiasing,
                Fidelity.Medium => cam.antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing && cam.antialiasingQuality == AntialiasingQuality.Medium,
                Fidelity.Ultra => cam.antialiasing == AntialiasingMode.TemporalAntiAliasing,
                _ => cam.antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing && cam.antialiasingQuality == AntialiasingQuality.High,
            };
            return Fidelity.Level == level && SaveData.Current.quality == level && aa
                && ShaderGlobals.Steps == Fidelity.StepsFor(level)
                && (Stage.Moon.shadows != LightShadows.None) == (level == Fidelity.Ultra)
                && Stage.Bloom.active == (level == Fidelity.Ultra)
                && Shader.IsKeywordEnabled("LL_FIDELITY_LOW") == (level == Fidelity.Low)
                && Shader.IsKeywordEnabled("LL_FIDELITY_ULTRA") == (level == Fidelity.Ultra);
        }

        static readonly int[] Order = { Fidelity.Low, Fidelity.Medium, Fidelity.High, Fidelity.Ultra };

        const int Rounds = 3;

        static float Median(List<float> v) { v.Sort(); return v[v.Count / 2]; }

        /// <summary>Measures every step in turn, <see cref="Rounds"/> times over, so a change in the
        /// shared machine's load falls on every step alike; keeps each step's median.</summary>
        static IEnumerator Rotate(Tour t, string scene, string how, float seconds, Dictionary<string, float[]> table, bool shots)
        {
            var avg = new Dictionary<int, List<float>>();
            var slow = new Dictionary<int, List<float>>();
            var r = new float[2];
            for (int round = 0; round < Rounds; round++)
                foreach (int level in Order)
                {
                    SetLevel(level);
                    yield return Frames(30);
                    if (shots && round == 0) yield return t.Shot($"{scene}_{how}_{level}_{Fidelity.Names[level].ToLowerInvariant()}");
                    yield return Measure(seconds, r);
                    if (!avg.ContainsKey(level)) { avg[level] = new List<float>(); slow[level] = new List<float>(); }
                    avg[level].Add(r[0]);
                    slow[level].Add(r[1]);
                    t.Log($"{scene}, {how}, round {round + 1}, {Fidelity.Names[level]}: {r[0]:0.00} ms average, slowest 1% {r[1]:0.00} ms ({Load()})");
                }
            foreach (int level in Order)
            {
                float a = Median(avg[level]), w = Median(slow[level]);
                table[$"{scene} {how} {Fidelity.Names[level]}"] = new[] { a, w };
                t.Log($"{scene}, {how}, {Fidelity.Names[level]}: median {a:0.00} ms a frame ({1000f / Mathf.Max(0.01f, a):0} fps), slowest 1% {w:0.00} ms");
            }
        }

        /// <summary>Holds the scene still (the night, the sea, the fog and the camera's sway) and
        /// shoots it at every step, then measures each step on that still frame.</summary>
        static IEnumerator Still(Tour t, string scene, Dictionary<string, float[]> table)
        {
            var g = Game.Instance;
            float sway = g.Rig.SwayAmount;
            g.Rig.SwayAmount = 0f;
            Time.timeScale = 0f;
            yield return Frames(10);
            foreach (int level in Order)
            {
                SetLevel(level);
                yield return Frames(60);   // Ultra's temporal anti-aliasing settles
                yield return t.Shot($"{scene}_{level}_{Fidelity.Names[level].ToLowerInvariant()}");
            }
            yield return Rotate(t, scene, "still", 2.5f, table, false);
            Time.timeScale = 1f;
            g.Rig.SwayAmount = sway;
        }

        static IEnumerator Night(Tour t, int night, float at, Dictionary<string, float[]> table)
        {
            var g = Game.Instance;
            SetLevel(Fidelity.High);
            g.TourHideAll();
            g.AutoPlay = true;
            g.TourBriefing(night);
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            g.Runner.AutoPlay = true;
            g.Runner.TimeScale = 4f;
            while (g.Runner != null && g.Runner.World.Time < at && !g.ShowingResults) yield return null;
            g.Runner.TimeScale = 1f;
            yield return Tour.Wait(2f);
            t.Log($"night {night} held at {g.Runner.World.Time:0.0} s of the night");
            yield return Still(t, $"night{night:00}", table);
            yield return Rotate(t, $"night{night:00}", "playing", 4f, table, true);
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var g = Game.Instance;
            var save = SaveData.Current;
            yield return Tour.Wait(4f);
            t.Log($"screen {Screen.width}x{Screen.height}, render scale {save.renderScale * 100:0}%, frame rate held to {Application.targetFrameRate} (vSync {QualitySettings.vSyncCount}), {SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsDeviceVersion}; {Load()}");
            Check(t, save.quality == Fidelity.High && Matches(Fidelity.High), $"a fresh save is on High, the game as released ({Describe()})");

            // Settings ▸ Graphics fidelity with the keys, the pad and the mouse.
            g.TourShowSettings();
            yield return Tour.Wait(1f);
            var s = (SettingsScreen)g.TourScreen("settings");
            var row = s.RowControl("Graphics fidelity");
            Check(t, row != null && s.RowControl("Fog and haze quality") == null, "Settings has Graphics fidelity in place of Fog and haze quality");
            EventSystem.current.SetSelectedGameObject(row.gameObject);
            yield return null;
            yield return Press(Key.RightArrow);
            yield return Tour.Wait(0.3f);
            Check(t, Matches(Fidelity.Ultra), $"→ chooses Ultra ({Describe()})");
            Check(t, s.AboutShown == "Graphics fidelity" && s.AboutText.Contains("Ultra: moon shadows, bloom"), $"the line under Settings says what Ultra does: \"{s.AboutText}\"");
            yield return t.Shot("settings_ultra");
            var pad = InputSystem.AddDevice<Gamepad>("TourPadFidelity");
            yield return null;
            yield return TourScripts.PadPress(pad, GamepadButton.DpadLeft);
            yield return TourScripts.PadPress(pad, GamepadButton.DpadLeft);
            yield return Tour.Wait(0.3f);
            Check(t, Matches(Fidelity.Medium), $"the pad's d-pad ← twice chooses Medium ({Describe()})");
            InputSystem.RemoveDevice(pad);
            yield return null;
            var corners = new Vector3[4];
            row.GetWorldCorners(corners);
            var left = new Vector2(Mathf.Lerp(corners[0].x, corners[2].x, 0.2f), (corners[0].y + corners[2].y) / 2f);
            var right = new Vector2(Mathf.Lerp(corners[0].x, corners[2].x, 0.8f), left.y);
            yield return Click(left);
            yield return Tour.Wait(0.3f);
            Check(t, Matches(Fidelity.Low), $"a click on the stepper's left side chooses Low ({Describe()})");
            Check(t, s.AboutText.Contains("Low:"), $"and the line says what Low does: \"{s.AboutText}\"");
            yield return t.Shot("settings_low");
            yield return Click(right);
            yield return Click(right);
            yield return Tour.Wait(0.3f);
            Check(t, Matches(Fidelity.High), $"two clicks on its right side go back to High ({Describe()})");
            // Kept with the other settings: the save's own words carry it, and an older save's
            // Low, Medium and High (once Fog and haze quality) read as before.
            save.quality = Fidelity.Ultra;
            var copy = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            Check(t, copy.quality == Fidelity.Ultra, $"the save keeps Ultra (\"quality\":{copy.quality})");
            save.quality = Fidelity.High;
            g.TourHideAll();
            g.TourTitle();
            TourScripts.MouseTo(new Vector2(-10, -10));
            yield return Tour.Wait(2f);

            // Every step, the same frame.
            var table = new Dictionary<string, float[]>();
            yield return Still(t, "title", table);
            foreach (int level in Order)
            {
                SetLevel(level);
                yield return Frames(5);
                Check(t, Matches(level), $"step {Describe()}");
            }
            yield return Night(t, 5, 75f, table);
            yield return Night(t, 12, 40f, table);
            SetLevel(Fidelity.High);

            // A table for the results: each step's frame time on each scene.
            t.Log("frame times, average / slowest 1% (ms):");
            foreach (int level in Order)
            {
                var sb = new System.Text.StringBuilder($"  {Fidelity.Names[level]}:");
                foreach (var key in table.Keys.Where(k => k.EndsWith(" " + Fidelity.Names[level])).ToList())
                    sb.Append($" | {key.Substring(0, key.Length - Fidelity.Names[level].Length - 1)} {table[key][0]:0.0} / {table[key][1]:0.0}");
                t.Log(sb.ToString());
            }
            t.Log($"real save untouched: {Game.HasArg("-llFresh")}");
            t.Log($"fidelity {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
