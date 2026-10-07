using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastLight.UI
{
    /// <summary>
    /// Procedural UI art drawn from signed-distance shapes: panels, rings, glows, the lighthouse
    /// lamp icon used for ratings, ship silhouettes for the manifest, key caps and parchment.
    /// </summary>
    public static class SpriteFactory
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        static Sprite Cached(string key, Func<Sprite> make)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            s = make();
            cache[key] = s;
            return s;
        }

        /// <summary>Rasterise an SDF (negative inside) into a white sprite with soft anti-aliased edges.</summary>
        static Texture2D Raster(int w, int h, Func<float, float, float> sdf, float soft = 1.2f, Func<float, float, Color> tint = null)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float d = sdf(fx, fy);
                    float a = Mathf.Clamp01(0.5f - d / soft);
                    var c = tint != null ? tint(fx, fy) : Color.white;
                    c.a *= a;
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static Sprite Make(Texture2D tex, Vector4 border = default)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        // ---------------------------------------------------------------- SDF primitives

        public static float Circle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        public static float Box(float x, float y, float cx, float cy, float hw, float hh, float r = 0f)
        {
            float qx = Mathf.Abs(x - cx) - hw + r, qy = Mathf.Abs(y - cy) - hh + r;
            float ox = Mathf.Max(qx, 0), oy = Mathf.Max(qy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        public static float Polygon(float x, float y, Vector2[] p)
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                var a = p[j]; var b = p[i];
                var e = b - a;
                var w = new Vector2(x, y) - a;
                float t = Mathf.Clamp01(Vector2.Dot(w, e) / e.sqrMagnitude);
                d = Mathf.Min(d, (w - e * t).magnitude);
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? -d : d;
        }

        static float Union(float a, float b) => Mathf.Min(a, b);

        /// <summary>Distance to the segment a-b.</summary>
        public static float Segment(float x, float y, Vector2 a, Vector2 b)
        {
            var e = b - a;
            var w = new Vector2(x, y) - a;
            float t = Mathf.Clamp01(Vector2.Dot(w, e) / e.sqrMagnitude);
            return (w - e * t).magnitude;
        }

        // ---------------------------------------------------------------- sprites

        public static Sprite Rounded => Cached("rounded", () => Make(Raster(64, 64, (x, y) => Box(x, y, 32, 32, 31, 31, 14)), new Vector4(20, 20, 20, 20)));

        public static Sprite RoundedSmall => Cached("roundedSmall", () => Make(Raster(32, 32, (x, y) => Box(x, y, 16, 16, 15, 15, 6)), new Vector4(9, 9, 9, 9)));

        public static Sprite Pill => Cached("pill", () => Make(Raster(64, 32, (x, y) => Box(x, y, 32, 16, 31, 15, 15)), new Vector4(16, 15, 16, 15)));

        public static Sprite Disc => Cached("disc", () => Make(Raster(128, 128, (x, y) => Circle(x, y, 64, 64, 62))));

        public static Sprite Ring => Cached("ring", () => Make(Raster(128, 128, (x, y) => Mathf.Abs(Circle(x, y, 64, 64, 58)) - 4f)));

        public static Sprite ThinRing => Cached("thinring", () => Make(Raster(128, 128, (x, y) => Mathf.Abs(Circle(x, y, 64, 64, 60)) - 1.6f)));

        public static Sprite Glow => Cached("glow", () =>
        {
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float dx = (x + 0.5f - 64) / 64f, dy = (y + 0.5f - 64) / 64f;
                    float r2 = dx * dx + dy * dy;
                    px[y * 128 + x] = new Color(1, 1, 1, Mathf.Clamp01(Mathf.Exp(-r2 * 4.5f) * (1 - r2)));
                }
            tex.SetPixels(px);
            tex.Apply();
            return Make(tex);
        });

        /// <summary>Horizontal soft bar (fades at both ends), for underlines and separators.</summary>
        public static Sprite Bar => Cached("bar", () =>
        {
            var tex = new Texture2D(256, 8, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 256; x++)
                {
                    float u = x / 255f;
                    float a = Mathf.SmoothStep(0, 1, Mathf.Min(u, 1 - u) * 4f) * (1 - Mathf.Abs(y - 3.5f) / 4f);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            return Make(tex);
        });

        /// <summary>Vertical gradient (opaque at the bottom) for screen-edge vignettes behind text.</summary>
        public static Sprite Fade => Cached("fade", () =>
        {
            var tex = new Texture2D(4, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(1, 0, y / 127f)));
            tex.Apply();
            return Make(tex);
        });

        /// <summary>Horizontal gradient (opaque at the left).</summary>
        public static Sprite FadeH => Cached("fadeH", () =>
        {
            var tex = new Texture2D(128, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 128; x++)
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(1, 0, x / 127f)));
            tex.Apply();
            return Make(tex);
        });

        /// <summary>The lamp used for ratings: a small lighthouse lantern with a glowing lens.</summary>
        public static Sprite Lamp => Cached("lamp", () => Make(Raster(128, 128, (x, y) =>
        {
            float d = Box(x, y, 64, 34, 22, 6, 2);                         // gallery
            d = Union(d, Box(x, y, 64, 60, 15, 20, 3));                    // lantern glass
            d = Union(d, Polygon(x, y, new[] { new Vector2(44, 80), new Vector2(84, 80), new Vector2(64, 102) })); // roof
            d = Union(d, Circle(x, y, 64, 106, 5));                        // ventilator
            d = Union(d, Box(x, y, 64, 18, 16, 10, 2));                    // tower top
            return d;
        }, 1.4f, (x, y) => y > 42 && y < 78 ? new Color(1f, 0.92f, 0.7f) : new Color(0.85f, 0.85f, 0.85f))));

        public static Sprite LampEmpty => Cached("lampEmpty", () => Make(Raster(128, 128, (x, y) =>
        {
            float d = Box(x, y, 64, 34, 22, 6, 2);
            d = Union(d, Mathf.Abs(Box(x, y, 64, 60, 15, 20, 3)) - 2.5f);
            d = Union(d, Polygon(x, y, new[] { new Vector2(44, 80), new Vector2(84, 80), new Vector2(64, 102) }));
            d = Union(d, Circle(x, y, 64, 106, 5));
            d = Union(d, Box(x, y, 64, 18, 16, 10, 2));
            return d;
        }, 1.4f)));

        /// <summary>Side-on ship silhouettes for the manifest (bow to the right).</summary>
        public static Sprite Ship(string type) => Cached("ship_" + type, () => Make(Raster(160, 64, (x, y) =>
        {
            switch (type)
            {
                case "steamer":
                {
                    float d = Polygon(x, y, new[] { new Vector2(8, 22), new Vector2(150, 22), new Vector2(156, 34), new Vector2(4, 30) });
                    d = Union(d, Box(x, y, 74, 36, 16, 6, 1));
                    d = Union(d, Box(x, y, 60, 46, 6, 12, 1));             // funnel
                    d = Union(d, Box(x, y, 116, 44, 1.6f, 18, 0));         // masts
                    d = Union(d, Box(x, y, 26, 42, 1.6f, 15, 0));
                    return d;
                }
                case "ferry":
                {
                    float d = Polygon(x, y, new[] { new Vector2(14, 20), new Vector2(144, 20), new Vector2(154, 32), new Vector2(8, 30) });
                    d = Union(d, Box(x, y, 76, 36, 44, 6, 2));
                    d = Union(d, Box(x, y, 80, 46, 30, 5, 2));
                    d = Union(d, Box(x, y, 64, 54, 6, 8, 1));
                    d = Union(d, Box(x, y, 110, 52, 1.4f, 14, 0));
                    return d;
                }
                default:
                {
                    float d = Polygon(x, y, new[] { new Vector2(30, 18), new Vector2(122, 18), new Vector2(136, 36), new Vector2(26, 30) });
                    d = Union(d, Box(x, y, 66, 38, 14, 10, 2));
                    d = Union(d, Box(x, y, 104, 46, 1.6f, 20, 0));
                    d = Union(d, Box(x, y, 40, 40, 1.4f, 14, 0));
                    return d;
                }
            }
        }, 1.3f)));

        /// <summary>Icons for the briefing's "new tonight" card, drawn as line art.</summary>
        public static Sprite Icon(string name) => Cached("icon_" + name, () => Make(Raster(96, 96, (x, y) =>
        {
            switch (name)
            {
                case "reef":
                {
                    // A rock just breaking the surface, with swell lines either side.
                    float rock = Polygon(x, y, new[] { new Vector2(30, 40), new Vector2(42, 66), new Vector2(52, 58), new Vector2(60, 72), new Vector2(70, 40) });
                    float w1 = Mathf.Abs(y - 36f - 3.5f * Mathf.Sin(x * 0.32f)) - 2.2f;
                    float w2 = Mathf.Abs(y - 24f - 3.5f * Mathf.Sin(x * 0.32f + 1.6f)) - 2.2f;
                    w1 = Mathf.Max(w1, Box(x, y, 48, 36, 40, 10));
                    w2 = Mathf.Max(w2, Box(x, y, 48, 24, 32, 10));
                    return Union(Mathf.Max(rock, -(y - 40f)), Union(w1, w2));
                }
                case "buoy":
                {
                    float body = Polygon(x, y, new[] { new Vector2(34, 30), new Vector2(62, 30), new Vector2(52, 62), new Vector2(44, 62) });
                    float cage = Mathf.Abs(Box(x, y, 48, 70, 7, 7, 2)) - 1.8f;
                    float lamp = Circle(x, y, 48, 70, 3.2f);
                    float sea = Mathf.Max(Mathf.Abs(y - 24f - 3f * Mathf.Sin(x * 0.3f)) - 2.2f, Box(x, y, 48, 24, 38, 8));
                    float rays = Union(Box(x, y, 48, 87, 1.6f, 5), Union(Box(x, y, 33, 72, 5, 1.6f), Box(x, y, 63, 72, 5, 1.6f)));
                    return Union(Union(body, cage), Union(Union(lamp, sea), rays));
                }
                case "storm":
                    return Polygon(x, y, new[] { new Vector2(56, 88), new Vector2(30, 46), new Vector2(46, 46), new Vector2(36, 8), new Vector2(68, 56), new Vector2(51, 56), new Vector2(66, 88) });
                case "flare":
                {
                    // A falling flare under its smoke trail.
                    float star = Circle(x, y, 48, 40, 7f);
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4f;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        float along = (x - 48) * d.x + (y - 40) * d.y, across = Mathf.Abs(-(x - 48) * d.y + (y - 40) * d.x);
                        star = Union(star, Mathf.Max(across - (k % 2 == 0 ? 2.2f : 1.4f), Mathf.Abs(along - 18f) - (k % 2 == 0 ? 6f : 3.5f)));
                    }
                    float trail = Mathf.Max(Mathf.Abs(x - 48f - 4f * Mathf.Sin(y * 0.18f)) - 1.6f, Box(x, y, 48, 76, 6, 12));
                    return Union(star, trail);
                }
                case "lantern":
                {
                    // The wrecker's lantern.
                    float glass = Mathf.Abs(Box(x, y, 48, 46, 13, 17, 3)) - 2f;
                    float top = Polygon(x, y, new[] { new Vector2(33, 64), new Vector2(63, 64), new Vector2(48, 76) });
                    float ring = Mathf.Abs(Circle(x, y, 48, 81, 4.5f)) - 1.6f;
                    float flame = Circle(x, y, 48, 44, 5f);
                    float base_ = Box(x, y, 48, 26, 16, 3, 1);
                    return Union(Union(glass, top), Union(Union(ring, flame), base_));
                }
                case "lanternSolid":
                {
                    // The wrecker's lantern as a solid silhouette, for marks too small for line art.
                    float glass = Box(x, y, 48, 46, 18, 20, 4);
                    float top = Polygon(x, y, new[] { new Vector2(26, 64), new Vector2(70, 64), new Vector2(48, 80) });
                    float ring = Mathf.Abs(Circle(x, y, 48, 85, 6f)) - 3f;
                    float base_ = Box(x, y, 48, 22, 22, 5, 2);
                    return Union(Union(glass, top), Union(ring, base_));
                }
                case "chevron":
                    // Points right (+x): "it's out there", for markers pinned at the screen's edge.
                    return Union(Segment(x, y, new Vector2(34, 18), new Vector2(66, 48)), Segment(x, y, new Vector2(66, 48), new Vector2(34, 78))) - 9f;
                case "tick":
                    // Home: a bold check mark.
                    return Union(Segment(x, y, new Vector2(20, 50), new Vector2(40, 28)), Segment(x, y, new Vector2(40, 28), new Vector2(76, 70))) - 8f;
                case "cross":
                    // Wrecked: a bold X.
                    return Union(Segment(x, y, new Vector2(24, 24), new Vector2(72, 72)), Segment(x, y, new Vector2(24, 72), new Vector2(72, 24))) - 8f;
                case "twin":
                {
                    // Two beams from one point, turning together.
                    var o = new Vector2(48, 16);
                    float Wedge(float ang)
                    {
                        var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        float px = x - o.x, py = y - o.y;
                        float along = px * d.x + py * d.y, across = Mathf.Abs(-px * d.y + py * d.x);
                        return Mathf.Max(Mathf.Max(across - along * 0.26f, -along + 4f), along - 66f);
                    }
                    float a = Mathf.Abs(Wedge(1.95f)) - 1.6f, b = Wedge(1.19f);
                    return Union(Union(a, b), Circle(x, y, o.x, o.y, 6f));
                }
                default:
                    return Mathf.Abs(Circle(x, y, 48, 48, 30)) - 2f;
            }
        }, 1.3f)));

        /// <summary>A key cap outline for hints.</summary>
        public static Sprite KeyCap => Cached("keycap", () => Make(Raster(64, 64, (x, y) => Mathf.Abs(Box(x, y, 32, 33, 28, 27, 9)) - 2.2f), new Vector4(20, 20, 20, 20)));

        /// <summary>A mouse with the given button ("left", "right", or "" for none) highlighted.</summary>
        public static Sprite Mouse(string button) => Cached("mouse_" + button, () => Make(Raster(96, 128, (x, y) =>
        {
            float body = Mathf.Abs(Box(x, y, 48, 60, 32, 50, 30)) - 2.5f;
            float split = Box(x, y, 48, 92, 1.4f, 18, 0);
            float across = Box(x, y, 48, 74, 32, 1.4f, 0);
            float d = Union(body, Union(split, across));
            if (button == "left") d = Union(d, Box(x, y, 31, 92, 15, 16, 10));
            if (button == "right") d = Union(d, Box(x, y, 65, 92, 15, 16, 10));
            return d;
        }, 1.3f)));

        /// <summary>Warm parchment for the logbook, with darker, foxed edges.</summary>
        public static Sprite Paper => Cached("paper", () =>
        {
            const int W = 512, H = 512;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            var baseCol = new Color(0.89f, 0.84f, 0.73f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float n = Mathf.PerlinNoise(u * 6f, v * 6f) * 0.6f + Mathf.PerlinNoise(u * 40f, v * 40f) * 0.25f + Mathf.PerlinNoise(u * 140f, v * 140f) * 0.15f;
                    float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
                    float vign = Mathf.SmoothStep(0f, 0.18f, edge);
                    var c = baseCol * (0.86f + n * 0.18f);
                    c = Color.Lerp(new Color(0.62f, 0.52f, 0.38f), c, 0.35f + 0.65f * vign);
                    float fox = Mathf.PerlinNoise(u * 9f + 3f, v * 9f + 7f);
                    if (fox > 0.78f) c *= 1f - (fox - 0.78f) * 0.25f;
                    c.a = Mathf.Clamp01(edge * 220f);
                    px[y * W + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return Make(tex, new Vector4(60, 60, 60, 60));
        });

        /// <summary>
        /// The logbook lying open: two parchment pages with a shaded gutter, the page edges of the
        /// block showing at the sides, foxing and a little wear. Drawn at 1024x744 (the page's aspect).
        /// </summary>
        public static Sprite BookSpread => Cached("bookSpread", () =>
        {
            const int W = 1024, H = 744;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            var baseCol = new Color(0.9f, 0.85f, 0.74f);
            var edgeCol = new Color(0.6f, 0.5f, 0.36f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float half = u < 0.5f ? u * 2f : (1f - u) * 2f;          // 0 at the outer edge, 1 at the gutter
                    // The pages bow up from the spine: a dip of shadow at the gutter, a lit crest just beside it.
                    float gutter = Mathf.Exp(-(1f - half) * (1f - half) * 900f);
                    float crest = Mathf.Exp(-Mathf.Pow((1f - half - 0.06f) * 14f, 2f));
                    float n = Mathf.PerlinNoise(u * 7f, v * 5f) * 0.55f + Mathf.PerlinNoise(u * 48f, v * 36f) * 0.28f + Mathf.PerlinNoise(u * 170f, v * 130f) * 0.17f;
                    var c = baseCol * (0.86f + n * 0.17f);
                    // Foxed, darker edges and corners.
                    float outer = Mathf.Min(u, 1f - u);
                    float wear = Mathf.SmoothStep(0f, 0.09f, Mathf.Min(outer, Mathf.Min(v, 1f - v)) + (Mathf.PerlinNoise(u * 13f, v * 13f) - 0.5f) * 0.03f);
                    c = Color.Lerp(edgeCol, c, 0.3f + 0.7f * wear);
                    float fox = Mathf.PerlinNoise(u * 11f + 3f, v * 8f + 7f);
                    if (fox > 0.74f) c *= 1f - (fox - 0.74f) * 0.35f;
                    c *= 1f - gutter * 0.42f;
                    c += new Color(0.03f, 0.025f, 0.015f) * crest;
                    // The block of pages showing at the outer edges: a few fine lines.
                    if (outer < 0.012f)
                    {
                        float line = Mathf.Abs(Mathf.Sin(outer * W * 1.6f));
                        c *= 0.82f + 0.18f * line;
                    }
                    c.a = Mathf.Clamp01(Mathf.Min(outer * W, Mathf.Min(y + 0.5f, H - y - 0.5f)) * 0.8f);
                    px[y * W + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Make(tex);
        });

        /// <summary>A red wax seal pressed over a sealed night: a blobby disc with a stamped ring.</summary>
        public static Sprite WaxSeal => Cached("waxSeal", () => Make(Raster(96, 96, (x, y) =>
        {
            float a = Mathf.Atan2(y - 48, x - 48);
            float r = 36f + 3.2f * Mathf.Sin(a * 7f + 1.3f) + 1.8f * Mathf.Sin(a * 13f);
            return Circle(x, y, 48, 48, r);
        }, 1.4f, (x, y) =>
        {
            float d = Mathf.Sqrt((x - 48) * (x - 48) + (y - 48) * (y - 48));
            float ring = Mathf.Abs(d - 22f) < 2.2f ? 0.72f : 1f;                  // the stamped ring
            float star = d < 13f && (Mathf.Abs(x - 48) < 2.2f || Mathf.Abs(y - 48) < 2.2f) ? 0.72f : 1f;
            float shade = 1.08f - (y - 48) / 48f * -0.18f - d / 48f * 0.25f;     // lit from above
            var c = new Color(0.62f, 0.13f, 0.1f) * ring * star * shade;
            c.a = 1f;
            return c;
        })));

        /// <summary>A radio portrait medallion: a brass ring around a dark disc.</summary>
        public static Sprite Medallion => Cached("medallion", () => Make(Raster(128, 128, (x, y) => Circle(x, y, 64, 64, 60), 1.3f,
            (x, y) =>
            {
                float r = Mathf.Sqrt((x - 64) * (x - 64) + (y - 64) * (y - 64));
                if (r > 52) return new Color(0.79f, 0.64f, 0.35f);
                if (r > 49) return new Color(0.35f, 0.28f, 0.16f);
                float g = 0.1f + 0.06f * (1 - r / 50f);
                return new Color(g * 0.9f, g, g * 1.2f);
            })));
    }
}
