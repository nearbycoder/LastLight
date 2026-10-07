using System;
using System.Collections.Generic;
using LastLight.Audio;
using LastLight.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>
    /// The night's chart, opened from the dawn card: Merrow Bay on paper with every ship's track,
    /// inked solid while the captain was steering, dotted red with a "?" where they lost their way
    /// and dashed amber with a lantern where a false light had them, and a cross at each wreck.
    /// Only what the keeper saw is drawn: reefs and sandbanks charted tonight, lanterns that burned.
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
            float y = 210f;
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
                y += 50f;
            }
            key.Apply();

            back = UiButton.Create(col, "Back to dawn", UiKit.Heading, 36, () => OnBack?.Invoke(), TextAnchor.MiddleCenter);
            back.Normal = Ink;
            back.Hover = new Color(0.55f, 0.3f, 0.1f);
            back.Label.GetComponent<Shadow>().enabled = false;
            ((RectTransform)back.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(290, 56));
            FirstSelected = back;
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
            DrawBay(map, w, log);
            DrawTracks(w, log);
            PlaceNames();
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

        public override void Show()
        {
            base.Show();
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
