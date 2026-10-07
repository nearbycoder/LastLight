using System.Collections;
using LastLight.Core;
using LastLight.Sim;
using LastLight.UI;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// The lamps tour (-llScript lamps, with -llFresh -llSampleSave): the lamps at stake under the
    /// score, and the briefing's record of a night already kept. Night II with the Little Auk
    /// neglected (a wreck), then night III left unkept until a ship loses its way. Every frame the
    /// HUD's lamps are compared with the simulation; at dawn they must match the card's lamps.
    /// </summary>
    public static class LampsTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["lamps"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        /// <summary>The lamps a night can still earn, worked out here from the ships themselves.</summary>
        static int Expected(SimWorld w)
        {
            if (w.Outcome == MissionOutcome.Failed) return 0;
            int wrecks = 0;
            bool wavered = false;
            foreach (var s in w.Ships)
            {
                if (s.State == ShipState.Wrecked) wrecks++;
                if (s.EverLost || s.EverLured) wavered = true;
            }
            return wrecks > 0 ? 1 : wavered ? 2 : 3;
        }

        /// <summary>Plays the night to dawn, checking the HUD every frame: it may trail the
        /// simulation by a frame (they update in turn), never more.</summary>
        static IEnumerator Watch(Tour t, Game g, string night, System.Func<SimWorld, bool> handOver = null)
        {
            var w = g.Runner.World;
            int behind = 0, worst = 0, last = 3;
            float waited = 0f;
            bool shotNote = false;
            while (!g.ShowingResults && waited < 300f)
            {
                waited += Time.unscaledDeltaTime;
                int want = Expected(w);
                var (shown, lit, note) = g.Hud.LampsShown;
                behind = lit == want ? 0 : behind + 1;
                worst = Mathf.Max(worst, behind);
                if (want < last && w.Outcome == MissionOutcome.Running)
                {
                    t.Log($"{night}: the sim's lamps fall to {want} at {w.Time:0.0} s");
                    last = want;
                }
                if (note != "" && !shotNote && w.Outcome == MissionOutcome.Running)
                {
                    shotNote = true;
                    t.Log($"{night}: the note reads \"{note}\" with {lit} lamps lit");
                    g.Runner.TimeScale = 0f;
                    yield return t.Shot($"lamps_{night.Replace(" ", "_")}_note");
                    g.Runner.TimeScale = 5f;
                }
                if (handOver != null && handOver(w)) { g.Runner.AutoPlay = true; handOver = null; }
                if (!shown) { Check(t, false, $"{night}: the lamps are showing during the night"); break; }
                yield return null;
            }
            Check(t, worst <= 1, $"{night}: the HUD's lamps followed the sim all night (at most {worst} frame{(worst == 1 ? "" : "s")} behind)");
            yield return Tour.Wait(2f);
            var (_, litAtDawn, _) = g.Hud.LampsShown;
            Check(t, g.ShowingResults && litAtDawn == w.Lamps, $"{night}: at dawn the HUD showed {litAtDawn} lamps and the card gives {w.Lamps} ({w.Outcome}, {w.Wrecks} wrecks)");
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            var g = Game.Instance;
            var save = SaveData.Current;
            yield return Tour.Wait(4f);

            // ---- The briefing's record line (the sample save: nights I to VII kept, VIII open).
            var briefing = (BriefingScreen)g.TourScreen("briefing");
            foreach (int n in new[] { 1, 3, 5, 8 })
            {
                g.TourHideAll();
                g.TourBriefing(n);
                yield return Tour.Wait(n == 3 ? 3.5f : 1.2f);
                string want = BriefingScreen.RecordText(save.lamps[n - 1], save.best[n - 1]);
                bool ok = briefing.RecordLine == want && (save.lamps[n - 1] > 0) == (want != "");
                if (save.lamps[n - 1] == 3) ok &= !want.Contains("Still to earn");
                if (save.lamps[n - 1] == 2) ok &= want.Contains("a steady hand") && !want.Contains("no ship lost");
                if (save.lamps[n - 1] == 1) ok &= want.Contains("no ship lost");
                Check(t, ok, $"night {n} ({save.lamps[n - 1]} lamps kept, best {save.best[n - 1]}): the briefing says \"{briefing.RecordLine}\"");
                if (n == 3) yield return t.Shot("lamps_briefing_night3");
            }

            // ---- Night II, the Little Auk neglected: a wreck takes two lamps at once.
            g.TourHideAll();
            g.AutoPlay = true;
            g.TourBriefing(2);
            g.Runner.Bot.Ignore = s => s.Name == "Little Auk";
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            yield return Tour.Wait(1f);
            var (shown0, lit0, _) = g.Hud.LampsShown;
            Check(t, shown0 && lit0 == 3, $"night II: three lamps at stake at dusk (shown {shown0}, lit {lit0})");
            yield return t.Shot("lamps_dusk");
            g.Runner.TimeScale = 5f;
            yield return Watch(t, g, "night II");

            // ---- Night III, nobody at the light until a ship loses its way; then the AutoKeeper.
            g.TourHideAll();
            g.AutoPlay = false;
            g.TourBriefing(int.Parse(Game.ArgString("-llLampsNight", "3")));
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            g.Runner.Def.allowedWrecks = 99;     // staging: the night runs on to dawn whatever happens
            g.Runner.TimeScale = 5f;
            yield return Watch(t, g, "night III", w =>
            {
                foreach (var s in w.Ships) if (s.EverLost || s.EverLured) return true;
                return false;
            });

            // ---- The Night Watch has its own strip: no lamps at stake.
            g.TourHideAll();
            g.AutoPlay = true;
            g.TourWatch();
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            yield return Tour.Wait(2f);
            var (shownWatch, _, _) = g.Hud.LampsShown;
            Check(t, !shownWatch, $"the Night Watch shows no lamps at stake (shown {shownWatch})");
            t.Log($"lamps {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
