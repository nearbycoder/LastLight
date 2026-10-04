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
