using System.Collections;
using LastLight.Core;
using LastLight.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LastLight.Automation
{
    /// <summary>
    /// The keeper's notes tour (-llScript notes, with -llFresh): the notes from the title with a new
    /// save (only night I's entries, and a line saying more will come) and with the season done
    /// (every entry). Walks the topics with the simulated keyboard and pad, checks every entry fits
    /// its page, that the words follow the device and the keys, and that the pause menu's notes
    /// open on tonight's idea and leave the night paused and unchanged.
    /// </summary>
    public static class NotesTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["notes"] = Run;

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

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var g = Game.Instance;
            var save = SaveData.Current;
            var notes = g.TourNotes;
            yield return Tour.Wait(5f);

            // ---- A new keeper: night I's notes, and the promise of more.
            int firstNight = KeeperNotes.For(save, false).Count;
            g.TourShowNotes();
            yield return Tour.Wait(1.2f);
            Check(t, notes.Visible && notes.TopicCount == firstNight && firstNight == 5 && notes.SealedNoteShowing,
                $"a new save's notes hold night I's {notes.TopicCount} entries, with more to come (sealed note {notes.SealedNoteShowing})");
            var titles = "";
            for (int i = 0; i < notes.TopicCount; i++) titles += (i > 0 ? ", " : "") + notes.TopicTitle(i);
            t.Log($"topics: {titles}");
            Check(t, notes.ShownId == "light", $"the book opens on the light ({notes.ShownId})");
            yield return t.Shot("notes_new_save");

            // Keys: the first press selects the first topic, the next ones walk down.
            yield return Press(Key.DownArrow);
            yield return Press(Key.DownArrow);
            yield return Press(Key.DownArrow);
            Check(t, notes.ShownId == "lamps", $"the arrow keys walk the topics and show each one ({Selected()}, showing {notes.ShownId})");
            yield return Press(Key.Escape);
            yield return Tour.Wait(0.8f);
            Check(t, !notes.Visible && g.TourShowingTitle, $"Esc closes the notes, back to the title (notes {notes.Visible}, title {g.TourShowingTitle})");

            // ---- The season done: every entry, and each fits its page.
            save.unlocked = 12;
            save.endingSeen = true;
            g.TourShowNotes();
            yield return Tour.Wait(1.2f);
            var all = KeeperNotes.For(save, false);
            Check(t, notes.TopicCount == all.Count && all.Count == 16 && !notes.SealedNoteShowing, $"a finished season's notes hold all {notes.TopicCount} entries, nothing sealed");
            var topicsRoom = notes.TopicsRect.rect.height;
            Check(t, notes.TopicsHeight <= topicsRoom, $"the topics fit the left page ({notes.TopicsHeight:0} of {topicsRoom:0})");
            bool fits = true;
            string worst = "";
            float worstNeed = 0f;
            foreach (var e in all)
            {
                notes.Refresh(save, e.Id);
                yield return null;
                var (need, room) = notes.BodyFit;
                if (need > worstNeed) { worstNeed = need; worst = e.Id; }
                if (need > room) { fits = false; t.Log($"entry {e.Id} needs {need:0} of {room:0}"); }
            }
            Check(t, fits, $"every entry fits its page (longest: {worst}, {worstNeed:0} of {notes.BodyFit.room:0})");

            // A pad: B closes, and the d-pad walks the topics.
            var pad = InputSystem.AddDevice<Gamepad>("NotesPad");
            yield return null;
            notes.Refresh(save, "light");
            yield return TourScripts.PadPress(pad, GamepadButton.DpadDown);   // selects the first topic
            for (int i = 0; i < 12; i++) yield return TourScripts.PadPress(pad, GamepadButton.DpadDown);
            Check(t, notes.ShownId == "wreckers", $"the d-pad walks the topics ({Selected()}, showing {notes.ShownId})");
            yield return t.Shot("notes_wreckers");
            // The pad was picked up with the book open: the entries are reworded where they stand.
            Check(t, InputMode.Pad, "the pad is the device in use");
            notes.Refresh(save, "light");
            yield return null;
            Check(t, notes.ShownBody.Contains("right stick") && notes.ShownBody.Contains("right trigger") && notes.ShownBody.Contains("Start"),
                $"with a pad the light's entry names the stick, trigger and Start: \"{notes.ShownBody.Substring(0, Mathf.Min(80, notes.ShownBody.Length))}…\"");
            yield return t.Shot("notes_pad");
            yield return TourScripts.PadPress(pad, GamepadButton.East);
            yield return Tour.Wait(0.8f);
            Check(t, !notes.Visible && g.TourShowingTitle, "B closes the notes");
            InputSystem.RemoveDevice(pad);
            yield return null;

            // ---- Keys: the notes name the keys the keeper chose (and the mouse words come back).
            InputMode.Set(false);
            save.keys.Bind(KeeperAction.Horn, 0, Key.H, out _);
            g.TourShowNotes();
            yield return Tour.Wait(0.8f);
            notes.Refresh(save, "fog");
            yield return null;
            Check(t, notes.ShownBody.Contains("Press H or click the right button"), $"the foghorn's entry names the rebound key: \"{FirstLine(notes.ShownBody, "foghorn")}\"");
            notes.Refresh(save, "light");
            yield return null;
            Check(t, notes.ShownBody.Contains("Move the mouse") && notes.ShownBody.Contains("Esc"), "with the mouse the light's entry talks of the mouse and Esc");
            save.keys.Reset();
            g.TourHideAll();

            // ---- From the pause menu: the night waits, and the book opens on tonight's idea.
            g.AutoPlay = true;
            g.TourBriefing(5);
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            g.Runner.TimeScale = 3f;
            while (g.Runner.World.Time < 20f) yield return null;
            g.Runner.TimeScale = 1f;
            g.TourPause();
            yield return Tour.Wait(0.6f);
            float simTime = g.Runner.World.Time;
            Check(t, g.TourPauseItem(3) == "Keeper's notes", $"the pause menu offers the notes ({g.TourPauseItem(3)})");
            g.TourPauseChoose(3);
            yield return Tour.Wait(1.2f);
            Check(t, notes.Visible && notes.ShownId == "fog", $"on night V the notes open on fog and the foghorn ({notes.ShownId})");
            yield return t.Shot("notes_from_pause");
            yield return Press(Key.Escape);
            yield return Tour.Wait(0.8f);
            Check(t, !notes.Visible && g.TourPaused && g.TourScreen("pause").Visible && Time.timeScale == 0f, $"Esc goes back to the pause menu, still paused (paused {g.TourPaused})");
            Check(t, Mathf.Abs(g.Runner.World.Time - simTime) < 0.1f && g.Night == 5, $"the night didn't move meanwhile ({simTime:0.0} s, now {g.Runner.World.Time:0.0} s)");
            g.TourResume();
            yield return Tour.Wait(1f);
            Check(t, !g.TourPaused && g.Runner.World.Time > simTime, "Resume carries on with the night");
            t.Log($"notes {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }

        static string FirstLine(string body, string word)
        {
            foreach (var s in body.Split('.')) if (s.Contains(word)) return s.Trim();
            return body;
        }
    }
}
