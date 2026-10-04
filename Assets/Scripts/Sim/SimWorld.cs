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
        readonly List<(Vector2 c, float r)> obstacles = new List<(Vector2, float)>(64);

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
                            w.DouseProgress += dt / (Beam.Focus > 0.5f ? 0.6f : 1.4f);
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
                    if (FalseBeamAt(w, s.Pos) < 0.3f) continue;
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
                    desiredHeading = Geo.Bearing(s.LuredBy.Site.Hazard - s.Pos);
                    speedFactor = 0.9f;
                    break;
                default:
                    desiredHeading = Geo.Bearing(SteerSailing(s));
                    speedFactor = s.Lit || s.InAura || !s.Inside ? 1f : 0.72f;
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

        Vector2 SteerSailing(SimShip s)
        {
            var st = s.Stats;
            var target = s.Route.Points[Mathf.Min(s.Waypoint, s.Route.Points.Length - 1)];
            var toTarget = target - s.Pos;
            var goal = toTarget.sqrMagnitude > 1e-4f ? toTarget.normalized : s.Forward;
            var desired = goal;

            // Known obstacles: stacks always; reefs and (for deep hulls) shoals only once charted.
            obstacles.Clear();
            float clearance = st.Radius + 2.2f;
            foreach (var k in Map.Stacks) obstacles.Add((k.Pos, k.Radius + clearance));
            foreach (var r in Reefs) if (r.Charted) obstacles.Add((r.Pos, r.Radius + clearance));
            if (st.DeepDraught)
                foreach (var sh in Shoals)
                    if (sh.Charted)
                        foreach (var p in sh.Axis) obstacles.Add((p, sh.Def.Rz + clearance));

            // If the waypoint itself sits inside a known hazard's buffer, move on to the next one.
            if (s.Waypoint < s.Route.Points.Length - 1)
                foreach (var (c, R) in obstacles)
                    if ((target - c).sqrMagnitude < R * R) { s.Waypoint++; break; }

            bool avoiding = false;
            for (int iter = 0; iter < 4; iter++)
            {
                int hit = FirstHit(s.Pos, desired, st.Lookahead, toTarget.magnitude, -1);
                if (hit < 0) break;
                avoiding = true;
                var (oc, oR) = obstacles[hit];
                var orel = oc - s.Pos;
                float dist = orel.magnitude;
                float half = Mathf.Asin(Mathf.Clamp01(oR / Mathf.Max(dist, 0.01f))) + 8f * Mathf.Deg2Rad;
                if (dist < oR) half = Mathf.PI * 0.5f + 0.2f; // inside the buffer: turn away hard
                float baseAng = Geo.Bearing(orel);
                float a = baseAng + half, b = baseAng - half;
                float goalAng = Geo.Bearing(goal);
                // Cost of each side: how far it turns us from the goal, plus a penalty if that
                // side runs straight into another known hazard.
                float costA = Mathf.Abs(Geo.DeltaAngle(goalAng, a)) + (FirstHit(s.Pos, Geo.Dir(a), st.Lookahead * 1.4f, float.MaxValue, hit) >= 0 ? 1.2f : 0f);
                float costB = Mathf.Abs(Geo.DeltaAngle(goalAng, b)) + (FirstHit(s.Pos, Geo.Dir(b), st.Lookahead * 1.4f, float.MaxValue, hit) >= 0 ? 1.2f : 0f);
                int side = costA <= costB ? 1 : -1;
                // Stick with the previous choice for the same obstacle unless it is clearly worse.
                if (s.AvoidSide != 0 && s.AvoidObstacle == hit && side != s.AvoidSide)
                {
                    float keep = s.AvoidSide > 0 ? costA : costB;
                    float other = s.AvoidSide > 0 ? costB : costA;
                    if (keep < other + 0.5f) side = s.AvoidSide;
                }
                if (iter == 0) { s.AvoidSide = side; s.AvoidObstacle = hit; }
                desired = Geo.Dir(side > 0 ? a : b);
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
        int FirstHit(Vector2 pos, Vector2 dir, float lookahead, float targetDist, int exclude)
        {
            int hit = -1;
            float best = float.MaxValue;
            for (int i = 0; i < obstacles.Count; i++)
            {
                if (i == exclude) continue;
                var (c, R) = obstacles[i];
                var rel = c - pos;
                float t = Vector2.Dot(rel, dir);
                if (t < -R || t > lookahead + R) continue;
                if (t > targetDist + R && rel.sqrMagnitude > R * R) continue;
                float perp = Mathf.Abs(Geo.Cross(dir, rel));
                if (perp >= R) continue;
                float key = rel.sqrMagnitude < R * R ? -1f : t;
                if (key < best) { best = key; hit = i; }
            }
            return hit;
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
