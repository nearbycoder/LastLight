using System.Collections;
using LastLight.Core;
using LastLight.Sim;
using LastLight.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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
            Tour.Scripts["nights"] = Nights;
            Tour.Scripts["ending"] = EndingTour;
            Tour.Scripts["input"] = InputTour;
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

        /// <summary>Several nights fast-forwarded by the AutoKeeper, shots at chosen game times.</summary>
        static IEnumerator Nights(Tour t)
        {
            var g = Game.Instance;
            g.AutoPlay = true;
            var list = Game.ArgString("-llNights", "5,7,8,9,11,12").Split(',');
            var times = Game.ArgString("-llTimes", "25,60").Split(',');
            foreach (var n in list)
            {
                int night = int.Parse(n);
                g.TourBriefing(night);
                yield return Tour.Wait(3.5f);
                g.TourBegin();
                g.Runner.TimeScale = 3f;
                foreach (var ts in times)
                {
                    float at = float.Parse(ts);
                    while (g.Runner != null && g.Runner.World.Time < at && !g.ShowingResults) yield return null;
                    g.Runner.TimeScale = 1f;
                    t.FrameStats();
                    yield return Tour.Wait(2.5f);
                    t.Log($"night {night} t={at}: {t.FrameStats()}");
                    yield return t.Shot($"night{night:00}_t{at:000}");
                    g.Runner.TimeScale = 3f;
                }
            }
        }

        static IEnumerator EndingTour(Tour t)
        {
            var g = Game.Instance;
            yield return Tour.Wait(2f);
            g.TourEnding();
            float[] at = { 8f, 26f, 44f, 50f, 60f, 68f, 80f };
            float clock = 0f;
            int k = 0;
            while (k < at.Length)
            {
                clock += Time.unscaledDeltaTime;
                if (clock >= at[k]) { yield return t.Shot($"ending_{k:00}"); k++; }
                yield return null;
            }
        }

        static void MouseTo(Vector2 screen, bool left = false, bool right = false)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var state = new MouseState { position = screen };
            if (left) state = state.WithButton(MouseButton.Left);
            if (right) state = state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse, state);
        }

        static void Key(Key key, bool down)
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            InputSystem.QueueStateEvent(kb, down ? new KeyboardState(key) : new KeyboardState());
        }

        /// <summary>Real input events: the lens follows the mouse, LMB focuses, Space sounds the horn.</summary>
        static IEnumerator InputTour(Tour t)
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var g = Game.Instance;
            g.AutoPlay = false;
            g.TourBriefing(5);
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            yield return Tour.Wait(1f);
            var cam = CameraRig.Instance.Cam;
            bool allOk = true;
            foreach (var target in new[] { new Vector2(-80, 60), new Vector2(70, 50), new Vector2(0, 100), new Vector2(-100, 0) })
            {
                var sp = cam.WorldToScreenPoint(new Vector3(target.x, 0, target.y));
                for (int i = 0; i < 40; i++) { MouseTo(new Vector2(sp.x, sp.y)); yield return null; }
                yield return Tour.Wait(1.2f);
                float want = Geo.Bearing(target - g.Runner.World.Beam.Origin);
                float err = Mathf.Abs(Geo.DeltaAngle(g.Runner.World.Beam.Bearing, want)) * Mathf.Rad2Deg;
                bool ok = err < 4f;
                allOk &= ok;
                t.Log($"{(ok ? "PASS" : "FAIL")} aim at {target}: beam error {err:0.0} deg");
            }
            yield return t.Shot("input_aim");
            var aim = cam.WorldToScreenPoint(new Vector3(0, 0, 100));
            for (int i = 0; i < 60; i++) { MouseTo(new Vector2(aim.x, aim.y), left: true); yield return null; }
            float focus = g.Runner.World.Beam.Focus;
            t.Log($"{(focus > 0.9f ? "PASS" : "FAIL")} holding the left button focuses the beam (focus {focus:0.00})");
            yield return t.Shot("input_focus");
            MouseTo(new Vector2(aim.x, aim.y));
            yield return Tour.Wait(0.5f);
            Key(UnityEngine.InputSystem.Key.Space, true);
            yield return null;
            yield return null;
            Key(UnityEngine.InputSystem.Key.Space, false);
            yield return Tour.Wait(0.3f);
            float cd = g.Runner.World.HornCooldown;
            t.Log($"{(cd > 10f ? "PASS" : "FAIL")} space sounds the foghorn (cooldown {cd:0.0})");
            yield return Tour.Wait(0.4f);
            yield return t.Shot("input_horn");
            Key(UnityEngine.InputSystem.Key.Escape, true);
            yield return null;
            Key(UnityEngine.InputSystem.Key.Escape, false);
            yield return Tour.Wait(0.5f);
            t.Log($"{(Time.timeScale == 0f ? "PASS" : "FAIL")} escape pauses");
            yield return t.Shot("input_pause");
            t.Log(allOk ? "aim PASS" : "aim FAIL");
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
