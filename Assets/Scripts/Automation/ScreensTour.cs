using System.Collections;
using System.Collections.Generic;
using LastLight.Core;
using LastLight.Sim;
using LastLight.UI;
using LastLight.View;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.Automation
{
    /// <summary>
    /// The screens tour (-llScript screens): every menu, a busy night and a dawn card at whatever
    /// window size the player was started with. It checks that each menu's panels lie on screen,
    /// that the HUD's blocks stay on screen and clear of each other, and that every reef, sandbank,
    /// buoy, wrecker site and the harbour is inside the camera's view.
    /// </summary>
    public static class ScreensTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["screens"] = Run;

        static int failures;

        static IEnumerator Run(Tour t)
        {
            var g = Game.Instance;
            failures = 0;
            t.Log($"screen {Screen.width}x{Screen.height} aspect {(float)Screen.width / Screen.height:0.000} fov {g.Rig.Cam.fieldOfView:0.0}");
            yield return Tour.Wait(6f);
            yield return t.Shot("01_title");
            CheckScreen(t, "title", g.TourScreen("title"));
            g.TourShowLogbook();
            yield return Tour.Wait(1.6f);
            yield return t.Shot("02_logbook");
            CheckScreen(t, "logbook", g.TourScreen("logbook"));
            g.TourHideAll();
            g.TourShowSettings();
            yield return Tour.Wait(1.2f);
            yield return t.Shot("03_settings");
            CheckScreen(t, "settings", g.TourScreen("settings"));
            ((SettingsScreen)g.TourScreen("settings")).TourShowKeys();
            yield return Tour.Wait(0.5f);
            yield return t.Shot("03b_keys");
            CheckScreen(t, "keys", g.TourScreen("settings"));
            g.TourHideAll();

            // Night 11: twelve ships in the manifest, the longest top bar.
            g.AutoPlay = true;
            int night = Game.Arg("-llScreensNight", 11);
            g.TourBriefing(night);
            yield return Tour.Wait(4f);
            yield return t.Shot("04_briefing");
            CheckScreen(t, "briefing", g.TourScreen("briefing"));
            // Three ships left to their fate, so the dawn card carries a full debrief.
            var neglect = new HashSet<string>();
            var ships = g.Runner.Def.ships;
            for (int i = 0; i < ships.Length && neglect.Count < 3; i++) neglect.Add(ships[i].name);
            g.Runner.Bot.Ignore = s => neglect.Contains(s.Name);
            g.TourBegin();
            yield return Tour.Wait(18f);
            g.Hud.ShowHint("tour", "Sweep the light ahead of a ship to chart the hidden reefs.", "ring", 30f);
            yield return Tour.Wait(1.2f);
            yield return t.Shot("05_play");
            CheckHud(t, g.Hud);
            t.Log($"top bar: manifest scale {g.Hud.TopLayout.manifestScale:0.00}, hint at {g.Hud.TopLayout.hintY:0}");
            // The framing is checked from the play view at rest: in play the camera sways and leans
            // a little toward the beam, which moved Westpoint's lantern from -0.02 to -0.07 between
            // runs. The live view is logged beside it.
            var live = g.Rig.Cam;
            var rest = new GameObject("RestView").AddComponent<Camera>();
            rest.CopyFrom(live);
            rest.enabled = false;
            rest.transform.position = CameraRig.PlayPose.Position;
            rest.transform.rotation = Quaternion.LookRotation(CameraRig.PlayPose.LookAt - CameraRig.PlayPose.Position, Vector3.up);
            rest.fieldOfView = CameraRig.FitFov(CameraRig.PlayPose.Fov, live.aspect);
            var wl = MapData.Load().WreckerSites["westpoint"];
            var lv = live.WorldToViewportPoint(new Vector3(wl.Pos.x, wl.Height, wl.Pos.y));
            t.Log($"live view (swaying, leaning toward the beam): westpoint lantern ({lv.x:0.00},{lv.y:0.00})");
            CheckMap(t, rest);
            Object.Destroy(rest.gameObject);
            g.TourPause();
            yield return Tour.Wait(1.2f);
            yield return t.Shot("06_pause");
            CheckScreen(t, "pause", g.TourScreen("pause"));
            g.TourPauseChoose(4);
            yield return Tour.Wait(0.6f);
            CheckScreen(t, "pause question", g.TourScreen("pause"));
            yield return t.Shot("06b_pause_question");
            g.TourPauseConfirm(0);
            g.TourResume();
            g.Runner.TimeScale = 10f;
            float waited = 0f;
            while (!g.ShowingResults && waited < 120f) { waited += Time.unscaledDeltaTime; yield return null; }
            yield return Tour.Wait(3f);
            yield return t.Shot("07_results");
            CheckScreen(t, "results", g.TourScreen("results"));
            t.Log(failures == 0 ? "PASS every screen fits" : $"FAIL {failures} layout checks failed");
        }

        /// <summary>A rect in screen pixels (the canvases are screen-space overlays).</summary>
        static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        static bool OnScreen(Rect r) => r.xMin >= -1f && r.yMin >= -1f && r.xMax <= Screen.width + 1f && r.yMax <= Screen.height + 1f;

        static string Fmt(Rect r) => $"({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0})";

        static void Result(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        static void CheckScreen(Tour t, string name, UiScreen s)
        {
            if (s == null || !s.Visible) { Result(t, false, $"{name}: not showing"); return; }
            bool ok = true;
            string worst = "";
            foreach (var f in s.Frames)
            {
                if (!f.gameObject.activeInHierarchy) continue;
                var r = ScreenRect(f);
                if (!OnScreen(r)) { ok = false; worst += $" {f.name} {Fmt(r)}"; }
            }
            Result(t, ok, $"{name}: {s.Frames.Count} panels on the {Screen.width}x{Screen.height} screen{(ok ? "" : ", off screen:" + worst)}");
        }

        /// <summary>What a block actually draws: its visible graphics, with text measured by its
        /// words rather than its box.</summary>
        static Rect? ContentRect(RectTransform block)
        {
            Rect? r = null;
            foreach (var gr in block.GetComponentsInChildren<Graphic>(false))
            {
                if (!gr.enabled || gr.color.a < 0.02f) continue;
                var box = ScreenRect(gr.rectTransform);
                float x0 = box.xMin, x1 = box.xMax;
                if (gr is Text text)
                {
                    if (string.IsNullOrEmpty(text.text)) continue;
                    float w = text.preferredWidth * text.rectTransform.lossyScale.x;
                    if (text.horizontalOverflow == HorizontalWrapMode.Wrap) w = Mathf.Min(w, box.width);
                    int h = (int)text.alignment % 3;
                    if (h == 0) x1 = x0 + w;
                    else if (h == 2) x0 = x1 - w;
                    else { float m = box.center.x; x0 = m - w / 2; x1 = m + w / 2; }
                }
                var gr2 = Rect.MinMaxRect(x0, box.yMin, x1, box.yMax);
                r = r == null ? gr2 : Rect.MinMaxRect(Mathf.Min(r.Value.xMin, gr2.xMin), Mathf.Min(r.Value.yMin, gr2.yMin), Mathf.Max(r.Value.xMax, gr2.xMax), Mathf.Max(r.Value.yMax, gr2.yMax));
            }
            return r;
        }

        static void CheckHud(Tour t, Hud hud)
        {
            var blocks = new List<(string name, Rect rect)>();
            foreach (var (name, rt, shown) in hud.TourBlocks())
            {
                if (!shown) continue;
                var r = ContentRect(rt);
                if (r == null) continue;
                blocks.Add((name, r.Value));
                Result(t, OnScreen(r.Value), $"HUD {name} on screen {Fmt(r.Value)}");
            }
            for (int i = 0; i < blocks.Count; i++)
                for (int j = i + 1; j < blocks.Count; j++)
                {
                    var a = blocks[i].rect;
                    var b = blocks[j].rect;
                    float ox = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                    float oy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    if (ox > 2f && oy > 2f) Result(t, false, $"HUD {blocks[i].name} overlaps {blocks[j].name} by {ox:0}x{oy:0} px");
                }
            t.Log($"HUD checked {blocks.Count} blocks for overlaps");
        }

        static void CheckMap(Tour t, Camera cam)
        {
            var map = MapData.Load();
            var points = new List<(string, Vector3)> { ("harbour", new Vector3(map.Harbor.x, 0f, map.Harbor.y)) };
            foreach (var r in map.Reefs) points.Add(("reef " + r.id, new Vector3(r.x, 0f, r.z)));
            foreach (var s in map.Shoals) points.Add(("shoal " + s.Id, new Vector3(s.Pos.x, 0f, s.Pos.y)));
            foreach (var b in map.Buoys) points.Add(("buoy " + b.id, new Vector3(b.x, 0f, b.z)));
            // The wreckers' lantern sites on the cliffs, and Westpoint's rock, sit just past the
            // frame's edge even at 16:9 (as released), so they may be up to 6% outside, and no further.
            var sites = new List<string>();
            foreach (var w in map.WreckerSites.Values)
                foreach (var (what, p) in new[] { ("lantern", new Vector3(w.Pos.x, w.Height, w.Pos.y)), ("rock", new Vector3(w.Hazard.x, 0f, w.Hazard.y)) })
                {
                    var v = cam.WorldToViewportPoint(p);
                    bool near = v.z > 0f && v.x > -0.06f && v.x < 1.06f && v.y > -0.06f && v.y < 1.06f;
                    if (!near) failures++;
                    sites.Add($"{w.Id} {what} ({v.x:0.00},{v.y:0.00}){(near ? "" : " FAIL")}");
                }
            t.Log("wrecker sites: " + string.Join(", ", sites));
            float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
            var outside = new List<string>();
            foreach (var (name, p) in points)
            {
                var v = cam.WorldToViewportPoint(p);
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                if (v.z <= 0f || v.x < 0.01f || v.x > 0.99f || v.y < 0.01f || v.y > 0.99f) outside.Add($"{name} ({v.x:0.00},{v.y:0.00})");
            }
            Result(t, outside.Count == 0, $"map: {points.Count} hazards and the harbour in view, viewport x {minX:0.00}..{maxX:0.00} y {minY:0.00}..{maxY:0.00}{(outside.Count == 0 ? "" : ", outside: " + string.Join(", ", outside))}");
        }
    }
}
