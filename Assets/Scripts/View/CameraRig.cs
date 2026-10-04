using UnityEngine;

namespace LastLight.View
{
    /// <summary>
    /// The camera: a fixed three-quarter view framing all of Merrow Bay, with a slow idle sway,
    /// trauma-based shake and smooth blends between poses (title close-up, play view, ending).
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public struct Pose
        {
            public Vector3 Position;
            public Vector3 LookAt;
            public float Fov;

            public Pose(Vector3 position, Vector3 lookAt, float fov)
            {
                Position = position;
                LookAt = lookAt;
                Fov = fov;
            }
        }

        public static readonly Pose PlayPose = new Pose(new Vector3(0f, 236f, -142f), new Vector3(0f, 0f, 42f), 30f);
        public static readonly Pose TitlePose = new Pose(new Vector3(-52f, 9f, -20f), new Vector3(0f, 17f, 30f), 40f);
        public static readonly Pose EndingPose = new Pose(new Vector3(-30f, 16f, -24f), new Vector3(-20f, 20f, 120f), 44f);

        public Camera Cam { get; private set; }
        Pose from, to, current;
        float blend = 1f, blendTime = 1f;
        float trauma;
        public float SwayAmount = 1f;
        Vector3 lookOffset;

        public static CameraRig Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            Cam = GetComponent<Camera>();
            current = from = to = PlayPose;
        }

        public void Snap(Pose pose)
        {
            from = to = current = pose;
            blend = 1f;
            Apply(current, 0f);
        }

        public void BlendTo(Pose pose, float seconds)
        {
            from = current;
            to = pose;
            blend = 0f;
            blendTime = Mathf.Max(0.01f, seconds);
        }

        public bool Blending => blend < 1f;

        public void Shake(float amount) => trauma = Mathf.Min(1f, trauma + amount);

        /// <summary>A gentle pull of the view towards where the beam points (play view only).</summary>
        public void SetLookBias(Vector3 bias) => lookOffset = Vector3.Lerp(lookOffset, bias, 0.05f);

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (blend < 1f)
            {
                blend = Mathf.Min(1f, blend + dt / blendTime);
                float t = blend * blend * (3f - 2f * blend);
                t = t * t * (3f - 2f * t);
                current.Position = Vector3.Lerp(from.Position, to.Position, t);
                current.LookAt = Vector3.Lerp(from.LookAt, to.LookAt, t);
                current.Fov = Mathf.Lerp(from.Fov, to.Fov, t);
                // Swoop: lift through an arc between distant poses.
                current.Position.y += Mathf.Sin(t * Mathf.PI) * Vector3.Distance(from.Position, to.Position) * 0.12f;
            }
            else current = to;
            trauma = Mathf.Max(0f, trauma - dt * 1.4f);
            Apply(current, Time.unscaledTime);
        }

        void Apply(Pose p, float time)
        {
            float sway = SwayAmount;
            var swayOffset = new Vector3(Mathf.Sin(time * 0.13f) * 1.6f, Mathf.Sin(time * 0.21f + 1f) * 0.9f, Mathf.Sin(time * 0.17f + 2f) * 1.2f) * sway;
            float shake = trauma * trauma;
            var shakeOffset = new Vector3(
                (Mathf.PerlinNoise(time * 22f, 0.3f) - 0.5f),
                (Mathf.PerlinNoise(0.7f, time * 22f) - 0.5f),
                (Mathf.PerlinNoise(time * 22f, 5.1f) - 0.5f)) * shake * 6f;
            var pos = p.Position + swayOffset + shakeOffset;
            transform.position = pos;
            var look = p.LookAt + lookOffset * sway;
            transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up) * Quaternion.Euler(0, 0, (Mathf.PerlinNoise(time * 18f, 9f) - 0.5f) * shake * 3f);
            if (Cam != null) Cam.fieldOfView = p.Fov;
        }

        /// <summary>Mouse ray onto the sea plane (y = 0).</summary>
        public bool ScreenToSea(Vector2 screen, out Vector3 point)
        {
            point = default;
            if (Cam == null) return false;
            var ray = Cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return false;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return false;
            point = ray.origin + ray.direction * t;
            return true;
        }
    }
}
