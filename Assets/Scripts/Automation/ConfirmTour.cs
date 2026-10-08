using System.Collections;
using LastLight.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LastLight.Automation
{
    /// <summary>
    /// The confirm tour (-llScript confirm, with -llFresh -llSeasonDone): the pause menu's items
    /// that throw a night away (Restart, Keeper's logbook, Leave) or end a watch ask first. Checks
    /// each with the keyboard, the simulated pad and a click: the question shows with Stay
    /// selected, the night stays paused and unchanged, back returns to the menu, and going ahead
    /// does what the item says.
    /// </summary>
    public static class ConfirmTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["confirm"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
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

        static string Selected()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "nothing";
        }

        static IEnumerator StartNight(Game g, int night)
        {
            g.TourHideAll();
            g.AutoPlay = true;
            if (night == 13) g.TourWatch(); else g.TourBriefing(night);
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            g.Runner.TimeScale = 4f;
            while (g.Runner.World.Time < 30f) yield return null;
            g.Runner.TimeScale = 1f;
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var g = Game.Instance;
            var save = SaveData.Current;
            yield return Tour.Wait(4f);

            // ---- Night II with the keyboard: Leave asks, a second Enter stays, Esc backs out.
            yield return StartNight(g, 2);
            float simTime = g.Runner.World.Time;
            yield return Press(Key.Escape);
            yield return Tour.Wait(0.6f);
            yield return Press(Key.DownArrow);          // selects Resume
            for (int i = 0; i < 4; i++) yield return Press(Key.DownArrow);
            string onItem = Selected();
            yield return Press(Key.Enter);
            yield return Tour.Wait(0.5f);
            Check(t, g.TourPaused && g.TourPauseConfirming && g.TourPauseConfirmHeading == "Leave tonight's watch?",
                $"Enter on {onItem} asks \"{g.TourPauseConfirmHeading}\" ({g.TourPauseConfirmLabel(0)} / {g.TourPauseConfirmLabel(1)}) and the night stays paused");
            Check(t, Selected().Contains("Stay"), $"Stay is selected ({Selected()})");
            yield return t.Shot("confirm_leave_night");
            yield return Press(Key.Enter);
            yield return Tour.Wait(0.4f);
            Check(t, g.TourPaused && !g.TourPauseConfirming && Selected() == onItem && g.Night == 2,
                $"a second Enter stays: paused {g.TourPaused}, back on {Selected()}, night {g.Night}");
            yield return Press(Key.Enter);
            yield return Tour.Wait(0.4f);
            bool asked = g.TourPauseConfirming;
            yield return Press(Key.Escape);
            yield return Tour.Wait(0.4f);
            Check(t, asked && g.TourPaused && !g.TourPauseConfirming && Time.timeScale == 0f,
                $"Esc backs out of the question to the menu, still paused (asked {asked}, paused {g.TourPaused})");
            Check(t, Mathf.Abs(g.Runner.World.Time - simTime) < 0.5f, $"the night didn't move meanwhile ({simTime:0.0} s, now {g.Runner.World.Time:0.0} s)");

            // ---- Restart, as a click: asks, and going ahead starts the night again.
            g.TourPauseChoose(1);
            yield return Tour.Wait(0.4f);
            Check(t, g.TourPauseConfirming && g.TourPauseConfirmHeading == "Start the night again?" && g.TourPauseConfirmLabel(1) == "Restart the night",
                $"Restart asks \"{g.TourPauseConfirmHeading}\" ({g.TourPauseConfirmLabel(0)} / {g.TourPauseConfirmLabel(1)})");
            g.TourPauseConfirm(1);
            yield return Tour.Wait(1.5f);
            Check(t, g.Current == Game.State.Playing && g.Night == 2 && g.Runner.World.Time < 10f,
                $"going ahead restarts night II ({g.Current}, sim {g.Runner.World.Time:0.0} s)");

            // ---- The simulated pad: Start pauses, A on the logbook asks, B backs out, A then
            // down and A opens the logbook.
            var pad = InputSystem.AddDevice<Gamepad>("ConfirmPad");
            yield return null;
            g.Runner.TimeScale = 4f;
            while (g.Runner.World.Time < 20f) yield return null;
            g.Runner.TimeScale = 1f;
            yield return TourScripts.PadPress(pad, GamepadButton.Start);
            yield return Tour.Wait(0.6f);
            if (!Selected().Contains("Resume")) yield return TourScripts.PadPress(pad, GamepadButton.DpadDown);
            for (int i = 0; i < 4; i++) yield return TourScripts.PadPress(pad, GamepadButton.DpadDown);
            onItem = Selected();
            yield return TourScripts.PadPress(pad, GamepadButton.South);
            yield return Tour.Wait(0.4f);
            Check(t, g.TourPauseConfirming && g.TourPauseConfirmLabel(1) == "Open the logbook" && Selected().Contains("Stay"),
                $"A on {onItem} asks \"{g.TourPauseConfirmHeading}\" ({g.TourPauseConfirmLabel(1)}), Stay selected ({Selected()})");
            yield return TourScripts.PadPress(pad, GamepadButton.East);
            yield return Tour.Wait(0.4f);
            Check(t, g.TourPaused && !g.TourPauseConfirming && Selected() == onItem, $"B backs out to the menu, still paused, on {Selected()}");
            yield return TourScripts.PadPress(pad, GamepadButton.South);
            yield return Tour.Wait(0.4f);
            yield return TourScripts.PadPress(pad, GamepadButton.DpadDown);
            string answer = Selected();
            yield return TourScripts.PadPress(pad, GamepadButton.South);
            yield return Tour.Wait(1.5f);
            Check(t, g.Current == Game.State.Logbook, $"A on {answer} opens the logbook ({g.Current})");
            InputSystem.RemoveDevice(pad);
            yield return null;

            // ---- A Night Watch: leaving offers to end (and keep) it instead; ending asks too.
            int kept = save.watches.Count;
            yield return StartNight(g, 13);
            g.TourPause();
            yield return Tour.Wait(0.6f);
            Check(t, save.watchUnderway.active, $"pausing a watch notes where it stands ({save.watchUnderway.score} points, {save.watchUnderway.seconds} s; with -llFresh nothing is written)");
            g.TourPauseChoose(5);
            yield return Tour.Wait(0.6f);
            Check(t, g.TourPauseConfirmHeading == "Leave without keeping the watch?" && g.TourPauseConfirmLabel(1) == "End the watch" && g.TourPauseConfirmLabel(2) == "Leave anyway",
                $"leaving a watch asks \"{g.TourPauseConfirmHeading}\" ({g.TourPauseConfirmLabel(0)} / {g.TourPauseConfirmLabel(1)} / {g.TourPauseConfirmLabel(2)})");
            yield return t.Shot("confirm_leave_watch");
            g.TourPauseConfirm(0);
            yield return Tour.Wait(0.3f);
            g.TourPauseChoose(1);
            yield return Tour.Wait(0.4f);
            Check(t, g.TourPauseConfirmHeading == "End the watch now?" && g.TourPauseConfirmLabel(1) == "End the watch",
                $"ending a watch asks \"{g.TourPauseConfirmHeading}\" ({g.TourPauseConfirmLabel(0)} / {g.TourPauseConfirmLabel(1)})");
            g.TourPauseConfirm(0);
            yield return Tour.Wait(0.3f);
            g.TourPauseChoose(5);
            yield return Tour.Wait(0.4f);
            g.TourPauseConfirm(2);
            yield return Tour.Wait(1.5f);
            Check(t, g.TourShowingTitle && save.watches.Count == kept, $"Leave anyway goes to the title without keeping the watch (title {g.TourShowingTitle}, watches {kept} -> {save.watches.Count})");
            Check(t, !save.watchUnderway.active, "and forgets the watch under way, so the next start doesn't keep it either");
            t.Log($"confirm {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
