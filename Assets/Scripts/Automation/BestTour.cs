using System.Collections;
using LastLight.Core;
using LastLight.Sim;
using LastLight.UI;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// The best tour (-llScript best, with -llFresh): the score to beat under the HUD's score. The
    /// tour stages the bests itself in the throwaway save: night III with a best of 880, played by
    /// the AutoKeeper, must show "BEST 880" and turn to "PAST YOUR BEST" exactly when the score
    /// passes 880; night IV, never kept, shows nothing; a Night Watch with a best of 600 turns to
    /// "NEW BEST". Every frame the line is compared with the simulation, and its place is checked
    /// against the lamps at stake and the horn.
    /// </summary>
    public static class BestTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["best"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        static string Fmt(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";

        static IEnumerator Begin(int night)
        {
            var g = Game.Instance;
            g.AutoPlay = true;
            if (night == NightWatch.Number) g.TourWatch(); else g.TourBriefing(night);
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            g.Runner.AutoPlay = true;
            yield return null;
            yield return null;
        }

        static void CheckPlace(Tour t, string what)
        {
            var r = Game.Instance.Hud.TourBestRects;
            bool clear = (!r.lampsOn || !LabelPlacer.Overlaps(r.best, r.lamps)) && (!r.hornOn || !LabelPlacer.Overlaps(r.best, r.horn));
            Check(t, clear, $"{what}: the line {Fmt(r.best)} clears the lamps {(r.lampsOn ? Fmt(r.lamps) : "(none)")} and the horn {(r.hornOn ? Fmt(r.horn) : "(none)")}");
        }

        /// <summary>Plays on, checking every frame that the line says what the score says. Returns
        /// when the line has turned (plus a moment), or at the timeout.</summary>
        static IEnumerator Follow(Tour t, string what, int best, string passedWords, float timeout, float timeScale, string shot)
        {
            var g = Game.Instance;
            g.Runner.TimeScale = timeScale;
            float waited = 0f;
            int frames = 0, behind = 0, worst = 0;
            float turnedAt = -1f;
            int scoreAtTurn = 0, prevScore = g.Runner.World.Score;
            while (waited < timeout && !g.ShowingResults)
            {
                waited += Time.unscaledDeltaTime;
                var w = g.Runner.World;
                var line = g.Hud.BestLine;
                // The HUD reads the sim before the sim's step in a frame, so it may be one frame behind.
                bool want = w.Score > best, wantPrev = prevScore > best;
                bool ok = line.passed == want || line.passed == wantPrev;
                string text = line.passed ? passedWords : $"BEST{best:N0}";
                ok &= line.text == text;
                if (!ok) { behind++; worst = Mathf.Max(worst, behind); } else behind = 0;
                if (line.passed && turnedAt < 0f) { turnedAt = w.Time; scoreAtTurn = w.Score; t.Log($"{what}: turned at sim {w.Time:0.0} s, score {prevScore} -> {w.Score} (best {best})"); }
                prevScore = w.Score;
                frames++;
                if (turnedAt >= 0f && waited > 0f && w.Time > turnedAt + 0.4f) break;
                yield return null;
            }
            Check(t, frames > 0 && worst == 0, $"{what}: over {frames} frames the line matched the score against {best:N0} (worst run of mismatched frames {worst})");
            Check(t, turnedAt >= 0f, $"{what}: it turned to \"{passedWords}\" when the score passed {best:N0} ({(turnedAt >= 0 ? $"score {scoreAtTurn}" : "never passed")})");
            if (turnedAt >= 0f)
            {
                g.Runner.TimeScale = 0f;
                yield return Tour.Wait(0.6f);
                CheckPlace(t, what + ", passed");
                yield return t.Shot(shot);
            }
            g.Runner.TimeScale = 1f;
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            var g = Game.Instance;
            var save = SaveData.Current;
            save.hints = false;
            save.shake = false;
            if (!Game.HasArg("-llFresh")) { Check(t, false, "runs only with -llFresh (it stages bests in the save)"); yield break; }
            yield return Tour.Wait(3f);

            // ---- Night III, kept before with 880.
            save.unlocked = 5;
            save.best[2] = 880;
            save.lamps[2] = 2;
            yield return Begin(3);
            var line = g.Hud.BestLine;
            Check(t, line.text == "BEST880" && line.best == 880 && !line.passed, $"night III opens with \"{line.text}\" under the score");
            yield return Tour.Wait(0.5f);
            CheckPlace(t, "night III");
            yield return t.Shot("best_night3_to_beat");
            yield return Follow(t, "night III", 880, "PASTYOURBEST", 240f, 3f, "best_night3_passed");

            // ---- Night IV, never kept: nothing to beat.
            yield return Begin(4);
            line = g.Hud.BestLine;
            Check(t, line.text == "" && line.best == 0, $"night IV, never kept, shows nothing (\"{line.text}\")");
            var r = g.Hud.TourBestRects;
            Check(t, r.lampsOn && Mathf.Abs(r.lamps.yMax - g.Hud.CanvasSize.y + 120f) < 3f, $"and the lamps stay where they were ({Fmt(r.lamps)})");

            // ---- A Night Watch with a best of 600.
            save.endingSeen = true;
            save.watchBest = 600;
            yield return Begin(NightWatch.Number);
            line = g.Hud.BestLine;
            Check(t, line.text == "BEST600" && line.best == 600, $"the watch opens with \"{line.text}\"");
            yield return Follow(t, "the watch", 600, "NEWBEST", 240f, 4f, "best_watch_new");
            t.Log($"best {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
