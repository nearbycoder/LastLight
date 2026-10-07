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
                    // A ship's tag hangs 40 units under the hull; a place's label sits 30 above its spot.
                    var want = n.target + new Vector2(0f, ship ? -40f : 30f);
                    float off = Vector2.Distance(n.bounds.center, want);
                    Check(t, off < 12f, $"{n.label} sits {(ship ? "under the hull" : "on its place")}: centre ({n.bounds.center.x:0},{n.bounds.center.y:0}), point ({n.target.x:0},{n.target.y:0}), off by {off:0.0}");
                }
            }
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
