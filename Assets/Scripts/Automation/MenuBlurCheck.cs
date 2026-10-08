using System.Collections;
using System.Collections.Generic;
using LastLight.Core;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// For the ui tour: how much the bay behind a menu softens. With every canvas hidden for a
    /// frame, the scene is captured with the softening held off and then on (the night is paused,
    /// the camera's sway stilled), and its fine detail compared: the mean difference between
    /// neighbouring pixels' brightness, over the middle of the screen where the pause card's items
    /// sit and over the whole frame.
    /// </summary>
    public static class MenuBlurCheck
    {
        static IEnumerator Capture(float[] detail)
        {
            var canvases = new List<Canvas>();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.enabled && c.isRootCanvas) { c.enabled = false; canvases.Add(c); }
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            foreach (var c in canvases) c.enabled = true;
            var px = tex.GetPixels();
            int w = tex.width, h = tex.height;
            Object.Destroy(tex);
            double mid = 0, all = 0, mean = 0;
            int nMid = 0, nAll = 0;
            for (int y = 0; y < h - 1; y++)
                for (int x = 0; x < w - 1; x++)
                {
                    float l = px[y * w + x].grayscale;
                    float d = Mathf.Abs(l - px[y * w + x + 1].grayscale) + Mathf.Abs(l - px[(y + 1) * w + x].grayscale);
                    all += d; nAll++; mean += l;
                    if (x > w * 0.3f && x < w * 0.7f && y > h * 0.18f && y < h * 0.82f) { mid += d; nMid++; }
                }
            detail[0] = (float)(mid / Mathf.Max(1, nMid)) * 1000f;
            detail[1] = (float)(all / Mathf.Max(1, nAll)) * 1000f;
            detail[2] = (float)(mean / Mathf.Max(1, nAll));
        }

        /// <summary>Measures with the pause card up; returns whether the detail behind it fell by at least half.</summary>
        public static IEnumerator Measure(Tour t, string what, bool[] ok)
        {
            var g = Game.Instance;
            float sway = g.Rig.SwayAmount;
            g.Rig.SwayAmount = 0f;
            var sharp = new float[3];
            var soft = new float[3];
            Stage.MenuBlurOff = true;
            yield return Tour.Wait(0.6f);
            yield return t.Shot($"{what}_sharp");
            yield return Capture(sharp);
            Stage.MenuBlurOff = false;
            yield return Tour.Wait(0.6f);
            yield return Capture(soft);
            g.Rig.SwayAmount = sway;
            ok[0] = Stage.MenuBlur > 0.99f && soft[0] <= 0.5f * sharp[0];
            t.Log($"{(ok[0] ? "PASS" : "FAIL")} {what}: the bay behind the menu softens (fine detail behind the items {sharp[0]:0.00} → {soft[0]:0.00}, {100f * (1f - soft[0] / Mathf.Max(1e-4f, sharp[0])):0}% less; whole frame {sharp[1]:0.00} → {soft[1]:0.00}; mean brightness {sharp[2]:0.000} → {soft[2]:0.000}; eased in {Stage.MenuBlur:0.00})");
        }
    }
}
