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
            Tour.Scripts["video"] = Video;
            Tour.Scripts["watch"] = Watch;
            Tour.Scripts["report"] = Report;
        }

        /// <summary>The content validation report (the same as Tools/validate.sh) from the built player.</summary>
        static IEnumerator Report(Tour t)
        {
            yield return null;
            t.Log("validation report\n" + Validation.Report());
        }

        const int VideoFps = 30;

        /// <summary>Waits a span of captured video time (frames, not wall-clock seconds).</summary>
        static IEnumerator Hold(float seconds)
        {
            int frames = Mathf.RoundToInt(seconds * VideoFps);
            for (int i = 0; i < frames; i++) yield return null;
        }

        /// <summary>
        /// A gameplay reel recorded with <see cref="Recorder"/>: the title, then stretches of five
        /// nights played by the AutoKeeper at real speed (each opening on its briefing card, one with
        /// a staged wreck), the dawn results and the ending. Spans between scenes are fast-forwarded
        /// off camera.
        /// </summary>
        static IEnumerator Video(Tour t)
        {
            var g = Game.Instance;
            g.AutoPlay = true;
            var rec = Recorder.Begin(t.OutDir, VideoFps);
            yield return Hold(1.5f);
            rec.Rolling = true;
            yield return Hold(7f);
            if (Game.Arg("-llVideoTest", 0) == 1)
            {
                g.TourBriefing(2);
                yield return Hold(4.5f);
                g.TourBegin();
                yield return Hold(6f);
                rec.Finish();
                yield break;
            }

            // On night 5 the keeper leaves the first trawler to its fate: it strikes the uncharted Teeth.
            (int night, float from, float to, int neglect)[] scenes = { (2, 0f, 36f, -1), (5, 0f, 34f, 0), (8, 34f, 56f, -1), (9, 28f, 58f, -1), (12, 48f, 74f, -1) };
            for (int i = 0; i < scenes.Length; i++)
            {
                var (night, from, to, neglect) = scenes[i];
                rec.Rolling = true;
                if (i == 0) g.TourBriefing(night);
                else
                {
                    g.TourDip(0.7f, () => g.TourBriefing(night));
                    yield return Hold(0.7f);
                }
                yield return Hold(4.5f);
                g.TourBegin();
                yield return null;
                if (neglect >= 0) g.TourNeglect(g.Runner.Def.ships[neglect].name);
                if (from > 0f)
                {
                    rec.Rolling = false;
                    g.Runner.TimeScale = 4f;
                    while (g.Runner != null && g.Runner.World.Time < from && !g.ShowingResults) yield return null;
                    g.Runner.TimeScale = 1f;
                    rec.Rolling = true;
                }
                while (g.Runner != null && g.Runner.World.Time < to && !g.ShowingResults) yield return null;
                t.Log($"night {night}: recorded to t={g.Runner?.World.Time:0}, {rec.Frames} frames so far");
            }

            rec.Rolling = false;
            g.Runner.TimeScale = 4f;
            while (!g.ShowingResults) yield return null;
            rec.Rolling = true;
            yield return Hold(8f);

            g.TourEnding();
            yield return Hold(72f);
            rec.Rolling = false;
            t.Log($"video: {rec.Frames} frames ({rec.Frames / (float)VideoFps:0.0} s)");
            rec.Finish();
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
            // The resolution setting really resizes the window.
            var save = SaveData.Current;
            int w0 = Screen.width, h0 = Screen.height;
            save.fullscreen = false;
            save.resWidth = 1280; save.resHeight = 720;
            save.Apply();
            yield return Tour.Wait(1.5f);
            bool resized = Screen.width == 1280 && Screen.height == 720;
            t.Log($"{(resized ? "PASS" : "FAIL")} resolution 1280x720 applied (screen {Screen.width}x{Screen.height})");
            save.resWidth = w0; save.resHeight = h0;
            save.Apply();
            yield return Tour.Wait(1.5f);
            t.Log($"restored {Screen.width}x{Screen.height}");
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

            // The dawn debrief: a night where the keeper neglects one ship, then one with two.
            var neglect = Game.ArgString("-llDebrief", "4:Razorbill|2:Little Auk,Shearwater").Split('|');
            for (int k = 0; k < neglect.Length; k++)
            {
                var parts = neglect[k].Split(':');
                var names = new System.Collections.Generic.HashSet<string>(parts[1].Split(','));
                g.TourHideAll();
                g.TourBriefing(int.Parse(parts[0]));
                g.Runner.Bot.Ignore = s => names.Contains(s.Name);
                yield return Tour.Wait(3.5f);
                g.TourBegin();
                g.Runner.TimeScale = 6f;
                waited = 0f;
                while (!g.ShowingResults && waited < 240f) { waited += Time.unscaledDeltaTime; yield return null; }
                t.Log($"night {parts[0]} {g.Runner.World.Outcome} at {g.Runner.World.Time:0}s after {waited:0}s real, results {(g.ShowingResults ? "shown" : "NOT shown")}");
                yield return Tour.Wait(4f);
                foreach (var line in LastLight.Sim.Debrief.Lines(g.Runner.World)) t.Log("debrief " + line);
                yield return t.Shot($"{8 + k:00}_results_debrief");
            }
        }

        /// <summary>
        /// The Night Watch (run with -llFresh -llSeasonDone): the title with the watch unlocked, its
        /// briefing, the watch under way, and its end. After a spell the keeper looks away from the
        /// ships so the third wreck comes quickly.
        /// </summary>
        static IEnumerator Watch(Tour t)
        {
            var g = Game.Instance;
            yield return Tour.Wait(6f);
            yield return t.Shot("01_title_watch");
            g.AutoPlay = true;
            g.TourWatch();
            yield return Tour.Wait(5f);
            yield return t.Shot("02_watch_briefing");
            g.TourBegin();
            g.Runner.TimeScale = 3f;
            while (g.Runner.World.Time < 150f) yield return null;
            g.Runner.TimeScale = 1f;
            yield return Tour.Wait(1f);
            yield return t.Shot("03_watch_play");
            g.Runner.Bot.Ignore = s => true;
            g.Runner.TimeScale = 3f;
            float waited = 0f;
            while (g.Runner.World.Wrecks < 1 && waited < 120f) { waited += Time.unscaledDeltaTime; yield return null; }
            g.Runner.TimeScale = 1f;
            yield return Tour.Wait(1.5f);
            yield return t.Shot("04_watch_wreck");
            g.Runner.TimeScale = 3f;
            waited = 0f;
            while (!g.ShowingResults && waited < 240f) { waited += Time.unscaledDeltaTime; yield return null; }
            yield return Tour.Wait(5f);
            yield return t.Shot("05_watch_results");
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

            // ---- A gamepad, plugged in mid-game: menu navigation, stick aim, trigger, A, Start, B.
            var pad = InputSystem.AddDevice<Gamepad>("TourPad");
            yield return null;
            var es = UnityEngine.EventSystems.EventSystem.current;
            string Selected() => es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "nothing";
            yield return PadPress(pad, GamepadButton.DpadDown);
            string first = Selected();
            yield return PadPress(pad, GamepadButton.DpadDown);
            string second = Selected();
            bool navOk = first != "nothing" && second != "nothing" && first != second;
            t.Log($"{(navOk ? "PASS" : "FAIL")} the d-pad moves through the pause menu ({first} -> {second})");
            yield return PadPress(pad, GamepadButton.DpadUp);
            yield return PadPress(pad, GamepadButton.South);
            yield return Tour.Wait(0.5f);
            t.Log($"{(Time.timeScale == 1f && !g.TourPaused ? "PASS" : "FAIL")} A on Resume resumes ({Selected()})");

            bool padAim = true;
            foreach (var stick in new[] { new Vector2(-0.7f, 0.7f), new Vector2(0.9f, 0.3f), new Vector2(0f, 1f) })
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = stick });
                yield return Tour.Wait(1.5f);
                float err = Mathf.Abs(Geo.DeltaAngle(g.Runner.World.Beam.Bearing, Geo.Bearing(stick))) * Mathf.Rad2Deg;
                padAim &= err < 4f;
                t.Log($"{(err < 4f ? "PASS" : "FAIL")} right stick {stick} points the lens: error {err:0.0} deg");
            }
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(0f, 1f), rightTrigger = 1f });
            yield return Tour.Wait(0.8f);
            float padFocus = g.Runner.World.Beam.Focus;
            t.Log($"{(padFocus > 0.9f ? "PASS" : "FAIL")} the right trigger focuses (focus {padFocus:0.00})");
            yield return t.Shot("input_pad");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            while (g.Runner.World.HornCooldown > 0f) yield return null;
            yield return PadPress(pad, GamepadButton.South);
            yield return Tour.Wait(0.3f);
            float padCd = g.Runner.World.HornCooldown;
            t.Log($"{(padCd > 10f ? "PASS" : "FAIL")} A sounds the foghorn (cooldown {padCd:0.0})");
            yield return PadPress(pad, GamepadButton.Start);
            yield return Tour.Wait(0.5f);
            bool padPaused = Time.timeScale == 0f;
            yield return PadPress(pad, GamepadButton.East);
            yield return Tour.Wait(0.5f);
            t.Log($"{(padPaused && Time.timeScale == 1f ? "PASS" : "FAIL")} Start pauses and B resumes");
            InputSystem.RemoveDevice(pad);

            // ---- The turn-speed setting scales how fast the keys swing the lens.
            var rates = new float[2];
            var speeds = new[] { 0.5f, 1.25f };
            for (int k = 0; k < 2; k++)
            {
                SaveData.Current.turnSpeed = speeds[k];
                Key(UnityEngine.InputSystem.Key.D, true);
                yield return Tour.Wait(0.4f);        // up to speed
                float b0 = g.Runner.World.Beam.Bearing, t0 = g.Runner.World.Time;
                yield return Tour.Wait(0.5f);
                rates[k] = Mathf.Abs(Geo.DeltaAngle(b0, g.Runner.World.Beam.Bearing)) * Mathf.Rad2Deg / Mathf.Max(0.01f, g.Runner.World.Time - t0);
                Key(UnityEngine.InputSystem.Key.D, false);
                yield return Tour.Wait(0.5f);
            }
            SaveData.Current.turnSpeed = 1f;
            bool scaled = rates[1] > rates[0] * 1.8f;
            t.Log($"{(scaled ? "PASS" : "FAIL")} turn speed setting: {rates[0]:0} deg/s at 0.5, {rates[1]:0} deg/s at 1.25");
            t.Log(navOk && padAim && padFocus > 0.9f && padCd > 10f && padPaused && scaled ? "gamepad PASS" : "gamepad FAIL");
        }

        static IEnumerator PadPress(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            yield return null;
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
