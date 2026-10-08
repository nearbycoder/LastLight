using System.Collections;
using LastLight.Core;
using LastLight.UI;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// The perf tour (-llScript perf, with -llFresh -llPerfNote -llFps 30): the dawn card's word
    /// after a slow night. Night I is played four times by the AutoKeeper. Held to 30 fps while
    /// Frame rate is on Display, dawn names Render scale 70% and Fog and haze quality Medium; the
    /// same again says nothing more this session; with Frame rate 30 chosen (and the two lowered)
    /// 30 fps is what was asked for, so nothing is said; and at the display's own rate nothing is
    /// said either, if the machine keeps up (the load average is logged with the frame rate).
    /// </summary>
    public static class PerfTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["perf"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        static string Load()
        {
            try { return System.IO.File.ReadAllText("/proc/loadavg").Split(' ')[0]; }
            catch { return "?"; }
        }

        /// <summary>Night I with the AutoKeeper, quickly in the night's time (the frame rate is the
        /// screen's, whatever the night's pace), to dawn; returns the dawn card's note.</summary>
        static IEnumerator Night(Tour t, string what, System.Action<string> note)
        {
            var g = Game.Instance;
            g.TourHideAll();
            g.AutoPlay = true;
            g.TourBriefing(1);
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            g.Runner.AutoPlay = true;
            g.Runner.TimeScale = 4f;
            float waited = 0f;
            while (!g.ShowingResults && waited < 240f) { waited += Time.unscaledDeltaTime; yield return null; }
            yield return Tour.Wait(1.5f);
            string shown = g.TourResults.PerfNoteShown;
            t.Log($"{what}: the night ran at {g.LastNightFps:0.0} fps (aiming for {SaveData.Current.FrameRate}, held to {Application.targetFrameRate}; load {Load()}); note \"{shown}\"");
            Check(t, g.ShowingResults, $"{what}: dawn came ({waited:0} s)");
            note(shown);
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            var g = Game.Instance;
            var save = SaveData.Current;
            yield return Tour.Wait(4f);
            Check(t, save.frameCap == 0 && save.renderScale > 0.99f && save.quality == 2 && Application.targetFrameRate == 30,
                $"a fresh save: Frame rate Display, render scale {save.renderScale * 100:0}%, fog quality {save.quality}, held to {Application.targetFrameRate} fps by -llFps");

            string note = null;
            yield return Night(t, "held to 30 fps", n => note = n);
            var said = System.Text.RegularExpressions.Regex.Match(note, @"about (\d+) frames a second");
            int about = said.Success ? int.Parse(said.Groups[1].Value) : 0;
            Check(t, about >= 25 && about <= 31 && Mathf.Abs(about - g.LastNightFps) < 1f && note.Contains("Settings ▸ Render scale 70% or Fog and haze quality Medium"),
                $"a night held to 30 fps on Display says so and names the next steps down: \"{note}\"");
            yield return t.Shot("perf_dawn_note");

            yield return Night(t, "again, the same settings", n => note = n);
            Check(t, note == "", $"the same again says nothing more this session (\"{note}\")");

            // The keeper takes the advice and caps the frame rate at 30: that's what was asked for.
            save.renderScale = 0.7f;
            save.quality = 1;
            save.frameCap = 30;
            save.Apply(display: false);
            yield return Night(t, "Frame rate 30 chosen", n => note = n);
            Check(t, note == "" && Mathf.Abs(g.LastNightFps - 30f) < 3f, $"with Frame rate 30 chosen, 30 fps says nothing (\"{note}\", {g.LastNightFps:0.0} fps)");
            yield return t.Shot("perf_dawn_capped");

            // At the display's own rate (the cheapest settings, to give a busy machine its best chance).
            save.renderScale = 0.5f;
            save.quality = 0;
            save.frameCap = 0;
            save.Apply(display: false);
            Application.targetFrameRate = save.FrameRate;
            g.TourForgetPerfAdvice();
            yield return Night(t, "at the display's rate", n => note = n);
            float fps = g.LastNightFps;
            if (fps >= 45f) Check(t, note == "", $"at {fps:0.0} fps (aiming for {save.FrameRate}) nothing is said (\"{note}\")");
            else Check(t, false, $"the machine didn't keep up ({fps:0.0} fps at load {Load()}), so the quiet case couldn't be checked; note \"{note}\"");

            // Dawn waits for the radio, but not for ever: a long call still going when the night
            // ends (as the night's own last call often is) mustn't hold dawn back or repeat that call.
            g.TourHideAll();
            g.AutoPlay = true;
            g.TourBriefing(1);
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            g.Runner.AutoPlay = true;
            g.Runner.TimeScale = 4f;
            float waited = 0f;
            while (g.Runner.World.Outcome == Sim.MissionOutcome.Running && waited < 240f) { waited += Time.unscaledDeltaTime; yield return null; }
            g.Radio.Say("pryce", "Keeper, a long word before you sleep: the glass is falling, the wind's backing round to the south-west, and I'd not trust the Long Sands tonight for all the coal in Cardiff.", 4);
            float ended = 0f;
            while (!g.ShowingResults && ended < 30f) { ended += Time.unscaledDeltaTime; yield return null; }
            int lastCalls = 0;
            foreach (var e in g.Radio.Log.Entries) if (e.Text.StartsWith("That's the lot")) lastCalls++;
            Check(t, g.ShowingResults && ended < 14f && lastCalls <= 1, $"with a long call on the radio as the night ended, dawn came after {ended:0.0} s (at most 3.5 s + 6 s past it, plus the call's start) and the night's last call was said {lastCalls} time(s)");
            t.Log($"perf {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
