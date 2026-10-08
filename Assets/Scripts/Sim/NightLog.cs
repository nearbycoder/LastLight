using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Sim
{
    /// <summary>
    /// The night as a chart, for dawn: each ship's track with the state it was in, and the reefs,
    /// sandbanks and false lights the keeper saw. It only reads the simulation after each step, so
    /// it never changes what happens. Points carry their time, and the light's sweep and the false
    /// lights' burning are kept too, so the chart can play the night back.
    /// </summary>
    public sealed class NightLog
    {
        public struct Point
        {
            public Vector2 Pos;
            public ShipState State;
            public float Time;
        }

        /// <summary>The light at a moment: where it pointed, how focused, how far it reached.</summary>
        public struct BeamSample
        {
            public float Time, Bearing, Focus, Range;
        }

        /// <summary>A false light that burned, from when to when (End is the night's end if it
        /// was still burning).</summary>
        public sealed class Burn
        {
            public string Site;
            public float Start, End = float.PositiveInfinity;
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
        public readonly List<BeamSample> Beam = new List<BeamSample>();
        public readonly List<Burn> Burns = new List<Burn>();
        /// <summary>The time of the last step recorded: how long the night ran.</summary>
        public float Duration { get; private set; }

        /// <summary>How often the light is sampled, in seconds of the night. A 30-minute watch keeps
        /// 18,000 samples; past MaxBeamSamples they thin, as the tracks do.</summary>
        public float BeamInterval { get; private set; } = 0.1f;
        public const int MaxBeamSamples = 24000;

        /// <summary>A point is kept each time a ship has moved this far, or changed state.</summary>
        public float Spacing { get; private set; } = 2.5f;
        /// <summary>Above this many points the log thins itself, so a long watch stays small (a
        /// 30-minute watch keeps about 17,000 at the starting spacing).</summary>
        public readonly int MaxPoints;
        public const int DefaultMaxPoints = 20000;
        public int PointCount { get; private set; }

        public NightLog(int maxPoints = DefaultMaxPoints) => MaxPoints = maxPoints;

        readonly Dictionary<SimShip, Track> byShip = new Dictionary<SimShip, Track>();
        readonly Dictionary<SimWrecker, Burn> burning = new Dictionary<SimWrecker, Burn>();

        public void Record(SimWorld w)
        {
            Duration = w.Time;
            if (Beam.Count == 0 || w.Time - Beam[Beam.Count - 1].Time >= BeamInterval - 1e-4f)
            {
                Beam.Add(new BeamSample { Time = w.Time, Bearing = w.Beam.Bearing, Focus = w.Beam.Focus, Range = w.Beam.Range });
                if (Beam.Count > MaxBeamSamples) ThinBeam();
            }
            foreach (var s in w.Ships)
            {
                if (!byShip.TryGetValue(s, out var t))
                {
                    t = new Track { Ship = s };
                    byShip[s] = t;
                    Tracks.Add(t);
                    Add(t, s, w.Time);
                    continue;
                }
                if (t.Done) continue;
                var last = t.Points[t.Points.Count - 1];
                if (s.State != last.State || (s.Pos - last.Pos).sqrMagnitude >= Spacing * Spacing) Add(t, s, w.Time);
                if (s.Resolved)
                {
                    // The last point is where the ship ended: in harbour, or on the rock.
                    var end = t.Points[t.Points.Count - 1];
                    if (end.Pos != s.Pos || end.State != s.State) Add(t, s, w.Time);
                    t.Done = true;
                }
            }
            for (int i = 0; i < w.Reefs.Count; i++) if (w.Reefs[i].Charted) ChartedReefs.Add(i);
            for (int i = 0; i < w.Shoals.Count; i++) if (w.Shoals[i].Charted) ChartedShoals.Add(i);
            foreach (var wr in w.Wreckers)
            {
                burning.TryGetValue(wr, out var b);
                if (wr.Burning)
                {
                    BurnedSites.Add(wr.Site.Id);
                    if (b != null && b.Site != wr.Site.Id) { b.End = w.Time; b = null; }
                    if (b == null) { b = new Burn { Site = wr.Site.Id, Start = w.Time }; Burns.Add(b); burning[wr] = b; }
                }
                else if (b != null) { b.End = w.Time; burning.Remove(wr); }
            }
            if (PointCount > MaxPoints) Thin();
        }

        void Add(Track t, SimShip s, float time)
        {
            t.Points.Add(new Point { Pos = s.Pos, State = s.State, Time = time });
            PointCount++;
        }

        void ThinBeam()
        {
            BeamInterval *= 2f;
            int n = 0;
            for (int i = 0; i < Beam.Count; i++) if (i % 2 == 0 || i == Beam.Count - 1) Beam[n++] = Beam[i];
            Beam.RemoveRange(n, Beam.Count - n);
        }

        // ---------------------------------------------------------------- playing it back

        /// <summary>Where a ship was at a moment of the night, and in what state: false before it
        /// entered the bay and after it reached harbour or left. A wrecked ship stays on its rock.</summary>
        public static bool ShipAt(Track t, float time, out Vector2 pos, out ShipState state)
        {
            var p = t.Points;
            pos = default;
            state = ShipState.Sailing;
            if (p.Count == 0 || time < p[0].Time) return false;
            var last = p[p.Count - 1];
            if (time >= last.Time)
            {
                pos = last.Pos;
                state = last.State;
                // Home (or away) is off the chart; a wreck stays where it struck.
                return !t.Done || last.State == ShipState.Wrecked;
            }
            int lo = 0, hi = p.Count - 1;   // p[lo].Time <= time < p[hi].Time
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (p[mid].Time <= time) lo = mid; else hi = mid;
            }
            float span = p[hi].Time - p[lo].Time;
            pos = Vector2.Lerp(p[lo].Pos, p[hi].Pos, span > 0f ? (time - p[lo].Time) / span : 1f);
            state = p[lo].State;
            return true;
        }

        /// <summary>The light at a moment of the night, between the samples either side.</summary>
        public BeamSample BeamAt(float time)
        {
            if (Beam.Count == 0) return default;
            if (time <= Beam[0].Time) return Beam[0];
            if (time >= Beam[Beam.Count - 1].Time) return Beam[Beam.Count - 1];
            int lo = 0, hi = Beam.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (Beam[mid].Time <= time) lo = mid; else hi = mid;
            }
            var a = Beam[lo];
            var b = Beam[hi];
            float k = (time - a.Time) / Mathf.Max(1e-5f, b.Time - a.Time);
            return new BeamSample
            {
                Time = time,
                Bearing = Geo.WrapAngle(a.Bearing + Geo.DeltaAngle(a.Bearing, b.Bearing) * k),
                Focus = Mathf.Lerp(a.Focus, b.Focus, k),
                Range = Mathf.Lerp(a.Range, b.Range, k),
            };
        }

        public enum MomentKind { Lost, Lured, Wrecked }

        /// <summary>A turning point of the night: a ship lost its way, was lured, or was wrecked.</summary>
        public struct Moment
        {
            public float Time;
            public SimShip Ship;
            public MomentKind Kind;
            /// <summary>Lost straight from a doused lure, rather than for want of the light.</summary>
            public bool AfterLure;
        }

        /// <summary>The night's turning points in the order they happened: each time a ship lost its
        /// way, was lured or was wrecked, as its track records it.</summary>
        public List<Moment> Moments()
        {
            var list = new List<Moment>();
            foreach (var t in Tracks)
            {
                var prev = ShipState.Sailing;
                foreach (var p in t.Points)
                {
                    if (p.State != prev && (p.State == ShipState.Lost || p.State == ShipState.Lured || p.State == ShipState.Wrecked))
                        list.Add(new Moment
                        {
                            Time = p.Time, Ship = t.Ship, AfterLure = prev == ShipState.Lured,
                            Kind = p.State == ShipState.Lost ? MomentKind.Lost : p.State == ShipState.Lured ? MomentKind.Lured : MomentKind.Wrecked,
                        });
                    prev = p.State;
                }
            }
            // In time order; at the same moment, in the order the ships sailed.
            var order = new List<(Moment m, int i)>();
            for (int i = 0; i < list.Count; i++) order.Add((list[i], i));
            order.Sort((a, b) => a.m.Time != b.m.Time ? a.m.Time.CompareTo(b.m.Time) : a.i.CompareTo(b.i));
            for (int i = 0; i < list.Count; i++) list[i] = order[i].m;
            return list;
        }

        /// <summary>Whether a wrecker site's lantern was burning at a moment of the night.</summary>
        public bool BurningAt(string site, float time)
        {
            foreach (var b in Burns) if (b.Site == site && time >= b.Start && time < b.End) return true;
            return false;
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
