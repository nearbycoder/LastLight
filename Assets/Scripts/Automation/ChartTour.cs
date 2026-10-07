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

        static IEnumerator ToDawn(Game g)
        {
            float waited = 0f;
            while (!g.ShowingResults && waited < 300f) { waited += Time.unscaledDeltaTime; yield return null; }
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
            yield return Press(Key.Escape);
            yield return Tour.Wait(0.6f);
            t.Log($"chart {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }

        static RectTransform FindButton(ResultsScreen results, string label)
        {
            foreach (var b in results.GetComponentsInChildren<UiButton>(true))
                if (b.Label.text == label && b.gameObject.activeInHierarchy) return (RectTransform)b.transform;
            return null;
        }
    }
}
