using UnityEngine;

namespace LastLight.Sim
{
    /// <summary>
    /// 2D helpers on the sea plane. Positions are (x, z) packed into Vector2. Bearings are radians,
    /// measured clockwise from north (+z), so forward = (sin b, cos b), matching
    /// Quaternion.Euler(0, b * Rad2Deg, 0) in Unity.
    /// </summary>
    public static class Geo
    {
        public static Vector2 Dir(float bearing) => new Vector2(Mathf.Sin(bearing), Mathf.Cos(bearing));

        public static float Bearing(Vector2 v) => Mathf.Atan2(v.x, v.y);

        /// <summary>Signed smallest difference b - a, in (-PI, PI].</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = Mathf.Repeat(b - a + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
            return d <= -Mathf.PI ? d + Mathf.PI * 2f : d;
        }

        public static float WrapAngle(float a) => Mathf.Repeat(a, Mathf.PI * 2f);

        public static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public static float SmoothStep01(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Distance from p to segment ab, and the parameter t of the closest point.</summary>
        public static float SegmentDistance(Vector2 a, Vector2 b, Vector2 p, out float t)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(a + ab * t, p);
        }

        /// <summary>Length of segment ab that lies inside the circle (c, r).</summary>
        public static float ChordLength(Vector2 a, Vector2 b, Vector2 c, float r)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return (a - c).sqrMagnitude < r * r ? 0f : 0f;
            var dir = d / len;
            var f = a - c;
            float bq = Vector2.Dot(f, dir);
            float cq = f.sqrMagnitude - r * r;
            float disc = bq * bq - cq;
            if (disc <= 0f) return 0f;
            float s = Mathf.Sqrt(disc);
            float t0 = Mathf.Max(-bq - s, 0f);
            float t1 = Mathf.Min(-bq + s, len);
            return Mathf.Max(0f, t1 - t0);
        }

        public static bool PointInPolygon(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[i];
                var b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>Closest point on the polygon outline to p.</summary>
        public static Vector2 ClosestOnPolygon(Vector2[] poly, Vector2 p, out float distance)
        {
            distance = float.MaxValue;
            var best = p;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                float d = SegmentDistance(poly[j], poly[i], p, out float t);
                if (d < distance)
                {
                    distance = d;
                    best = Vector2.Lerp(poly[j], poly[i], t);
                }
            }
            return best;
        }

        /// <summary>Rotates v by angle radians (clockwise, in bearing convention).</summary>
        public static Vector2 Rotate(Vector2 v, float angle)
        {
            float s = Mathf.Sin(angle), c = Mathf.Cos(angle);
            return new Vector2(v.x * c + v.y * s, -v.x * s + v.y * c);
        }

        /// <summary>Cheap deterministic 1D value noise in [-1, 1].</summary>
        public static float Noise1(float x, int seed)
        {
            int i = Mathf.FloorToInt(x);
            float f = x - i;
            float u = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(i, seed), Hash(i + 1, seed), u) * 2f - 1f;
        }

        public static float Hash(int i, int seed)
        {
            unchecked
            {
                uint h = (uint)(i * 374761393 + seed * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
