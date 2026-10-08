using System.Collections;
using LastLight.Core;
using LastLight.Sim;
using LastLight.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LastLight.Automation
{
    /// <summary>
    /// The chart tour (-llScript chart, with -llFresh): the night's chart from the dawn card. Night
    /// II with the Little Auk neglected (a lost ship and a wreck), opened with a click, the arrows
    /// and Enter, and the simulated pad; then night IX left unkept for a spell so ships are lost and
    /// lured. Checks every wreck has its cross where the wreck lies on the chart, that the "?" and
    /// lantern marks match the night's log, and that Esc, B and "Back to dawn" return to the card.
    /// The replay: played and paused with a click, stepped with the arrows and the pad, jumped with a
    /// click on the timeline, and at several moments every ship's mark is where the log puts it.
    /// </summary>
    public static class ChartTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["chart"] = Run;

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

        /// <summary>The night's time when it ended (the bay sails on behind the dawn card).</summary>
        static float endedAt;

        static IEnumerator ToDawn(Game g)
        {
            float waited = 0f;
            endedAt = -1f;
            while (!g.ShowingResults && waited < 300f)
            {
                if (endedAt < 0f && g.Runner.World.Outcome != MissionOutcome.Running) endedAt = g.Runner.World.Time;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return Tour.Wait(4f);
        }

        /// <summary>The marks against the simulation: a cross per wreck where the wreck projects onto
        /// the chart's frame on screen, and a "?" or lantern per time a ship lost its way or was lured.</summary>
        static void CheckChart(Tour t, Game g, string night)
        {
            var chart = g.TourChart;
            var w = g.Runner.World;
            var log = g.Runner.Log;
            var area = chart.Area;
            var corners = new Vector3[4];
            area.GetWorldCorners(corners);     // an overlay canvas: world space is screen pixels
            var lo = (Vector2)corners[0];
            var hi = (Vector2)corners[2];
            var view = ChartScreen.View;
            // The bay is fitted into the frame and centred: screen pixels per world unit.
            float k = Mathf.Min((hi.x - lo.x) / view.width, (hi.y - lo.y) / view.height);
            var mid = (lo + hi) * 0.5f;
            int wrecked = 0;
            foreach (var s in w.Ships)
            {
                if (s.State != ShipState.Wrecked) continue;
                wrecked++;
                RectTransform mark = null;
                foreach (var (name, rt) in chart.WreckMarks) if (name == s.Name) mark = rt;
                if (mark == null) { Check(t, false, $"{night}: the {s.Name}'s wreck has a cross on the chart"); continue; }
                var want = mid + (s.Pos - view.center) * k;
                var got = (Vector2)mark.position;
                bool inside = want.x >= lo.x && want.x <= hi.x && want.y >= lo.y && want.y <= hi.y;
                Check(t, Vector2.Distance(want, got) < 3f && inside,
                    $"{night}: the {s.Name}'s cross sits on its wreck at ({s.Pos.x:0},{s.Pos.y:0}): screen ({got.x:0},{got.y:0}), expected ({want.x:0},{want.y:0}), inside the frame {inside}");
                NightLog.Track track = null;
                foreach (var tr in log.Tracks) if (tr.Ship == s) track = tr;
                var end = track != null && track.Points.Count > 0 ? track.Points[track.Points.Count - 1] : default;
                Check(t, track != null && end.Pos == s.Pos && end.State == ShipState.Wrecked, $"{night}: the {s.Name}'s track ends at its wreck ({end.Pos.x:0.0},{end.Pos.y:0.0}, {end.State})");
            }
            Check(t, chart.WreckMarks.Count == wrecked, $"{night}: {chart.WreckMarks.Count} crosses for {wrecked} wrecks");
            int lost = 0, lured = 0, afterLure = 0, lostCount = 0, luredCount = 0;
            foreach (var tr in log.Tracks)
                for (int i = 1; i < tr.Points.Count; i++)
                {
                    if (tr.Points[i].State == tr.Points[i - 1].State) continue;
                    if (tr.Points[i].State == ShipState.Lost) { lost++; if (tr.Points[i - 1].State == ShipState.Lured) afterLure++; }
                    if (tr.Points[i].State == ShipState.Lured) lured++;
                }
            foreach (var s in w.Ships) { lostCount += s.LostCount; luredCount += s.LuredCount; }
            // A doused lure can leave its ship lost: a "?" on the chart, not a count in the debrief.
            Check(t, chart.LostMarks == lost && chart.LuredMarks == lured && lost - afterLure == lostCount && lured == luredCount,
                $"{night}: {chart.LostMarks} \"?\" and {chart.LuredMarks} lantern marks; the log has {lost} turns to lost ({afterLure} after a lure) and {lured} to lured, the ships counted {lostCount} and {luredCount}");
            // Names don't sit on each other or on a wreck's cross.
            Rect ScreenRect(RectTransform rt)
            {
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
            }
            var names = chart.PlaceMarks;
            int clashes = 0;
            var clashList = new System.Collections.Generic.List<string>();
            for (int i = 0; i < names.Count; i++)
            {
                var a = ScreenRect(names[i].mark);
                for (int j = i + 1; j < names.Count; j++)
                    if (LabelPlacer.Overlaps(a, ScreenRect(names[j].mark))) { clashes++; clashList.Add($"{names[i].place} and {names[j].place}"); }
                foreach (var (ship, cross) in chart.WreckMarks)
                    if (LabelPlacer.Overlaps(a, ScreenRect(cross))) { clashes++; clashList.Add($"{names[i].place} and the {ship}'s cross"); }
            }
            Check(t, clashes == 0, $"{night}: none of the chart's {names.Count} names covers another or a cross{(clashes > 0 ? ": " + string.Join("; ", clashList) : "")}");
            t.Log($"{night}: {log.Tracks.Count} tracks, {log.PointCount} points, {log.ChartedReefs.Count} reefs and {log.ChartedShoals.Count} sandbanks charted, lanterns {string.Join(",", log.BurnedSites)}");
        }

        static Vector2 ScreenOf(RectTransform area, Vector2 local) => area.TransformPoint(area.rect.center + local);

        /// <summary>Hold the replay at moments of the night and check what it shows against the log:
        /// each ship's mark where its track puts it (on screen, within 3 px of the bay's own
        /// projection), its "?" or lantern with its state, the light's sweep, and each wreck's cross
        /// only from the moment it struck.</summary>
        static IEnumerator CheckReplay(Tour t, Game g, string night, string shotPrefix)
        {
            var chart = g.TourChart;
            var log = g.Runner.Log;
            var w = g.Runner.World;
            var area = chart.Area;
            var corners = new Vector3[4];
            area.GetWorldCorners(corners);
            var lo = (Vector2)corners[0];
            var hi = (Vector2)corners[2];
            var view = ChartScreen.View;
            float k = Mathf.Min((hi.x - lo.x) / view.width, (hi.y - lo.y) / view.height);
            var mid = (lo + hi) * 0.5f;
            float dur = log.Duration;
            // Polled once a frame at up to 6× speed, so the end is known to about a frame's worth of night.
            Check(t, dur > 10f && Mathf.Abs(dur - endedAt) < 1f, $"{night}: the replay runs the whole night and stops where it ended ({dur:0.0} s; the night ended at {endedAt:0.0} s)");
            int worstMisses = 0, marksChecked = 0;
            float worstPx = 0f;
            float[] moments = { 0.08f, 0.3f, 0.55f, 0.8f, 1f };
            for (int m = 0; m < moments.Length; m++)
            {
                float time = moments[m] * dur;
                chart.SetReplayTime(time);
                yield return null;
                int shown = 0, misses = 0;
                foreach (var tr in log.Tracks)
                {
                    bool afloat = NightLog.ShipAt(tr, time, out var pos, out var state) && state != ShipState.Wrecked;
                    var mark = chart.ReplayMark(tr.Ship.Name);
                    if (mark.shown != afloat) { misses++; t.Log($"  {tr.Ship.Name} at {time:0.0}s: shown {mark.shown}, afloat {afloat}"); continue; }
                    if (!afloat) continue;
                    shown++;
                    var want = mid + (pos - view.center) * k;
                    var got = ScreenOf(area, mark.pos);
                    float px = Vector2.Distance(want, got);
                    worstPx = Mathf.Max(worstPx, px);
                    if (px > 3f || mark.lost != (state == ShipState.Lost) || mark.lured != (state == ShipState.Lured)) { misses++; t.Log($"  {tr.Ship.Name} at {time:0.0}s: {px:0.0} px off, {state}, ? {mark.lost}, lantern {mark.lured}"); }
                    marksChecked++;
                }
                var beam = log.BeamAt(time);
                bool beamOk = Mathf.Abs(Geo.DeltaAngle(beam.Bearing, chart.BeamShown.Bearing)) < 1e-3f;
                int crossMisses = 0;
                foreach (var tr in log.Tracks)
                {
                    if (tr.Ship.State != ShipState.Wrecked) continue;
                    bool struck = time >= tr.Points[tr.Points.Count - 1].Time;
                    if (chart.CrossShown(tr.Ship.Name) != struck) crossMisses++;
                }
                worstMisses += misses + crossMisses + (beamOk ? 0 : 1);
                t.Log($"{night} replay at {chart.TimeShown}: {shown} ships afloat, {misses} marks wrong, light at {beam.Bearing * Mathf.Rad2Deg:0}° focus {beam.Focus:0.00} {(beamOk ? "drawn" : "NOT drawn")}, {crossMisses} crosses wrong");
                if (m == 1 || m == 2) yield return t.Shot($"{shotPrefix}_{m}");
            }
            Check(t, worstMisses == 0 && marksChecked > 0, $"{night}: at five moments every ship's mark, \"?\", lantern, cross and the light match the log ({marksChecked} marks, worst {worstPx:0.0} px)");
        }

        static IEnumerator Click(Vector2 at)
        {
            TourScripts.MouseTo(at);
            yield return null;
            yield return null;
            TourScripts.MouseTo(at, left: true);
            yield return null;
            yield return null;
            TourScripts.MouseTo(at);
            yield return null;
        }

        static IEnumerator Run(Tour t)
        {
            Hud.NamesMakeRoom = !Game.HasArg("-llNamesOverlap");   // names as round 7 drew them, for a before-and-after
            failures = 0;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var g = Game.Instance;
            SaveData.Current.shake = false;
            yield return Tour.Wait(4f);

            // ---- Night II, the Little Auk neglected.
            g.AutoPlay = true;
            g.TourBriefing(2);
            g.Runner.Bot.Ignore = s => s.Name == "Little Auk";
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            g.Runner.TimeScale = 6f;
            yield return ToDawn(g);
            var results = g.TourResults;
            Check(t, results.ButtonLabels.Contains("Chart"), $"the dawn card offers the chart ({results.ButtonLabels})");
            yield return t.Shot("chart_00_dawn_card");

            // A click on Chart.
            var chartButton = FindButton(results, "Chart");
            var at = RectTransformUtility.WorldToScreenPoint(null, chartButton.position);
            TourScripts.MouseTo(at);
            yield return null;
            yield return null;
            TourScripts.MouseTo(at, left: true);
            yield return null;
            yield return null;
            TourScripts.MouseTo(at);
            yield return Tour.Wait(1.2f);
            Check(t, g.TourChart.Visible && !results.Visible, $"a click on Chart opens the chart (chart {g.TourChart.Visible}, card {results.Visible})");
            CheckChart(t, g, "night II");
            yield return t.Shot("chart_01_night2");

            // The replay, with the mouse: Replay plays, the night moves on, a second click pauses.
            var chart = g.TourChart;
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, chart.ReplayButton.transform.position));
            float t0 = chart.ReplayTime;
            yield return Tour.Wait(1.5f);
            float ran = chart.ReplayTime - t0;
            bool played = chart.ReplayPlaying && chart.Replaying;
            yield return t.Shot("chart_04_replay_playing");
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, chart.ReplayButton.transform.position));
            float held = chart.ReplayTime;
            yield return Tour.Wait(0.6f);
            Check(t, played && ran > 1.5f * chart.ReplayRate * 0.5f && !chart.ReplayPlaying && Mathf.Abs(chart.ReplayTime - held) < 1e-3f,
                $"a click on Replay plays the night ({ran:0.0} s of night in 1.5 s at {chart.ReplayRate:0}×), a second click pauses it at {UiKit.Clock(held)} (button \"{chart.ReplayButton.Label.text}\")");
            // A click halfway along the timeline jumps there.
            var bar = (RectTransform)chart.Timeline.transform;
            var barCorners = new Vector3[4];
            bar.GetWorldCorners(barCorners);
            yield return Click(new Vector2((barCorners[0].x + barCorners[2].x) * 0.5f, (barCorners[0].y + barCorners[2].y) * 0.5f));
            float half = chart.ReplayTime / chart.ReplayDuration;
            Check(t, Mathf.Abs(half - 0.5f) < 0.03f && !chart.ReplayPlaying, $"a click halfway along the timeline jumps to the middle of the night ({half:0.000}, {chart.TimeShown})");
            // The arrows step it.
            float before = chart.ReplayTime;
            yield return Press(Key.RightArrow);
            yield return Press(Key.RightArrow);
            float stepped = chart.ReplayTime - before;
            yield return Press(Key.LeftArrow);
            float back1 = chart.ReplayTime - before;
            Check(t, Selected().Contains("Slider") && Mathf.Abs(stepped - 10f) < 0.6f && Mathf.Abs(back1 - 5f) < 0.6f, $"with the timeline chosen ({Selected()}), → twice steps {stepped:0.0} s and ← one back to +{back1:0.0} s");
            yield return CheckMoments(t, g, "night II");
            yield return CheckReplay(t, g, "night II", "chart_05_replay_night2");
            yield return Press(Key.Escape);
            yield return Tour.Wait(0.8f);
            Check(t, !g.TourChart.Visible && results.Visible && g.ShowingResults, $"Esc goes back to the dawn card (chart {g.TourChart.Visible}, card {results.Visible})");

            // The arrows and Enter: down picks the first button, right walks to Chart.
            yield return Press(Key.DownArrow);
            string path = Selected();
            for (int i = 0; i < 4 && !Selected().Contains("Chart"); i++) { yield return Press(Key.RightArrow); path += " > " + Selected(); }
            yield return Press(Key.Enter);
            yield return Tour.Wait(1f);
            Check(t, g.TourChart.Visible, $"the arrows reach Chart ({path}) and Enter opens it");
            g.TourChart.Back();
            yield return Tour.Wait(0.8f);
            Check(t, !g.TourChart.Visible && results.Visible, "\"Back to dawn\" returns to the card");

            // The simulated pad: A on Chart, B back, and Chart is still chosen.
            var pad = InputSystem.AddDevice<Gamepad>("ChartPad");
            yield return null;
            yield return TourScripts.PadPress(pad, GamepadButton.DpadRight);
            for (int i = 0; i < 4 && !Selected().Contains("Chart"); i++) yield return TourScripts.PadPress(pad, GamepadButton.DpadRight);
            string onPad = Selected();
            yield return TourScripts.PadPress(pad, GamepadButton.South);
            yield return Tour.Wait(1f);
            bool padOpened = g.TourChart.Visible;
            yield return t.Shot("chart_02_pad");
            // On the pad: down from Back to Replay, A plays, A pauses, down to the timeline, right steps.
            string padPath = Selected();
            yield return TourScripts.PadPress(pad, GamepadButton.DpadDown);
            padPath += " > " + Selected();
            yield return TourScripts.PadPress(pad, GamepadButton.South);
            yield return Tour.Wait(0.8f);
            bool padPlaying = g.TourChart.ReplayPlaying;
            yield return TourScripts.PadPress(pad, GamepadButton.South);
            float padHeld = g.TourChart.ReplayTime;
            yield return TourScripts.PadPress(pad, GamepadButton.DpadDown);
            padPath += " > " + Selected();
            yield return TourScripts.PadPress(pad, GamepadButton.DpadRight);
            float padStep = g.TourChart.ReplayTime - padHeld;
            Check(t, padPlaying && !g.TourChart.ReplayPlaying && Mathf.Abs(padStep - 5f) < 0.6f,
                $"the pad ({padPath}): A plays the replay ({padPlaying}) and pauses it, and right on the timeline steps {padStep:0.0} s");
            // The moments and play on the pad, wherever the selection is.
            {
                var c = g.TourChart;
                Check(t, c.ControlsShown.Contains("X plays") && c.ControlsShown.Contains("LB and RB"), $"with the pad in hand the chart names its buttons: \"{c.ControlsShown.Replace("\n", " / ")}\"");
                c.SetReplayTime(0f);
                yield return TourScripts.PadPress(pad, GamepadButton.RightShoulder);
                float first = c.ReplayTime;
                yield return TourScripts.PadPress(pad, GamepadButton.RightShoulder);
                float second = c.ReplayTime;
                yield return TourScripts.PadPress(pad, GamepadButton.LeftShoulder);
                float back = c.ReplayTime;
                var targets = Targets(c);
                // With one moment, a second RB has nowhere to go and LB goes back to the start.
                float wantSecond = targets.Count > 1 ? targets[1] : targets.Count > 0 ? targets[0] : 0f;
                float wantBack = targets.Count > 1 ? targets[0] : 0f;
                bool padJumps = targets.Count >= 1 && Mathf.Abs(first - targets[0]) < 0.05f && Mathf.Abs(second - wantSecond) < 0.05f && Mathf.Abs(back - wantBack) < 0.05f;
                Check(t, padJumps, $"RB jumps to {UiKit.Clock(first)} and {UiKit.Clock(second)}, LB back to {UiKit.Clock(back)} (moments' marks at {string.Join(", ", targets.ConvertAll(x => UiKit.Clock(x)))})");
                yield return TourScripts.PadPress(pad, GamepadButton.West);
                yield return Tour.Wait(0.5f);
                bool westPlays = c.ReplayPlaying;
                yield return TourScripts.PadPress(pad, GamepadButton.West);
                Check(t, westPlays && !c.ReplayPlaying && c.Visible, $"X plays and pauses the replay with {Selected()} chosen ({westPlays})");
            }
            yield return TourScripts.PadPress(pad, GamepadButton.East);
            yield return Tour.Wait(0.8f);
            Check(t, padOpened && !g.TourChart.Visible && results.Visible && Selected().Contains("Chart"),
                $"the pad: A on {onPad} opens the chart ({padOpened}), B goes back to the card with {Selected()} chosen");
            InputSystem.RemoveDevice(pad);
            yield return null;

            // ---- Night IX: nobody keeps the light for a while, so ships are lost and lured.
            g.TourHideAll();
            g.AutoPlay = false;
            g.TourBriefing(int.Parse(Game.ArgString("-llChartNight", "9")));
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            g.Runner.Def.allowedWrecks = 99;      // staging: let the night run on to dawn
            g.Runner.TimeScale = 4f;
            float waited = 0f;
            bool luredSeen = false;
            while (waited < 150f && !g.ShowingResults)
            {
                waited += Time.unscaledDeltaTime;
                foreach (var s in g.Runner.World.Ships) luredSeen |= s.LuredCount > 0;
                if (luredSeen && g.Runner.World.Time > 100f) break;
                yield return null;
            }
            g.Runner.AutoPlay = true;
            g.Runner.TimeScale = 6f;
            yield return ToDawn(g);
            Check(t, luredSeen, $"night IX: a ship was lured while the light was left unkept");
            g.TourShowChart();
            yield return Tour.Wait(1.2f);
            CheckChart(t, g, "night IX");
            yield return t.Shot("chart_03_night9");
            yield return CheckMoments(t, g, "night IX");
            yield return CheckReplay(t, g, "night IX", "chart_06_replay_night9");
            yield return Press(Key.Escape);
            yield return Tour.Wait(0.6f);
            t.Log($"chart {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }

        /// <summary>Where each jump should land: a few seconds before each moment, once per distinct time.</summary>
        static System.Collections.Generic.List<float> Targets(ChartScreen c)
        {
            var list = new System.Collections.Generic.List<float>();
            foreach (var m in c.ReplayMoments)
            {
                float at = Mathf.Max(0f, m.Time - ChartScreen.MomentLead);
                if (list.Count == 0 || at > list[list.Count - 1] + 0.05f) list.Add(at);
            }
            return list;
        }

        /// <summary>Space plays and pauses with Back chosen (and doesn't go back); E and Q, Page Down
        /// and Page Up jump between the night's moments; the arrows step with Back chosen; the line
        /// under the clock names the moment ahead, and the controls line names the keys.</summary>
        static IEnumerator CheckMoments(Tour t, Game g, string night)
        {
            var c = g.TourChart;
            var targets = Targets(c);
            int wrecks = 0, lost = 0, lured = 0;
            foreach (var m in c.ReplayMoments)
            {
                if (m.Kind == NightLog.MomentKind.Wrecked) wrecks++;
                else if (m.Kind == NightLog.MomentKind.Lured) lured++;
                else lost++;
            }
            Check(t, c.ReplayMoments.Count > 0 && wrecks == g.Runner.World.Wrecks, $"{night}: {c.ReplayMoments.Count} moments on the timeline ({wrecks} wrecks, {lost} lost, {lured} lured; the night had {g.Runner.World.Wrecks} wrecks)");
            Check(t, c.ControlsShown.Contains("Space plays") && c.ControlsShown.Contains("Q and E"), $"the chart names the replay's keys: \"{c.ControlsShown.Replace("\n", " / ")}\"");
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(c.BackButton.gameObject);
            c.SetReplayTime(0f);
            yield return null;
            yield return Press(Key.Space);
            yield return Tour.Wait(0.6f);
            bool spacePlays = c.ReplayPlaying && c.Visible;
            yield return Press(Key.Space);
            Check(t, spacePlays && !c.ReplayPlaying && c.Visible && g.ShowingResults, $"Space plays and pauses the replay with {Selected()} chosen, and doesn't press it (chart still open: {c.Visible})");
            c.SetReplayTime(0f);
            var landed = new System.Collections.Generic.List<string>();
            bool allLanded = true;
            for (int i = 0; i < targets.Count && i < 4; i++)
            {
                yield return Press(i % 2 == 0 ? Key.E : Key.PageDown);
                bool ok = Mathf.Abs(c.ReplayTime - targets[i]) < 0.05f;
                // The line names the moment jumped to.
                foreach (var jm in c.ReplayMoments)
                    if (Mathf.Abs(Mathf.Max(0f, jm.Time - ChartScreen.MomentLead) - targets[i]) < 0.05f)
                    {
                        ok &= c.MomentShown.StartsWith("Next at " + UiKit.Clock(jm.Time)) && c.MomentShown.Contains(jm.Ship.Name);
                        break;
                    }
                allLanded &= ok;
                landed.Add($"{UiKit.Clock(c.ReplayTime)} \"{c.MomentShown}\"");
                if (i == 0)
                {
                    var m = c.ReplayMoments[0];
                    Check(t, c.MomentShown.Contains(m.Ship.Name) && c.MomentShown.StartsWith("Next at " + UiKit.Clock(m.Time)), $"the line under the clock names the moment ahead: \"{c.MomentShown}\"");
                    yield return t.Shot($"chart_07_moment_{night.Replace(" ", "")}");
                }
            }
            Check(t, allLanded, $"E and Page Down jump to {MomentLeadText} before each moment, and the line names it: {string.Join("; ", landed)}");
            float at = c.ReplayTime;
            yield return Press(Key.Q);
            float q = c.ReplayTime;
            yield return Press(Key.PageUp);
            float pgUp = c.ReplayTime;
            int k = Mathf.Min(targets.Count, 4) - 1;
            bool backOk = k >= 1 ? Mathf.Abs(q - targets[k - 1]) < 0.05f && Mathf.Abs(pgUp - (k >= 2 ? targets[k - 2] : 0f)) < 0.05f : Mathf.Abs(q) < 0.05f;
            Check(t, backOk, $"Q and Page Up jump back: {UiKit.Clock(at)} -> {UiKit.Clock(q)} -> {UiKit.Clock(pgUp)}");
            float before = c.ReplayTime;
            yield return Press(Key.RightArrow);
            float stepped = c.ReplayTime - before;
            Check(t, Mathf.Abs(stepped - 5f) < 0.6f && Selected() == c.BackButton.gameObject.name, $"with {Selected()} chosen, → steps {stepped:0.0} s");
            c.SetReplayTime(c.ReplayDuration);
            yield return null;
            Check(t, c.MomentShown == "Nothing more went wrong." || c.MomentShown.StartsWith(UiKit.Clock(c.ReplayMoments[c.ReplayMoments.Count - 1].Time)), $"at the end: \"{c.MomentShown}\"");
        }

        static string MomentLeadText => $"{ChartScreen.MomentLead:0} s";

        static RectTransform FindButton(ResultsScreen results, string label)
        {
            foreach (var b in results.GetComponentsInChildren<UiButton>(true))
                if (b.Label.text == label && b.gameObject.activeInHierarchy) return (RectTransform)b.transform;
            return null;
        }
    }
}
