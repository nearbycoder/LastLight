using System.Collections;
using System.Collections.Generic;
using LastLight.Core;
using LastLight.Sim;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// The help tour (-llScript help, with -llFresh): night II fails (the keeper neglects two
    /// ships) and is tried again from the dawn card. The first failure says nothing more; the
    /// second points to Settings ▸ Game speed; at 70% speed on Standard nothing is offered; on Hard
    /// it points to Difficulty; a kept night clears the count, and a different night starts afresh.
    /// </summary>
    public static class HelpTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["help"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        /// <summary>Plays a night to dawn with the AutoKeeper, neglecting the named ships.</summary>
        static IEnumerator ToDawn(Tour t, int night, bool fromCard, params string[] neglect)
        {
            var g = Game.Instance;
            var names = new HashSet<string>(neglect);
            if (fromCard)
            {
                g.TourResults.OnRetry();
                yield return Tour.Wait(1.2f);
            }
            else
            {
                g.TourHideAll();
                g.TourBriefing(night);
                yield return Tour.Wait(3.5f);
                g.TourBegin();
            }
            g.Runner.AutoPlay = true;
            g.Runner.Bot.Ignore = s => names.Contains(s.Name);
            g.Runner.TimeScale = 6f;
            float waited = 0f;
            while (!g.ShowingResults && waited < 240f) { waited += Time.unscaledDeltaTime; yield return null; }
            yield return Tour.Wait(2.5f);
            t.Log($"night {night} {g.Runner.World.Outcome} at sim {g.Runner.World.Time:0} s");
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            var g = Game.Instance;
            var save = SaveData.Current;
            save.hints = false;
            if (!Game.HasArg("-llFresh")) { Check(t, false, "runs only with -llFresh (it changes settings)"); yield break; }
            g.AutoPlay = true;
            yield return Tour.Wait(3f);
            var results = g.TourResults;

            yield return ToDawn(t, 2, false, "Little Auk", "Shearwater");
            Check(t, g.Runner.World.Outcome == MissionOutcome.Failed && results.HelpNoteShown == "", $"the first failure says nothing more (\"{results.HelpNoteShown}\")");
            yield return ToDawn(t, 2, true, "Little Auk", "Shearwater");
            string note = results.HelpNoteShown;
            Check(t, g.Runner.World.Outcome == MissionOutcome.Failed && note.Contains("Game speed") && note.Contains("85% or 70%"), $"the second points to the game speed: \"{note}\"");
            yield return t.Shot("help_game_speed");

            save.gameSpeed = 0.7f;
            yield return ToDawn(t, 2, true, "Little Auk", "Shearwater");
            Check(t, results.HelpNoteShown == "", $"at 70% on Standard there's nothing more to offer (\"{results.HelpNoteShown}\")");

            save.difficulty = 1;
            yield return ToDawn(t, 2, true, "Little Auk", "Shearwater");
            note = results.HelpNoteShown;
            Check(t, note.Contains("Difficulty"), $"on Hard it points to the difficulty: \"{note}\"");
            yield return t.Shot("help_difficulty");

            // A kept night clears the count.
            save.difficulty = 0;
            save.gameSpeed = 1f;
            yield return ToDawn(t, 2, true);
            Check(t, g.Runner.World.Outcome == MissionOutcome.Won && results.HelpNoteShown == "", $"a kept night says nothing (\"{results.HelpNoteShown}\")");
            yield return ToDawn(t, 2, true, "Little Auk", "Shearwater");
            Check(t, results.HelpNoteShown == "", $"and the next failure is a first again (\"{results.HelpNoteShown}\")");
            // A different night starts its own count.
            yield return ToDawn(t, 4, false, "Razorbill", "Kittiwake", "Little Auk");
            Check(t, g.Runner.World.Outcome != MissionOutcome.Failed || results.HelpNoteShown == "", $"night IV after night II's failure starts afresh (\"{results.HelpNoteShown}\", {g.Runner.World.Outcome})");
            t.Log($"help {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
