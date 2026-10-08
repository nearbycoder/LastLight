using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using LastLight.Core;
using LastLight.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LastLight.Automation
{
    /// <summary>
    /// The fullscreen tour (-llScript fullscreen, with -llFresh), only inside Tools/nested.sh: F11
    /// and Alt+Enter on the title switch between a window and fullscreen (and Alt+Enter doesn't
    /// also choose the menu item), and Settings ▸ Display and the save follow each time, also with
    /// Settings open. Then, as a finding rather than a check, the nested KWin is asked through a
    /// KWin script to make the window fullscreen, as the desktop's own shortcut would, and what the
    /// game sees is logged. Ends windowed.
    /// </summary>
    public static class FullscreenTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["fullscreen"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        static bool Full => Screen.fullScreenMode != FullScreenMode.Windowed;

        static IEnumerator Keys(params Key[] keys)
        {
            var kb = Keyboard.current;
            // Modifiers first, as a hand presses them, then the last key with them.
            for (int i = 1; i <= keys.Length; i++)
            {
                var held = new Key[i];
                Array.Copy(keys, held, i);
                InputSystem.QueueStateEvent(kb, new KeyboardState(held));
                yield return null;
                yield return null;
            }
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
            yield return null;
        }

        /// <summary>Waits up to 4 s for the window to reach the mode, then a moment for things to settle.</summary>
        static IEnumerator Until(bool full, float[] took)
        {
            float t0 = Unscaled.Time;
            while (Unscaled.Time - t0 < 4f && Full != full) yield return null;
            took[0] = Unscaled.Time - t0;
            yield return Tour.Wait(0.6f);
        }

        /// <summary>Runs a script in the nested KWin, as Tools/season_tour.sh's closewatch does.</summary>
        static string KWin(Tour t, string name, string js)
        {
            string path = Path.Combine(t.OutDir, name + ".js");
            File.WriteAllText(path, js);
            string Run(params string[] args)
            {
                // No argument here holds a quote or a space but the path, which is quoted.
                var psi = new ProcessStartInfo("qdbus6", string.Join(" ", Array.ConvertAll(args, a => "\"" + a + "\"")))
                    { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                using var p = Process.Start(psi);
                string output = p.StandardOutput.ReadToEnd().Trim() + p.StandardError.ReadToEnd().Trim();
                p.WaitForExit(5000);
                return output;
            }
            string id = Run("org.kde.KWin", "/Scripting", "org.kde.kwin.Scripting.loadScript", path, name);
            string ran = Run("org.kde.KWin", $"/Scripting/Script{id}", "org.kde.kwin.Script.run");
            string gone = Run("org.kde.KWin", "/Scripting", "org.kde.kwin.Scripting.unloadScript", name);
            return $"script {id}, run \"{ran}\", unloaded {gone}";
        }

        /// <summary>KWin's own view of the window: a script resizes it to 800 wide only if KWin
        /// doesn't hold it fullscreen. Returns the game's width two seconds later in result[0].</summary>
        static IEnumerator Probe(Tour t, string name, float[] result)
        {
            int pid = Process.GetCurrentProcess().Id;
            string ran = KWin(t, name, $"for (const w of workspace.windowList()) if (w.pid === {pid} && !w.fullScreen) w.frameGeometry = {{ x: 20, y: 20, width: 800, height: 600 }};\n");
            yield return Tour.Wait(2f);
            result[0] = Screen.width;
            t.Log($"probe {name} ({ran}): {Screen.fullScreenMode} {Screen.width}x{Screen.height}");
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            string display = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? "";
            if (!display.StartsWith("lastlight-nested-"))
            {
                Check(t, false, $"the fullscreen tour runs only inside Tools/nested.sh (WAYLAND_DISPLAY is \"{display}\"); nothing was switched");
                t.Log("fullscreen FAIL (refused)");
                yield break;
            }
            var g = Game.Instance;
            var save = SaveData.Current;
            var s = (SettingsScreen)g.TourScreen("settings");
            var title = (TitleScreen)g.TourScreen("title");
            var took = new float[1];
            yield return Tour.Wait(4f);
            Check(t, !Full, $"started in a window as the command line asked ({Screen.width}x{Screen.height}, {Screen.fullScreenMode})");
            // A window smaller than the screen, so each switch shows in the window's size as well
            // as in the mode Unity reports.
            int screenW = Screen.currentResolution.width, screenH = Screen.currentResolution.height;   // the nested KWin's screen
            save.fullscreen = false;
            save.resWidth = 1280; save.resHeight = 720;
            save.Apply();
            float waitedSize = 0f;
            while (waitedSize < 4f && Screen.width != 1280) { waitedSize += Time.unscaledDeltaTime; yield return null; }
            yield return Tour.Wait(0.5f);
            Check(t, !Full && Screen.width == 1280 && Screen.height == 720, $"a 1280x720 window on the {screenW}x{screenH} screen ({Screen.width}x{Screen.height})");
            // The control: while it's a window, KWin's probe resizes it.
            var probe = new float[1];
            yield return Probe(t, "ll-probe-window", probe);
            Check(t, probe[0] == 800f, $"KWin's probe resizes the window while it's a window (to {probe[0]:0} wide), so it can tell");
            save.Apply();
            waitedSize = 0f;
            while (waitedSize < 4f && Screen.width != 1280) { waitedSize += Time.unscaledDeltaTime; yield return null; }
            yield return Tour.Wait(0.5f);

            // F11 on the title.
            yield return Keys(Key.F11);
            yield return Until(true, took);
            Check(t, Full && save.fullscreen && g.TourShowingTitle, $"F11 makes it fullscreen ({Screen.fullScreenMode}, {Screen.width}x{Screen.height}, after {took[0]:0.00} s; save fullscreen {save.fullscreen})");
            yield return Probe(t, "ll-probe-f11", probe);
            Check(t, probe[0] != 800f && Full, $"KWin holds it fullscreen: its probe leaves it alone ({probe[0]:0} wide)");
            yield return t.Shot("fullscreen_f11");
            yield return Keys(Key.F11);
            yield return Until(false, took);
            Check(t, !Full && Screen.width == 1280 && !save.fullscreen, $"F11 again puts it back in its window ({Screen.width}x{Screen.height}, after {took[0]:0.00} s; save fullscreen {save.fullscreen})");

            // Alt+Enter, with the title's first item chosen: it switches and chooses nothing.
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(title.TourFirst.gameObject);
            yield return null;
            string chosen = title.TourFirst.name;
            yield return Keys(Key.LeftAlt, Key.Enter);
            yield return Until(true, took);
            bool stayed = g.TourShowingTitle && title.Visible;
            Check(t, Full && save.fullscreen && stayed, $"Alt+Enter makes it fullscreen ({Screen.width}x{Screen.height}, after {took[0]:0.00} s) and the title stays, with {chosen} chosen (still on the title: {stayed})");
            yield return Probe(t, "ll-probe-altenter", probe);
            Check(t, probe[0] != 800f && Full, $"KWin holds it fullscreen after Alt+Enter too ({probe[0]:0} wide)");
            yield return Keys(Key.LeftAlt, Key.Enter);
            yield return Until(false, took);
            Check(t, !Full && Screen.width == 1280 && !save.fullscreen && g.TourShowingTitle, $"Alt+Enter again puts it back in its window ({Screen.width}x{Screen.height}, after {took[0]:0.00} s)");

            // With Settings open, the Display row follows.
            g.TourShowSettings();
            yield return Tour.Wait(1f);
            string before = s.DisplayShown;
            yield return Keys(Key.F11);
            yield return Until(true, took);
            yield return Tour.Wait(0.3f);
            string after = s.DisplayShown;
            Check(t, before == "Windowed" && after == "Fullscreen", $"Settings ▸ Display follows F11 while it's open (\"{before}\", then \"{after}\")");
            yield return t.Shot("fullscreen_settings");
            yield return Keys(Key.F11);
            yield return Until(false, took);
            g.TourHideAll();
            g.TourTitle();
            yield return Tour.Wait(1f);

            // A finding, not a check: KWin's own fullscreen (its shortcut or window menu), asked for
            // here through a KWin script on a smaller window. Unity's Wayland backend is watched to
            // see whether it notices.
            int pid = Process.GetCurrentProcess().Id;
            string beforeKWin = $"{Screen.fullScreenMode} {Screen.width}x{Screen.height}";
            string ran = KWin(t, "ll-fullscreen-on", $"for (const w of workspace.windowList()) if (w.pid === {pid}) {{ print(\"lastlight: fullscreen \" + w.caption); w.fullScreen = true; }}\n");
            yield return Tour.Wait(3f);
            t.Log($"finding: KWin asked to make the window fullscreen ({ran}): the game saw {beforeKWin} before and {Screen.fullScreenMode} {Screen.width}x{Screen.height} after 3 s (save fullscreen {save.fullscreen})");
            yield return t.Shot("fullscreen_kwin");
            ran = KWin(t, "ll-fullscreen-off", $"for (const w of workspace.windowList()) if (w.pid === {pid}) {{ w.fullScreen = false; }}\n");
            yield return Tour.Wait(2f);
            t.Log($"finding: KWin asked to put it back ({ran}): {Screen.fullScreenMode} {Screen.width}x{Screen.height}");
            save.resWidth = save.resHeight = 0;
            save.Apply();
            yield return Tour.Wait(1.5f);

            // Whatever happened above, end in a window.
            if (Full) { save.fullscreen = false; save.Apply(); yield return Until(false, took); }
            Check(t, !Full, $"ends in a window ({Screen.width}x{Screen.height})");
            t.Log($"real save untouched: {Game.HasArg("-llFresh")}");
            t.Log($"fullscreen {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
