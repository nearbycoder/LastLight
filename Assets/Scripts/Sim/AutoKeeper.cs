using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Sim
{
    /// <summary>
    /// A greedy lighthouse keeper used to prove every night can be won (EditMode tests), to drive
    /// screenshot tours, and as the attract-mode on the title screen. It plays through the same
    /// KeeperInput as the player, so the lens's speed limits apply to it too.
    /// </summary>
    public sealed class AutoKeeper
    {
        enum TaskKind { None, Ship, Reef, Shoal, Buoy, Wrecker, Idle, Scout }

        struct Task
        {
            public TaskKind Kind;
            public int Index;            // ship id, reef/shoal/buoy/wrecker index
            public Vector2 Target;
            public float Score;
            public bool WantFocus;
        }

        Task current;
        float commit;
        public float Skill = 1f;          // 0..1, lower = slower reactions (attract mode)
        public System.Func<SimShip, bool> Ignore;   // ships the keeper neglects (to stage a wreck for the reel)
        public float AimError;            // degrees of hand wobble around the target (0 = perfect aim)
        public float Hesitation;          // extra seconds before switching to a new task
        public bool Blind;                // doesn't know where hidden hazards are until they're charted
        float reaction, wobbleT, scoutDwell;
        readonly HashSet<int> seenReefs = new HashSet<int>(), seenShoals = new HashSet<int>();
        readonly Dictionary<int, float> scoutedAt = new Dictionary<int, float>();

        /// <summary>
        /// A deliberately sloppy keeper, as a rough stand-in for a new player when tuning: slow to
        /// react, slow to change its mind, and a shaky hand. It still knows where the hidden reefs
        /// are, which no player does, so treat its results as a difficulty curve, not a verdict.
        /// </summary>
        public static AutoKeeper Novice() => new AutoKeeper { Skill = 0f, AimError = 5f, Hesitation = 0.5f, Blind = true };
        readonly List<Vector2> path = new List<Vector2>(16);

        public string Describe() => current.Kind == TaskKind.None ? "-" : $"{current.Kind} {current.Index} ({current.Score:0})";

        public KeeperInput Decide(SimWorld w, float dt)
        {
            commit -= dt;
            reaction -= dt;
            if (Blind)
            {
                // A blind keeper learns the hazards as the light (or lightning) reveals them.
                for (int i = 0; i < w.Reefs.Count; i++) if (w.Reefs[i].Charted) seenReefs.Add(i);
                for (int i = 0; i < w.Shoals.Count; i++) if (w.Shoals[i].Charted) seenShoals.Add(i);
                // Sweeping ahead of a ship: after a couple of seconds on its course, move on.
                if (current.Kind == TaskKind.Scout && Mathf.Abs(Geo.DeltaAngle(w.Beam.Bearing, Geo.Bearing(current.Target - w.Beam.Origin))) < w.Beam.HalfAngle)
                {
                    scoutDwell += dt;
                    if (scoutDwell > 2f) { scoutedAt[current.Index] = w.Time; scoutDwell = 0f; commit = 0f; reaction = 0f; current.Kind = TaskKind.None; }
                }
            }
            var input = new KeeperInput { HasTarget = true, TargetBearing = w.Beam.Bearing };

            if (!StillValid(w, current) || commit <= 0f || reaction <= 0f)
            {
                var best = Choose(w);
                bool keep = StillValid(w, current) && commit > 0f && best.Score < current.Score * 1.25f + 10f;
                if (!keep)
                {
                    if (best.Kind != current.Kind || best.Index != current.Index) commit = 0.6f + Hesitation;
                    current = best;
                }
                reaction = Mathf.Lerp(0.6f, 0.05f, Skill);
            }
            else
            {
                current = Refresh(w, current);
            }

            if (current.Kind != TaskKind.None)
            {
                input.TargetBearing = Geo.Bearing(current.Target - w.Beam.Origin);
                input.Focus = current.WantFocus;
                if (AimError > 0f)
                {
                    wobbleT += dt;
                    float n = Mathf.PerlinNoise(wobbleT * 0.7f, 0.3f) * 2f - 1f + (Mathf.PerlinNoise(wobbleT * 2.3f, 5.1f) * 2f - 1f) * 0.4f;
                    input.TargetBearing += n * AimError * Mathf.Deg2Rad * 1.6f;
                }
            }

            // Foghorn when more than one ship is in trouble, or a ship is lost in fog.
            if (w.Mission.foghorn && w.HornCooldown <= 0f)
            {
                int trouble = 0;
                bool lostInFog = false;
                foreach (var s in w.Ships)
                {
                    if (!s.Active || !s.Inside) continue;
                    if (s.State == ShipState.Lost) { trouble++; if (s.InFog) lostInFog = true; }
                    else if (s.State == ShipState.Sailing && s.Confidence < 0.3f) trouble++;
                }
                if (trouble >= 2 || lostInFog) input.Horn = true;
            }
            return input;
        }

        bool StillValid(SimWorld w, Task t)
        {
            switch (t.Kind)
            {
                case TaskKind.Ship:
                    var s = FindShip(w, t.Index);
                    if (s == null || !s.Active || Ignored(s)) return false;
                    return s.State != ShipState.Sailing || s.Confidence < 0.97f;
                case TaskKind.Reef: return w.Reefs[t.Index].ChartTimer < w.ChartTime - 0.5f;
                case TaskKind.Shoal: return w.Shoals[t.Index].ChartTimer < w.ChartTime - 0.5f;
                case TaskKind.Buoy: return !w.Buoys[t.Index].Burning || w.Buoys[t.Index].Charge < 0.5f;
                case TaskKind.Wrecker: return w.Wreckers[t.Index].Burning;
                case TaskKind.Idle: return false;
                case TaskKind.Scout:
                    var sc = FindShip(w, t.Index);
                    return sc != null && sc.Active && sc.State == ShipState.Sailing;
                default: return false;
            }
        }

        Task Refresh(SimWorld w, Task t)
        {
            if (t.Kind == TaskKind.Ship)
            {
                var s = FindShip(w, t.Index);
                if (s != null) { t.Target = Lead(w, s); t.WantFocus = NeedFocus(w, s.Pos); }
            }
            if (t.Kind == TaskKind.Scout)
            {
                var s = FindShip(w, t.Index);
                if (s != null) { t.Target = ScoutPoint(w, s); t.WantFocus = NeedFocus(w, t.Target); }
            }
            return t;
        }

        bool Ignored(SimShip s) => Ignore != null && Ignore(s);

        static SimShip FindShip(SimWorld w, int id)
        {
            foreach (var s in w.Ships) if (s.Id == id) return s;
            return null;
        }

        static bool NeedFocus(SimWorld w, Vector2 p)
        {
            float d = Vector2.Distance(p, w.Beam.Origin);
            return d > SimBeam.WideRange * 0.8f || w.FogAt(p) > 0.25f || (w.Fog.Count > 0 && w.BeamAt(p) < 0.4f && d > 50f);
        }

        /// <summary>A point swept back and forth along the ship's course ahead, where reefs would be.</summary>
        static Vector2 ScoutPoint(SimWorld w, SimShip s)
        {
            float reach = s.Stats.Lookahead + s.Stats.Speed * 6f;
            float d = 10f + reach * (0.5f + 0.5f * Mathf.Sin(w.Time * 1.5f + s.Id));
            return SimWorld.CourseAhead(s, d);
        }

        /// <summary>Aim slightly ahead of a moving ship.</summary>
        static Vector2 Lead(SimWorld w, SimShip s) => s.Pos + s.Velocity * 0.25f;

        Task Choose(SimWorld w)
        {
            var best = new Task { Kind = TaskKind.None, Score = 0f };

            foreach (var s in w.Ships)
            {
                if (!s.Active || Ignored(s)) continue;
                bool soon = s.Inside || w.Map.InBounds(s.Pos, 12f);
                if (!soon) continue;

                float score;
                if (s.State == ShipState.Lured) score = 210f;
                else if (s.State == ShipState.Lost) score = 160f + (1f - Mathf.Clamp01(s.StateTime / 10f)) * 10f;
                else
                {
                    float drain = s.Stats.DrainTime * (s.InFog ? 1f / 1.6f : 1f) * (s.Damaged ? 1f / 1.5f : 1f) / Mathf.Max(0.3f, w.Mission.drainScale);
                    float timeLeft = s.Confidence * drain;
                    score = s.InAura ? 0f : 130f * (1f - Mathf.Clamp01((timeLeft - 1.5f) / 5f));
                }
                Consider(ref best, new Task { Kind = TaskKind.Ship, Index = s.Id, Target = Lead(w, s), Score = score, WantFocus = NeedFocus(w, s.Pos) });

                if (Blind && s.State == ShipState.Sailing && s.Inside)
                {
                    // Not knowing where the rocks are, light the water ahead of each ship in turn.
                    scoutedAt.TryGetValue(s.Id, out float last);
                    float since = last > 0f ? w.Time - last : 99f;
                    float sc = 25f + 95f * Mathf.Clamp01((since - 1f) / 7f);
                    var p = ScoutPoint(w, s);
                    Consider(ref best, new Task { Kind = TaskKind.Scout, Index = s.Id, Target = p, Score = sc, WantFocus = NeedFocus(w, p) });
                }

                if (s.State == ShipState.Sailing || s.State == ShipState.Lost)
                {
                    for (int mode = 0; mode < 2; mode++)
                    {
                        var hazard = FirstHazardAhead(w, s, mode == 1, out float eta, out TaskKind kind, out Vector2 hpos);
                        if (hazard < 0) continue;
                        float hs = 178f * (1f - Mathf.Clamp01((eta - 3f) / 11f));
                        Consider(ref best, new Task { Kind = kind, Index = hazard, Target = hpos, Score = hs, WantFocus = NeedFocus(w, hpos) });
                    }
                }

                // Ships in a beam shadow need a buoy.
                if (s.Inside && w.Occlusion(w.Beam.Origin, s.Pos) < 0.5f)
                {
                    foreach (var b in w.Buoys)
                    {
                        if (b.Charge > 0.35f) continue;
                        if (Vector2.Distance(b.Pos, s.Pos + s.Velocity * 3f) > SimBuoy.AuraRadius + 8f) continue;
                        Consider(ref best, new Task { Kind = TaskKind.Buoy, Index = b.Index, Target = b.Pos, Score = 150f, WantFocus = NeedFocus(w, b.Pos) });
                    }
                }
            }

            foreach (var wr in w.Wreckers)
            {
                if (!wr.Burning) continue;
                float threat = 0f;
                foreach (var s in w.Ships)
                {
                    if (!s.Active || !s.Inside || Vector2.Distance(s.Pos, wr.Site.Pos) > SimWrecker.Range + 10f) continue;
                    threat = Mathf.Max(threat, s.State == ShipState.Lured ? 1f : s.Confidence < 0.65f ? 0.8f : 0.35f);
                }
                Consider(ref best, new Task { Kind = TaskKind.Wrecker, Index = wr.Index, Target = wr.Site.Pos, Score = 45f + threat * 150f, WantFocus = true });
            }

            // Pre-charge buoys on busy channels when nothing is urgent.
            if (best.Score < 40f)
            {
                foreach (var b in w.Buoys)
                {
                    if (b.Charge > 0.3f) continue;
                    foreach (var s in w.Ships)
                    {
                        if (!s.Active || Vector2.Distance(s.Pos, b.Pos) > 60f) continue;
                        Consider(ref best, new Task { Kind = TaskKind.Buoy, Index = b.Index, Target = b.Pos, Score = 35f, WantFocus = NeedFocus(w, b.Pos) });
                        break;
                    }
                }
            }

            // Idle: keep the least confident ship topped up.
            if (best.Kind == TaskKind.None)
            {
                SimShip low = null;
                foreach (var s in w.Ships)
                    if (s.Active && s.Inside && !Ignored(s) && (low == null || s.Confidence < low.Confidence)) low = s;
                if (low != null)
                    best = new Task { Kind = TaskKind.Ship, Index = low.Id, Target = Lead(w, low), Score = 5f, WantFocus = NeedFocus(w, low.Pos) };
            }
            return best;
        }

        static void Consider(ref Task best, Task t)
        {
            if (t.Score > best.Score) best = t;
        }

        /// <summary>
        /// The first uncharted reef (or, for steamers, shoal) along the ship's intended path within
        /// its look-ahead horizon, and the time until the ship reaches it.
        /// </summary>
        int FirstHazardAhead(SimWorld w, SimShip s, bool alongBow, out float eta, out TaskKind kind, out Vector2 pos)
        {
            eta = 0f;
            kind = TaskKind.None;
            pos = default;
            float speed = Mathf.Max(1f, s.Stats.Speed * 0.8f);
            float horizon = s.Stats.Lookahead + speed * 9f;
            path.Clear();
            path.Add(s.Pos);
            float acc = 0f;
            var cur = s.Pos;
            if (alongBow || s.State == ShipState.Lost)
            {
                // Off the planned line (lost, or swinging round something): follow the bow.
                var v = s.Velocity.sqrMagnitude > 0.01f ? s.Velocity.normalized : s.Forward;
                path.Add(s.Pos + v * horizon);
            }
            else
            {
                for (int i = s.Waypoint; i < s.Route.Points.Length && acc < horizon; i++)
                {
                    var p = s.Route.Points[i];
                    acc += Vector2.Distance(cur, p);
                    path.Add(p);
                    cur = p;
                }
            }

            int bestIndex = -1;
            float bestDist = float.MaxValue;
            for (int r = 0; r < w.Reefs.Count; r++)
            {
                var reef = w.Reefs[r];
                if (s.KnownReefs.Contains(r)) continue;
                if (Blind && !seenReefs.Contains(r)) continue;
                if (reef.Charted && reef.ChartTimer > 6f) continue;
                float along = AlongPath(reef.Pos, reef.Radius + s.Stats.Radius + 2.5f, horizon);
                if (along >= 0f && along < bestDist) { bestDist = along; bestIndex = r; kind = TaskKind.Reef; pos = reef.Pos; }
            }
            if (s.Stats.DeepDraught)
            {
                for (int k = 0; k < w.Shoals.Count; k++)
                {
                    var sh = w.Shoals[k];
                    if (s.KnownShoals.Contains(k)) continue;
                    if (Blind && !seenShoals.Contains(k)) continue;
                    if (sh.Charted && sh.ChartTimer > 6f) continue;
                    foreach (var p in sh.Axis)
                    {
                        float along = AlongPath(p, sh.Def.Rz + s.Stats.Radius + 2.5f, horizon);
                        if (along >= 0f && along < bestDist) { bestDist = along; bestIndex = k; kind = TaskKind.Shoal; pos = sh.Def.Pos; }
                    }
                }
            }
            eta = bestIndex >= 0 ? bestDist / speed : 0f;
            return bestIndex;
        }

        float AlongPath(Vector2 c, float radius, float horizon)
        {
            float acc = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                var a = path[i];
                var b = path[i + 1];
                float d = Geo.SegmentDistance(a, b, c, out float t);
                float segLen = Vector2.Distance(a, b);
                if (d < radius) return acc + segLen * t;
                acc += segLen;
                if (acc > horizon) break;
            }
            return -1f;
        }
    }
}
