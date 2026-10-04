using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Sim
{
    public enum SimEventType
    {
        ShipIncoming, ShipSpawned, ShipEntered, ShipLit, ShipLost, ShipFound, ShipLured, ShipFreed,
        ShipArrived, ShipWrecked, ShipFlare, ShipDanger,
        ReefCharted, ShoalCharted, BuoyLit, BuoyOut,
        Horn, Lightning, WreckerLit, WreckerDoused, WreckerDousing,
        MissionWon, MissionFailed,
    }

    public struct SimEvent
    {
        public SimEventType Type;
        public SimShip Ship;
        public int Index;
        public Vector2 Pos;
        public string Text;
    }

    public enum MissionOutcome { Running, Won, Failed }

    /// <summary>
    /// The whole game state of one night, stepped at a fixed rate. Pure C#: no scene objects, so
    /// it runs identically in the game, in EditMode tests and under the AutoKeeper bot.
    /// </summary>
    public sealed class SimWorld
    {
        public readonly MapData Map;
        public readonly MissionDef Mission;
        public readonly SimBeam Beam = new SimBeam();
        public readonly List<SimShip> Ships = new List<SimShip>();
        public readonly List<SimReef> Reefs = new List<SimReef>();
        public readonly List<SimShoal> Shoals = new List<SimShoal>();
        public readonly List<SimBuoy> Buoys = new List<SimBuoy>();
        public readonly List<SimFogBank> Fog = new List<SimFogBank>();
        public readonly List<SimWrecker> Wreckers = new List<SimWrecker>();
        public readonly List<SimEvent> Events = new List<SimEvent>();

        public float Time;
        public int Wrecks, Arrivals, Score;
        public MissionOutcome Outcome = MissionOutcome.Running;
        public float HornCooldown;
        public const float HornCooldownTime = 14f;
        public const float HornRange = 140f;
        public float Flash;              // lightning flash, 1 at the strike, decays
        public float LightningTimer;
        public Vector2 Current;
        public float Rain;
        public bool Frozen;              // stop ships (used by the ending)
        public bool ShipsDone => nextSpawn >= schedule.Length && !Ships.Exists(s => s.Active);

        readonly SpawnDef[] schedule;
        readonly bool[] incomingSent;
        int nextSpawn;
        int nextShipId = 1;
        readonly System.Random rng;
        struct Obstacle
        {
            public Vector2 C;
            public float R;          // bounding radius (circle obstacles: the radius)
            public Vector2[] Poly;   // convex outline for sandbanks, null for circles
        }

        readonly List<Obstacle> obstacles = new List<Obstacle>(64);

        public int TotalShips => schedule.Length;
        public int SpawnedShips => nextSpawn;
        public SpawnDef[] Schedule => schedule;

        public SimWorld(MapData map, MissionDef mission, int seed = 7)
        {
            Map = map;
            Mission = mission;
            rng = new System.Random(seed);
            Beam.Origin = map.Lighthouse;
            Beam.Height = map.LensHeight;
            Beam.Bearing = 0f;

            var groups = new HashSet<string>(mission.reefGroups ?? new string[0]);
            foreach (var r in map.Reefs)
            {
                if (!groups.Contains(r.group)) continue;
                Reefs.Add(new SimReef { Index = Reefs.Count, Id = r.id, Group = r.group, Pos = new Vector2(r.x, r.z), Radius = r.r });
            }
            var shoalIds = new HashSet<string>(mission.shoals ?? new string[0]);
            foreach (var s in map.Shoals)
                if (shoalIds.Contains(s.Id)) Shoals.Add(new SimShoal { Def = s, Axis = s.AxisPoints(5) });
            var buoyIds = new HashSet<string>(mission.buoys ?? new string[0]);
            foreach (var b in map.Buoys)
                if (buoyIds.Contains(b.id))
                    Buoys.Add(new SimBuoy { Index = Buoys.Count, Id = b.id, Name = b.name, Kind = b.kind, Pos = new Vector2(b.x, b.z) });
            foreach (var f in mission.fog ?? new FogDef[0])
                Fog.Add(new SimFogBank { Pos = new Vector2(f.x, f.z), Velocity = new Vector2(f.vx, f.vz), Radius = f.r, Density = f.density });
            foreach (var w in mission.wreckers ?? new WreckerDef[0])
            {
                var sites = new List<WreckerSite>();
                foreach (var id in w.sites)
                    if (map.WreckerSites.TryGetValue(id, out var site)) sites.Add(site);
                if (sites.Count == 0) continue;
                Wreckers.Add(new SimWrecker { Index = Wreckers.Count, Def = w, Sites = sites.ToArray(), Timer = w.start });
            }
            if (mission.storm != null && mission.storm.enabled)
            {
                Current = new Vector2(mission.storm.cx, mission.storm.cz);
                Rain = mission.storm.rain;
                LightningTimer = Mathf.Lerp(mission.storm.lightningMin, mission.storm.lightningMax, (float)rng.NextDouble()) * 0.5f;
            }
            Beam.Rain = Rain;

            schedule = (SpawnDef[])(mission.ships ?? new SpawnDef[0]).Clone();
            Array.Sort(schedule, (a, b) => a.t.CompareTo(b.t));
            incomingSent = new bool[schedule.Length];
        }

        // ------------------------------------------------------------------ light

        /// <summary>True-beam intensity at a point on the sea, including stack shadows and fog.</summary>
        public float BeamAt(Vector2 p)
        {
            float i = SimBeam.Wedge(Beam.Origin, Beam.Direction, Beam.HalfAngle, Beam.Range, p);
            if (i <= 0f) return 0f;
            i *= Beam.Strength;
            i *= Occlusion(Beam.Origin, p);
            if (i <= 0f) return 0f;
            if (Fog.Count > 0)
            {
                float chord = 0f;
                foreach (var f in Fog) chord += Geo.ChordLength(Beam.Origin, p, f.Pos, f.Radius) * f.Density;
                i *= Mathf.Exp(-Beam.FogExtinction * chord);
            }
            return i;
        }

        /// <summary>1 = clear line of sight from the lens, 0 = in the shadow of a stack.</summary>
        public float Occlusion(Vector2 origin, Vector2 p)
        {
            float vis = 1f;
            foreach (var s in Map.Stacks)
            {
                if ((p - s.Pos).sqrMagnitude < (s.Radius + 0.6f) * (s.Radius + 0.6f)) continue;
                float d = Geo.SegmentDistance(origin, p, s.Pos, out float t);
                if (t <= 0f || t >= 1f) continue;
                vis *= Geo.SmoothStep01(s.Radius * 0.75f, s.Radius * 1.05f, d);
                if (vis <= 0f) return 0f;
            }
            return vis;
        }

        public float FalseBeamAt(SimWrecker w, Vector2 p)
        {
            if (!w.Burning) return 0f;
            return SimBeam.Wedge(w.Site.Pos, Geo.Dir(w.Bearing), SimWrecker.HalfAngle * Mathf.Deg2Rad, SimWrecker.Range, p);
        }

        public float FogAt(Vector2 p)
        {
            float d = 0f;
            foreach (var f in Fog)
            {
                float dist = Vector2.Distance(p, f.Pos);
                d = Mathf.Max(d, f.Density * (1f - Geo.SmoothStep01(f.Radius * 0.55f, f.Radius, dist)));
            }
            return d;
        }

        public bool InAura(Vector2 p)
        {
            foreach (var b in Buoys)
                if (b.Burning && (b.Pos - p).sqrMagnitude < SimBuoy.AuraRadius * SimBuoy.AuraRadius) return true;
            return false;
        }

        // ------------------------------------------------------------------ step

        public void Step(float dt, in KeeperInput input)
        {
            Events.Clear();
            Time += dt;
            Beam.Step(input, dt);
            HornCooldown = Mathf.Max(0f, HornCooldown - dt);
            Flash = Mathf.Max(0f, Flash - dt * 3.5f);

            if (input.Horn && Mission.foghorn && HornCooldown <= 0f && Outcome == MissionOutcome.Running) SoundHorn();

            StepFog(dt);
            StepStorm(dt);
            StepReefs(dt);
            StepBuoys(dt);
            StepWreckers(dt);
            StepSpawns();
            if (!Frozen)
                foreach (var s in Ships) StepShip(s, dt);

            if (Outcome == MissionOutcome.Running && ShipsDone)
            {
                Outcome = MissionOutcome.Won;
                Emit(SimEventType.MissionWon);
            }
        }

        void Emit(SimEventType type, SimShip ship = null, int index = -1, Vector2 pos = default, string text = null)
        {
            Events.Add(new SimEvent { Type = type, Ship = ship, Index = index, Pos = ship != null && pos == default ? ship.Pos : pos, Text = text });
        }

        void SoundHorn()
        {
            HornCooldown = HornCooldownTime;
            Emit(SimEventType.Horn, pos: Beam.Origin);
            foreach (var s in Ships)
            {
                if (!s.Active || (s.Pos - Beam.Origin).sqrMagnitude > HornRange * HornRange) continue;
                s.Confidence = Mathf.Min(1f, s.Confidence + 0.45f);
                if (s.State == ShipState.Lost)
                {
                    s.Confidence = Mathf.Max(s.Confidence, 0.35f);
                    SetState(s, ShipState.Sailing);
                    Emit(SimEventType.ShipFound, s);
                }
            }
        }

        void StepFog(float dt)
        {
            var b = Map.Bounds;
            foreach (var f in Fog)
            {
                f.Pos += f.Velocity * dt;
                float m = f.Radius + 20f;
                if (f.Pos.x > b.xMax + m) f.Pos.x = b.xMin - m + 1f;
                if (f.Pos.x < b.xMin - m) f.Pos.x = b.xMax + m - 1f;
                if (f.Pos.y > b.yMax + m) f.Pos.y = b.yMin - m + 1f;
                if (f.Pos.y < b.yMin - m) f.Pos.y = b.yMax + m - 1f;
            }
        }

        void StepStorm(float dt)
        {
            var storm = Mission.storm;
            if (storm == null || !storm.enabled) return;
            LightningTimer -= dt;
            if (LightningTimer > 0f) return;
            LightningTimer = Mathf.Lerp(storm.lightningMin, storm.lightningMax, (float)rng.NextDouble());
            Flash = 1f;
            Emit(SimEventType.Lightning);
            foreach (var r in Reefs) r.ChartTimer = Mathf.Max(r.ChartTimer, 6f);
            foreach (var s in Shoals) s.ChartTimer = Mathf.Max(s.ChartTimer, 6f);
        }

        void StepReefs(float dt)
        {
            float rate = 1f + Beam.Focus;
            foreach (var r in Reefs)
            {
                r.Light = BeamAt(r.Pos);
                bool lit = r.Light >= SimBeam.LitThreshold;
                if (lit)
                {
                    r.Exposure = Mathf.Min(SimReef.ExposureNeeded, r.Exposure + dt * rate);
                    if (r.Exposure >= SimReef.ExposureNeeded)
                    {
                        if (!r.Charted) Emit(SimEventType.ReefCharted, index: r.Index, pos: r.Pos);
                        r.ChartTimer = SimReef.ChartDuration;
                    }
                }
                else
                {
                    r.Exposure = Mathf.Max(0f, r.Exposure - dt * 0.5f);
                    r.ChartTimer = Mathf.Max(0f, r.ChartTimer - dt);
                }
            }
            for (int i = 0; i < Shoals.Count; i++)
            {
                var s = Shoals[i];
                float best = 0f;
                foreach (var p in s.Axis) best = Mathf.Max(best, BeamAt(p));
                s.Light = best;
                if (best >= SimBeam.LitThreshold)
                {
                    s.Exposure = Mathf.Min(SimReef.ExposureNeeded, s.Exposure + dt * rate);
                    if (s.Exposure >= SimReef.ExposureNeeded)
                    {
                        if (!s.Charted) Emit(SimEventType.ShoalCharted, index: i, pos: s.Def.Pos);
                        s.ChartTimer = SimReef.ChartDuration;
                    }
                }
                else
                {
                    s.Exposure = Mathf.Max(0f, s.Exposure - dt * 0.5f);
                    s.ChartTimer = Mathf.Max(0f, s.ChartTimer - dt);
                }
            }
        }

        void StepBuoys(float dt)
        {
            foreach (var b in Buoys)
            {
                b.Light = BeamAt(b.Pos);
                bool wasBurning = b.Burning;
                if (b.Light >= SimBeam.LitThreshold)
                {
                    if (b.Charge < 0.6f) Emit(SimEventType.BuoyLit, index: b.Index, pos: b.Pos);
                    b.Charge = 1f;
                }
                else
                {
                    b.Charge = Mathf.Max(0f, b.Charge - dt / SimBuoy.BurnTime);
                    if (wasBurning && !b.Burning) Emit(SimEventType.BuoyOut, index: b.Index, pos: b.Pos);
                }
            }
        }

        void StepWreckers(float dt)
        {
            foreach (var w in Wreckers)
            {
                switch (w.State)
                {
                    case WreckerState.Waiting:
                    case WreckerState.Doused:
                        w.Timer -= dt;
                        if (w.Timer <= 0f)
                        {
                            if (w.State == WreckerState.Doused) w.SiteIndex = (w.SiteIndex + 1) % w.Sites.Length;
                            w.State = WreckerState.Burning;
                            w.DouseProgress = 0f;
                            w.Bearing = w.Site.AimMin * Mathf.Deg2Rad;
                            w.SweepDir = 1;
                            Emit(SimEventType.WreckerLit, index: w.Index, pos: w.Site.Pos);
                        }
                        break;
                    case WreckerState.Burning:
                        Sweep(w, dt);
                        float light = BeamAt(w.Site.Pos);
                        if (light >= SimBeam.LitThreshold)
                        {
                            if (w.DouseProgress <= 0f) Emit(SimEventType.WreckerDousing, index: w.Index, pos: w.Site.Pos);
                            w.DouseProgress += dt / (Beam.Focus > 0.5f ? 0.45f : 1.0f);
                        }
                        else w.DouseProgress = Mathf.Max(0f, w.DouseProgress - dt * 0.6f);
                        if (w.DouseProgress >= 1f)
                        {
                            w.State = WreckerState.Doused;
                            w.Timer = w.Def.relight;
                            Emit(SimEventType.WreckerDoused, index: w.Index, pos: w.Site.Pos);
                            foreach (var s in Ships)
                            {
                                if (s.State != ShipState.Lured || s.LuredBy != w) continue;
                                SetState(s, s.Confidence > 0f ? ShipState.Sailing : ShipState.Lost);
                                Emit(SimEventType.ShipFreed, s);
                            }
                        }
                        break;
                }
            }
        }

        void Sweep(SimWrecker w, float dt)
        {
            float speed = w.Def.sweep * Mathf.Deg2Rad;
            if (w.Def.mimic)
            {
                w.Bearing = Geo.WrapAngle(w.Bearing + speed * dt);
                return;
            }
            float min = w.Site.AimMin * Mathf.Deg2Rad;
            float width = Mathf.Repeat(w.Site.AimMax - w.Site.AimMin, 360f) * Mathf.Deg2Rad;
            float s = Mathf.Repeat(w.Bearing - min, Mathf.PI * 2f);
            if (s > width + 0.5f) s = 0f;
            s += speed * dt * w.SweepDir;
            if (s >= width) { s = width; w.SweepDir = -1; }
            if (s <= 0f) { s = 0f; w.SweepDir = 1; }
            w.Bearing = Geo.WrapAngle(min + s);
        }

        void StepSpawns()
        {
            for (int i = nextSpawn; i < schedule.Length; i++)
            {
                if (incomingSent[i] || Time < schedule[i].t - 4f) continue;
                incomingSent[i] = true;
                if (Map.Routes.TryGetValue(schedule[i].route, out var r))
                    Emit(SimEventType.ShipIncoming, index: i, pos: EntryPoint(r));
            }
            while (nextSpawn < schedule.Length && Time >= schedule[nextSpawn].t)
            {
                var def = schedule[nextSpawn++];
                if (!Map.Routes.TryGetValue(def.route, out var route))
                {
                    Debug.LogWarning($"[Sim] unknown route {def.route}");
                    continue;
                }
                var type = ShipStats.Parse(def.type);
                var ship = new SimShip
                {
                    Id = nextShipId++,
                    Type = type,
                    Stats = ShipStats.For(type),
                    Name = def.name,
                    Captain = def.captain,
                    Damaged = def.damaged,
                    Route = route,
                    Pos = route.Points[0],
                    Heading = Geo.Bearing(route.Points[1] - route.Points[0]),
                };
                ship.Speed = ship.Stats.Speed * (ship.Damaged ? 0.7f : 1f);
                Ships.Add(ship);
                Emit(SimEventType.ShipSpawned, ship);
            }
        }

        /// <summary>Where a route crosses into the play area (for the incoming chevrons).</summary>
        public Vector2 EntryPoint(Route r)
        {
            var b = Map.Bounds;
            for (int i = 0; i < r.Points.Length - 1; i++)
            {
                var a = r.Points[i];
                var c = r.Points[i + 1];
                for (int k = 0; k <= 40; k++)
                {
                    var p = Vector2.Lerp(a, c, k / 40f);
                    if (Map.InBounds(p)) return p;
                }
            }
            return r.Points[0];
        }

        // ------------------------------------------------------------------ ships

        void SetState(SimShip s, ShipState state)
        {
            if (s.State == state) return;
            s.State = state;
            s.StateTime = 0f;
            if (state != ShipState.Lured) s.LuredBy = null;
        }

        void StepShip(SimShip s, float dt)
        {
            if (!s.Active) return;
            var st = s.Stats;
            s.Age += dt;
            s.StateTime += dt;
            bool wasInside = s.Inside;
            s.Inside = Map.InBounds(s.Pos);
            if (s.Inside && !s.EverInside)
            {
                s.EverInside = true;
                Emit(SimEventType.ShipEntered, s);
            }

            // ---- light and confidence
            bool wasLit = s.Lit;
            s.Light = BeamAt(s.Pos);
            s.Lit = s.Light >= SimBeam.LitThreshold;
            s.InAura = InAura(s.Pos);
            s.InFog = FogAt(s.Pos) > 0.35f;
            if (s.Lit && !wasLit && s.TimeSinceLit > 1.2f) Emit(SimEventType.ShipLit, s);
            s.TimeSinceLit = s.Lit ? 0f : s.TimeSinceLit + dt;

            if (!s.Inside) { /* out at sea with their own bearings */ }
            else if (s.Lit) s.Confidence = Mathf.Min(1f, s.Confidence + 2.4f * dt);
            else if (s.InAura) s.Confidence = Mathf.Min(1f, s.Confidence + 0.4f * dt);
            else if (s.State != ShipState.Lured)
            {
                float drain = dt / st.DrainTime * Mission.drainScale;
                if (s.InFog) drain *= 1.6f;
                if (s.Damaged) drain *= 1.5f;
                s.Confidence = Mathf.Max(0f, s.Confidence - drain);
            }

            // ---- state machine
            switch (s.State)
            {
                case ShipState.Sailing:
                    if (s.Confidence <= 0f)
                    {
                        SetState(s, ShipState.Lost);
                        s.EverLost = true;
                        Emit(SimEventType.ShipLost, s);
                    }
                    break;
                case ShipState.Lost:
                    if (s.Lit || s.Confidence > 0.25f)
                    {
                        SetState(s, ShipState.Sailing);
                        Emit(SimEventType.ShipFound, s);
                    }
                    break;
                case ShipState.Lured:
                    if (s.Lit)
                    {
                        SetState(s, ShipState.Sailing);
                        Emit(SimEventType.ShipFreed, s);
                    }
                    break;
            }
            if ((s.State == ShipState.Sailing || s.State == ShipState.Lost) && s.Inside && !s.Lit)
            {
                foreach (var w in Wreckers)
                {
                    // A captain who has just seen the true light is not fooled; a doubtful one is.
                    if (FalseBeamAt(w, s.Pos) < 0.3f || s.Confidence > 0.5f) continue;
                    SetState(s, ShipState.Lured);
                    s.LuredBy = w;
                    s.EverLured = true;
                    Emit(SimEventType.ShipLured, s, w.Index);
                    break;
                }
            }

            // ---- distress flares from damaged ships nobody is looking at
            if (s.Damaged && s.Inside)
            {
                s.FlareTimer -= dt;
                if (s.FlareTimer <= 0f)
                {
                    s.FlareTimer = 18f;
                    if (s.TimeSinceLit > 3f) Emit(SimEventType.ShipFlare, s);
                }
            }

            // ---- steering
            float speedFactor;
            float desiredHeading;
            var drift = Current;
            switch (s.State)
            {
                case ShipState.Lost:
                    desiredHeading = s.Heading + Geo.Noise1(s.Age * 0.35f, s.Id) * 0.9f;
                    speedFactor = 0.45f;
                    drift = Current * 2f + new Vector2(0f, -0.9f);
                    break;
                case ShipState.Lured:
                    speedFactor = 0.9f;
                    desiredHeading = Crab((s.LuredBy.Site.Hazard - s.Pos).normalized, st.Speed * speedFactor);
                    break;
                default:
                    speedFactor = s.Lit || s.InAura || !s.Inside ? 1f : 0.72f;
                    desiredHeading = Crab(SteerSailing(s), st.Speed * speedFactor * (s.Damaged ? 0.7f : 1f));
                    break;
            }
            if (s.Damaged) speedFactor *= 0.7f;

            float turn = st.TurnRate * Mathf.Deg2Rad * dt * (s.Damaged ? 0.8f : 1f);
            if (s.State == ShipState.Lost) turn *= 0.6f;
            s.Heading = Geo.WrapAngle(s.Heading + Mathf.Clamp(Geo.DeltaAngle(s.Heading, desiredHeading), -turn, turn));
            s.Speed = Mathf.MoveTowards(s.Speed, st.Speed * speedFactor, 1.6f * dt);
            s.Velocity = s.Forward * s.Speed + drift;
            s.Pos += s.Velocity * dt;

            AdvanceWaypoint(s);
            CheckArrival(s);
            if (s.Active) CheckCollisions(s);
        }

        /// <summary>The point `ahead` units along the route from the ship's position on its current leg.</summary>
        static Vector2 Carrot(SimShip s, float ahead)
        {
            var pts = s.Route.Points;
            int i = Mathf.Clamp(s.Waypoint, 1, pts.Length - 1);
            var a = pts[i - 1];
            var b = pts[i];
            var ab = b - a;
            float len = ab.magnitude;
            float t = len > 1e-4f ? Mathf.Clamp01(Vector2.Dot(s.Pos - a, ab) / (len * len)) : 1f;
            float remaining = ahead;
            var p = a + ab * t;
            float left = len * (1f - t);
            while (remaining > left && i < pts.Length - 1)
            {
                remaining -= left;
                p = pts[i];
                i++;
                left = Vector2.Distance(p, pts[i]);
            }
            if (i >= pts.Length - 1 && remaining > left)
            {
                // Past the end: keep going straight out (through-traffic leaving the map).
                var dir = (pts[pts.Length - 1] - pts[pts.Length - 2]).normalized;
                return pts[pts.Length - 1] + dir * (remaining - left);
            }
            return p + (pts[i] - p).normalized * remaining;
        }

        /// <summary>Heading that makes good the desired track through the water's current.</summary>
        float Crab(Vector2 track, float speed)
        {
            if (Current.sqrMagnitude < 1e-4f) return Geo.Bearing(track);
            var head = track * Mathf.Max(speed, 0.5f) - Current;
            return Geo.Bearing(head);
        }

        static Vector2[] EllipsePoly(Shoal sh, float pad, int n = 16)
        {
            var pts = new Vector2[n];
            float a = sh.Angle * Mathf.Deg2Rad;
            var ax = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var ay = new Vector2(-ax.y, ax.x);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n * Mathf.PI * 2f;
                pts[i] = sh.Pos + ax * Mathf.Cos(t) * (sh.Rx + pad) + ay * Mathf.Sin(t) * (sh.Rz + pad);
            }
            return pts;
        }

        Vector2 SteerSailing(SimShip s)
        {
            var st = s.Stats;
            // Pure pursuit: aim at a point a look-ahead along the route, so ships round their
            // waypoints smoothly and see hazards on the next leg in good time.
            var target = Carrot(s, Mathf.Max(st.Lookahead * 0.8f, 12f));
            var toTarget = target - s.Pos;
            var goal = toTarget.sqrMagnitude > 1e-4f ? toTarget.normalized : s.Forward;
            var desired = goal;

            // Known obstacles: stacks always; reefs and (for deep hulls) shoals only once charted.
            obstacles.Clear();
            float clearance = st.Radius + 2.6f;
            float memory = st.Lookahead * 2.5f + 10f;
            foreach (var k in Map.Stacks) obstacles.Add(new Obstacle { C = k.Pos, R = k.Radius + clearance });
            foreach (var r in Reefs)
            {
                float d2 = (r.Pos - s.Pos).sqrMagnitude;
                // A captain who has seen a reef charted near his course remembers it until he is past.
                if (r.Charted && d2 < memory * memory) s.KnownReefs.Add(r.Index);
                else if (d2 > memory * memory * 1.6f) s.KnownReefs.Remove(r.Index);
                if (r.Charted || s.KnownReefs.Contains(r.Index)) obstacles.Add(new Obstacle { C = r.Pos, R = r.Radius + clearance });
            }
            if (st.DeepDraught)
                for (int i = 0; i < Shoals.Count; i++)
                {
                    var sh = Shoals[i];
                    float d = Vector2.Distance(sh.Def.Pos, s.Pos) - sh.Def.Rx;
                    if (sh.Charted && d < memory) s.KnownShoals.Add(i);
                    else if (d > memory * 1.3f) s.KnownShoals.Remove(i);
                    if (sh.Charted || s.KnownShoals.Contains(i))
                        obstacles.Add(new Obstacle { C = sh.Def.Pos, R = sh.Def.Rx + clearance, Poly = EllipsePoly(sh.Def, clearance * 0.8f) });
                }

            // If the waypoint itself sits inside a known hazard's buffer, move on to the next one.
            if (s.Waypoint < s.Route.Points.Length - 1)
            {
                var wp = s.Route.Points[s.Waypoint];
                foreach (var o in obstacles)
                    if (o.Poly != null ? Geo.PointInPolygon(o.Poly, wp) : (wp - o.C).sqrMagnitude < o.R * o.R) { s.Waypoint++; break; }
            }

            GroupObstacles();
            s.SteerDebug = "";
            bool avoiding = false;
            for (int iter = 0; iter < 4; iter++)
            {
                int hit = FirstHit(s.Pos, desired, st.Lookahead, float.MaxValue, -1);
                if (hit < 0) break;
                avoiding = true;
                int grp = groupOf[hit];
                float baseAng = Geo.Bearing(obstacles[hit].C - s.Pos);
                // Angular extent of the whole group of overlapping hazards, as seen from the ship.
                float maxA = -Mathf.PI, minA = Mathf.PI;
                bool inside = false;
                for (int i = 0; i < obstacles.Count; i++)
                {
                    if (groupOf[i] != grp) continue;
                    var ob = obstacles[i];
                    var rel = ob.C - s.Pos;
                    float dist = rel.magnitude;
                    if (ob.Poly == null)
                    {
                        if (dist < ob.R) { inside = true; continue; }
                        float th = Geo.DeltaAngle(baseAng, Geo.Bearing(rel));
                        float half = Mathf.Asin(Mathf.Clamp01(ob.R / Mathf.Max(dist, 0.01f)));
                        maxA = Mathf.Max(maxA, th + half);
                        minA = Mathf.Min(minA, th - half);
                    }
                    else
                    {
                        if (Geo.PointInPolygon(ob.Poly, s.Pos)) { inside = true; continue; }
                        foreach (var v in ob.Poly)
                        {
                            float da = Geo.DeltaAngle(baseAng, Geo.Bearing(v - s.Pos));
                            maxA = Mathf.Max(maxA, da);
                            minA = Mathf.Min(minA, da);
                        }
                    }
                }
                if (inside || maxA < minA)
                {
                    maxA = Mathf.Max(maxA, Mathf.PI * 0.5f + 0.2f);
                    minA = Mathf.Min(minA, -Mathf.PI * 0.5f - 0.2f);
                }
                float margin = (8f + 140f / st.TurnRate) * Mathf.Deg2Rad;   // slow turners give more room
                float a = baseAng + maxA + margin;
                float b = baseAng + minA - margin;
                float goalAng = Geo.Bearing(goal);
                // Cost of each side: how far it turns us from the goal, plus a penalty if that
                // side runs straight into another known hazard.
                float hitDist = Vector2.Distance(obstacles[hit].C, s.Pos);
                int blockA = FirstHit(s.Pos, Geo.Dir(a), st.Lookahead * 1.2f, float.MaxValue, grp);
                int blockB = FirstHit(s.Pos, Geo.Dir(b), st.Lookahead * 1.2f, float.MaxValue, grp);
                float costA = Mathf.Abs(Geo.DeltaAngle(goalAng, a)) + (blockA >= 0 ? 1.2f : 0f);
                float costB = Mathf.Abs(Geo.DeltaAngle(goalAng, b)) + (blockB >= 0 ? 1.2f : 0f);
                int side = costA <= costB ? 1 : -1;
                // Commit: once a side is chosen for this group, hold it until the group is passed,
                // unless that side is now blocked close at hand. Dithering is what runs ships aground.
                int groupKey = GroupKey(grp);
                if (s.AvoidSide != 0 && s.AvoidObstacle == groupKey && side != s.AvoidSide)
                {
                    int block = s.AvoidSide > 0 ? blockA : blockB;
                    bool blockedNear = block >= 0 && Vector2.Distance(obstacles[block].C, s.Pos) < hitDist + obstacles[block].R;
                    if (!blockedNear) side = s.AvoidSide;
                }
                if (iter == 0) { s.AvoidSide = side; s.AvoidObstacle = groupKey; }
                desired = Geo.Dir(side > 0 ? a : b);
                if (iter == 0) s.SteerDebug = $"hit={hit} grp={grp} base={baseAng * Mathf.Rad2Deg:0} a={a * Mathf.Rad2Deg:0} b={b * Mathf.Rad2Deg:0} side={side} goal={goalAng * Mathf.Rad2Deg:0}";
            }
            if (!avoiding) { s.AvoidSide = 0; s.AvoidObstacle = -1; }

            // Keep off the coast (except when running into the harbour mouth).
            bool nearHarbor = s.Route.ToHarbor && (s.Pos - Map.Harbor).sqrMagnitude < 26f * 26f;
            if (!nearHarbor && s.Inside)
            {
                var probe = s.Pos + desired * st.Lookahead * 0.6f;
                float d = Map.DistanceToCoast(probe, out var closest);
                bool probeOnLand = Map.OnLand(probe);
                if (probeOnLand || d < 9f)
                {
                    var away = (probe - closest).normalized;
                    if (probeOnLand) away = -away;
                    float w = probeOnLand ? 1.5f : (9f - d) / 9f;
                    desired = (desired + away * w).normalized;
                }
            }

            // Gentle separation from other ships.
            foreach (var o in Ships)
            {
                if (o == s || !o.Active) continue;
                var rel = s.Pos - o.Pos;
                float min = st.Radius + o.Stats.Radius + 3f;
                float d2 = rel.sqrMagnitude;
                if (d2 > min * min || d2 < 1e-4f) continue;
                float d = Mathf.Sqrt(d2);
                desired = (desired + rel / d * (1f - d / min) * 0.8f).normalized;
            }
            return desired;
        }

        /// <summary>Index of the nearest known obstacle the ray (pos, dir) runs into, or -1.</summary>
        int FirstHit(Vector2 pos, Vector2 dir, float lookahead, float targetDist, int excludeGroup)
        {
            int hit = -1;
            float best = float.MaxValue;
            for (int i = 0; i < obstacles.Count; i++)
            {
                if (excludeGroup >= 0 && i < groupOf.Count && groupOf[i] == excludeGroup) continue;
                var o = obstacles[i];
                var rel = o.C - pos;
                float t = Vector2.Dot(rel, dir);
                if (t < -o.R || t > lookahead + o.R) continue;
                if (t > targetDist + o.R && rel.sqrMagnitude > o.R * o.R) continue;
                float perp = Mathf.Abs(Geo.Cross(dir, rel));
                if (perp >= o.R) continue;
                float key;
                if (o.Poly == null) key = rel.sqrMagnitude < o.R * o.R ? -1f : t;
                else
                {
                    if (Geo.PointInPolygon(o.Poly, pos)) key = -1f;
                    else
                    {
                        float tHit = RayPolygon(pos, dir, o.Poly);
                        if (tHit < 0f || tHit > lookahead || tHit > targetDist + 2f) continue;
                        key = tHit;
                    }
                }
                if (key < best) { best = key; hit = i; }
            }
            return hit;
        }

        readonly List<int> groupOf = new List<int>(64);

        /// <summary>Union overlapping hazard buffers: a ship has to go round the lot.</summary>
        void GroupObstacles()
        {
            groupOf.Clear();
            for (int i = 0; i < obstacles.Count; i++) groupOf.Add(i);
            for (int i = 0; i < obstacles.Count; i++)
                for (int j = i + 1; j < obstacles.Count; j++)
                {
                    var a = obstacles[i];
                    var b = obstacles[j];
                    if ((a.C - b.C).sqrMagnitude >= (a.R + b.R) * (a.R + b.R)) continue;
                    int ga = Root(i), gb = Root(j);
                    if (ga != gb) groupOf[Mathf.Max(ga, gb)] = Mathf.Min(ga, gb);
                }
            for (int i = 0; i < obstacles.Count; i++) groupOf[i] = Root(i);
        }

        int Root(int i)
        {
            while (groupOf[i] != i) i = groupOf[i];
            return i;
        }

        /// <summary>A stable id for a group across steps (its first member's centre, hashed).</summary>
        int GroupKey(int grp)
        {
            var c = obstacles[grp].C;
            return Mathf.RoundToInt(c.x * 7f) * 1000 + Mathf.RoundToInt(c.y * 7f);
        }

        /// <summary>Distance along the ray to the polygon outline, or -1.</summary>
        static float RayPolygon(Vector2 pos, Vector2 dir, Vector2[] poly)
        {
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[j];
                var e = poly[i] - a;
                float den = Geo.Cross(dir, e);
                if (Mathf.Abs(den) < 1e-6f) continue;
                var w = a - pos;
                float t = Geo.Cross(w, e) / den;
                float u = Geo.Cross(w, dir) / den;
                if (t >= 0f && u >= 0f && u <= 1f && t < best) best = t;
            }
            return best == float.MaxValue ? -1f : best;
        }

        void AdvanceWaypoint(SimShip s)
        {
            var pts = s.Route.Points;
            while (s.Waypoint < pts.Length - 1)
            {
                var wp = pts[s.Waypoint];
                var next = pts[s.Waypoint + 1];
                bool close = (s.Pos - wp).sqrMagnitude < s.Stats.WaypointRadius * s.Stats.WaypointRadius;
                bool passed = Vector2.Dot(s.Pos - wp, next - wp) > 0f && (s.Pos - wp).sqrMagnitude < 40f * 40f;
                if (!close && !passed) break;
                s.Waypoint++;
            }
        }

        void CheckArrival(SimShip s)
        {
            if (s.State == ShipState.Lured) return;
            bool arrived;
            if (s.Route.ToHarbor) arrived = (s.Pos - Map.Harbor).sqrMagnitude < Map.HarborRadius * Map.HarborRadius;
            else arrived = s.Waypoint >= s.Route.Points.Length - 1 && s.EverInside && !Map.InBounds(s.Pos, 2f);
            if (!arrived) return;
            SetState(s, ShipState.Arrived);
            Arrivals++;
            int points = s.Stats.Points * (s.Damaged ? 2 : 1) + (s.SteadyHand ? 50 : 0);
            Score += points;
            Emit(SimEventType.ShipArrived, s, points);
        }

        void CheckCollisions(SimShip s)
        {
            var st = s.Stats;
            string cause = null;
            foreach (var k in Map.Stacks)
                if ((s.Pos - k.Pos).sqrMagnitude < Sq(k.Radius + st.Radius * 0.7f)) { cause = k.Name; break; }
            if (cause == null)
                foreach (var r in Reefs)
                    if ((s.Pos - r.Pos).sqrMagnitude < Sq(r.Radius + st.Radius * 0.55f)) { cause = ReefGroupName(r.Group); break; }
            if (cause == null && st.DeepDraught)
                foreach (var sh in Shoals)
                    if (sh.Def.Contains(s.Pos, -0.5f)) { cause = sh.Def.Name; break; }
            if (cause == null && s.Inside && Map.OnLand(s.Pos + s.Forward * st.Radius * 0.8f)) cause = "the shore";
            if (cause == null) return;
            s.WreckCause = cause;
            SetState(s, ShipState.Wrecked);
            Wrecks++;
            Emit(SimEventType.ShipWrecked, s, text: cause);
            if (Outcome == MissionOutcome.Running && Wrecks > Mission.allowedWrecks)
            {
                Outcome = MissionOutcome.Failed;
                Emit(SimEventType.MissionFailed);
            }
        }

        static float Sq(float x) => x * x;

        public static string ReefGroupName(string group) => group switch
        {
            "teeth" => "the Merrow Teeth",
            "widow" => "Widow's Ledge",
            "hens" => "the Hen's Chicks",
            "collar" => "Gannet's Collar",
            "outer" => "the Outer Ground",
            _ => "a reef",
        };

        // ------------------------------------------------------------------ results

        public int SteadyArrivals
        {
            get
            {
                int n = 0;
                foreach (var s in Ships) if (s.State == ShipState.Arrived && s.SteadyHand) n++;
                return n;
            }
        }

        /// <summary>Lamps earned: 1 survived, 2 no wrecks, 3 no wrecks and nobody ever lost or lured.</summary>
        public int Lamps
        {
            get
            {
                if (Outcome != MissionOutcome.Won) return 0;
                if (Wrecks > 0) return 1;
                foreach (var s in Ships) if (!s.SteadyHand) return 2;
                return 3;
            }
        }
    }
}
