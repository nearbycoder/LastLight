using System;
using System.Collections.Generic;
using LastLight.Audio;
using LastLight.Core;
using LastLight.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>
    /// The night's chart, opened from the dawn card: Merrow Bay on paper with every ship's track,
    /// inked solid while the captain was steering, dotted red with a "?" where they lost their way
    /// and dashed amber with a lantern where a false light had them, and a cross at each wreck.
    /// Only what the keeper saw is drawn: reefs and sandbanks charted tonight, lanterns that burned.
    /// Replay plays the night back on the paper: the ships at their places, named, the light's
    /// sweep, the false lights while they burned, and each wreck as it happens.
    /// </summary>
    public sealed class ChartScreen : UiScreen
    {
        public Action OnBack;
        RectTransform panel, area, marks, legendInk;
        Text heading, sub;
        UiButton back;
        readonly List<ChartInk> inks = new List<ChartInk>();
        ChartInk baseInk;
        float scale;
        Vector2 centre;
        readonly List<(string ship, RectTransform mark)> wreckMarks = new List<(string, RectTransform)>();
        readonly List<(string place, RectTransform mark)> placeMarks = new List<(string, RectTransform)>();
        // Names wait until everything is drawn, then go down most important first, clear of the
        // marks (crosses, "?", lanterns, rocks, the light) and of each other.
        readonly List<(Text text, int rank)> pendingNames = new List<(Text, int)>();
        readonly List<Rect> keepClear = new List<Rect>();
        const int RankWreck = 0, RankWavered = 1, RankWrecker = 2, RankHazard = 3, RankPlace = 4, RankStack = 5;
        /// <summary>The "?" and lantern marks drawn where ships lost their way or were lured (for tours).</summary>
        public int LostMarks { get; private set; }
        public int LuredMarks { get; private set; }

        // The part of the bay drawn: the play area with a little sea around it.
        public static readonly Rect View = Rect.MinMaxRect(-150f, -52f, 150f, 134f);

        public static readonly Color Ink = new Color(0.16f, 0.13f, 0.1f);
        static readonly Color InkSoft = new Color(0.38f, 0.3f, 0.21f);
        public static readonly Color LostRed = new Color(0.74f, 0.15f, 0.09f);
        public static readonly Color LuredAmber = new Color(0.86f, 0.46f, 0.02f);
        // Names are written darker than the marks, so they read on the paper at any colour vision.
        static readonly Color NameRed = new Color(0.56f, 0.09f, 0.05f);
        static readonly Color NameAmber = new Color(0.5f, 0.25f, 0.02f);
        static readonly Color Sea = new Color(0.36f, 0.55f, 0.66f, 0.13f);
        static readonly Color Land = new Color(0.78f, 0.67f, 0.48f, 0.95f);
        static readonly Color Coast = new Color(0.3f, 0.23f, 0.15f, 0.9f);

        public static ChartScreen Create(Transform canvas)
        {
            var s = canvas.gameObject.AddComponent<ChartScreen>();
            s.Init(canvas, "Chart");
            s.Build();
            return s;
        }

        void Build()
        {
            var dim = UiKit.Image("Dim", Root, null, new Color(0, 0.01f, 0.02f, 0.7f));
            dim.rectTransform.Fill();
            panel = UiKit.Rect("Panel", Root).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1780, 980));
            Frames.Add(panel);
            var paper = UiKit.Image("Paper", panel, SpriteFactory.Paper, Color.white, true);
            paper.rectTransform.Fill();
            var shadow = paper.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(10, -14);

            // The chart itself, on the left, clipped to its frame.
            area = UiKit.Rect("Area", panel).Pin(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(46, 0), new Vector2(1380, 900));
            area.gameObject.AddComponent<RectMask2D>();
            scale = Mathf.Min(area.sizeDelta.x / View.width, area.sizeDelta.y / View.height);
            centre = View.center;
            baseInk = ChartInk.Create("Base", area);
            marks = UiKit.Rect("Marks", area).Fill();
            var frame = ChartInk.Create("Frame", panel);
            var fr = area.sizeDelta * 0.5f;
            var c = area.anchoredPosition + new Vector2(fr.x, 0) - new Vector2(panel.sizeDelta.x * 0.5f, 0);
            frame.Outline(new[] { c + new Vector2(-fr.x, -fr.y), c + new Vector2(fr.x, -fr.y), c + new Vector2(fr.x, fr.y), c + new Vector2(-fr.x, fr.y) }, 3f, InkSoft);
            frame.Apply();

            // The key, on the right.
            var col = UiKit.Rect("Key", panel).Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(300, 900));
            heading = Label(col, "The night's chart", UiKit.Title, 40, Ink, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, -14), new Vector2(320, 56));
            heading.horizontalOverflow = HorizontalWrapMode.Overflow;
            sub = UiKit.Text("Sub", col, "", UiKit.Italic, 23, InkSoft, TextAnchor.UpperLeft);
            sub.rectTransform.Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -80), new Vector2(300, 90));
            sub.lineSpacing = 1.05f;
            legendInk = UiKit.Rect("KeyInk", col).Fill();
            var key = legendInk.gameObject.AddComponent<ChartInk>();
            key.raycastTarget = false;
            (string text, Action<ChartInk, Vector2> draw)[] rows =
            {
                ("On course", (k, p) => k.Line(p - new Vector2(22, 0), p + new Vector2(22, 0), 2.6f, TrackInk)),
                ("Lost its way", (k, p) => k.Polyline(new[] { p - new Vector2(22, 0), p + new Vector2(22, 0) }, 3.2f, LostRed, 3f, 5f)),
                ("Lured by a false light", (k, p) => k.Polyline(new[] { p - new Vector2(22, 0), p + new Vector2(22, 0) }, 3.2f, LuredAmber, 11f, 6f)),
                ("Wrecked", null),
                ("A reef you charted", (k, p) => Reef(k, p, 9f)),
                ("A sandbank you charted", (k, p) => Shoal(k, p, new Vector2(24, 10), 0f)),
                ("A false light that burned", null),
                ("Buoys and the light", (k, p) => { k.Disc(p - new Vector2(22, 0), 5f, BuoyColor("green")); k.Disc(p - new Vector2(9, 0), 5f, BuoyColor("red")); k.Disc(p + new Vector2(4, 0), 5f, BuoyColor("bell")); Light(k, p + new Vector2(22, 0), 7f); }),
            };
            float y = 200f;
            for (int i = 0; i < rows.Length; i++)
            {
                var at = new Vector2(-150 + 30, 450 - y);   // the KeyInk rect is centred on the column
                if (rows[i].draw != null) rows[i].draw(key, at);
                else if (i == 3) Icon(col, "cross", LostRed, new Vector2(30, -y), 30f);
                else Icon(col, "lanternSolid", LuredAmber, new Vector2(30, -y), 30f);
                if (i == 1) Glyph(col, "?", LostRed, new Vector2(64, -y - 1), 26);
                if (i == 2) Icon(col, "lanternSolid", LuredAmber, new Vector2(64, -y), 22f);
                var t = Label(col, rows[i].text, UiKit.BodyMedium, 22, Ink, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(i == 1 || i == 2 ? 84 : 70, -y), new Vector2(240, 30));
                t.rectTransform.pivot = new Vector2(0, 0.5f);
                y += 46f;
            }
            key.Apply();

            back = UiButton.Create(col, "Back to dawn", UiKit.Heading, 36, () => OnBack?.Invoke(), TextAnchor.MiddleCenter);
            back.Normal = Ink;
            back.Hover = new Color(0.55f, 0.3f, 0.1f);
            back.Label.GetComponent<Shadow>().enabled = false;
            back.IgnoreSpace = true;
            ((RectTransform)back.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(290, 56));
            FirstSelected = back;
            BuildReplay(col);
        }

        static readonly Color TrackInk = new Color(0.16f, 0.13f, 0.1f, 0.72f);

        static Color BuoyColor(string kind) => kind switch
        {
            "green" => new Color(0.18f, 0.48f, 0.26f),
            "red" => new Color(0.72f, 0.16f, 0.1f),
            _ => new Color(0.2f, 0.18f, 0.15f),
        };

        static void Reef(ChartInk k, Vector2 p, float r)
        {
            // A rock awash: a dark heart in a dotted ring of broken water.
            k.Disc(p, r * 0.55f, new Color(0.22f, 0.17f, 0.12f, 0.95f), 14);
            var ring = new List<Vector2>();
            for (int a = 0; a < 16; a++) ring.Add(p + new Vector2(Mathf.Cos(a * Mathf.PI / 8f), Mathf.Sin(a * Mathf.PI / 8f)) * r);
            k.Outline(ring, 1.6f, new Color(0.22f, 0.17f, 0.12f, 0.8f), 2f, 3f);
        }

        static void Shoal(ChartInk k, Vector2 p, Vector2 radii, float angleDeg)
        {
            var pts = new List<Vector2>();
            float a0 = angleDeg * Mathf.Deg2Rad;
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2f / 28f;
                var l = new Vector2(Mathf.Cos(a) * radii.x, Mathf.Sin(a) * radii.y);
                pts.Add(p + new Vector2(l.x * Mathf.Cos(a0) - l.y * Mathf.Sin(a0), l.x * Mathf.Sin(a0) + l.y * Mathf.Cos(a0)));
            }
            k.Polygon(pts, new Color(0.84f, 0.72f, 0.45f, 0.75f));
            k.Outline(pts, 1.8f, new Color(0.3f, 0.23f, 0.15f, 0.85f), 3f, 3f);
        }

        static void Light(ChartInk k, Vector2 p, float r)
        {
            k.Disc(p, r + 2.5f, Ink);
            k.Disc(p, r, new Color(0.95f, 0.72f, 0.25f));
        }

        static Image Icon(Transform parent, string icon, Color color, Vector2 pos, float size)
        {
            var img = UiKit.Image("Mark", parent, SpriteFactory.Icon(icon), color);
            img.raycastTarget = false;
            img.rectTransform.Pin(new Vector2(0, 1), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            return img;
        }

        static Text Glyph(Transform parent, string glyph, Color color, Vector2 pos, int size)
        {
            var t = Label(parent, glyph, UiKit.BodyBold, size, color, TextAnchor.MiddleCenter, new Vector2(0, 1), pos, new Vector2(size + 8, size + 8));
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Where a point of the bay lies on the chart, in the chart area's local space
        /// (its centre is 0,0). East is right and north is up, as from the light.</summary>
        public Vector2 Project(Vector2 world) => (world - centre) * scale;

        public void Setup(MapData map, SimWorld w, NightLog log, string title, string subtitle)
        {
            heading.text = "The night's chart";
            sub.text = title + "\n" + subtitle;
            wreckMarks.Clear();
            placeMarks.Clear();
            pendingNames.Clear();
            keepClear.Clear();
            LostMarks = LuredMarks = 0;
            foreach (Transform t in marks) Destroy(t.gameObject);
            foreach (var k in inks) Destroy(k.gameObject);
            inks.Clear();
            lanterns.Clear();
            crosses.Clear();
            DrawBay(map, w, log);
            DrawTracks(w, log);
            PlaceNames();
            SetupReplay(map, log);
        }

        void PlaceNames()
        {
            if (!Hud.NamesMakeRoom) return;
            var half = area.sizeDelta * 0.5f;
            var bounds = new Rect(-half, area.sizeDelta);
            var placed = new List<Rect>(keepClear);
            var order = new List<int>();
            for (int i = 0; i < pendingNames.Count; i++) order.Add(i);
            order.Sort((a, b) => pendingNames[a].rank != pendingNames[b].rank ? pendingNames[a].rank.CompareTo(pendingNames[b].rank) : a.CompareTo(b));
            var steps = new List<Vector2>();
            foreach (int i in order)
            {
                var rt = pendingNames[i].text.rectTransform;
                var size = rt.sizeDelta;
                // Up and down a line at a time, then off to either side.
                steps.Clear();
                steps.AddRange(LabelPlacer.Vertical(size.y, 2));
                float side = size.x * 0.5f + 20f;
                foreach (float dy in new[] { 0f, size.y, -size.y })
                {
                    steps.Add(new Vector2(side, dy));
                    steps.Add(new Vector2(-side, dy));
                }
                steps.Add(new Vector2(0f, 3f * size.y));
                steps.Add(new Vector2(0f, -3f * size.y));
                var rect = new Rect(rt.anchoredPosition - size * 0.5f, size);
                rt.anchoredPosition += LabelPlacer.Place(rect, steps, placed, bounds);
            }
        }

        void KeepClear(Vector2 p, Vector2 size) => keepClear.Add(new Rect(p - size * 0.5f, size));

        void DrawBay(MapData map, SimWorld w, NightLog log)
        {
            var k = baseInk;
            k.Clear();
            var half = area.sizeDelta * 0.5f;
            k.Polygon(new[] { -half, new Vector2(half.x, -half.y), half, new Vector2(-half.x, half.y) }, Sea);
            foreach (var poly in map.Land)
            {
                var pts = new List<Vector2>(poly.Length);
                foreach (var p in poly) pts.Add(Project(p));
                k.Polygon(pts, Land);
                k.Outline(pts, 2.4f, Coast);
            }
            // Sea stacks and the harbour are on every chart.
            foreach (var s in map.Stacks)
            {
                float r = Mathf.Max(5f, s.Radius * scale);
                k.Disc(Project(s.Pos), r, new Color(0.36f, 0.3f, 0.22f, 0.95f), 16);
                KeepClear(Project(s.Pos), Vector2.one * r * 1.6f);
                Place(s.Name, Project(s.Pos) + new Vector2(0, -r - 14f), 18, InkSoft, rank: RankStack);
            }
            Place("Porthkell", Project(map.Harbor) + new Vector2(0, 26f), 22, Ink, rank: RankPlace);
            foreach (var b in map.Buoys) k.Disc(Project(new Vector2(b.x, b.z)), 5f, BuoyColor(b.kind), 12);
            Light(k, Project(map.Lighthouse), 8f);
            KeepClear(Project(map.Lighthouse), new Vector2(22f, 22f));
            Place("Gannet Head", Project(map.Lighthouse) + new Vector2(0, -24f), 20, Ink, rank: RankPlace);

            // Only what the keeper saw tonight: the reefs and sands charted, and the lanterns lit.
            var groups = new Dictionary<string, (Vector2 top, string name)>();
            foreach (int i in log.ChartedReefs)
            {
                var r = w.Reefs[i];
                var p = Project(r.Pos);
                Reef(k, p, Mathf.Max(8f, r.Radius * scale));
                string name = SimWorld.ReefGroupName(r.Group);
                if (!groups.TryGetValue(r.Group, out var g) || p.y > g.top.y) groups[r.Group] = (p, name);
            }
            foreach (var g in groups.Values)
            {
                string n = g.name.StartsWith("the ") ? "The " + g.name.Substring(4) : g.name;
                Place(n, g.top + new Vector2(0, 30f), 21, Ink, rank: RankHazard);
            }
            foreach (int i in log.ChartedShoals)
            {
                var s = w.Shoals[i].Def;
                Shoal(k, Project(s.Pos), new Vector2(s.Rx, s.Rz) * scale, s.Angle);
                Place(s.Name, Project(s.Pos), 21, Ink, rank: RankHazard);
            }
            foreach (var site in map.WreckerSites.Values)
            {
                if (!log.BurnedSites.Contains(site.Id)) continue;
                var p = Project(site.Pos);
                var img = UiKit.Image("Lantern", marks, SpriteFactory.Icon("lanternSolid"), LuredAmber);
                img.raycastTarget = false;
                img.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), p, new Vector2(30, 30));
                lanterns[site.Id] = img;
                KeepClear(p, new Vector2(30f, 30f));
                Place(site.Name, p + new Vector2(0, 28f), 20, NameAmber, rank: RankWrecker);
            }
            k.Apply();
        }

        void DrawTracks(SimWorld w, NightLog log)
        {
            var ink = NewInk();
            var named = new HashSet<SimShip>();
            // Plain stretches first, so the lost and lured ones are drawn over them.
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var t in log.Tracks)
                {
                    var pts = t.Points;
                    float phase = 0f;   // how far into the dash pattern this stretch has run, so dashes run on
                    for (int i = 1; i < pts.Count; i++)
                    {
                        var state = pts[i - 1].State;
                        if (i == 1 || state != pts[i - 2].State) phase = 0f;
                        bool trouble = state == ShipState.Lost || state == ShipState.Lured;
                        var a = Project(pts[i - 1].Pos);
                        var b = Project(pts[i].Pos);
                        float len = Vector2.Distance(a, b);
                        if (trouble == (pass == 1))
                        {
                            if (ink.Full) { ink.Apply(); ink = NewInk(); }
                            if (state == ShipState.Lost) ink.Polyline(new[] { a, b }, 3.4f, LostRed, 3f, 5f, phase);
                            else if (state == ShipState.Lured) ink.Polyline(new[] { a, b }, 3.4f, LuredAmber, 11f, 6f, phase);
                            else ink.Line(a, b, 2.4f, TrackInk);
                        }
                        phase += len;
                    }
                }
            }
            ink.Apply();

            // Marks where trouble began, and at each wreck; a ship's name once, by its worst moment.
            foreach (var t in log.Tracks)
            {
                var pts = t.Points;
                for (int i = 1; i < pts.Count; i++)
                {
                    if (pts[i].State == pts[i - 1].State) continue;
                    var p = Project(pts[i].Pos);
                    if (pts[i].State == ShipState.Lost) { Glyph(marks, "?", LostRed, p + new Vector2(0, 16f), 28, true); LostMarks++; }
                    else if (pts[i].State == ShipState.Lured) { Mark("lanternSolid", LuredAmber, p + new Vector2(0, 16f), 24f); LuredMarks++; }
                }
                var ship = t.Ship;
                if (ship.State == ShipState.Wrecked)
                {
                    var p = Project(pts[pts.Count - 1].Pos);
                    var cross = Mark("cross", LostRed, p, 32f);
                    wreckMarks.Add((ship.Name, cross.rectTransform));
                    crosses.Add((cross, pts[pts.Count - 1].Time));
                    Place(ship.Name, p + new Vector2(0, -31f), 21, NameRed, true, UiKit.BodyBold, RankWreck);
                    named.Add(ship);
                }
            }
            foreach (var t in log.Tracks)
            {
                if (named.Contains(t.Ship) || t.Ship.SteadyHand) continue;
                // A ship that wavered but came through: its name where the trouble began.
                var pts = t.Points;
                for (int i = 1; i < pts.Count; i++)
                {
                    if (pts[i].State == pts[i - 1].State || (pts[i].State != ShipState.Lost && pts[i].State != ShipState.Lured)) continue;
                    var c = pts[i].State == ShipState.Lost ? NameRed : NameAmber;
                    Place(t.Ship.Name, Project(pts[i].Pos) + new Vector2(0, 42f), 20, c, true, UiKit.BodyBold, RankWavered);
                    break;
                }
            }
        }

        ChartInk NewInk()
        {
            var k = ChartInk.Create("Tracks", area);
            k.transform.SetSiblingIndex(marks.GetSiblingIndex());
            inks.Add(k);
            return k;
        }

        Image Mark(string icon, Color color, Vector2 p, float size)
        {
            var img = UiKit.Image("Mark", marks, SpriteFactory.Icon(icon), color);
            img.raycastTarget = false;
            img.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), p, new Vector2(size, size));
            KeepClear(p, new Vector2(size, size));
            return img;
        }

        Text Glyph(Transform parent, string glyph, Color color, Vector2 pos, int size, bool centred)
        {
            var t = Label(parent, glyph, UiKit.BodyBold, size, color, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), pos, new Vector2(size + 8, size + 8));
            t.raycastTarget = false;
            KeepClear(pos, new Vector2(size * 0.6f, size));
            return t;
        }

        Text Place(string name, Vector2 p, int size, Color color, bool halo = false, Font font = null, int rank = RankPlace)
        {
            var t = Label(marks, name, font ?? UiKit.Italic, size, color, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), p, new Vector2(360, size + 10));
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            // As wide as the words, so names can be set close without covering each other.
            t.rectTransform.sizeDelta = new Vector2(t.preferredWidth + 6f, size + 4f);
            pendingNames.Add((t, rank));
            if (halo)
            {
                // A paper-coloured edge keeps a name readable where it crosses a track.
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.86f, 0.82f, 0.72f, 0.7f);
                o.effectDistance = new Vector2(1.2f, -1.2f);
            }
            placeMarks.Add((name, t.rectTransform));
            return t;
        }

        // ---------------------------------------------------------------- replay

        NightLog log;
        Vector2 lightPos;
        RectTransform live;
        ChartInk liveInk, beamInk;
        UiButton replayButton;
        UiSlider timeline;
        Text timeText, momentText, controlsText;
        RectTransform ticks;
        Image touchHit;
        List<NightLog.Moment> moments = new List<NightLog.Moment>();
        int controlsFor = -1;   // the device the replay's controls line was written for: 0 keys, 1 pad, 2 touch
        /// <summary>A jump to a moment lands this many seconds of the night before it.</summary>
        public const float MomentLead = 6f;
        readonly Dictionary<string, Image> lanterns = new Dictionary<string, Image>();
        readonly List<(Image cross, float time)> crosses = new List<(Image, float)>();
        // One mark per ship that sailed: its name, and a "?" or lantern while lost or lured.
        readonly List<(NightLog.Track track, Text name, Text lost, Image lured)> shipMarks = new List<(NightLog.Track, Text, Text, Image)>();
        bool replaying, playing;
        float replayTime;

        /// <summary>The replay runs at this many times the night's speed, or faster for a long
        /// watch, so no replay takes much more than 45 seconds.</summary>
        public float ReplayRate => log == null ? 6f : Mathf.Max(6f, log.Duration / 45f);
        static readonly Color BeamFill = new Color(0.98f, 0.8f, 0.36f, 0.3f);
        static readonly Color BeamEdge = new Color(0.72f, 0.5f, 0.12f, 0.55f);
        static readonly Color PaperHalo = new Color(0.86f, 0.82f, 0.72f, 0.85f);

        void BuildReplay(RectTransform col)
        {
            replayButton = UiButton.Create(col, "Replay the night", UiKit.Heading, 32, ToggleReplay, TextAnchor.MiddleCenter);
            replayButton.Normal = Ink;
            replayButton.Hover = new Color(0.55f, 0.3f, 0.1f);
            replayButton.Label.GetComponent<Shadow>().enabled = false;
            replayButton.IgnoreSpace = true;
            ((RectTransform)replayButton.transform).Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -590), new Vector2(290, 50));

            timeline = UiSlider.Create(col, 0f, v => Scrub(v));
            ((RectTransform)timeline.transform).Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -640), new Vector2(290, 40));
            // The slider is drawn for a dark panel; on paper its track wants ink.
            foreach (var img in timeline.GetComponentsInChildren<Image>())
            {
                if (img.name == "Bg") img.color = new Color(Ink.r, Ink.g, Ink.b, 0.22f);
                if (img.name == "Fill") img.color = new Color(0.62f, 0.42f, 0.14f);
            }
            timeline.KnobIdle = Ink;
            timeline.KnobHot = new Color(0.55f, 0.3f, 0.1f);
            // The night's moments as ticks along the timeline, so they're easy to find: wrecks tall
            // and red, ships lost (red) or lured (amber) short.
            ticks = UiKit.Rect("Ticks", timeline.transform).Stretch(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(12, -12), new Vector2(-12, 12));
            ticks.SetAsFirstSibling();
            // For a finger, more of the timeline takes a touch (taller than it's drawn, down to the
            // clock under it), and a tap by a tick jumps to that moment.
            touchHit = UiKit.Image("TouchHit", timeline.transform, null, new Color(0, 0, 0, 0));
            touchHit.raycastTarget = true;
            touchHit.rectTransform.Stretch(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, -38), new Vector2(16, 26));
            touchHit.gameObject.SetActive(false);
            timeline.SnapPress = SnapToMoment;
            timeText = Label(col, "", UiKit.BodyMedium, 21, InkSoft, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -672), new Vector2(290, 30));
            timeText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            // The moment ahead (or just past), in words; and the replay's keys.
            momentText = Label(col, "", UiKit.Italic, 20, Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -716), new Vector2(300, 50));
            momentText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            momentText.lineSpacing = 0.95f;
            controlsText = Label(col, "", UiKit.BodyMedium, 18, InkSoft, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -775), new Vector2(300, 44));
            controlsText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            controlsText.lineSpacing = 1f;

            Navigation Nav(Selectable up, Selectable down) => new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };
            replayButton.navigation = Nav(back, timeline);
            timeline.navigation = Nav(replayButton, back);
            back.navigation = Nav(timeline, replayButton);

            // The light goes under the tracks and marks; the ships and their names on top.
            beamInk = ChartInk.Create("Light", area);
            live = UiKit.Rect("Replay", area).Fill();
            liveInk = ChartInk.Create("Live", live);
        }

        void SetupReplay(MapData map, NightLog log)
        {
            this.log = log;
            lightPos = map.Lighthouse;
            foreach (var m in shipMarks) { Destroy(m.name.gameObject); Destroy(m.lost.gameObject); Destroy(m.lured.gameObject); }
            shipMarks.Clear();
            foreach (Transform t in ticks) Destroy(t.gameObject);
            live.SetAsLastSibling();
            beamInk.transform.SetSiblingIndex(baseInk.transform.GetSiblingIndex() + 1);
            foreach (var t in log.Tracks)
            {
                var name = Label(live, t.Ship.Name, UiKit.BodyBold, 17, Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 24));
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                name.raycastTarget = false;
                var o = name.gameObject.AddComponent<Outline>();
                o.effectColor = PaperHalo;
                o.effectDistance = new Vector2(1.3f, -1.3f);
                var lost = Label(live, "?", UiKit.BodyBold, 24, LostRed, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28, 30));
                lost.raycastTarget = false;
                var lured = UiKit.Image("Lured", live, SpriteFactory.Icon("lanternSolid"), LuredAmber);
                lured.raycastTarget = false;
                lured.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
                shipMarks.Add((t, name, lost, lured));
            }
            moments = log.Moments();
            foreach (var m in moments)
            {
                bool wreck = m.Kind == NightLog.MomentKind.Wrecked;
                var tick = UiKit.Image(m.Kind.ToString(), ticks, null, m.Kind == NightLog.MomentKind.Lured ? LuredAmber : LostRed);
                tick.raycastTarget = false;
                float x = log.Duration > 0f ? m.Time / log.Duration : 0f;
                tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(x, 0.5f);
                tick.rectTransform.sizeDelta = wreck ? new Vector2(3, 22) : new Vector2(2, 10);
                tick.rectTransform.anchoredPosition = wreck ? Vector2.zero : new Vector2(0, -12);
                if (wreck) tick.transform.SetAsLastSibling();
            }
            timeline.Step = log.Duration > 0f ? Mathf.Clamp(5f / log.Duration, 0.005f, 0.05f) : 0.05f;
            replaying = playing = false;
            replayTime = 0f;
            ApplyReplay();
        }

        /// <summary>Replay: play from the start (or on from where it was stopped), or pause.</summary>
        public void ToggleReplay()
        {
            if (log == null) return;
            if (playing) { playing = false; ApplyReplay(); return; }
            if (!replaying || replayTime >= log.Duration - 0.01f) replayTime = 0f;
            replaying = playing = true;
            ApplyReplay();
        }

        void Scrub(float fraction)
        {
            if (log == null) return;
            replaying = true;
            playing = false;
            replayTime = fraction * log.Duration;
            ApplyReplay();
        }

        /// <summary>For tours: hold the replay at a moment of the night.</summary>
        public void SetReplayTime(float time)
        {
            if (log == null) return;
            replaying = true;
            playing = false;
            replayTime = Mathf.Clamp(time, 0f, log.Duration);
            ApplyReplay();
        }

        /// <summary>Jumps to a few seconds before the next (or previous) moment: a ship lost, lured
        /// or wrecked. Before the first moment, back goes to the start. A playing replay plays on.</summary>
        public void JumpMoment(int direction)
        {
            if (log == null) return;
            float to = -1f;
            if (direction > 0)
            {
                foreach (var m in moments)
                    if (Mathf.Max(0f, m.Time - MomentLead) > replayTime + 0.05f) { to = Mathf.Max(0f, m.Time - MomentLead); break; }
                if (to < 0f) return;   // nothing more happens
            }
            else
            {
                to = 0f;
                for (int i = moments.Count - 1; i >= 0; i--)
                    if (Mathf.Max(0f, moments[i].Time - MomentLead) < replayTime - 0.05f) { to = Mathf.Max(0f, moments[i].Time - MomentLead); break; }
            }
            replaying = true;
            replayTime = Mathf.Clamp(to, 0f, log.Duration);
            ApplyReplay();
            Sfx.Play("ui_tick", 0.35f, 1.1f, 0, Bus.Ui, 0f);
        }

        static int ControlsDevice => InputMode.Pad ? 1 : InputMode.Touch ? 2 : 0;

        /// <summary>A finger's tap on the timeline: within a few percent of a moment's tick it
        /// jumps to just before that moment (as Q and E do); elsewhere it stays where it landed.</summary>
        float SnapToMoment(float fraction)
        {
            if (log == null || log.Duration <= 0f) return fraction;
            float best = 0.045f, to = fraction;
            foreach (var m in moments)
            {
                float d = Mathf.Abs(m.Time / log.Duration - fraction);
                if (d < best) { best = d; to = Mathf.Max(0f, m.Time - MomentLead) / log.Duration; }
            }
            return to;
        }

        /// <summary>Steps the replay by some seconds of the night, paused (as the timeline's arrows do).</summary>
        public void StepReplay(float seconds)
        {
            if (log == null) return;
            SetReplayTime(replayTime + seconds);
        }

        // Space (or the pad's left face button) plays and pauses wherever the selection is; Q and E,
        // Page Up and Down, or the shoulder buttons jump between moments; the arrows and the d-pad
        // step it even when the timeline isn't chosen (when it is, it steps itself).
        void ReplayKeys()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            var es = EventSystem.current;
            bool onTimeline = es != null && es.currentSelectedGameObject == timeline.gameObject;
            if ((kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)) ToggleReplay();
            if ((kb != null && (kb.eKey.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame)) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) JumpMoment(1);
            if ((kb != null && (kb.qKey.wasPressedThisFrame || kb.pageUpKey.wasPressedThisFrame)) || (pad != null && pad.leftShoulder.wasPressedThisFrame)) JumpMoment(-1);
            if (onTimeline) return;
            if ((kb != null && kb.rightArrowKey.wasPressedThisFrame) || (pad != null && pad.dpad.right.wasPressedThisFrame)) StepReplay(5f);
            if ((kb != null && kb.leftArrowKey.wasPressedThisFrame) || (pad != null && pad.dpad.left.wasPressedThisFrame)) StepReplay(-5f);
        }

        void Update()
        {
            if (!Visible || log == null) return;
            ReplayKeys();
            if (controlsFor != ControlsDevice) ShowControls();
            if (Platform.IsWeb) Platform.ChartState(replayTime, log.Duration, playing);
            if (!playing) return;
            replayTime += Unscaled.Delta * ReplayRate;
            if (replayTime >= log.Duration) { replayTime = log.Duration; playing = false; }
            ApplyReplay();
        }

        void ApplyReplay()
        {
            if (log == null) return;
            string clock = $"{UiKit.Clock(replayTime)} of {UiKit.Clock(log.Duration)}";
            timeText.text = replaying ? clock : $"The night ran {UiKit.Clock(log.Duration)}";
            momentText.text = MomentLine();
            replayButton.Label.text = playing ? "Pause" : replaying && replayTime > 0.01f && replayTime < log.Duration - 0.01f ? "Play on" : "Replay the night";
            timeline.Value = log.Duration > 0f ? replayTime / log.Duration : 0f;
            // The whole night faint underneath while it plays back; as it was once it's done.
            foreach (var k in inks) k.canvasRenderer.SetAlpha(replaying ? 0.32f : 1f);
            foreach (var (cross, time) in crosses) cross.canvasRenderer.SetAlpha(!replaying || replayTime >= time ? 1f : 0f);
            foreach (var kv in lanterns)
                kv.Value.canvasRenderer.SetAlpha(!replaying ? 1f : log.BurningAt(kv.Key, replayTime) ? 1f : 0.22f);

            liveInk.Clear();
            beamInk.Clear();
            if (replaying)
            {
                var b = log.BeamAt(replayTime);
                float half = Mathf.Lerp(SimBeam.WideHalfDeg, SimBeam.FocusHalfDeg, b.Focus) * Mathf.Deg2Rad;
                var fan = new List<Vector2> { Project(lightPos) };
                for (int i = 0; i <= 12; i++) fan.Add(Project(lightPos + Geo.Dir(b.Bearing - half + 2f * half * i / 12f) * b.Range));
                beamInk.Polygon(fan, BeamFill);
                beamInk.Line(fan[0], fan[1], 1.6f, BeamEdge);
                beamInk.Line(fan[0], fan[fan.Count - 1], 1.6f, BeamEdge);
                BeamShown = b;
            }
            foreach (var (track, name, lost, lured) in shipMarks)
            {
                bool shown = replaying && NightLog.ShipAt(track, replayTime, out var pos, out var state) && state != ShipState.Wrecked;
                name.enabled = lost.enabled = lured.enabled = false;
                if (!shown) continue;
                NightLog.ShipAt(track, replayTime, out pos, out state);
                var p = Project(pos);
                var c = state == ShipState.Lost ? LostRed : state == ShipState.Lured ? LuredAmber : Ink;
                liveInk.Disc(p, 9.5f, PaperHalo, 16);
                liveInk.Disc(p, 7f, c, 16);
                name.enabled = true;
                name.color = state == ShipState.Lost ? NameRed : state == ShipState.Lured ? NameAmber : Ink;
                name.rectTransform.anchoredPosition = p + new Vector2(0, -20f);
                if (state == ShipState.Lost) { lost.enabled = true; lost.rectTransform.anchoredPosition = p + new Vector2(16f, 14f); }
                if (state == ShipState.Lured) { lured.enabled = true; lured.rectTransform.anchoredPosition = p + new Vector2(16f, 14f); }
            }
            liveInk.Apply();
            beamInk.Apply();
        }

        /// <summary>The moment ahead, or the one just past: "Next at 1:47: the Dunlin struck Widow's Ledge".</summary>
        string MomentLine()
        {
            if (moments.Count == 0) return "A clean night: no ship lost its way.";
            // The moment just jumped to (a few seconds ahead) first; otherwise one just past, for a
            // couple of seconds; otherwise the next.
            int next = -1, past = -1;
            for (int i = 0; i < moments.Count; i++)
            {
                if (moments[i].Time > replayTime + 0.05f) { next = i; break; }
                if (moments[i].Time >= replayTime - 2f) past = i;
            }
            int shown = next >= 0 && moments[next].Time - replayTime <= MomentLead + 0.1f ? next : past >= 0 ? past : next;
            if (shown < 0) return "Nothing more went wrong.";
            var m = moments[shown];
            string when = !replaying ? $"First, at {UiKit.Clock(m.Time)}" : m.Time > replayTime + 0.05f ? $"Next at {UiKit.Clock(m.Time)}" : UiKit.Clock(m.Time);
            return $"{when}: {MomentWords(m)}";
        }

        public static string MomentWords(NightLog.Moment m) => m.Kind switch
        {
            NightLog.MomentKind.Wrecked => $"the {m.Ship.Name} {Debrief.Hit(m.Ship)}",
            NightLog.MomentKind.Lured => $"the {m.Ship.Name} was lured",
            _ => m.AfterLure ? $"the {m.Ship.Name}, freed, lost its way" : $"the {m.Ship.Name} lost its way",
        };

        /// <summary>The replay's keys, or the pad's buttons in the chosen style's names.</summary>
        void ShowControls()
        {
            controlsFor = ControlsDevice;
            controlsText.text = InputMode.Pick(
                $"Space plays  ·  ← → step 5 s\n{KeyBindings.KeyName(Key.Q)} and {KeyBindings.KeyName(Key.E)} jump to each moment",
                $"{PadButtons.West} plays  ·  d-pad steps 5 s\n{PadButtons.LeftShoulder} and {PadButtons.RightShoulder} jump to each moment",
                "Drag the timeline to scrub\ntap by a mark to jump to that moment");
            if (touchHit != null) touchHit.gameObject.SetActive(InputMode.Touch);
        }

        // For tours.
        public bool Replaying => replaying;
        public string MomentShown => momentText.text;
        public string ControlsShown => controlsText.text;
        public IReadOnlyList<NightLog.Moment> ReplayMoments => moments;
        public bool ReplayPlaying => playing;
        public float ReplayTime => replayTime;
        public float ReplayDuration => log != null ? log.Duration : 0f;
        public NightLog.BeamSample BeamShown { get; private set; }
        public UiButton ReplayButton => replayButton;
        public UiButton BackButton => back;
        public UiSlider Timeline => timeline;
        public string TimeShown => timeText.text;
        /// <summary>A ship's mark in the replay: whether it's showing, where (chart area space), and
        /// whether its "?" or lantern is up.</summary>
        public (bool shown, Vector2 pos, bool lost, bool lured) ReplayMark(string ship)
        {
            foreach (var (track, name, lost, lured) in shipMarks)
                if (track.Ship.Name == ship) return (name.enabled, name.rectTransform.anchoredPosition + new Vector2(0, 20f), lost.enabled, lured.enabled);
            return (false, Vector2.zero, false, false);
        }
        public bool CrossShown(string ship)
        {
            for (int i = 0; i < wreckMarks.Count; i++)
                if (wreckMarks[i].ship == ship) return wreckMarks[i].mark.GetComponent<Image>().canvasRenderer.GetAlpha() > 0.5f;
            return false;
        }

        public override void Show()
        {
            base.Show();
            ShowControls();
            panel.localScale = Vector3.one * 0.96f;
            Tween.Scale(panel, 0.96f, 1f, 0.45f, 0f, Tween.EaseOutBack);
            Sfx.Play("ui_page", 0.6f);
        }

        // For tours.
        public IReadOnlyList<(string ship, RectTransform mark)> WreckMarks => wreckMarks;
        public IReadOnlyList<(string place, RectTransform mark)> PlaceMarks => placeMarks;
        public RectTransform PlaceMark(string place)
        {
            foreach (var p in placeMarks) if (p.place == place) return p.mark;
            return null;
        }
        public RectTransform Area => area;
        public void Back() => OnBack?.Invoke();
    }
}
