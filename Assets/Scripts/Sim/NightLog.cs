using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Sim
{
    /// <summary>
    /// The night as a chart, for dawn: each ship's track with the state it was in, and the reefs,
    /// sandbanks and false lights the keeper saw. It only reads the simulation after each step, so
    /// it never changes what happens.
    /// </summary>
    public sealed class NightLog
    {
        public struct Point
        {
            public Vector2 Pos;
            public ShipState State;
        }

        public sealed class Track
        {
            public SimShip Ship;
            public readonly List<Point> Points = new List<Point>();
            public bool Done;
        }

        public readonly List<Track> Tracks = new List<Track>();
        public readonly HashSet<int> ChartedReefs = new HashSet<int>();
        public readonly HashSet<int> ChartedShoals = new HashSet<int>();
        /// <summary>Wrecker sites whose lantern burned tonight, by site id.</summary>
        public readonly HashSet<string> BurnedSites = new HashSet<string>();

        /// <summary>A point is kept each time a ship has moved this far, or changed state.</summary>
        public float Spacing { get; private set; } = 2.5f;
        /// <summary>Above this many points the log thins itself, so a long watch stays small (a
        /// 30-minute watch keeps about 17,000 at the starting spacing).</summary>
        public readonly int MaxPoints;
        public const int DefaultMaxPoints = 20000;
        public int PointCount { get; private set; }

        public NightLog(int maxPoints = DefaultMaxPoints) => MaxPoints = maxPoints;

        readonly Dictionary<SimShip, Track> byShip = new Dictionary<SimShip, Track>();

        public void Record(SimWorld w)
        {
            foreach (var s in w.Ships)
            {
                if (!byShip.TryGetValue(s, out var t))
                {
                    t = new Track { Ship = s };
                    byShip[s] = t;
                    Tracks.Add(t);
                    Add(t, s);
                    continue;
                }
                if (t.Done) continue;
                var last = t.Points[t.Points.Count - 1];
                if (s.State != last.State || (s.Pos - last.Pos).sqrMagnitude >= Spacing * Spacing) Add(t, s);
                if (s.Resolved)
                {
                    // The last point is where the ship ended: in harbour, or on the rock.
                    var end = t.Points[t.Points.Count - 1];
                    if (end.Pos != s.Pos || end.State != s.State) Add(t, s);
                    t.Done = true;
                }
            }
            for (int i = 0; i < w.Reefs.Count; i++) if (w.Reefs[i].Charted) ChartedReefs.Add(i);
            for (int i = 0; i < w.Shoals.Count; i++) if (w.Shoals[i].Charted) ChartedShoals.Add(i);
            foreach (var wr in w.Wreckers) if (wr.Burning) BurnedSites.Add(wr.Site.Id);
            if (PointCount > MaxPoints) Thin();
        }

        void Add(Track t, SimShip s)
        {
            t.Points.Add(new Point { Pos = s.Pos, State = s.State });
            PointCount++;
        }

        /// <summary>Halve the points along every track, keeping each track's ends and every change
        /// of state, and keep points twice as far apart from now on.</summary>
        void Thin()
        {
            Spacing *= 2f;
            int count = 0;
            var kept = new List<Point>();
            foreach (var t in Tracks)
            {
                var p = t.Points;
                kept.Clear();
                for (int i = 0; i < p.Count; i++)
                {
                    bool keep = i == 0 || i == p.Count - 1 || i % 2 == 0 || p[i].State != p[i - 1].State || (i + 1 < p.Count && p[i + 1].State != p[i].State);
                    if (keep) kept.Add(p[i]);
                }
                p.Clear();
                p.AddRange(kept);
                count += p.Count;
            }
            PointCount = count;
        }
    }
}
