using System.Collections;
using LastLight.Core;
using LastLight.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LastLight.Automation
{
    /// <summary>
    /// The keys tour (-llScript keys, with -llFresh): rebinds keys through Settings ▸ Keys with
    /// the simulated keyboard (Enter on a slot, then the new key), checks Esc backs out one step at
    /// a time, then plays night V to check the new keys work and the old ones don't, and that the
    /// prompts name the new keys. Ends by resetting the keys.
    /// </summary>
    public static class KeysTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["keys"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        static IEnumerator Press(Key key, float hold = 0f)
        {
            TourScripts.Key(key, true);
            yield return null;
            yield return null;
            if (hold > 0f) yield return Tour.Wait(hold);
            TourScripts.Key(key, false);
            yield return null;
            yield return null;
        }

        static string Selected() => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null ? EventSystem.current.currentSelectedGameObject.name : "nothing";

        /// <summary>Select a slot, press Enter on it, then the new key.</summary>
        static IEnumerator Rebind(SettingsScreen s, KeeperAction a, int slot, Key key)
        {
            EventSystem.current.SetSelectedGameObject(GameObject.Find($"Slot {a} {slot + 1}"));
            yield return null;
            yield return Press(Key.Enter);
            yield return Press(key);
            yield return null;
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var g = Game.Instance;
            var keys = SaveData.Current.keys;
            yield return Tour.Wait(4f);
            var title = (TitleScreen)g.TourScreen("title");
            var s = (SettingsScreen)g.TourScreen("settings");
            g.TourShowSettings();
            yield return Tour.Wait(1f);
            yield return t.Shot("keys_settings");
            s.TourShowKeys();
            yield return Tour.Wait(0.6f);
            Check(t, s.KeysOpen && s.SlotLabel(KeeperAction.Horn, 0) == "Space" && s.SlotLabel(KeeperAction.Focus, 0) == "Shift",
                $"the keys panel shows the defaults (horn \"{s.SlotLabel(KeeperAction.Horn, 0)}\", focus \"{s.SlotLabel(KeeperAction.Focus, 0)}\", turn left \"{s.SlotLabel(KeeperAction.TurnLeft, 1)}\")");
            yield return t.Shot("keys_defaults");

            // Enter on the horn's first slot waits for a key; H takes it.
            yield return Rebind(s, KeeperAction.Horn, 0, Key.H);
            Check(t, !s.Listening && keys.Get(KeeperAction.Horn, 0) == Key.H && s.SlotLabel(KeeperAction.Horn, 0) == "H",
                $"Enter then H puts H on the horn (slot shows \"{s.SlotLabel(KeeperAction.Horn, 0)}\", note \"{s.KeysNote}\")");
            // J for focus, in Shift's place.
            yield return Rebind(s, KeeperAction.Focus, 0, Key.J);
            Check(t, keys.Get(KeeperAction.Focus, 0) == Key.J, $"J replaces Shift for focus ({s.KeysNote})");
            // An arrow can be bound without moving the selection, and a key in use moves.
            yield return Rebind(s, KeeperAction.TurnLeft, 2, Key.DownArrow);
            string after = Selected();
            Check(t, keys.Get(KeeperAction.TurnLeft, 2) == Key.DownArrow && after == "Slot TurnLeft 3",
                $"↓ binds to turn left without moving the selection (slot \"{s.SlotLabel(KeeperAction.TurnLeft, 2)}\", selected {after})");
            yield return Rebind(s, KeeperAction.Horn, 1, Key.W);
            Check(t, keys.Get(KeeperAction.Horn, 1) == Key.W && keys.Get(KeeperAction.Focus, 1) == Key.None,
                $"W moves from focus to the horn: \"{s.KeysNote}\"");
            yield return Rebind(s, KeeperAction.Horn, 2, Key.P);
            Check(t, keys.Get(KeeperAction.Horn, 2) == Key.None && !s.Listening, $"P stays as pause and backs out ({s.KeysNote})");
            yield return t.Shot("keys_rebound");

            // Esc steps back: out of waiting for a key, then out of the panel, then out of Settings.
            EventSystem.current.SetSelectedGameObject(GameObject.Find("Slot Horn 3"));
            yield return null;
            yield return Press(Key.Enter);
            bool waiting = s.Listening;
            yield return Press(Key.Escape);
            Check(t, waiting && !s.Listening && s.KeysOpen && s.Visible, $"Esc stops waiting for a key and leaves the panel open (was waiting {waiting})");
            yield return Press(Key.Escape);
            Check(t, !s.KeysOpen && s.Visible, $"a second Esc closes the keys panel back to Settings ({Selected()})");
            yield return Press(Key.Escape);
            yield return Tour.Wait(0.6f);
            Check(t, !s.Visible, "a third Esc closes Settings");
            string footer = title.FooterShown;
            Check(t, footer.Contains("H  foghorn"), $"the title's strip names the horn's key: \"{footer}\"");

            // ---- Night V: the new keys work, the old ones don't, the prompts say so.
            g.AutoPlay = false;
            g.TourBriefing(5);
            yield return Tour.Wait(3.5f);
            var briefing = (BriefingScreen)g.TourScreen("briefing");
            Check(t, briefing.NewThingShown.Contains("H for the horn"), $"the fog card names H: \"{briefing.NewThingShown}\"");
            g.TourBegin();
            yield return Tour.Wait(2f);
            var w = g.Runner.World;
            Check(t, g.Hud.HornKeyShown == "H", $"the horn gauge says \"{g.Hud.HornKeyShown}\"");
            yield return Press(Key.Space);
            yield return Tour.Wait(0.3f);
            Check(t, w.HornCooldown <= 0f, $"Space no longer sounds the horn (cooldown {w.HornCooldown:0.0})");
            yield return Press(Key.H);
            yield return Tour.Wait(0.3f);
            Check(t, w.HornCooldown > 10f, $"H sounds the horn (cooldown {w.HornCooldown:0.0})");
            // Focus read while the key is still down.
            TourScripts.Key(Key.J, true);
            yield return Tour.Wait(1.2f);
            float focusJ = w.Beam.Focus;
            TourScripts.Key(Key.J, false);
            yield return Tour.Wait(1.5f);
            TourScripts.Key(Key.LeftShift, true);
            yield return Tour.Wait(1.2f);
            float focusShift = w.Beam.Focus;
            TourScripts.Key(Key.LeftShift, false);
            yield return null;
            Check(t, focusJ > 0.9f && focusShift < 0.3f, $"J focuses ({focusJ:0.00}) and Shift no longer does ({focusShift:0.00})");
            float before = w.Beam.Bearing;
            yield return Press(Key.DownArrow, 0.8f);
            float turned = Mathf.DeltaAngle(before * Mathf.Rad2Deg, w.Beam.Bearing * Mathf.Rad2Deg);
            Check(t, Mathf.Abs(turned) > 5f, $"↓ turns the lens ({turned:0.0}°)");
            g.TourPause();
            yield return Tour.Wait(0.8f);
            string card = g.TourPauseControls;
            Check(t, card.Contains("H or right button") && card.Contains("left button or J"), $"the pause card names the keys: \"{card.Replace("\n", " | ")}\"");
            yield return t.Shot("keys_pause_card");
            g.TourResume();

            // ---- Reset brings the defaults back.
            g.TourHideAll();
            g.TourTitle();
            yield return Tour.Wait(1f);
            g.TourShowSettings();
            yield return Tour.Wait(0.6f);
            s.TourShowKeys();
            yield return Tour.Wait(0.3f);
            EventSystem.current.SetSelectedGameObject(GameObject.Find("Button Reset to defaults"));
            yield return null;
            yield return Press(Key.Enter);
            Check(t, keys.Get(KeeperAction.Horn, 0) == Key.Space && keys.Get(KeeperAction.Focus, 0) == Key.LeftShift && keys.Get(KeeperAction.TurnLeft, 2) == Key.None && title.FooterShown.Contains("Space  foghorn"),
                $"Reset to defaults restores the keys ({s.KeysNote}; strip \"{title.FooterShown}\")");
            g.TourHideAll();
            t.Log($"real save untouched: {Game.HasArg("-llFresh")}");
            t.Log($"keys {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
