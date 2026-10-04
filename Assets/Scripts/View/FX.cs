using LastLight.Audio;
using UnityEngine;

namespace LastLight.View
{
    /// <summary>Particle and decal effects, all built in code.</summary>
    public static class FX
    {
        static Material lit, add, rain;
        static Transform root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { lit = add = rain = null; root = null; }

        public static Material Lit => lit != null ? lit : lit = Resources.Load<Material>("Materials/LL_ParticleLit");
        public static Material Add => add != null ? add : add = Resources.Load<Material>("Materials/LL_ParticleAdd");

        static Material Rain
        {
            get
            {
                if (rain != null) return rain;
                rain = new Material(Lit);
                rain.SetFloat("_Streak", 1f);
                rain.SetFloat("_Softness", 1f);
                rain.SetFloat("_LightBoost", 3.5f);
                return rain;
            }
        }

        static Transform Root
        {
            get
            {
                if (root != null) return root;
                root = new GameObject("FX").transform;
                return root;
            }
        }

        static ParticleSystem System(string name, Transform parent, Material mat, int max = 200)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : Root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.Distance;
            return ps;
        }

        static Gradient Fade(Color c, float a0, float a1, float peakAt = 0.15f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) },
                      new[] { new GradientAlphaKey(a0 * 0.3f, 0), new GradientAlphaKey(a0, peakAt), new GradientAlphaKey(a1, 1) });
            return g;
        }

        // ---------------------------------------------------------------- continuous

        /// <summary>Foam wake streaming from a ship's stern; set the rate from its speed.</summary>
        public static ParticleSystem Wake(Transform stern, float width)
        {
            var ps = System("Wake", stern, Lit, 400);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(width * 0.5f, width * 0.9f);
            main.startColor = new Color(0.95f, 0.97f, 1f, 1f);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width * 0.6f, 0.05f, 0.5f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(Color.white, 0.55f, 0f, 0.1f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.4f));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            ps.Play();
            return ps;
        }

        /// <summary>Funnel smoke drifting off downwind.</summary>
        public static ParticleSystem Smoke(Transform funnel, float rate)
        {
            var ps = System("Smoke", funnel, Lit, 120);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startColor = new Color(0.28f, 0.27f, 0.27f, 1f);
            main.gravityModifier = -0.02f;
            var em = ps.emission;
            em.rateOverTime = rate;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(-90, 0, 0);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            vel.y = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.6f, -0.2f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(Color.white, 0.5f, 0f, 0.15f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 3.2f));
            ps.Play();
            return ps;
        }

        /// <summary>Rain over the bay (follows nothing; covers the play area).</summary>
        public static ParticleSystem RainSheet(float intensity)
        {
            var ps = System("Rain", null, Rain, 6000);
            ps.transform.position = new Vector3(0, 60, 40);
            var main = ps.main;
            main.startLifetime = 1.2f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            main.startColor = new Color(0.8f, 0.86f, 0.95f, 0.55f);
            var em = ps.emission;
            em.rateOverTime = 3500f * intensity;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(340, 4, 220);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-6f, -4f);
            vel.y = new ParticleSystem.MinMaxCurve(-55f, -48f);
            vel.z = new ParticleSystem.MinMaxCurve(-2f, -1f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.07f;
            r.lengthScale = 3f;
            ps.Play();
            return ps;
        }

        // ---------------------------------------------------------------- bursts

        static void Burst(ParticleSystem ps, int count, float destroyAfter)
        {
            ps.Emit(count);
            Object.Destroy(ps.gameObject, destroyAfter);
        }

        public static void Splash(Vector3 pos, float scale)
        {
            var ps = System("Splash", null, Lit, 300);
            ps.transform.position = pos;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 2.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f * scale, 13f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.6f * scale);
            main.startColor = new Color(0.92f, 0.96f, 1f, 1f);
            main.gravityModifier = 1.1f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 1.5f * scale;
            shape.rotation = new Vector3(-90, 0, 0);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(Color.white, 0.9f, 0f, 0.05f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.7f, 1, 1.8f));
            Burst(ps, (int)(70 * scale), 3f);
            FoamRing(pos, 3f * scale, 14f * scale, 2.5f);
        }

        public static void Sparks(Vector3 pos, Color color, int count = 60)
        {
            var ps = System("Sparks", null, Add, 200);
            ps.transform.position = pos;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startColor = color;
            main.gravityModifier = 0.9f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = Fade(Color.white, 1f, 0f, 0.02f);
            Burst(ps, count, 2.5f);
        }

        /// <summary>A ring of white water spreading on the sea.</summary>
        public static void FoamRing(Vector3 pos, float from, float to, float time)
        {
            var r = Glows.Ring("FoamRing", Root, 1f, new Color(0.9f, 0.95f, 1f) * 0.6f, new Vector4(0.8f, 0.97f, 0.06f, 0), new Vector4(1, 0, 0, 0));
            r.transform.position = new Vector3(pos.x, 0.25f, pos.z);
            var fx = r.gameObject.AddComponent<RingFx>();
            fx.From = from;
            fx.To = to;
            fx.Time = time;
            fx.Color = new Color(0.8f, 0.86f, 0.95f) * 0.5f;
        }

        /// <summary>The foghorn's shockwave rolling out across the bay.</summary>
        public static void HornWave(Vector3 origin)
        {
            for (int i = 0; i < 2; i++)
            {
                var r = Glows.Ring("HornWave", Root, 1f, Color.black, new Vector4(0.972f, 0.99f, 0.006f, 0), new Vector4(1, 0, 0, 0));
                r.transform.position = new Vector3(origin.x, 0.3f, origin.z);
                var fx = r.gameObject.AddComponent<RingFx>();
                fx.From = 8f;
                fx.To = 150f;
                fx.Time = 2.6f + i * 0.5f;
                fx.Delay = i * 0.35f;
                fx.Color = new Color(1f, 0.85f, 0.6f) * (1.4f - i * 0.6f);
            }
        }

        public static void ArrivalGlow(Vector3 pos)
        {
            var r = Glows.Ring("Arrival", Root, 1f, Color.black, new Vector4(0.75f, 0.95f, 0.05f, 0), new Vector4(1, 0, 0, 0));
            r.transform.position = new Vector3(pos.x, 0.3f, pos.z);
            var fx = r.gameObject.AddComponent<RingFx>();
            fx.From = 4f;
            fx.To = 22f;
            fx.Time = 1.6f;
            fx.Color = new Color(1.4f, 1.1f, 0.6f);
        }

        public static void ChartPing(Vector3 pos, float radius)
        {
            var r = Glows.Ring("Chart", Root, 1f, Color.black, new Vector4(0.85f, 0.97f, 0.04f, 0), new Vector4(1, 0, 0, 0));
            r.transform.position = new Vector3(pos.x, 0.35f, pos.z);
            var fx = r.gameObject.AddComponent<RingFx>();
            fx.From = radius * 1.2f;
            fx.To = radius * 3.2f;
            fx.Time = 1.1f;
            fx.Color = new Color(0.8f, 0.9f, 1.1f) * 0.9f;
        }

        public static void Flare(Vector3 from)
        {
            var go = new GameObject("Flare");
            go.transform.SetParent(Root, false);
            go.transform.position = from + Vector3.up * 2f;
            go.AddComponent<FlareFx>();
        }

        public static void Debris(Vector3 pos, Vector2 drift, bool lifeboat)
        {
            string[] parts = { "debris_plank", "debris_crate", "debris_barrel", "debris_plank" };
            for (int i = 0; i < parts.Length + (lifeboat ? 1 : 0); i++)
            {
                bool boat = i == parts.Length;
                var go = ModelLibrary.SpawnPart("debris", boat ? "debris_boat" : parts[i], Root);
                if (go == null)
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.Destroy(go.GetComponent<Collider>());
                    go.transform.SetParent(Root, false);
                    go.transform.localScale = new Vector3(0.4f, 0.15f, 2f);
                    go.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Lit(new Color(0.45f, 0.35f, 0.24f));
                }
                var f = go.AddComponent<Floater>();
                float a = Random.Range(0f, Mathf.PI * 2f);
                f.Pos = new Vector2(pos.x, pos.z) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1f, 4f);
                f.Drift = boat ? drift.normalized * 1.6f + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.3f : drift * 0.25f + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(0.3f, 1.2f);
                f.Spin = boat ? 0f : Random.Range(-40f, 40f);
                f.Life = boat ? 40f : Random.Range(18f, 28f);
                f.Yaw = boat ? Mathf.Atan2(f.Drift.x, f.Drift.y) * Mathf.Rad2Deg : Random.Range(0f, 360f);
                if (boat)
                {
                    var anchor = ModelLibrary.Find(go.transform, "lamp_boat");
                    var lampPos = anchor != null ? go.transform.InverseTransformPoint(anchor.position) : new Vector3(0, 1f, 0.5f);
                    Glows.Glow("Lifeboat Lamp", go.transform, lampPos, 1.6f, new Color(2.4f, 1.6f, 0.7f));
                }
            }
        }
    }

    /// <summary>Expanding, fading ring decal.</summary>
    public sealed class RingFx : MonoBehaviour
    {
        public float From = 1f, To = 10f, Time = 1f, Delay;
        public Color Color = Color.white;
        float t;
        Material mat;

        void Start()
        {
            mat = GetComponent<MeshRenderer>().sharedMaterial;
            Apply(0f);
        }

        void Update()
        {
            if (Delay > 0f) { Delay -= UnityEngine.Time.deltaTime; Apply(0f); return; }
            t += UnityEngine.Time.deltaTime / Time;
            if (t >= 1f) { Destroy(gameObject); return; }
            Apply(t);
        }

        void Apply(float k)
        {
            float e = 1f - Mathf.Pow(1f - k, 2.5f);
            float r = Mathf.Lerp(From, To, e);
            transform.localScale = new Vector3(r * 2f, 1f, r * 2f);
            mat.SetColor("_Color", Color * (Delay > 0f ? 0f : (1f - k) * Mathf.Clamp01(k * 12f)));
        }
    }

    /// <summary>Something floating on the swell, drifting and fading away.</summary>
    public sealed class Floater : MonoBehaviour
    {
        public Vector2 Pos, Drift;
        public float Spin, Yaw, Life = 20f;
        float age, seed;

        void Start() => seed = Random.Range(0f, 10f);

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            Pos += Drift * dt;
            Drift *= 1f - dt * 0.05f;
            Yaw += Spin * dt;
            float h = Waves.Height(Pos, Time.time);
            float sink = Mathf.Clamp01((age - (Life - 4f)) / 4f) * 1.5f;
            transform.position = new Vector3(Pos.x, h * 0.9f - sink + 0.1f, Pos.y);
            transform.rotation = Quaternion.Euler(Mathf.Sin(Time.time * 1.3f + seed) * 8f, Yaw, Mathf.Cos(Time.time * 1.1f + seed) * 8f);
            if (age > Life) Destroy(gameObject);
        }
    }

    /// <summary>A red distress flare: arcs up, hangs over the water lighting it, drifts down.</summary>
    public sealed class FlareFx : MonoBehaviour
    {
        float t;
        Vector3 start;
        MeshRenderer glow;
        Light light;
        ParticleSystem trail;

        void Start()
        {
            start = transform.position;
            glow = Glows.Glow("Flare Glow", transform, Vector3.zero, 5f, new Color(6f, 1.2f, 0.6f));
            light = Glows.PointLight("Flare Light", transform, Vector3.zero, new Color(1f, 0.25f, 0.15f), 0f, 55f);
            trail = FX.Smoke(transform, 14f);
            var main = trail.main;
            main.startColor = new Color(0.6f, 0.45f, 0.45f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSpeed = 0.2f;
            Sfx.PlayAt("flare", start, 0.8f);
        }

        void Update()
        {
            t += Time.deltaTime;
            float rise = Mathf.Min(t, 1.4f);
            float y = 26f * (1f - Mathf.Pow(1f - rise / 1.4f, 2f)) - Mathf.Max(0f, t - 1.4f) * 1.8f;
            transform.position = start + new Vector3(t * 0.6f, y, 0);
            float life = Mathf.Clamp01((9f - t) / 2f);
            float flick = 0.85f + 0.15f * Mathf.PerlinNoise(t * 20f, 0.5f);
            light.intensity = 26f * life * flick * Mathf.Clamp01(t * 3f);
            Glows.SetColor(glow, new Color(7f, 1.4f, 0.7f) * life * flick);
            if (t > 4f && trail != null) { var em = trail.emission; em.rateOverTime = 0f; }
            if (t > 9.5f) Destroy(gameObject);
        }
    }
}
