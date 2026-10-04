using UnityEngine;

namespace LastLight.Sim
{
    /// <summary>What the keeper (player or bot) is doing this step.</summary>
    public struct KeeperInput
    {
        public bool HasTarget;       // aim at TargetBearing (mouse / stick / bot)
        public float TargetBearing;  // radians
        public float Turn;           // -1..1 direct turning (keyboard), used when HasTarget is false
        public bool Focus;
        public bool Horn;
    }

    /// <summary>
    /// The lighthouse lens: a heavy rotating mass that chases the aim bearing with a capped speed,
    /// plus the wide/focused beam shape. The beam is a soft-edged wedge on the sea plane; the same
    /// formula is evaluated in the shaders (LLBeam.hlsl) so what looks lit is what is lit.
    /// </summary>
    public sealed class SimBeam
    {
        public Vector2 Origin;
        public float Height;
        public float Bearing;            // radians
        public float AngularVelocity;    // radians per second
        public float Focus;              // 0 wide .. 1 focused (smoothed)
        public float Power = 1f;         // lamp output, 0 when extinguished
        public float Rain;               // 0..1, shortens the reach

        public const float WideHalfDeg = 13f;
        public const float FocusHalfDeg = 4.5f;
        public const float WideRange = 112f;
        public const float FocusRange = 188f;
        public const float MaxSpeedWide = 200f;   // deg/s
        public const float MaxSpeedFocus = 115f;  // deg/s
        public const float Accel = 1150f;         // deg/s^2
        public const float SpringOmega = 11f;     // rad/s, natural frequency of the lens on its aim
        public const float SpringDamping = 0.6f;  // < 1: big swings overshoot ~2.5 deg and settle
        public const float LitThreshold = 0.25f;

        public float HalfAngle => Mathf.Lerp(WideHalfDeg, FocusHalfDeg, Focus) * Mathf.Deg2Rad;
        public float Range => Mathf.Lerp(WideRange, FocusRange, Focus) * (1f - 0.15f * Rain);
        public float Strength => Mathf.Lerp(1f, 1.3f, Focus) * Power;
        public Vector2 Direction => Geo.Dir(Bearing);
        public float FogExtinction => Mathf.Lerp(0.05f, 0.012f, Focus);

        public void Step(in KeeperInput input, float dt)
        {
            Focus = Mathf.MoveTowards(Focus, input.Focus ? 1f : 0f, dt / 0.18f);
            float maxSpeed = Mathf.Lerp(MaxSpeedWide, MaxSpeedFocus, Focus) * Mathf.Deg2Rad;
            float accelMax = Accel * Mathf.Deg2Rad;
            if (input.HasTarget)
            {
                // A heavy lens on a spring: it builds up speed, coasts, and settles with a slight
                // overshoot instead of stopping dead on the aim.
                float diff = Geo.DeltaAngle(Bearing, input.TargetBearing);
                float accel = SpringOmega * SpringOmega * diff - 2f * SpringDamping * SpringOmega * AngularVelocity;
                AngularVelocity += Mathf.Clamp(accel, -accelMax, accelMax) * dt;
                AngularVelocity = Mathf.Clamp(AngularVelocity, -maxSpeed, maxSpeed);
            }
            else
            {
                float desired = Mathf.Clamp(input.Turn, -1f, 1f) * maxSpeed * 0.8f;
                AngularVelocity = Mathf.MoveTowards(AngularVelocity, desired, accelMax * dt);
            }
            Bearing = Geo.WrapAngle(Bearing + AngularVelocity * dt);
        }

        /// <summary>
        /// Unoccluded, fog-free wedge intensity at p (0..~1.3). Shared shape for the true beam and
        /// the wreckers' false lights.
        /// </summary>
        public static float Wedge(Vector2 origin, Vector2 dir, float halfAngle, float range, Vector2 p)
        {
            var d = p - origin;
            float dist = d.magnitude;
            if (dist < 2f) return 1f;
            float cosA = Vector2.Dot(d / dist, dir);
            float outer = Mathf.Cos(halfAngle);
            float inner = Mathf.Cos(halfAngle * 0.55f);
            float wedge = Geo.SmoothStep01(outer, inner, cosA);
            if (wedge <= 0f) return 0f;
            float reach = 1f - Geo.SmoothStep01(range * 0.72f, range, dist);
            float atten = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(dist / range));
            return wedge * reach * atten;
        }
    }
}
