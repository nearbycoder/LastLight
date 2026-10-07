using System.Collections.Generic;
using LastLight.Core;
using LastLight.Sim;
using LastLight.View;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>
    /// Names on the water: a tag under the hull of a ship that is on the radio or named on it, and
    /// a label on a place a call names (or a reef group the first time it's charted), so the radio's
    /// "the Auk" and "the Hen Bell" and the debrief's "the Hen's Chicks" have somewhere to point.
    /// Each stays inside the screen edge, with a chevron, when what it names is out of frame.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>The trailer's shoot turns the names off, so a re-shoot matches the released cut.</summary>
        public static bool NamesShown = true;

        RectTransform namesLayer;
        const float BgAlpha = 0.75f;   // dark enough to read over the beam and white water

        sealed class NameTag
        {
            public RectTransform Root;
            public Image Bg, Dot, Chevron;
            public Text Label;
            public SimShip Ship;         // a ship's tag follows its hull; otherwise a fixed point
            public Vector3 World;
            public string Key;
            public float Life, MaxLife;
            public bool Pinned;
        }

        readonly List<NameTag> names = new List<NameTag>();

        NameTag MakeTag(string key, string label, Color dot, bool ship)
        {
            var t = new NameTag { Key = key, Root = UiKit.Rect("Name", namesLayer) };
            t.Root.anchorMin = t.Root.anchorMax = Vector2.zero;
            t.Bg = UiKit.Image("Bg", t.Root, SpriteFactory.Pill, new Color(0.02f, 0.035f, 0.05f, BgAlpha), true);
            t.Bg.raycastTarget = false;
            t.Label = UiKit.Text("Label", t.Root, label, ship ? UiKit.BodyBold : UiKit.Italic, ship ? 20 : 23, UiKit.Paper, TextAnchor.MiddleCenter).Shadowed(0.8f, 1.5f);
            t.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
            float w = t.Label.preferredWidth + (ship ? 44f : 30f);
            t.Root.sizeDelta = new Vector2(w, 34f);
            t.Bg.rectTransform.Fill();
            t.Label.rectTransform.Fill();
            if (ship)
            {
                // The speaker's colour, as on the radio's medallion.
                t.Label.rectTransform.offsetMin = new Vector2(24f, 0f);
                t.Dot = UiKit.Image("Dot", t.Root, SpriteFactory.Disc, dot);
                t.Dot.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20f, 0f), new Vector2(12f, 12f));
            }
            t.Chevron = UiKit.Image("Chevron", t.Root, SpriteFactory.Icon("chevron"), UiKit.Paper);
            t.Chevron.rectTransform.anchorMin = t.Chevron.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.Chevron.rectTransform.sizeDelta = new Vector2(30, 30);
            // Unseen until the first update places it (it can be made after the HUD's update this
            // frame, and would otherwise flash at the canvas's corner).
            t.Label.color = new Color(0, 0, 0, 0);
            t.Bg.color = new Color(0, 0, 0, 0);
            if (t.Dot != null) t.Dot.color = new Color(0, 0, 0, 0);
            t.Chevron.enabled = false;
            t.Root.anchoredPosition = new Vector2(-9999f, -9999f);
            names.Add(t);
            return t;
        }

        NameTag FindTag(string key)
        {
            foreach (var t in names) if (t.Key == key) return t;
            return null;
        }

        /// <summary>Tag a ship for a while (a radio call's length); a ship already tagged keeps its tag longer.</summary>
        public void NameShip(SimShip ship, Color speaker, float seconds)
        {
            if (!NamesShown || ship == null || !ship.Active) return;
            string key = "ship:" + ship.Id;
            var t = FindTag(key) ?? MakeTag(key, ship.Name, speaker, true);
            t.Ship = ship;
            if (t.Dot != null) t.Dot.color = new Color(speaker.r, speaker.g, speaker.b, t.Dot.color.a);
            Refresh(t, seconds);
        }

        /// <summary>Label a place on the water for a few seconds.</summary>
        public void NamePlace(string key, string label, Vector3 world, float seconds)
        {
            if (!NamesShown) return;
            var t = FindTag(key) ?? MakeTag(key, label, default, false);
            t.World = world;
            Refresh(t, seconds);
        }

        static void Refresh(NameTag t, float seconds)
        {
            float age = t.MaxLife - t.Life;
            t.Life = Mathf.Max(t.Life, seconds);
            // Keep the fade-in done if it was already showing.
            t.MaxLife = t.Life + Mathf.Min(age, 0.3f);
        }

        void ClearNames()
        {
            foreach (var t in names) if (t.Root != null) Destroy(t.Root.gameObject);
            names.Clear();
        }

        void UpdateNames(float dt)
        {
            var cam = CameraRig.Instance != null ? CameraRig.Instance.Cam : Camera.main;
            if (cam == null) return;
            float scale = CanvasSize.x / Mathf.Max(1, Screen.width);
            for (int i = names.Count - 1; i >= 0; i--)
            {
                var t = names[i];
                t.Life -= dt;
                if (t.Ship != null && !t.Ship.Active) t.Life = Mathf.Min(t.Life, 0.4f);
                if (t.Life <= 0f || t.Root == null || !NamesShown)
                {
                    if (t.Root != null) Destroy(t.Root.gameObject);
                    names.RemoveAt(i);
                    continue;
                }
                var world = t.Ship != null ? new Vector3(t.Ship.Pos.x, 1f, t.Ship.Pos.y) : t.World;
                var sp = cam.WorldToScreenPoint(world);
                var at = sp.z > 0f ? new Vector2(sp.x, sp.y) * scale : new Vector2(-999, -999);
                // A ship's tag hangs under the hull (its lost or lured mark sits above); a place's
                // label sits just above the spot.
                var pos = at + (t.Ship != null ? new Vector2(0f, -40f) : new Vector2(0f, 30f));
                bool pinned = PinToEdge(at, out var edgeAt, out var outward);
                if (pinned)
                {
                    // Out of frame: inside the edge, below where a pinned status mark would be.
                    pos = edgeAt + new Vector2(0f, t.Ship != null ? -62f : 0f);
                    float half = t.Root.sizeDelta.x * 0.5f + 8f;
                    pos.x = Mathf.Clamp(pos.x, half, CanvasSize.x - half);
                }
                t.Pinned = pinned;
                t.Root.anchoredPosition = pos;
                float age = t.MaxLife - t.Life;
                float a = Mathf.Clamp01(age / 0.3f) * Mathf.Clamp01(t.Life / 0.6f);
                t.Label.color = new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, a);
                t.Bg.color = new Color(0.02f, 0.035f, 0.05f, BgAlpha * a);
                if (t.Dot != null) { var c = t.Dot.color; c.a = a; t.Dot.color = c; }
                // The chevron sits beyond the tag's end, pointing out of frame.
                t.Chevron.enabled = pinned;
                if (pinned)
                {
                    float reach = Mathf.Abs(outward.x) * (t.Root.sizeDelta.x * 0.5f + 18f) + Mathf.Abs(outward.y) * 34f;
                    t.Chevron.rectTransform.anchoredPosition = outward * reach;
                    t.Chevron.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg);
                    t.Chevron.color = new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, a * (0.6f + 0.4f * Mathf.Sin(Unscaled.Time * 6f)));
                }
            }
        }

        /// <summary>For the tours: the names showing, with their bounds on the canvas, the canvas
        /// point they name, and whether they're pinned at the edge.</summary>
        public List<(string label, Rect bounds, Vector2 target, bool pinned, float alpha)> TourNames()
        {
            var list = new List<(string, Rect, Vector2, bool, float)>();
            var cam = CameraRig.Instance != null ? CameraRig.Instance.Cam : Camera.main;
            float scale = CanvasSize.x / Mathf.Max(1, Screen.width);
            foreach (var t in names)
            {
                if (t.Root == null) continue;
                var world = t.Ship != null ? new Vector3(t.Ship.Pos.x, 1f, t.Ship.Pos.y) : t.World;
                var sp = cam.WorldToScreenPoint(world);
                var bounds = CanvasRect(t.Root);
                if (t.Chevron.enabled) bounds = Union(bounds, CanvasRect(t.Chevron.rectTransform));
                list.Add((t.Label.text, bounds, new Vector2(sp.x, sp.y) * scale, t.Pinned, t.Label.color.a));
            }
            return list;
        }
    }
}
