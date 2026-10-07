using System.Collections;
using LastLight.Core;
using LastLight.Sim;
using LastLight.UI;
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
            Tour.Scripts["flash"] = FlashTour;
            Tour.Scripts["breakers"] = BreakersTour;
            Tour.Scripts["status"] = StatusTour;
            Tour.Scripts["radiolog"] = RadioLogTour;
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

            // The stakes: night 8 allows two wrecks, and the briefing says so.
            g.TourHideAll();
            g.TourBriefing(8);
            yield return Tour.Wait(4f);
            yield return t.Shot("07b_briefing_stakes");
            t.Log($"briefing stakes for night 8 (allows {g.Runner.Def.allowedWrecks}): \"{UI.BriefingScreen.StakesText(g.Runner.Def.allowedWrecks)}\"");
            bool allowanceOk = true;

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
                int wrecksSeen = 0;
                while (!g.ShowingResults && waited < 240f)
                {
                    waited += Time.unscaledDeltaTime;
                    var w = g.Runner.World;
                    if (w.Wrecks > wrecksSeen && w.Outcome == MissionOutcome.Running)
                    {
                        // The HUD's allowance row after each wreck: hulls crossed out, and the words.
                        wrecksSeen = w.Wrecks;
                        g.Runner.TimeScale = 0f;
                        yield return Tour.Wait(0.8f);
                        var (crossed, hulls, caption) = g.Hud.AllowanceShown();
                        string want = UiKit.Spaced(UI.Hud.AllowanceCaption(g.Runner.Def.allowedWrecks, w.Wrecks));
                        bool ok = hulls == g.Runner.Def.allowedWrecks && crossed == Mathf.Min(w.Wrecks, hulls) && caption == want;
                        allowanceOk &= ok;
                        t.Log($"{(ok ? "PASS" : "FAIL")} night {parts[0]} after wreck {w.Wrecks}: {crossed} of {hulls} hulls crossed, \"{caption}\"");
                        yield return t.Shot($"{8 + k:00}_allowance_wreck{w.Wrecks}");
                        g.Runner.TimeScale = 6f;
                    }
                    yield return null;
                }
                t.Log($"night {parts[0]} {g.Runner.World.Outcome} at {g.Runner.World.Time:0}s after {waited:0}s real, results {(g.ShowingResults ? "shown" : "NOT shown")}");
                yield return Tour.Wait(4f);
                foreach (var line in LastLight.Sim.Debrief.Lines(g.Runner.World)) t.Log("debrief " + line);
                yield return t.Shot($"{8 + k:00}_results_debrief");
            }
            t.Log($"{(allowanceOk ? "PASS" : "FAIL")} the HUD's wreck allowance follows the wrecks");
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
            // Fast-forward to the first squall at full blow.
            g.Runner.TimeScale = 4f;
            float sqWait = 0f;
            while (g.Runner.World.StormStrength < 0.95f && sqWait < 240f && !g.ShowingResults) { sqWait += Time.unscaledDeltaTime; yield return null; }
            g.Runner.TimeScale = 1f;
            yield return Tour.Wait(1.5f);
            t.Log($"squall at {g.Runner.World.Time:0}s: strength {g.Runner.World.StormStrength:0.00}, current {g.Runner.World.Current.magnitude:0.00}, rain {g.Runner.World.Rain:0.00}");
            yield return t.Shot("04_watch_squall");
            g.Runner.Bot.Ignore = s => true;
            g.Runner.TimeScale = 3f;
            float waited = 0f;
            while (g.Runner.World.Wrecks < 1 && waited < 120f) { waited += Time.unscaledDeltaTime; yield return null; }
            g.Runner.TimeScale = 1f;
            yield return Tour.Wait(1.5f);
            yield return t.Shot("05_watch_wreck");
            g.Runner.TimeScale = 3f;
            waited = 0f;
            while (!g.ShowingResults && waited < 240f) { waited += Time.unscaledDeltaTime; yield return null; }
            yield return Tour.Wait(5f);
            yield return t.Shot("06_watch_results");
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

        internal static void MouseTo(Vector2 screen, bool left = false, bool right = false)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var state = new MouseState { position = screen };
            if (left) state = state.WithButton(MouseButton.Left);
            if (right) state = state.WithButton(MouseButton.Right);
            InputSystem.QueueStateEvent(mouse, state);
        }

        internal static void Key(Key key, bool down)
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
            string keysStrip = g.TourPauseControls;
            t.Log($"{(keysStrip.Contains("Mouse") && keysStrip.Contains("Esc") ? "PASS" : "FAIL")} the pause menu's controls are in mouse and key words: \"{keysStrip}\"");
            CheckRadioLog(t, g);
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

            // ---- Prompts follow the device in use, and each hint shows once per save.
            bool promptsOk = InputMode.Pad;
            t.Log($"{(InputMode.Pad ? "PASS" : "FAIL")} the pad is the device in use after pad input");
            SaveData.Current.hintsSeen.Clear();
            g.TourBriefing(5);
            yield return Tour.Wait(3.5f);
            yield return t.Shot("input_briefing_pad");
            g.TourBegin();
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(0f, 1f) });
            float hintWait = 0f;
            while (g.Hud.HintOnScreen == null && hintWait < 30f) { hintWait += Time.unscaledDeltaTime; yield return null; }
            string padHint = g.Hud.HintOnScreen ?? "";
            yield return Tour.Wait(0.8f);
            yield return t.Shot("input_hint_pad");
            bool padWords = padHint.Contains("stick") || padHint.Contains("trigger") || padHint.Contains("Press A");
            t.Log($"{(padWords ? "PASS" : "FAIL")} the hint speaks pad: \"{padHint}\"");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            var mid = new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
            for (int i = 0; i < 10; i++) { MouseTo(mid + new Vector2(i * 12f, 0f)); yield return null; }
            yield return Tour.Wait(0.3f);
            string keyHint = g.Hud.HintOnScreen ?? "";
            bool keyWords = !InputMode.Pad && keyHint != padHint && keyHint != "";
            t.Log($"{(keyWords ? "PASS" : "FAIL")} moving the mouse rewords it: \"{keyHint}\"");
            yield return t.Shot("input_hint_keys");
            var seen = new System.Collections.Generic.List<string>(SaveData.Current.hintsSeen);
            g.TourBriefing(5);
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            string again = null;
            while (g.Runner.World.Time < 20f)
            {
                foreach (var id in seen) if (again == null && g.Hud.HintShowing(id)) again = g.Hud.HintOnScreen;
                yield return null;
            }
            bool once = seen.Count > 0 && again == null;
            t.Log($"{(once ? "PASS" : "FAIL")} hints seen once ({string.Join(", ", seen)}) stay away on the next night{(again != null ? ": showed \"" + again + "\"" : "")}");
            promptsOk &= padWords && keyWords && once;

            // ---- Nobody at the lamp: losing focus pauses, and so does unplugging the pad in use.
            yield return PadPress(pad, GamepadButton.DpadLeft);   // the pad in hand again
            g.FocusLost();
            yield return Tour.Wait(0.4f);
            bool focusPause = g.TourPaused && Time.timeScale == 0f;
            t.Log($"{(focusPause ? "PASS" : "FAIL")} losing window focus pauses the night");
            string padStrip = g.TourPauseControls;
            bool padStripOk = InputMode.Pad && padStrip.Contains("Right stick") && padStrip.Contains("Start");
            t.Log($"{(padStripOk ? "PASS" : "FAIL")} the pause menu's controls are in pad words: \"{padStrip}\"");
            yield return t.Shot("input_pause_pad");
            g.TourResume();
            yield return PadPress(pad, GamepadButton.DpadLeft);
            InputSystem.RemoveDevice(pad);
            yield return Tour.Wait(0.4f);
            bool unplugPause = g.TourPaused && Time.timeScale == 0f && !InputMode.Pad;
            t.Log($"{(unplugPause ? "PASS" : "FAIL")} unplugging the pad in use pauses the night");
            yield return t.Shot("input_unplugged");
            g.TourResume();
            promptsOk &= focusPause && unplugPause;

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

            // ---- Focus: Toggle switches it with a press of the button, key or trigger; Hold still holds.
            var focusPad = InputSystem.AddDevice<Gamepad>("TourPad3");
            yield return null;
            var mouseAt = cam.WorldToScreenPoint(new Vector3(0, 0, 100));
            Vector2 at = new Vector2(mouseAt.x, mouseAt.y);
            float Focus() => g.Runner.World.Beam.Focus;
            bool toggleOk = true;
            SaveData.Current.focusToggle = true;
            for (int k = 0; k < 3; k++)
            {
                string how = k == 0 ? "left button" : k == 1 ? "Shift" : "right trigger";
                for (int press = 0; press < 2; press++)
                {
                    if (k == 0) { MouseTo(at, left: true); yield return null; yield return null; MouseTo(at); }
                    else if (k == 1) { Key(UnityEngine.InputSystem.Key.LeftShift, true); yield return null; yield return null; Key(UnityEngine.InputSystem.Key.LeftShift, false); }
                    else { InputSystem.QueueStateEvent(focusPad, new GamepadState { rightTrigger = 1f }); yield return null; yield return null; InputSystem.QueueStateEvent(focusPad, new GamepadState()); }
                    yield return Tour.Wait(1.0f);   // released, and the lens has had time to narrow or widen
                    bool want = press == 0;
                    bool ok = want ? Focus() > 0.9f : Focus() < 0.1f;
                    toggleOk &= ok;
                    t.Log($"{(ok ? "PASS" : "FAIL")} Toggle: {(press == 0 ? "a press of" : "a second press of")} the {how} {(want ? "focuses, and it stays focused after release" : "widens the beam again")} (focus {Focus():0.00})");
                }
            }
            // While paused, a click on the menu is not a focus press.
            g.TourPause();
            yield return null;
            MouseTo(at, left: true); yield return null; yield return null; MouseTo(at);
            yield return null;
            g.TourResume();
            yield return Tour.Wait(1.0f);
            bool pausedClick = Focus() < 0.1f;
            t.Log($"{(pausedClick ? "PASS" : "FAIL")} Toggle: a click in the pause menu doesn't switch focus (focus {Focus():0.00})");
            toggleOk &= pausedClick;
            if (g.Runner.Controls.FocusOn) { Key(UnityEngine.InputSystem.Key.LeftShift, true); yield return null; Key(UnityEngine.InputSystem.Key.LeftShift, false); }
            SaveData.Current.focusToggle = false;
            MouseTo(at, left: true);
            yield return Tour.Wait(1.0f);
            bool holdOn = Focus() > 0.9f;
            MouseTo(at);
            yield return Tour.Wait(1.0f);
            bool holdOff = Focus() < 0.1f;
            t.Log($"{(holdOn && holdOff ? "PASS" : "FAIL")} Hold: focused while the button is down, wide once it's let go");
            toggleOk &= holdOn && holdOff;
            t.Log(toggleOk ? "focus toggle PASS" : "focus toggle FAIL");

            // ---- The pointer hides while the pad is in use, and comes back with the mouse.
            yield return PadPress(focusPad, GamepadButton.DpadLeft);
            yield return null;
            bool hidden = InputMode.Pad && !Cursor.visible;
            for (int i = 0; i < 10; i++) { MouseTo(at + new Vector2(i * 12f, 0f)); yield return null; }
            yield return null;
            bool shown = !InputMode.Pad && Cursor.visible;
            t.Log($"{(hidden && shown ? "PASS" : "FAIL")} the pointer hides on pad input ({hidden}) and returns with the mouse ({shown})");
            InputSystem.RemoveDevice(focusPad);
            t.Log(navOk && padAim && padFocus > 0.9f && padCd > 10f && padPaused && scaled ? "gamepad PASS" : "gamepad FAIL");
            // What the input system sees on this machine (a real pad would be listed here).
            foreach (var d in InputSystem.devices) t.Log($"device: {d.layout} \"{d.displayName}\" ({d.description.interfaceName} {d.description.product})");
            t.Log(promptsOk ? "prompts PASS" : "prompts FAIL");
            // The title's control strip in pad words.
            InputMode.Set(true);
            g.TourTitle();
            yield return Tour.Wait(4f);
            yield return t.Shot("input_title_pad");

            // ---- Settings by pad: down the left column, into the right one, then to Done.
            var pad2 = InputSystem.AddDevice<Gamepad>("TourPad2");
            yield return null;
            g.TourShowSettings();
            yield return Tour.Wait(1.2f);
            for (int i = 0; i < 10; i++) yield return PadPress(pad2, GamepadButton.DpadDown);
            var sel = es.currentSelectedGameObject;
            bool rightColumn = sel != null && ((RectTransform)sel.transform).anchoredPosition.x > 0f;
            for (int i = 0; i < 11; i++) yield return PadPress(pad2, GamepadButton.DpadDown);   // eleven rows on the right since Brightness
            string last = Selected();
            bool settingsNav = rightColumn && last == "Button Done";
            t.Log($"{(settingsNav ? "PASS" : "FAIL")} the d-pad walks both settings columns to Done (right column reached: {rightColumn}, ended on {last})");
            InputSystem.RemoveDevice(pad2);
            g.TourHideAll();
        }

        /// <summary>The pause menu's radio log shows the night's latest calls, the newest last.</summary>
        static bool CheckRadioLog(Tour t, Game g)
        {
            var log = g.Radio.Log;
            var (shown, newest) = g.TourPauseLog;
            bool ok = log.Count > 0 && shown > 0 && shown <= log.Count && newest == log.Entries[log.Count - 1].Text;
            t.Log($"{(ok ? "PASS" : "FAIL")} the pause menu's radio log shows {shown} of the night's {log.Count} calls, newest \"{newest}\"");
            return ok;
        }

        /// <summary>A night with plenty of radio, paused late on to read the log back.</summary>
        static IEnumerator RadioLogTour(Tour t)
        {
            var g = Game.Instance;
            g.AutoPlay = true;
            g.TourBriefing(Game.Arg("-llNight2", 6));
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            g.Runner.TimeScale = 1.5f;
            yield return Tour.Wait(Game.Arg("-llPauseAt", 70));
            g.TourPause();
            yield return Tour.Wait(1.2f);
            CheckRadioLog(t, g);
            yield return t.Shot("radio_log");
            g.TourResume();
        }

        internal static IEnumerator PadPress(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            yield return null;
        }

        /// <summary>
        /// A lightning strike on night 8 with Reduce flashing off, then on: a shot at the peak of
        /// each, and the screen's mean brightness at the peak and just before.
        /// </summary>
        static IEnumerator FlashTour(Tour t)
        {
            var g = Game.Instance;
            g.AutoPlay = true;
            g.TourBriefing(8);
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            foreach (bool reduce in new[] { false, true })
            {
                SaveData.Current.reduceFlashing = reduce;
                SaveData.Current.Apply(display: false);
                g.Runner.TimeScale = 3f;
                var w = g.Runner.World;
                while (w.Flash < 0.5f) yield return null;      // let any strike in progress pass
                while (w.Flash > 0.05f) yield return null;
                g.Runner.TimeScale = 1f;
                yield return new WaitForEndOfFrame();
                float before = MeanBrightness();
                while (w.Flash < 0.9f) yield return null;
                yield return new WaitForEndOfFrame();
                float peak = MeanBrightness(System.IO.Path.Combine(t.OutDir, reduce ? "flash_reduced.png" : "flash_full.png"));
                t.Log($"lightning, reduce flashing {(reduce ? "on" : "off")}: screen brightness {before:0.000} -> {peak:0.000} (x{peak / Mathf.Max(before, 1e-4f):0.00})");
            }
        }

        /// <summary>Mean luminance of the frame just drawn (0..1); optionally saves it as a PNG.</summary>
        /// <summary>
        /// Night 2 with the keeper ignoring one trawler: the crew's "breakers ahead" warning (the
        /// ring flickers, the hint shows) before it strikes, and a captain ringing for full astern.
        /// </summary>
        static IEnumerator BreakersTour(Tour t)
        {
            var g = Game.Instance;
            g.AutoPlay = true;
            string ignore = Game.ArgString("-llIgnore", "Little Auk");
            g.TourBriefing(Game.Arg("-llNight2", 2));
            g.Runner.Bot.Ignore = s => s.Name == ignore;
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            g.Runner.TimeScale = 2f;
            bool warned = false, astern = false;
            float waited = 0f;
            while ((!warned || !astern) && !g.ShowingResults && waited < 240f)
            {
                waited += Time.unscaledDeltaTime;
                foreach (var s in g.Runner.World.Ships)
                {
                    if (!warned && s.Danger > 0f && s.Name == ignore)
                    {
                        warned = true;
                        g.Runner.TimeScale = 1f;
                        yield return Tour.Wait(0.9f);
                        t.Log($"breakers ahead for {s.Name} at {g.Runner.World.Time:0}s; hint: {g.Hud.HintOnScreen}");
                        yield return t.Shot("breakers_warning");
                        g.Runner.TimeScale = 2f;
                        break;
                    }
                    if (!astern && s.Astern)
                    {
                        astern = true;
                        g.Runner.TimeScale = 1f;
                        yield return Tour.Wait(0.5f);
                        t.Log($"full astern: {s.Name} at {g.Runner.World.Time:0}s, speed {s.Speed:0.0}");
                        yield return t.Shot("full_astern");
                        g.Runner.TimeScale = 2f;
                        break;
                    }
                }
                yield return null;
            }
            t.Log($"{(warned ? "PASS" : "FAIL")} a breakers warning came for the neglected {ignore}; {(astern ? "PASS" : "FAIL")} a captain went full astern");
        }

        /// <summary>
        /// Night 9 with nobody at the lamp: ships lose their way and the wreckers lure them. Takes a
        /// shot when a lost ship and a lured one are both in the bay, and checks their glyphs show.
        /// </summary>
        static IEnumerator StatusTour(Tour t)
        {
            var g = Game.Instance;
            g.AutoPlay = false;
            g.TourBriefing(Game.Arg("-llNight2", 10));
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            // Staging: with nobody at the lamp the night can fail on wrecks before a wrecker lures
            // anyone, so this run lets it go on (the HUD keeps showing the real allowance).
            var def = g.Runner.Def;
            int allowed = def.allowedWrecks;
            def.allowedWrecks = 99;
            g.Runner.TimeScale = 2f;
            float waited = 0f;
            bool lostShot = false, luredShot = false, lostOk = false, luredOk = false, marksOk = true;
            // The top bar's mark for a ship must say the same as the ship (by shape, not colour).
            bool MarkMatches(SimShip s, string want)
            {
                string got = "";
                foreach (var (ship, mark) in g.Hud.ManifestMarks()) if (ship == s.Name) got = mark;
                t.Log($"{(got == want ? "PASS" : "FAIL")} manifest mark for {s.Name}: {(got == "" ? "none" : got)} (ship is {want})");
                return got == want;
            }
            while (waited < 300f && !g.ShowingResults && !(lostShot && luredShot))
            {
                waited += Time.unscaledDeltaTime;
                foreach (var s in g.Runner.World.Ships)
                {
                    if (!s.Inside || s.Pos.y < 5f) continue;
                    bool lost = s.State == ShipState.Lost && !lostShot, lured = s.State == ShipState.Lured && !luredShot;
                    if (!lost && !lured) continue;
                    g.Runner.TimeScale = 0.2f;
                    yield return Tour.Wait(0.5f);
                    bool shows = g.Hud.StatusShowing(s.Id);
                    marksOk &= MarkMatches(s, lost ? "lost" : "lured");
                    if (lost) { lostShot = true; lostOk = shows; t.Log($"lost: {s.Name} at {s.Pos}, glyph {(shows ? "showing" : "MISSING")}"); yield return t.Shot("status_lost"); }
                    else { luredShot = true; luredOk = shows; t.Log($"lured: {s.Name} by {s.LuredBy?.Site.Name}, glyph {(shows ? "showing" : "MISSING")}"); yield return t.Shot("status_lured"); }
                    g.Runner.TimeScale = 2f;
                    break;
                }
                yield return null;
            }
            def.allowedWrecks = allowed;
            t.Log($"{(lostOk && luredOk ? "PASS" : "FAIL")} a lost ship shows a '?' ({lostOk}) and a lured ship a lantern and tether ({luredOk})");
            // The other two marks on a fresh night 2: the bot keeps the lamp but leaves its first
            // ship to its fate, so one ship comes home and one is wrecked.
            g.TourBriefing(2);
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            g.Runner.AutoPlay = true;
            g.TourNeglect(g.Runner.Def.ships[0].name);
            g.Runner.TimeScale = 4f;
            bool homeSeen = false, wreckSeen = false;
            waited = 0f;
            while (waited < 240f && !g.ShowingResults && !(homeSeen && wreckSeen))
            {
                waited += Time.unscaledDeltaTime;
                SimShip home = null, wreck = null;
                foreach (var s in g.Runner.World.Ships)
                {
                    if (!homeSeen && home == null && s.State == ShipState.Arrived) home = s;
                    if (!wreckSeen && wreck == null && s.State == ShipState.Wrecked) wreck = s;
                }
                if (home != null || wreck != null)
                {
                    yield return Tour.Wait(0.3f);
                    if (home != null) { homeSeen = true; marksOk &= MarkMatches(home, "home"); }
                    if (wreck != null) { wreckSeen = true; marksOk &= MarkMatches(wreck, "wrecked"); }
                }
                yield return null;
            }
            yield return t.Shot("status_manifest");
            t.Log($"{(marksOk && homeSeen && wreckSeen ? "PASS" : "FAIL")} manifest marks follow the ships by shape (home seen {homeSeen}, wreck seen {wreckSeen})");
        }

        internal static float MeanBrightness(string save = null)
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (save != null) System.IO.File.WriteAllBytes(save, tex.EncodeToPNG());
            var px = tex.GetPixels32();
            double sum = 0;
            for (int i = 0; i < px.Length; i += 7) sum += (0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b) / 255.0;
            Object.Destroy(tex);
            return (float)(sum / (px.Length / 7.0));
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
