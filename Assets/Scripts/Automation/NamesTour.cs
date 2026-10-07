using System.Collections;
using LastLight.Core;
using LastLight.Sim;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// The names tour (-llScript names): names on the water. Night I: Ianto's "at the harbour"
    /// labels Porthkell, and a ship on the radio gets a tag under its hull. Night III: the Hen Bell
    /// is labelled on its buoy. Night II: no reef is named before it's charted, and the Teeth get
    /// their name the first time they are. Each label is checked against the projected point it
    /// names, or, when pinned at the edge, for being wholly on screen.
    /// </summary>
    public static class NamesTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["names"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        static string Fmt(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";

        /// <summary>Waits for a label (or any ship tag when <paramref name="label"/> is null) to show
        /// fully, then checks where it sits. Returns the label's text, or null if none came.</summary>
        static IEnumerator WaitAndCheck(Tour t, string label, bool ship, float timeout, string[] result)
        {
            var g = Game.Instance;
            float waited = 0f;
            result[0] = null;
            while (waited < timeout && !g.ShowingResults)
            {
                waited += Time.unscaledDeltaTime;
                foreach (var n in g.Hud.TourNames())
                {
                    bool isShip = false;
                    foreach (var s in g.Runner.World.Ships) isShip |= s.Name == n.label;
                    if (n.alpha > 0.95f && (label != null ? n.label == label : isShip == ship)) { result[0] = n.label; break; }
                }
                if (result[0] != null) break;
                yield return null;
            }
            Check(t, result[0] != null, $"{(label ?? (ship ? "a ship's tag" : "a place's label"))} shows (waited {waited:0.0} s, sim {g.Runner.World.Time:0} s)");
            if (result[0] == null) yield break;
            g.Runner.TimeScale = 0f;
            yield return null;
            yield return null;
            foreach (var n in g.Hud.TourNames())
            {
                if (n.label != result[0]) continue;
                var size = g.Hud.CanvasSize;
                bool inside = n.bounds.xMin >= 0f && n.bounds.yMin >= 0f && n.bounds.xMax <= size.x && n.bounds.yMax <= size.y;
                Check(t, inside, $"{n.label} is on screen: {Fmt(n.bounds)} in {size.x:0}x{size.y:0}{(n.pinned ? ", pinned at the edge" : "")}");
                if (!n.pinned)
                {
                    // A ship's tag hangs 40 units under the hull; a place's label sits 30 above its
                    // spot, or a step or two (38 units each) up or down when that's taken.
                    var want = n.target + new Vector2(0f, ship ? -40f : 30f);
                    var d = n.bounds.center - want;
                    float off = d.magnitude;
                    float stepped = Mathf.Abs(Mathf.Abs(d.y) - Mathf.Round(Mathf.Abs(d.y) / 38f) * 38f);
                    bool near = off < 12f || (Mathf.Abs(d.x) < 12f && Mathf.Abs(d.y) <= 2f * 38f + 12f && stepped < 12f);
                    Check(t, near, $"{n.label} sits {(ship ? "under the hull" : "on its place")}: centre ({n.bounds.center.x:0},{n.bounds.center.y:0}), point ({n.target.x:0},{n.target.y:0}), off by {off:0.0}{(off >= 12f ? $" (moved {d.y:0} to keep clear)" : "")}");
                }
            }
        }

        /// <summary>Watches the names for a while: every frame, no two names showing may overlap,
        /// and none may cover a lost or lured ship's mark. Shoots the first frame with the most names.</summary>
        static IEnumerator WatchClear(Tour t, string night, float seconds, string shot)
        {
            var g = Game.Instance;
            float waited = 0f;
            int frames = 0, crowded = 0, clashes = 0, moved = 0, most = 0;
            string first = null;
            var movedNames = new System.Collections.Generic.List<string>();
            while (waited < seconds && !g.ShowingResults)
            {
                waited += Time.unscaledDeltaTime;
                var shown = g.Hud.TourNames().FindAll(n => n.alpha > 0.05f);
                var marks = g.Hud.TourStatusMarks();
                frames++;
                if (shown.Count + marks.Count > 1) crowded++;
                var offsets = g.Hud.TourNameOffsets();
                bool anyMoved = false;
                foreach (var n in shown)
                    if (offsets.TryGetValue(n.label, out var o) && o != Vector2.zero)
                    {
                        anyMoved = true;
                        if (!movedNames.Contains(n.label)) { movedNames.Add(n.label); t.Log($"{night}: {n.label} moved ({o.x:0},{o.y:0}) to keep clear, at sim {g.Runner.World.Time:0.0} s"); }
                    }
                if (anyMoved) moved++;
                for (int i = 0; i < shown.Count; i++)
                {
                    for (int j = i + 1; j < shown.Count; j++)
                        if (UI.LabelPlacer.Overlaps(shown[i].bounds, shown[j].bounds))
                        {
                            clashes++;
                            first ??= $"{shown[i].label} {Fmt(shown[i].bounds)} and {shown[j].label} {Fmt(shown[j].bounds)} at sim {g.Runner.World.Time:0.0} s";
                        }
                    foreach (var m in marks)
                        if (UI.LabelPlacer.Overlaps(shown[i].bounds, m))
                        {
                            clashes++;
                            first ??= $"{shown[i].label} {Fmt(shown[i].bounds)} and a ship's mark {Fmt(m)} at sim {g.Runner.World.Time:0.0} s";
                        }
                }
                int full = shown.FindAll(n => n.alpha > 0.9f).Count;
                if (full >= 2 && full > most)
                {
                    most = full;
                    t.Log($"{night}: {most} names at once: {string.Join(", ", shown.ConvertAll(n => $"{n.label} {Fmt(n.bounds)}"))}; {marks.Count} marks");
                    yield return t.Shot(shot);
                }
                yield return null;
            }
            Check(t, frames > 0 && clashes == 0, $"{night}: no name covers another or a ship's mark over {frames} frames ({crowded} with two or more showing, {most} fully shown at most, {moved} with a name moved aside){(first != null ? "; first clash: " + first : "")}");
        }

        static IEnumerator Night(int night)
        {
            var g = Game.Instance;
            g.AutoPlay = false;
            g.TourBriefing(night);
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            g.Runner.AutoPlay = true;
            g.Runner.TimeScale = 1f;
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            var g = Game.Instance;
            SaveData.Current.shake = false;
            SaveData.Current.hints = false;   // the hint panel would sit over the names in the shots
            // -llNamesOverlap: names as round 7 drew them, to show the check catches a clash.
            UI.Hud.NamesMakeRoom = !Game.HasArg("-llNamesOverlap");
            yield return Tour.Wait(3f);
            var found = new string[1];

            // ---- Night I: "Evening, keeper. Ianto at the harbour."
            yield return Night(1);
            yield return WaitAndCheck(t, "Porthkell", false, 20f, found);
            if (found[0] != null) yield return t.Shot("names_harbour");
            g.Runner.TimeScale = 2f;
            yield return WaitAndCheck(t, null, true, 90f, found);
            if (found[0] != null)
            {
                t.Log($"tagged {found[0]} for: \"{g.Radio.Current?.Text}\"");
                yield return t.Shot("names_ship");
            }

            // ---- Night III: "Light the Hen Bell before the Auk gets there."
            yield return Night(3);
            yield return WaitAndCheck(t, "Hen Bell", false, 25f, found);
            if (found[0] != null) yield return t.Shot("names_hen_bell");
            // Round 7's captures had the Hen Bell and the Hen's Chicks on top of each other over a
            // lost Little Auk: leave the Auk to the dark and watch the names.
            g.Runner.TimeScale = 1f;
            g.TourNeglect("Little Auk");
            yield return WatchClear(t, "night III", float.Parse(Game.ArgString("-llNamesWatch", "45")), "names_crowded_night3");

            // ---- Night II: nothing named before it's charted; the Teeth named when they are.
            // The keeper looks away from the Teeth while Ianto names them, then the bot charts them.
            yield return Night(2);
            g.Runner.AutoPlay = false;
            var map = g.Runner.World.Map;
            g.Runner.SetInitialBearing(Geo.Bearing(map.Harbor - map.Lighthouse));
            float waited = 0f;
            bool named = false, early = false, charted = false;
            while (waited < 12f && !(named && waited > 4f))
            {
                waited += Time.unscaledDeltaTime;
                named |= g.Radio.Current != null && g.Radio.Current.Text.Contains("Teeth");
                foreach (var r in g.Runner.World.Reefs) charted |= r.Charted;
                foreach (var n in g.Hud.TourNames()) early |= n.label == PlaceNames.GroupLabel("teeth");
                yield return null;
            }
            Check(t, named && !charted && !early, $"Ianto names the Teeth before they're charted, and no label points at them (called {named}, charted {charted}, labelled {early})");
            g.Runner.AutoPlay = true;
            yield return WaitAndCheck(t, PlaceNames.GroupLabel("teeth"), false, 30f, found);
            if (found[0] != null)
            {
                // The label stands over tonight's Teeth.
                var centre = PlaceNames.GroupCentre(g.Runner.World, "teeth");
                foreach (var n in g.Hud.TourNames())
                    if (n.label == found[0])
                    {
                        var sp = View.CameraRig.Instance.Cam.WorldToScreenPoint(centre) * (g.Hud.CanvasSize.x / Screen.width);
                        Check(t, Vector2.Distance(n.target, sp) < 2f, $"the Teeth's label stands over the Teeth ({sp.x:0},{sp.y:0})");
                    }
                yield return t.Shot("names_teeth");
            }
            g.Runner.TimeScale = 1f;
            t.Log($"names {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
