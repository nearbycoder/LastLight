using System.Collections;
using LastLight.Core;
using LastLight.Sim;
using LastLight.UI;
using LastLight.View;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// The off-screen tour (-llScript offscreen): Corley Cove's lantern (night 9) and Westpoint's
    /// (night 10) sit just past the frame's edges at 16:9. Waits for each to burn and checks that a
    /// marker is pinned inside the nearest edge, wholly on screen and clear of the HUD, and that it
    /// goes once the lantern is doused. On night 10 it also checks that a ship lured out of frame
    /// keeps its lantern mark on screen.
    /// </summary>
    public static class OffscreenTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["offscreen"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        static Vector3 LanternWorld(WreckerSite s) => new Vector3(s.Pos.x, s.Height + 2f, s.Pos.y);

        /// <summary>Out of frame as the HUD judges it: off the screen or within its edge margin.</summary>
        static bool OffFrame(Vector3 world)
        {
            var v = CameraRig.Instance.Cam.WorldToViewportPoint(world);
            var size = Game.Instance.Hud.CanvasSize;
            float x = v.x * size.x, y = v.y * size.y, m = Hud.EdgeMargin;
            return v.z <= 0f || x < m || x > size.x - m || y < m || y > size.y - m;
        }

        static string Fmt(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";

        /// <summary>Wholly on the canvas, and clear of the HUD's blocks that are showing.</summary>
        static void CheckPlacement(Tour t, Rect r, string what)
        {
            var g = Game.Instance;
            var size = g.Hud.CanvasSize;
            bool inside = r.xMin >= 0f && r.yMin >= 0f && r.xMax <= size.x && r.yMax <= size.y;
            Check(t, inside, $"{what} is on screen: {Fmt(r)} in {size.x:0}x{size.y:0}");
            foreach (var (name, rect, shown) in g.Hud.TourBlocks())
            {
                if (!shown) continue;
                var b = g.Hud.TourCanvasRect(rect);
                if (b.Overlaps(r)) Check(t, false, $"{what} overlaps the HUD's {name} {Fmt(b)}");
            }
        }

        /// <summary>Checks the marker for the wrecker burning at a site: there when its lantern is
        /// out of frame, on the right side, and placed well.</summary>
        static void CheckMarker(Tour t, SimWrecker w)
        {
            var g = Game.Instance;
            var site = w.Site;
            var v = CameraRig.Instance.Cam.WorldToViewportPoint(LanternWorld(site));
            bool off = OffFrame(LanternWorld(site));
            bool found = false;
            foreach (var (id, bounds, outward) in g.Hud.TourEdgeMarkers())
            {
                if (id != site.Id) continue;
                found = true;
                bool side = v.x < 0f ? outward.x < -0.5f : v.x > 1f ? outward.x > 0.5f : true;
                Check(t, side, $"{site.Name}'s marker points out toward its lantern (viewport {v.x:0.00},{v.y:0.00}, chevron {outward.x:0.00},{outward.y:0.00})");
                CheckPlacement(t, bounds, $"{site.Name}'s marker");
            }
            Check(t, found == off, $"{site.Name}'s lantern is {(off ? "out of frame" : "in frame")} at ({v.x:0.00},{v.y:0.00}) and its marker is {(found ? "showing" : "not showing")}");
        }

        static SimWrecker BurningAt(SimWorld w, string site)
        {
            foreach (var wr in w.Wreckers) if (wr.Burning && wr.Site.Id == site) return wr;
            return null;
        }

        static IEnumerator Doused(Tour t, SimWrecker w)
        {
            var g = Game.Instance;
            g.Runner.AutoPlay = true;
            float waited = 0f;
            while (w.Burning && waited < 60f && !g.ShowingResults) { waited += Time.unscaledDeltaTime; yield return null; }
            yield return Tour.Wait(0.3f);
            bool showing = false;
            foreach (var (id, _, _) in g.Hud.TourEdgeMarkers()) showing |= id == w.Site.Id;
            Check(t, !w.Burning && !showing, $"once {w.Site.Name}'s lantern is doused its marker goes (doused {!w.Burning}, marker {(showing ? "still showing" : "gone")})");
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            var g = Game.Instance;
            // The camera leans in on each wreck; with shake off the frame stays put (-llFresh, so
            // the save is a blank one and isn't written).
            SaveData.Current.shake = false;
            yield return Tour.Wait(3f);

            // ---- Night IX: the Corleys' first lantern, at Corley Cove, past the right edge.
            g.AutoPlay = false;
            g.TourBriefing(9);
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            // Staging: nobody keeps the lamp at first, so wrecks are let run on (the HUD shows the
            // real allowance).
            int allowed = g.Runner.Def.allowedWrecks;
            g.Runner.Def.allowedWrecks = 99;
            g.Runner.TimeScale = 3f;
            SimWrecker corley = null;
            float waited = 0f;
            while (corley == null && waited < 90f) { waited += Time.unscaledDeltaTime; corley = BurningAt(g.Runner.World, "corley"); yield return null; }
            Check(t, corley != null, $"night IX: Corley Cove's lantern burns (sim {g.Runner.World.Time:0} s)");
            if (corley != null)
            {
                g.Runner.TimeScale = 0.25f;
                yield return Tour.Wait(0.6f);
                CheckMarker(t, corley);
                yield return t.Shot("offscreen_corley");
                yield return Doused(t, corley);
            }
            g.Runner.Def.allowedWrecks = allowed;

            // ---- Night X: the bot keeps the lamp until the Corleys leave Black Hen for Westpoint,
            // past the left edge; then the lantern is left burning, and a ship is left to it.
            g.AutoPlay = false;
            g.TourBriefing(10);
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            allowed = g.Runner.Def.allowedWrecks;
            g.Runner.Def.allowedWrecks = 99;
            g.Runner.AutoPlay = true;
            g.Runner.TimeScale = 3f;
            SimWrecker west = null;
            waited = 0f;
            while (waited < 120f)
            {
                waited += Time.unscaledDeltaTime;
                foreach (var wr in g.Runner.World.Wreckers)
                    if (wr.State == WreckerState.Doused && wr.Sites[(wr.SiteIndex + 1) % wr.Sites.Length].Id == "westpoint") west = wr;
                if (west != null) break;
                yield return null;
            }
            g.Runner.AutoPlay = false;
            waited = 0f;
            while (west != null && !west.Burning && waited < 60f) { waited += Time.unscaledDeltaTime; yield return null; }
            Check(t, west != null && west.Burning && west.Site.Id == "westpoint", $"night X: the Corleys move to Westpoint (sim {g.Runner.World.Time:0} s)");
            if (west != null && west.Burning)
            {
                g.Runner.TimeScale = 0.25f;
                yield return Tour.Wait(0.6f);
                CheckMarker(t, west);
                yield return t.Shot("offscreen_westpoint");

                // A ship lured toward Westpoint's rock, out of frame, keeps its mark on screen. The
                // rock is only just outside, so a lured ship is out of frame for a moment before it
                // strikes: each one is checked on the frame after it leaves the view.
                bool rockOff = OffFrame(new Vector3(west.Site.Hazard.x, 0f, west.Site.Hazard.y));
                if (!rockOff) t.Log("skip: Westpoint's rock is in frame at this screen shape, so a lured ship stays in view");
                g.Runner.TimeScale = 1f;
                SimShip lured = null;
                bool shown = false, pinned = false;
                Rect r = default;
                waited = 0f;
                while (rockOff && !shown && waited < 200f && west.Burning)
                {
                    waited += Time.unscaledDeltaTime;
                    SimShip candidate = null;
                    foreach (var s in g.Runner.World.Ships)
                        if (s.Inside && s.State == ShipState.Lured && s.LuredBy == west && OffFrame(new Vector3(s.Pos.x, 2f, s.Pos.y))) candidate = s;
                    yield return null;
                    if (candidate != null && candidate.State == ShipState.Lured)
                    {
                        lured = candidate;
                        shown = g.Hud.StatusBounds(lured.Id, out r, out pinned);
                    }
                }
                if (rockOff) Check(t, lured != null, $"a ship is lured out of frame toward Westpoint ({(lured != null ? lured.Name : "none in time")})");
                if (lured != null)
                {
                    Check(t, shown && pinned, $"{lured.Name}'s lured mark is pinned at the edge (showing {shown}, pinned {pinned}, ship at {lured.Pos})");
                    if (shown) CheckPlacement(t, r, $"{lured.Name}'s lured mark");
                    g.Runner.TimeScale = 0f;
                    yield return Tour.Wait(0.3f);
                    yield return t.Shot("offscreen_lured");
                }
                g.Runner.TimeScale = 3f;
                yield return Doused(t, west);
            }
            g.Runner.Def.allowedWrecks = allowed;
            t.Log($"offscreen {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
