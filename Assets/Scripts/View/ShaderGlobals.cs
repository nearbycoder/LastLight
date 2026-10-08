using LastLight.Sim;
using UnityEngine;

namespace LastLight.View
{
    /// <summary>Pushes the beam, fog, false lights and mood into the global shader state every frame.</summary>
    public static class ShaderGlobals
    {
        static readonly int BeamOrigin = Shader.PropertyToID("_LLBeamOrigin");
        static readonly int BeamDir = Shader.PropertyToID("_LLBeamDir");
        static readonly int BeamParams = Shader.PropertyToID("_LLBeamParams");
        static readonly int BeamColor = Shader.PropertyToID("_LLBeamColor");
        static readonly int BeamTrail = Shader.PropertyToID("_LLBeamTrail");
        static readonly int Occluders = Shader.PropertyToID("_LLOccluders");
        static readonly int OccluderCount = Shader.PropertyToID("_LLOccluderCount");
        static readonly int FogBanks = Shader.PropertyToID("_LLFogBanks");
        static readonly int FogCount = Shader.PropertyToID("_LLFogCount");
        static readonly int FalsePos = Shader.PropertyToID("_LLFalsePos");
        static readonly int FalseDir = Shader.PropertyToID("_LLFalseDir");
        static readonly int FalseCount = Shader.PropertyToID("_LLFalseCount");
        static readonly int FalseColor = Shader.PropertyToID("_LLFalseColor");
        static readonly int Flash = Shader.PropertyToID("_LLFlash");
        static readonly int Ambient = Shader.PropertyToID("_LLAmbient");
        static readonly int Haze = Shader.PropertyToID("_LLHaze");
        static readonly int Dawn = Shader.PropertyToID("_LLDawn");
        static readonly int AtmoSteps = Shader.PropertyToID("_LLAtmoSteps");
        static readonly int FrameIndex = Shader.PropertyToID("_LLFrameIndex");

        static readonly Vector4[] occ = new Vector4[8];
        static readonly Vector4[] fog = new Vector4[8];
        static readonly Vector4[] fpos = new Vector4[4];
        static readonly Vector4[] fdir = new Vector4[4];

        public static Color BeamTint = new Color(1f, 0.84f, 0.58f);
        public static float BeamBrightness = 2.4f;
        public static Color FalseTint = new Color(1f, 0.5f, 0.22f);
        public static Color AmbientColor = new Color(0.06f, 0.085f, 0.13f);
        public static float MoonBrightness = 1f;
        public static float HazeAmount = 1f;
        public static float DawnAmount;
        public static int Steps = 28;

        /// <summary>Beam with an explicit (interpolated) bearing.</summary>
        public static void PushBeam(SimBeam beam, float bearing, float flicker)
        {
            var dir = Geo.Dir(bearing);
            float half = beam.HalfAngle;
            Shader.SetGlobalVector(BeamOrigin, new Vector4(beam.Origin.x, beam.Height, beam.Origin.y, beam.Power));
            Shader.SetGlobalVector(BeamDir, new Vector4(dir.x, dir.y, Mathf.Cos(half), Mathf.Cos(half * 0.55f)));
            Shader.SetGlobalVector(BeamParams, new Vector4(beam.Range, beam.Strength * flicker, beam.Focus, beam.FogExtinction));
            Shader.SetGlobalVector(BeamColor, (Vector4)(BeamTint * BeamBrightness));
            float w = beam.AngularVelocity;
            var lag = Geo.Dir(bearing - w * TrailLag);
            float trail = TrailStrength * Mathf.Clamp01((Mathf.Abs(w) * Mathf.Rad2Deg - 30f) / 150f);
            Shader.SetGlobalVector(BeamTrail, new Vector4(lag.x, lag.y, 0f, trail));
        }

        public static float TrailLag = 0.07f;      // seconds of swing the afterglow lags behind
        public static float TrailStrength = 0.35f;

        public static void PushWorld(SimWorld w, float time)
        {
            int n = Mathf.Min(8, w.Map.Stacks.Count);
            for (int i = 0; i < 8; i++)
                occ[i] = i < n ? new Vector4(w.Map.Stacks[i].Pos.x, w.Map.Stacks[i].Pos.y, w.Map.Stacks[i].Radius, 0) : Vector4.zero;
            Shader.SetGlobalVectorArray(Occluders, occ);
            Shader.SetGlobalFloat(OccluderCount, n);

            int fc = Mathf.Min(8, w.Fog.Count);
            for (int i = 0; i < 8; i++)
                fog[i] = i < fc ? new Vector4(w.Fog[i].Pos.x, w.Fog[i].Pos.y, w.Fog[i].Radius, w.Fog[i].Density) : Vector4.zero;
            Shader.SetGlobalVectorArray(FogBanks, fog);
            Shader.SetGlobalFloat(FogCount, fc);

            int wc = Mathf.Min(4, w.Wreckers.Count);
            for (int i = 0; i < 4; i++)
            {
                if (i >= wc) { fpos[i] = Vector4.zero; fdir[i] = Vector4.zero; continue; }
                var wr = w.Wreckers[i];
                float flick = 0.8f + 0.2f * Mathf.PerlinNoise(time * 7f, i * 3.1f);
                float on = wr.Burning ? flick * (1f - wr.DouseProgress * 0.6f) : 0f;
                var site = wr.Site;
                var d = Geo.Dir(wr.Bearing);
                float half = SimWrecker.HalfAngle * Mathf.Deg2Rad;
                fpos[i] = new Vector4(site.Pos.x, site.Height + 2f, site.Pos.y, on);
                fdir[i] = new Vector4(d.x, d.y, Mathf.Cos(half), Mathf.Cos(half * 0.55f));
            }
            Shader.SetGlobalVectorArray(FalsePos, fpos);
            Shader.SetGlobalVectorArray(FalseDir, fdir);
            Shader.SetGlobalFloat(FalseCount, wc);
            Shader.SetGlobalVector(FalseColor, (Vector4)(FalseTint * 2.0f));
            Shader.SetGlobalFloat(Flash, w.Flash * FlashScale);
        }

        /// <summary>Settings: Reduce flashing scales the lightning's whole-bay flash.</summary>
        public static float FlashScale = 1f;

        public static void PushMood()
        {
            Shader.SetGlobalVector(Ambient, new Vector4(AmbientColor.r, AmbientColor.g, AmbientColor.b, MoonBrightness));
            Shader.SetGlobalFloat(Haze, HazeAmount);
            Shader.SetGlobalFloat(Dawn, DawnAmount);
            Shader.SetGlobalFloat(AtmoSteps, Steps);
            // Ultra's dither moves on every frame, even with the night paused, so its temporal
            // anti-aliasing averages it away.
            Shader.SetGlobalFloat(FrameIndex, Time.frameCount % 64);
        }

        /// <summary>Clears world-dependent state (title screen, no night running).</summary>
        public static void ClearWorld()
        {
            for (int i = 0; i < 8; i++) { fog[i] = Vector4.zero; }
            for (int i = 0; i < 4; i++) { fpos[i] = Vector4.zero; fdir[i] = Vector4.zero; }
            Shader.SetGlobalVectorArray(FogBanks, fog);
            Shader.SetGlobalFloat(FogCount, 0);
            Shader.SetGlobalVectorArray(FalsePos, fpos);
            Shader.SetGlobalVectorArray(FalseDir, fdir);
            Shader.SetGlobalFloat(FalseCount, 0);
            Shader.SetGlobalFloat(Flash, 0);
        }
    }
}
