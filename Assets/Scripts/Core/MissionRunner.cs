using System;
using System.Collections.Generic;
using LastLight.Sim;
using LastLight.View;
using UnityEngine;

namespace LastLight.Core
{
    /// <summary>
    /// Runs one night: steps the simulation at a fixed 60 Hz from the keeper's input (or the
    /// AutoKeeper), keeps the views in sync with interpolation, and broadcasts sim events to the
    /// audio, FX, radio and HUD layers.
    /// </summary>
    public sealed class MissionRunner : MonoBehaviour
    {
        public const float StepTime = 1f / 60f;

        public SimWorld World { get; private set; }
        public MissionDef Def { get; private set; }
        public bool AutoPlay;
        public bool Paused;
        /// <summary>Before the watch begins: nothing sails, the lens turns slowly on its own.</summary>
        public bool Holding;
        /// <summary>Title-screen mode: the lens sweeps steadily like a real light.</summary>
        public bool Attract;
        public float AttractTurn = 0.13f;
        public float TimeScale = 1f;
        public event Action<SimEvent> OnEvent;
        public KeeperControls Controls { get; } = new KeeperControls();
        public AutoKeeper Bot { get; } = new AutoKeeper();
        public float Bearing { get; private set; }
        /// <summary>Milliseconds spent stepping the simulation last frame (profiling).</summary>
        public float StepMs { get; private set; }
        readonly System.Diagnostics.Stopwatch stepWatch = new System.Diagnostics.Stopwatch();

        WorldView world;
        Transform root;
        readonly Dictionary<SimShip, ShipView> ships = new Dictionary<SimShip, ShipView>();
        readonly List<ShipView> finished = new List<ShipView>();
        readonly List<ReefView> reefs = new List<ReefView>();
        readonly List<BuoyView> buoys = new List<BuoyView>();
        float accumulator;
        float prevBearing;
        KeeperInput lastInput;

        public IEnumerable<ShipView> ShipViews => ships.Values;

        public static MissionRunner Begin(MissionDef def, WorldView world, bool autoPlay, int seed = 7)
        {
            var go = new GameObject("Night " + def.night);
            var r = go.AddComponent<MissionRunner>();
            r.Def = def;
            r.world = world;
            r.AutoPlay = autoPlay;
            r.World = new SimWorld(world.Map, def, seed);
            r.root = go.transform;
            r.BuildViews();
            r.prevBearing = r.Bearing = r.World.Beam.Bearing;
            r.Controls.SyncTo(r.World.Beam.Bearing);
            ShaderGlobals.HazeAmount = def.haze;
            return r;
        }

        /// <summary>Continue the beam from where the title-screen lens was pointing.</summary>
        public void SetInitialBearing(float bearing)
        {
            World.Beam.Bearing = bearing;
            prevBearing = Bearing = bearing;
            Controls.SyncTo(bearing);
        }

        void BuildViews()
        {
            var hazards = new GameObject("Hazards").transform;
            hazards.SetParent(root, false);
            foreach (var r in World.Reefs) reefs.Add(ReefView.Create(r, hazards));
            foreach (var s in World.Shoals) ShoalView.Create(s, hazards);
            foreach (var b in World.Buoys) buoys.Add(BuoyView.Create(b, hazards));
            foreach (var w in World.Wreckers) WreckerView.Create(w, hazards);
        }

        void Update()
        {
            if (Paused) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f) * TimeScale;
            accumulator += dt;

            if (Holding)
            {
                prevBearing = World.Beam.Bearing;
                World.Beam.Step(new KeeperInput { Turn = AttractTurn }, dt);
                Bearing = World.Beam.Bearing;
                Controls.SyncTo(Bearing);
                Render(1f);
                return;
            }

            Controls.Sensitivity = SaveData.Current.turnSpeed;
            KeeperInput input = AutoPlay || Attract ? new KeeperInput { Turn = AttractTurn } : Controls.Read(World.Beam.Origin);
            int steps = 0;
            stepWatch.Start();
            while (accumulator >= StepTime && steps < 12)
            {
                if (AutoPlay && !Attract) input = Bot.Decide(World, StepTime);
                foreach (var v in ships.Values) v.BeforeStep();
                prevBearing = World.Beam.Bearing;
                World.Step(StepTime, input);
                lastInput = input;
                if (input.Horn) { Controls.ConsumeHorn(); input.Horn = false; }
                foreach (var v in ships.Values) v.AfterStep();
                Dispatch();
                accumulator -= StepTime;
                steps++;
            }
            stepWatch.Stop();
            if (steps == 12) accumulator = 0f;

            StepMs = (float)stepWatch.Elapsed.TotalMilliseconds;
            stepWatch.Reset();

            float alpha = Mathf.Clamp01(accumulator / StepTime);
            Bearing = prevBearing + Geo.DeltaAngle(prevBearing, World.Beam.Bearing) * alpha;
            Render(alpha);
        }

        void Dispatch()
        {
            foreach (var e in World.Events)
            {
                switch (e.Type)
                {
                    case SimEventType.ShipSpawned:
                        ships[e.Ship] = ShipView.Create(e.Ship, root);
                        break;
                    case SimEventType.ShipLit:
                    case SimEventType.ShipFound:
                    case SimEventType.ShipFreed:
                        if (ships.TryGetValue(e.Ship, out var sv)) sv.OnLit();
                        break;
                    case SimEventType.ReefCharted:
                        if (e.Index >= 0 && e.Index < reefs.Count) reefs[e.Index].OnCharted();
                        break;
                    case SimEventType.BuoyLit:
                        if (e.Index >= 0 && e.Index < buoys.Count) buoys[e.Index].OnLit();
                        break;
                }
                OnEvent?.Invoke(e);
            }
        }

        void Render(float alpha)
        {
            finished.Clear();
            foreach (var v in ships.Values)
            {
                v.Render(alpha);
                if (v.Finished) finished.Add(v);
            }
            foreach (var v in finished)
            {
                ships.Remove(v.Ship);
                Destroy(v.gameObject);
            }
            var cam = CameraRig.Instance != null ? CameraRig.Instance.Cam : Camera.main;
            float flicker = 1f + Mathf.Sin(Time.time * 23f) * 0.012f + (Mathf.PerlinNoise(Time.time * 3f, 0.5f) - 0.5f) * 0.05f;
            ShaderGlobals.PushBeam(World.Beam, Bearing, flicker);
            ShaderGlobals.PushWorld(World, Time.time);
            world.Lighthouse.Render(World.Beam, Bearing, cam);
        }

        public void Teardown()
        {
            foreach (var v in ships.Values) if (v != null) Destroy(v.gameObject);
            ships.Clear();
            ShaderGlobals.ClearWorld();
            Destroy(gameObject);
        }

        public KeeperInput LastInput => lastInput;
    }
}
