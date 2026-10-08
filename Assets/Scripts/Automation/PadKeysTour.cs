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
    /// The pad's buttons tour (-llScript padkeys, with -llFresh): with only the simulated pad,
    /// opens Settings ▸ Keys and buttons, binds the horn to RB and focus to LB (emptying LT), checks
    /// B binds rather than backs out while a slot waits, Start backs out, and focus keeps its last
    /// button; then plays night V to check RB sounds the horn and A doesn't, LB focuses and the
    /// triggers don't, the stick aims meanwhile, and the prompts name RB and LB (and R1 and L1 with
    /// PlayStation names). Ends by resetting the keys and buttons.
    /// </summary>
    public static class PadKeysTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["padkeys"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        static string Selected() => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null ? EventSystem.current.currentSelectedGameObject.name : "nothing";

        static IEnumerator Press(Gamepad pad, GamepadButton b) => TourScripts.PadPress(pad, b);

        /// <summary>A on the chosen slot, then the new button.</summary>
        static IEnumerator Rebind(Gamepad pad, GamepadButton b)
        {
            yield return Press(pad, GamepadButton.South);
            yield return Press(pad, b);
            yield return null;
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var g = Game.Instance;
            var bound = SaveData.Current.pad;
            yield return Tour.Wait(4f);
            var pad = InputSystem.AddDevice<Gamepad>("TourPadKeys");
            yield return null;
            var title = (TitleScreen)g.TourScreen("title");
            var s = (SettingsScreen)g.TourScreen("settings");
            g.TourShowSettings();
            yield return Tour.Wait(1f);
            // Down the left column with the d-pad to Keys and buttons, and A opens the panel.
            EventSystem.current.SetSelectedGameObject(s.RowControl("Keys and buttons").gameObject);
            yield return null;
            yield return Press(pad, GamepadButton.South);
            yield return Tour.Wait(0.6f);
            Check(t, InputMode.Pad && s.KeysOpen && Selected() == "Slot TurnLeft 1", $"A on Keys and buttons opens the panel ({Selected()} chosen, pad prompts {InputMode.Pad})");
            Check(t, s.PadSlotLabel(KeeperAction.Horn, 0) == "A" && s.PadSlotLabel(KeeperAction.Focus, 0) == "RT" && s.PadSlotLabel(KeeperAction.Focus, 1) == "LT",
                $"the pad's slots show the defaults (horn \"{s.PadSlotLabel(KeeperAction.Horn, 0)}\" \"{s.PadSlotLabel(KeeperAction.Horn, 1)}\", focus \"{s.PadSlotLabel(KeeperAction.Focus, 0)}\" \"{s.PadSlotLabel(KeeperAction.Focus, 1)}\")");
            yield return t.Shot("padkeys_defaults");

            // The d-pad walks the grid: down to the foghorn's row, then across the keys to the buttons.
            for (int i = 0; i < 3; i++) yield return Press(pad, GamepadButton.DpadDown);
            for (int i = 0; i < 3; i++) yield return Press(pad, GamepadButton.DpadRight);
            Check(t, Selected() == "Pad Horn 1", $"down three and right three reaches the horn's first button ({Selected()})");
            yield return Rebind(pad, GamepadButton.RightShoulder);
            Check(t, !s.Listening && bound.Get(KeeperAction.Horn, 0) == GamepadButton.RightShoulder && s.PadSlotLabel(KeeperAction.Horn, 0) == "RB" && Selected() == "Pad Horn 1",
                $"A then RB puts RB on the horn (slot \"{s.PadSlotLabel(KeeperAction.Horn, 0)}\", note \"{s.KeysNote}\", still on {Selected()})");

            // Up to focus's first button: LB in RT's place, then View empties LT.
            yield return Press(pad, GamepadButton.DpadUp);
            Check(t, Selected() == "Pad Focus 1", $"up reaches focus's first button ({Selected()})");
            yield return Rebind(pad, GamepadButton.LeftShoulder);
            yield return Press(pad, GamepadButton.DpadRight);
            yield return Rebind(pad, GamepadButton.Select);
            Check(t, bound.Get(KeeperAction.Focus, 0) == GamepadButton.LeftShoulder && bound.Get(KeeperAction.Focus, 1) == null,
                $"LB replaces RT for focus and View empties LT (\"{s.PadSlotLabel(KeeperAction.Focus, 0)}\", \"{s.PadSlotLabel(KeeperAction.Focus, 1)}\"; note \"{s.KeysNote}\")");
            // Focus's last button can't be emptied.
            yield return Press(pad, GamepadButton.DpadLeft);
            yield return Rebind(pad, GamepadButton.Select);
            Check(t, bound.Get(KeeperAction.Focus, 0) == GamepadButton.LeftShoulder, $"View on focus's last button keeps it: \"{s.KeysNote}\"");

            // While a slot waits, B is a button to bind, not a way back.
            yield return Press(pad, GamepadButton.DpadDown);
            yield return Press(pad, GamepadButton.DpadRight);
            Check(t, Selected() == "Pad Horn 2", $"down and right reaches the horn's second button ({Selected()})");
            yield return Rebind(pad, GamepadButton.East);
            Check(t, bound.Get(KeeperAction.Horn, 1) == GamepadButton.East && s.KeysOpen && s.Visible,
                $"B binds to the horn while the slot waits, and the panel stays open (\"{s.PadSlotLabel(KeeperAction.Horn, 1)}\", note \"{s.KeysNote}\")");
            yield return Rebind(pad, GamepadButton.Select);
            Check(t, bound.Get(KeeperAction.Horn, 1) == null, $"View empties it again (\"{s.KeysNote}\")");
            // Start backs out of waiting, changing nothing.
            yield return Rebind(pad, GamepadButton.Start);
            Check(t, !s.Listening && s.KeysOpen && bound.Get(KeeperAction.Horn, 1) == null && s.KeysNote == "Nothing changed.",
                $"Start stops waiting and leaves the panel open (note \"{s.KeysNote}\")");
            yield return t.Shot("padkeys_rebound");
            // B, not waiting, steps back: to Settings, then out.
            yield return Press(pad, GamepadButton.East);
            Check(t, !s.KeysOpen && s.Visible && Selected() == "Button Change", $"B closes the panel back to Settings ({Selected()})");
            yield return Press(pad, GamepadButton.East);
            yield return Tour.Wait(0.6f);
            Check(t, !s.Visible, "a second B closes Settings");
            string footer = title.FooterShown;
            Check(t, footer.Contains("Hold LB  focus") && footer.Contains("RB  foghorn"), $"the title's strip names the buttons: \"{footer}\"");

            // ---- Night V: the new buttons work, the old ones don't, and the prompts say so.
            g.AutoPlay = false;
            g.TourBriefing(5);
            yield return Tour.Wait(3.5f);
            var briefing = (BriefingScreen)g.TourScreen("briefing");
            Check(t, briefing.NewThingShown.Contains("Hold LB to focus, RB for the horn"), $"the fog card names LB and RB: \"{briefing.NewThingShown}\"");
            var horn = Feedback.TourHint("horn");
            var focusHint = Feedback.TourHint("focus");
            Check(t, horn.text == "Fog! Press RB to sound the foghorn." && horn.icon == "RB" && focusHint.text.StartsWith("Hold LB to focus") && focusHint.icon == "LB",
                $"the hints name them: \"{horn.text}\" [{horn.icon}], \"{focusHint.text}\" [{focusHint.icon}]");
            g.TourBegin();
            yield return Tour.Wait(2f);
            var w = g.Runner.World;
            Check(t, g.Hud.HornKeyShown == "RB", $"the horn gauge says \"{g.Hud.HornKeyShown}\"");
            yield return Press(pad, GamepadButton.South);
            yield return Tour.Wait(0.3f);
            Check(t, w.HornCooldown <= 0f, $"A no longer sounds the horn (cooldown {w.HornCooldown:0.0})");
            yield return Press(pad, GamepadButton.RightShoulder);
            yield return Tour.Wait(0.3f);
            Check(t, w.HornCooldown > 10f, $"RB sounds the horn (cooldown {w.HornCooldown:0.0})");
            // The triggers, held hard, no longer focus.
            InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f, leftTrigger = 1f });
            yield return Tour.Wait(1.2f);
            float focusTriggers = w.Beam.Focus;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Tour.Wait(1.5f);
            // LB focuses while the right stick swings the light out to sea, north-west or north-east,
            // whichever is further from where it points now.
            float before = w.Beam.Bearing;
            Vector2 nw = new Vector2(-0.8f, 0.6f), ne = new Vector2(0.8f, 0.6f);
            var aim = Mathf.Abs(Mathf.DeltaAngle(before * Mathf.Rad2Deg, Sim.Geo.Bearing(nw) * Mathf.Rad2Deg)) > Mathf.Abs(Mathf.DeltaAngle(before * Mathf.Rad2Deg, Sim.Geo.Bearing(ne) * Mathf.Rad2Deg)) ? nw : ne;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = aim }.WithButton(GamepadButton.LeftShoulder));
            yield return Tour.Wait(2.5f);
            float focusLB = w.Beam.Focus;
            float target = Sim.Geo.Bearing(aim);
            float off = Mathf.Abs(Mathf.DeltaAngle(w.Beam.Bearing * Mathf.Rad2Deg, target * Mathf.Rad2Deg));
            float moved = Mathf.Abs(Mathf.DeltaAngle(before * Mathf.Rad2Deg, w.Beam.Bearing * Mathf.Rad2Deg));
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            Check(t, focusTriggers < 0.3f && focusLB > 0.9f, $"LB focuses ({focusLB:0.00}) and the triggers no longer do ({focusTriggers:0.00})");
            Check(t, moved > 5f && off < 25f, $"the right stick aims while LB is held (turned {moved:0.0}°, {off:0.0}° from the stick's bearing)");
            g.TourPause();
            yield return Tour.Wait(0.8f);
            string card = g.TourPauseControls;
            Check(t, card.Contains(">Hold LB<") && card.Contains(">RB<"), $"the pause card names the buttons: \"{card.Replace("\n", " | ")}\"");
            yield return t.Shot("padkeys_pause_card");
            // PlayStation names for the same buttons.
            SaveData.Current.padStyle = (int)PadStyle.PlayStation;
            g.TourResume();
            yield return Tour.Wait(0.5f);
            g.TourPause();
            yield return Tour.Wait(0.8f);
            card = g.TourPauseControls;
            horn = Feedback.TourHint("horn");
            Check(t, card.Contains(">Hold L1<") && card.Contains(">R1<") && horn.text == "Fog! Press R1 to sound the foghorn.",
                $"with PlayStation names: \"{card.Replace("\n", " | ")}\", \"{horn.text}\"");
            SaveData.Current.padStyle = (int)PadStyle.Auto;
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
            yield return Press(pad, GamepadButton.South);
            Check(t, bound.Get(KeeperAction.Horn, 0) == GamepadButton.South && bound.Get(KeeperAction.Focus, 0) == GamepadButton.RightTrigger && bound.Get(KeeperAction.Focus, 1) == GamepadButton.LeftTrigger
                && title.FooterShown.Contains("A  foghorn") && title.FooterShown.Contains("Hold RT  focus"),
                $"Reset to defaults restores the buttons ({s.KeysNote}; strip \"{title.FooterShown}\")");
            g.TourHideAll();
            InputSystem.RemoveDevice(pad);
            t.Log($"real save untouched: {Game.HasArg("-llFresh")}");
            t.Log($"padkeys {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
