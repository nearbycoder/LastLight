using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>
    /// Vector ink for the dawn chart: filled polygons, lines (solid or dashed) and discs, built as
    /// one UI mesh in the graphic's local space. Crisp at any screen size, and no textures.
    /// </summary>
    public sealed class ChartInk : MaskableGraphic
    {
        readonly List<UIVertex> verts = new List<UIVertex>();
        readonly List<int> tris = new List<int>();

        /// <summary>Vertices so far. A UI mesh holds about 65,000, so callers start a new graphic
        /// once one passes <see cref="Full"/>.</summary>
        public int Vertices => verts.Count;
        public bool Full => verts.Count > 56000;

        public static ChartInk Create(string name, Transform parent)
        {
            var rt = UiKit.Rect(name, parent).Fill();
            var ink = rt.gameObject.AddComponent<ChartInk>();
            ink.raycastTarget = false;
            return ink;
        }

        public void Clear()
        {
            verts.Clear();
            tris.Clear();
            SetVerticesDirty();
        }

        public void Apply() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (verts.Count == 0) return;
            vh.AddUIVertexStream(verts, tris);
        }

        int Vert(Vector2 p, Color32 c)
        {
            verts.Add(new UIVertex { position = p, color = c, uv0 = Vector2.zero });
            return verts.Count - 1;
        }

        /// <summary>A filled polygon (simple, either winding), by ear clipping.</summary>
        public void Polygon(IList<Vector2> pts, Color color)
        {
            int n = pts.Count;
            if (n < 3) return;
            Color32 c = color;
            int start = verts.Count;
            foreach (var p in pts) Vert(p, c);
            var idx = new List<int>(n);
            for (int i = 0; i < n; i++) idx.Add(i);
            float area = 0f;
            for (int i = 0; i < n; i++) { var a = pts[i]; var b = pts[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            float sign = area >= 0f ? 1f : -1f;
            int guard = n * n;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = pts[i0], b = pts[i1], cc = pts[i2];
                    if (Cross(b - a, cc - b) * sign <= 0f) continue;   // a reflex corner
                    bool inside = false;
                    foreach (int j in idx)
                    {
                        if (j == i0 || j == i1 || j == i2) continue;
                        if (InTriangle(pts[j], a, b, cc)) { inside = true; break; }
                    }
                    if (inside) continue;
                    tris.Add(start + i0); tris.Add(start + i1); tris.Add(start + i2);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;   // degenerate outline: leave the rest unfilled
            }
            if (idx.Count == 3) { tris.Add(start + idx[0]); tris.Add(start + idx[1]); tris.Add(start + idx[2]); }
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        /// <summary>A straight stroke from a to b.</summary>
        public void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            var n = new Vector2(-d.y, d.x) / len * (width * 0.5f);
            // Square ends half a width long, so joined strokes meet without gaps.
            var e = d / len * (width * 0.5f);
            Color32 c = color;
            int i = Vert(a - e - n, c);
            Vert(a - e + n, c);
            Vert(b + e + n, c);
            Vert(b + e - n, c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i + 2); tris.Add(i + 3); tris.Add(i);
        }

        /// <summary>A polyline, solid when <paramref name="gap"/> is 0, otherwise dashes of
        /// <paramref name="dash"/> with gaps between; <paramref name="phase"/> carries the pattern
        /// on from one call to the next so a track's dashes run on unbroken.</summary>
        public void Polyline(IList<Vector2> pts, float width, Color color, float dash = 0f, float gap = 0f, float phase = 0f)
        {
            if (gap <= 0f)
            {
                for (int i = 1; i < pts.Count; i++) Line(pts[i - 1], pts[i], width, color);
                return;
            }
            float period = dash + gap, t = phase % period;
            for (int i = 1; i < pts.Count; i++)
            {
                var a = pts[i - 1];
                var d = pts[i] - a;
                float len = d.magnitude, at = 0f;
                if (len < 1e-4f) continue;
                var dir = d / len;
                while (at < len)
                {
                    float left = t < dash ? dash - t : period - t;
                    float step = Mathf.Min(left, len - at);
                    if (t < dash) Line(a + dir * at, a + dir * (at + step), width, color);
                    at += step;
                    t = (t + step) % period;
                }
            }
        }

        /// <summary>A filled circle.</summary>
        public void Disc(Vector2 c, float r, Color color, int sides = 20)
        {
            Color32 col = color;
            int centre = Vert(c, col);
            for (int k = 0; k <= sides; k++)
            {
                float a = k * Mathf.PI * 2f / sides;
                Vert(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, col);
                if (k > 0) { tris.Add(centre); tris.Add(centre + k + 1); tris.Add(centre + k); }
            }
        }

        /// <summary>An outline through the given points, closed.</summary>
        public void Outline(IList<Vector2> pts, float width, Color color, float dash = 0f, float gap = 0f)
        {
            var closed = new List<Vector2>(pts) { pts[0] };
            Polyline(closed, width, color, dash, gap);
        }
    }
}
