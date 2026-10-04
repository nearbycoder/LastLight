using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Sim
{
    /// <summary>JSON schema of Resources/Data/merrow_bay.json (written by ArtSource/map/merrow_bay.py).</summary>
    [Serializable]
    public class MapJson
    {
        public string name;
        public float[] lighthouse;
        public float lensHeight;
        public float[] bounds;
        public PolyJson[] land;
        public StackJson[] stacks;
        public ReefJson[] reefs;
        public ShoalJson[] shoals;
        public BuoyJson[] buoys;
        public HarborJson harbor;
        public RouteJson[] routes;
        public WreckerSiteJson[] wreckerSites;
    }

    [Serializable] public class PolyJson { public string name; public float[] pts; }
    [Serializable] public class StackJson { public string id, name; public float x, z, r, h; }
    [Serializable] public class ReefJson { public string id, group; public float x, z, r; }
    [Serializable] public class ShoalJson { public string id, name; public float x, z, rx, rz, angle; }
    [Serializable] public class BuoyJson { public string id, name, kind; public float x, z; }
    [Serializable] public class HarborJson { public float x, z, r; public float[] dock; }
    [Serializable] public class RouteJson { public string id; public float[] pts; }
    [Serializable] public class WreckerSiteJson { public string id, name; public float x, z, h; public float[] aim; public float[] hazard; }

    public sealed class Stack
    {
        public string Id, Name;
        public Vector2 Pos;
        public float Radius, Height;
    }

    public sealed class Shoal
    {
        public string Id, Name;
        public Vector2 Pos;
        public float Rx, Rz, Angle; // angle in degrees, counter-clockwise like the chart

        /// <summary>Ellipse test, with the radii grown by <paramref name="pad"/>.</summary>
        public bool Contains(Vector2 p, float pad = 0f)
        {
            var d = p - Pos;
            float a = -Angle * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            var l = new Vector2(d.x * c - d.y * s, d.x * s + d.y * c);
            float rx = Rx + pad, rz = Rz + pad;
            return (l.x * l.x) / (rx * rx) + (l.y * l.y) / (rz * rz) <= 1f;
        }

        /// <summary>Points along the long axis, used for charting tests and avoidance circles.</summary>
        public Vector2[] AxisPoints(int count)
        {
            var pts = new Vector2[count];
            float a = Angle * Mathf.Deg2Rad;
            var axis = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : Mathf.Lerp(-1f, 1f, i / (float)(count - 1));
                pts[i] = Pos + axis * (Rx - Rz) * t;
            }
            return pts;
        }
    }

    public sealed class WreckerSite
    {
        public string Id, Name;
        public Vector2 Pos;
        public float Height;
        public float AimMin, AimMax; // degrees, bearing convention, sweep from min to max (may wrap)
        public Vector2 Hazard;       // the rock the false light lures ships onto
    }

    public sealed class Route
    {
        public string Id;
        public Vector2[] Points;
        public bool ToHarbor;
    }

    /// <summary>Parsed, query-friendly version of the map.</summary>
    public sealed class MapData
    {
        public string Name;
        public Vector2 Lighthouse;
        public float LensHeight;
        public Rect Bounds;
        public List<Vector2[]> Land = new List<Vector2[]>();
        public List<Stack> Stacks = new List<Stack>();
        public List<ReefJson> Reefs = new List<ReefJson>();
        public List<Shoal> Shoals = new List<Shoal>();
        public List<BuoyJson> Buoys = new List<BuoyJson>();
        public Vector2 Harbor;
        public float HarborRadius;
        public Vector2[] Dock;
        public Dictionary<string, Route> Routes = new Dictionary<string, Route>();
        public Dictionary<string, WreckerSite> WreckerSites = new Dictionary<string, WreckerSite>();

        static MapData cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cached = null;

        public static MapData Load()
        {
            if (cached != null) return cached;
            var text = Resources.Load<TextAsset>("Data/merrow_bay");
            if (text == null) throw new Exception("Missing Resources/Data/merrow_bay.json");
            cached = Parse(text.text);
            return cached;
        }

        public static MapData Parse(string json)
        {
            var j = JsonUtility.FromJson<MapJson>(json);
            var m = new MapData
            {
                Name = j.name,
                Lighthouse = new Vector2(j.lighthouse[0], j.lighthouse[1]),
                LensHeight = j.lensHeight,
                Bounds = Rect.MinMaxRect(j.bounds[0], j.bounds[1], j.bounds[2], j.bounds[3]),
                Harbor = new Vector2(j.harbor.x, j.harbor.z),
                HarborRadius = j.harbor.r,
                Dock = ToPoints(j.harbor.dock),
            };
            foreach (var p in j.land) m.Land.Add(ToPoints(p.pts));
            foreach (var s in j.stacks)
                m.Stacks.Add(new Stack { Id = s.id, Name = s.name, Pos = new Vector2(s.x, s.z), Radius = s.r, Height = s.h });
            m.Reefs.AddRange(j.reefs);
            foreach (var s in j.shoals)
                m.Shoals.Add(new Shoal { Id = s.id, Name = s.name, Pos = new Vector2(s.x, s.z), Rx = s.rx, Rz = s.rz, Angle = s.angle });
            m.Buoys.AddRange(j.buoys);
            foreach (var r in j.routes)
            {
                var pts = ToPoints(r.pts);
                m.Routes[r.id] = new Route
                {
                    Id = r.id,
                    Points = pts,
                    ToHarbor = Vector2.Distance(pts[pts.Length - 1], m.Harbor) < 1f,
                };
            }
            foreach (var w in j.wreckerSites)
            {
                m.WreckerSites[w.id] = new WreckerSite
                {
                    Id = w.id, Name = w.name, Pos = new Vector2(w.x, w.z), Height = w.h,
                    AimMin = w.aim[0], AimMax = w.aim[1], Hazard = new Vector2(w.hazard[0], w.hazard[1]),
                };
            }
            return m;
        }

        static Vector2[] ToPoints(float[] flat)
        {
            var pts = new Vector2[flat.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(flat[i * 2], flat[i * 2 + 1]);
            return pts;
        }

        public bool OnLand(Vector2 p)
        {
            foreach (var poly in Land)
                if (Geo.PointInPolygon(poly, p)) return true;
            return false;
        }

        public float DistanceToCoast(Vector2 p, out Vector2 closest)
        {
            float best = float.MaxValue;
            closest = p;
            foreach (var poly in Land)
            {
                var c = Geo.ClosestOnPolygon(poly, p, out float d);
                if (d < best) { best = d; closest = c; }
            }
            return best;
        }

        public bool InBounds(Vector2 p, float margin = 0f) =>
            p.x >= Bounds.xMin - margin && p.x <= Bounds.xMax + margin &&
            p.y >= Bounds.yMin - margin && p.y <= Bounds.yMax + margin;
    }
}
