using System.Collections.Generic;
using LastLight.Sim;
using UnityEngine;

namespace LastLight.View
{
    /// <summary>
    /// One vessel on the water: hull (Blender model or stand-in), running lights, cabin glow, the
    /// confidence ring, riding the swell, and the sinking / docking animations after it resolves.
    /// </summary>
    public sealed class ShipView : MonoBehaviour
    {
        public SimShip Ship { get; private set; }
        Transform hull;
        readonly List<MeshRenderer> lamps = new List<MeshRenderer>();
        readonly List<Color> lampColors = new List<Color>();
        Light cabinLight, catchLight;
        float catchAge = 9f, catchStrength;
        ParticleSystem wake, smoke;
        readonly MeshRenderer[] course = new MeshRenderer[7];
        float courseShow;
        MeshRenderer ring;
        Material ringMat;
        MeshRenderer lostMark;
        Vector2 prevPos, curPos;
        float prevHeading, curHeading;
        float fade = 1f;
        float sinkT;
        MeshRenderer fireGlow;
        Light fireLight;
        ParticleSystem fireSmoke;
        bool gulped;
        float litPulse;
        int dockIndex;
        float shownConfidence = 1f;
        float bobSeed;

        public bool Finished { get; private set; }

        static readonly Color RingCalm = new Color(0.9f, 0.85f, 0.7f) * 1.1f;
        static readonly Color RingLow = new Color(1.6f, 0.75f, 0.2f);
        static readonly Color RingLost = new Color(2.2f, 0.25f, 0.18f);
        static readonly Color RingLured = new Color(2.0f, 0.55f, 0.08f);
        static readonly Color RingDanger = new Color(2.4f, 2.1f, 1.6f);

        public static ShipView Create(SimShip ship, Transform parent)
        {
            var go = new GameObject($"Ship {ship.Id} {ship.Name}");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ShipView>();
            v.Ship = ship;
            v.prevPos = v.curPos = ship.Pos;
            v.prevHeading = v.curHeading = ship.Heading;
            v.bobSeed = ship.Id * 1.37f;
            v.Build();
            v.Render(1f);
            return v;
        }

        string ModelName => Ship.Type switch
        {
            ShipType.Steamer => "steamer",
            ShipType.Ferry => "ferry",
            _ => "trawler",
        };

        void Build()
        {
            hull = new GameObject("Hull").transform;
            hull.SetParent(transform, false);
            var model = ModelLibrary.Spawn(ModelName, hull);
            var st = Ship.Stats;
            if (model == null) BuildStandIn(st);
            Transform cabinAnchor = null;
            if (model != null)
            {
                foreach (var anchor in ModelLibrary.FindAll(model.transform, "lamp_"))
                {
                    if (anchor.name.StartsWith("lamp_cabin")) { cabinAnchor ??= anchor; continue; }
                    if (Ship.Damaged) continue;
                    AddLamp(anchor, Vector3.zero, LampSize(anchor.name), LampColor(anchor.name));
                }
                if (Ship.Damaged)
                {
                    // Lamps out: darken the lit windows too.
                    foreach (var rend in model.GetComponentsInChildren<Renderer>())
                    {
                        var mats = rend.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                            if (mats[i] != null && mats[i].name.StartsWith("emit_")) mats[i] = MaterialLibrary.Lit(new Color(0.06f, 0.06f, 0.07f));
                        rend.sharedMaterials = mats;
                    }
                }
            }

            if (!Ship.Damaged)
            {
                var cabinPos = cabinAnchor != null ? hull.InverseTransformPoint(cabinAnchor.position) + Vector3.up * 0.6f : new Vector3(0, 3.5f, -st.Length * 0.15f);
                cabinLight = Glows.PointLight("Cabin", hull, cabinPos, new Color(1f, 0.72f, 0.42f), 3.5f, st.Length * 1.3f);
            }
            else
            {
                // A smouldering glow on deck makes the damaged ship just findable.
                AddLamp(hull, new Vector3(0.4f, 2.2f, st.Length * 0.1f), 1.6f, new Color(1.6f, 0.45f, 0.1f));
            }

            // Wake from the stern, smoke from the funnel.
            var wakeAnchor = model != null ? ModelLibrary.Find(model.transform, "fx_wake") : null;
            var wakeHost = new GameObject("Wake").transform;
            wakeHost.SetParent(transform, false);
            wakeHost.localPosition = wakeAnchor != null ? new Vector3(0, 0.15f, transform.InverseTransformPoint(wakeAnchor.position).z) : new Vector3(0, 0.15f, -st.Length * 0.5f);
            wake = FX.Wake(wakeHost, st.Radius * 1.2f);
            var smokeAnchor = model != null ? ModelLibrary.Find(model.transform, "fx_smoke") : null;
            if (smokeAnchor != null && Ship.Type != ShipType.Trawler)
                smoke = FX.Smoke(smokeAnchor, Ship.Type == ShipType.Steamer ? 5f : 3f);

            // Rim flash when the beam catches the ship: a warm light just off the bow, towards the tower.
            catchLight = Glows.PointLight("Catch", transform, new Vector3(0, st.Length * 0.35f + 3f, 0), new Color(1f, 0.82f, 0.55f), 0f, st.Length * 2.4f + 8f);
            catchLight.enabled = false;

            float r = st.Length * 0.62f + 1.5f;
            ring = Glows.Ring("Confidence", transform, r, RingCalm, new Vector4(0.86f, 0.93f, 0.025f, 0), new Vector4(1, 0, 0, 0));
            ringMat = ring.sharedMaterial;
            lostMark = Glows.Ring("Lost", transform, r * 1.35f, RingLost, new Vector4(0.9f, 0.95f, 0.03f, 0), new Vector4(1, 10, 9f, 0.6f));
            lostMark.enabled = false;
            // The captain's intended course: a dotted line ahead, so the keeper knows where to sweep.
            for (int i = 0; i < course.Length; i++)
            {
                course[i] = Glows.Glow("Course", null, Vector3.zero, 1.1f, Color.black);
                course[i].transform.SetParent(transform.parent, false);
            }
        }

        static Color LampColor(string anchor)
        {
            if (anchor.Contains("port")) return new Color(3.2f, 0.35f, 0.25f);
            if (anchor.Contains("stbd")) return new Color(0.3f, 2.8f, 0.9f);
            if (anchor.Contains("window") || anchor.Contains("cabin")) return new Color(2.2f, 1.2f, 0.45f);
            return new Color(2.6f, 2.4f, 2.0f);
        }

        static float LampSize(string anchor) => anchor.Contains("window") ? 1.3f : anchor.Contains("mast") ? 2.2f : 1.8f;

        void AddLamp(Transform parent, Vector3 local, float size, Color c)
        {
            var g = Glows.Glow("Lamp", parent, local, size, c);
            lamps.Add(g);
            lampColors.Add(c);
        }

        void BuildStandIn(ShipStats st)
        {
            float L = st.Length, W = st.Radius * 1.25f;
            Color hullCol, deckCol;
            switch (Ship.Type)
            {
                case ShipType.Steamer: hullCol = new Color(0.16f, 0.15f, 0.15f); deckCol = new Color(0.55f, 0.42f, 0.3f); break;
                case ShipType.Ferry: hullCol = new Color(0.82f, 0.8f, 0.76f); deckCol = new Color(0.3f, 0.36f, 0.45f); break;
                default: hullCol = new Color(0.45f, 0.18f, 0.14f); deckCol = new Color(0.62f, 0.58f, 0.5f); break;
            }
            Box(hull, new Vector3(0, 0.7f, 0), new Vector3(W, 1.8f, L), hullCol);
            Box(hull, new Vector3(0, 0.7f, L * 0.5f), new Vector3(W * 0.6f, 1.6f, W * 0.6f), hullCol, 45f);
            float cabinZ = Ship.Type == ShipType.Trawler ? -L * 0.2f : 0f;
            float cabinL = Ship.Type == ShipType.Trawler ? L * 0.3f : L * 0.45f;
            Box(hull, new Vector3(0, 2.2f, cabinZ), new Vector3(W * 0.75f, 1.4f, cabinL), deckCol);
            if (Ship.Type != ShipType.Trawler)
                Box(hull, new Vector3(0, 3.5f, cabinZ - 0.5f), new Vector3(W * 0.45f, 2.2f, W * 0.45f), Ship.Type == ShipType.Ferry ? new Color(0.75f, 0.3f, 0.2f) : new Color(0.1f, 0.1f, 0.1f));
            Box(hull, new Vector3(0, 4f, L * 0.25f), new Vector3(0.25f, 5f, 0.25f), new Color(0.4f, 0.35f, 0.3f));
            if (!Ship.Damaged)
            {
                AddLamp(hull, new Vector3(0, 6.6f, L * 0.25f), 2.2f, new Color(2.6f, 2.4f, 2.0f));
                AddLamp(hull, new Vector3(-W * 0.55f, 2.1f, L * 0.05f), 1.8f, new Color(3.2f, 0.35f, 0.25f));
                AddLamp(hull, new Vector3(W * 0.55f, 2.1f, L * 0.05f), 1.8f, new Color(0.3f, 2.8f, 0.9f));
                AddLamp(hull, new Vector3(0, 2f, -L * 0.5f), 1.5f, new Color(2.2f, 2.1f, 1.8f));
                if (Ship.Type == ShipType.Ferry)
                    for (int i = 0; i < 5; i++)
                    {
                        AddLamp(hull, new Vector3(-W * 0.39f, 2.3f, -L * 0.18f + i * 1.0f), 0.9f, new Color(2.2f, 1.2f, 0.45f));
                        AddLamp(hull, new Vector3(W * 0.39f, 2.3f, -L * 0.18f + i * 1.0f), 0.9f, new Color(2.2f, 1.2f, 0.45f));
                    }
            }
        }

        static void Box(Transform parent, Vector3 pos, Vector3 size, Color c, float yaw = 0f)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(parent, false);
            b.transform.localPosition = pos;
            b.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            b.transform.localScale = size;
            b.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Lit(c, 1.3f, 0.15f, 0.4f);
        }

        /// <summary>Snapshot before a sim step, for interpolation.</summary>
        public void BeforeStep()
        {
            prevPos = curPos;
            prevHeading = curHeading;
        }

        public void AfterStep()
        {
            curPos = Ship.Pos;
            curHeading = Ship.Heading;
        }

        /// <summary>The beam has just found this ship (after a spell in the dark).</summary>
        public void OnLit()
        {
            litPulse = 1f;
            catchAge = 0f;
            // A rescue (lost ship found) or a nervous ship gets the bigger answer.
            catchStrength = Ship.State == ShipState.Lost || Ship.Confidence < 0.4f ? 1.4f : 1f;
            FX.CatchRing(transform.position, Ship.Stats.Length * 0.62f + 1.5f, catchStrength);
        }

        // Two quick flashes of the white lamps, the captain answering the light.
        float AnswerBlink()
        {
            float a = catchAge - 0.3f;
            if (a < 0f || a > 0.75f) return 0f;
            float phase = Mathf.Repeat(a, 0.32f) / 0.32f;
            return a > 0.64f ? 0f : Mathf.Sin(Mathf.Clamp01(phase / 0.55f) * Mathf.PI);
        }

        /// <summary>Interpolated render; alpha is the fraction into the next sim step.</summary>
        public void Render(float alpha)
        {
            float dt = Time.deltaTime;
            var st = Ship.Stats;
            Vector2 p;
            float heading;
            if (Ship.State == ShipState.Arrived || Ship.State == ShipState.Wrecked)
            {
                AnimateResolved(dt);
                p = curPos;
                heading = curHeading;
            }
            else
            {
                p = Vector2.Lerp(prevPos, curPos, alpha);
                heading = prevHeading + Geo.DeltaAngle(prevHeading, curHeading) * alpha;
            }

            float t = Time.time;
            var fwd = Geo.Dir(heading);
            var right = new Vector2(fwd.y, -fwd.x);
            float h = Waves.Height(p, t);
            float hb = Waves.Height(p + fwd * st.Length * 0.4f, t);
            float hs = Waves.Height(p - fwd * st.Length * 0.4f, t);
            float hr = Waves.Height(p + right * st.Radius, t);
            float hl = Waves.Height(p - right * st.Radius, t);
            float pitch = Mathf.Atan2(hs - hb, st.Length * 0.8f) * Mathf.Rad2Deg * 1.4f;
            float roll = Mathf.Atan2(hl - hr, st.Radius * 2f) * Mathf.Rad2Deg * 1.4f + Mathf.Sin(t * 0.9f + bobSeed) * 1.2f;
            float sinkDepth = 0f;
            if (Ship.State == ShipState.Wrecked)
            {
                float lurch = Mathf.Exp(-sinkT * 7f) * Mathf.Sin(sinkT * 38f) * 9f;
                roll += sinkT * 16f + lurch;
                pitch += sinkT * 5f + sinkT * sinkT * 4f - lurch * 0.4f;
                sinkDepth = sinkT * sinkT * 1.6f;
            }
            transform.position = new Vector3(p.x, h * 0.85f - sinkDepth, p.y);
            transform.rotation = Quaternion.Euler(0f, heading * Mathf.Rad2Deg, 0f);
            hull.localRotation = Quaternion.Euler(pitch, 0f, roll);
            // Keep the ring flat on the water.
            ring.transform.localPosition = new Vector3(0, 0.25f - h * 0.85f + sinkDepth, 0);
            lostMark.transform.localPosition = ring.transform.localPosition;

            UpdateRing(dt);
            UpdateLamps(t);
            UpdateCourse(dt);
            if (wake != null)
            {
                var em = wake.emission;
                float sp = Ship.Resolved ? (Ship.State == ShipState.Arrived ? Ship.Stats.Speed * 0.5f * fade : 0f) : Ship.Speed;
                em.rateOverTime = sp * 4.5f * Fidelity.Particles;
            }
            if (smoke != null && Ship.State == ShipState.Wrecked) { var em = smoke.emission; em.rateOverTime = 0f; }
        }

        void UpdateRing(float dt)
        {
            litPulse = Mathf.Max(0f, litPulse - dt * 2.5f);
            shownConfidence = Mathf.MoveTowards(shownConfidence, Ship.Confidence, dt * 3f);
            Color c;
            float fill = shownConfidence;
            float pulseDepth = 0f;
            bool resolved = Ship.Resolved;
            switch (Ship.State)
            {
                case ShipState.Lost:
                    c = RingLost; fill = 1f; pulseDepth = 0.6f; break;
                case ShipState.Lured:
                    c = RingLured; fill = 1f; pulseDepth = 0.5f; break;
                default:
                    c = Color.Lerp(RingLow, RingCalm, Mathf.Clamp01((shownConfidence - 0.2f) / 0.4f));
                    if (shownConfidence < 0.3f) pulseDepth = 0.45f;
                    // Breakers ahead: the ring flickers white while the hazard is on its track.
                    if (Ship.Danger > 0f) { c = Color.Lerp(c, RingDanger, Mathf.PingPong(Time.time * 7f, 1f)); fill = Mathf.Max(fill, 0.35f); }
                    break;
            }
            float visible = resolved ? 0f : Ship.Inside ? 1f : 0.35f;
            c *= visible * fade * (1f + litPulse * 1.5f) * (Ship.Lit ? 1.2f : 0.85f);
            ringMat.SetColor("_Color", c);
            ringMat.SetVector("_Arc", new Vector4(fill, 0, 7f, pulseDepth));
            lostMark.enabled = !resolved && (Ship.State == ShipState.Lost || Ship.State == ShipState.Lured);
            if (lostMark.enabled)
                lostMark.sharedMaterial.SetColor("_Color", (Ship.State == ShipState.Lost ? RingLost : RingLured) * 0.8f);
        }

        void UpdateCourse(float dt)
        {
            bool show = Ship.State == ShipState.Sailing && Ship.Inside;
            courseShow = Mathf.MoveTowards(courseShow, show ? (Ship.Lit ? 1f : 0.45f) : 0f, dt * 2.5f);
            for (int i = 0; i < course.Length; i++)
            {
                var r = course[i];
                if (r == null) continue;
                if (courseShow <= 0.01f) { r.enabled = false; continue; }
                r.enabled = true;
                float d = 8f + i * 7f + Mathf.Repeat(Time.time * 4f, 7f);
                var p = SimWorld.CourseAhead(Ship, d);
                r.transform.position = new Vector3(p.x, 0.6f, p.y);
                float fadeAlong = 1f - (d - 8f) / (course.Length * 7f);
                Glows.SetColor(r, new Color(0.9f, 0.85f, 0.7f) * 0.55f * courseShow * fadeAlong * fade);
            }
        }

        void OnDestroy()
        {
            foreach (var r in course) if (r != null) Destroy(r.gameObject);
        }

        void UpdateLamps(float t)
        {
            bool wrecked = Ship.State == ShipState.Wrecked;
            float dim = fade;
            catchAge += Time.deltaTime;
            float blink = Ship.Resolved ? 0f : AnswerBlink() * catchStrength;
            for (int i = 0; i < lamps.Count; i++)
            {
                var c = lampColors[i];
                float flicker = Ship.Damaged ? 0.6f + 0.4f * Mathf.PerlinNoise(t * 6f, i) : 1f;
                if (wrecked)
                {
                    // The lamps die one by one, sputtering first.
                    float outAt = 0.15f + Mathf.Repeat(i * 0.618f + bobSeed, 1f) * 0.85f;
                    flicker *= sinkT > outAt ? 0f : sinkT > outAt - 0.18f ? (Mathf.PerlinNoise(t * 24f, i * 1.7f) > 0.42f ? 1f : 0.1f) : 1f;
                }
                bool white = c.r > 1.8f && c.g > 1.8f;
                float answer = white ? 1f + blink * 2.6f : Ship.Damaged ? 1f + blink * 1.2f : 1f;
                Glows.SetColor(lamps[i], c * dim * flicker * answer);
            }
            if (cabinLight != null) cabinLight.intensity = 3.5f * dim * (wrecked ? (sinkT < 0.45f ? Mathf.PerlinNoise(t * 18f, 3f) : 0f) : 1f);
            if (wrecked) UpdateFire(t);
            float flash = catchAge < 1.6f && !Ship.Resolved ? Mathf.Exp(-catchAge * 3.2f) * Mathf.Clamp01(catchAge * 25f) : 0f;
            catchLight.enabled = flash > 0.01f;
            if (catchLight.enabled) catchLight.intensity = 14f * flash * catchStrength * fade;
        }

        void UpdateFire(float t)
        {
            if (fireGlow == null)
            {
                var local = new Vector3(0.3f, 2.6f, Ship.Stats.Length * 0.08f);
                fireGlow = Glows.Glow("Fire", hull, local, 6.5f + Ship.Stats.Length * 0.15f, Color.black);
                fireLight = Glows.PointLight("Fire Light", hull, local + Vector3.up * 2f, new Color(1f, 0.48f, 0.18f), 0f, 34f + Ship.Stats.Length);
                fireSmoke = FX.Smoke(fireGlow.transform, 10f);
                var main = fireSmoke.main;
                main.startColor = new Color(0.1f, 0.09f, 0.085f, 1f);
            }
            float flick = 0.7f + 0.3f * Mathf.PerlinNoise(t * 9f, bobSeed) + 0.15f * Mathf.PerlinNoise(t * 31f, 2f);
            float fire = Mathf.Clamp01(sinkT * 8f) * Mathf.Clamp01((1.7f - sinkT) / 0.7f) * fade;
            Glows.SetColor(fireGlow, new Color(3.6f, 1.25f, 0.3f) * fire * flick);
            fireLight.intensity = 22f * fire * flick;
            fireLight.enabled = fire > 0.01f;
            if (fireSmoke == null) return;
            var em = fireSmoke.emission;
            em.rateOverTime = sinkT < 1.9f ? 10f * Fidelity.Particles : 0f;
        }

        void AnimateResolved(float dt)
        {
            if (Ship.State == ShipState.Wrecked)
            {
                sinkT += dt * 0.32f;
                if (!gulped && sinkT > 1.85f)
                {
                    gulped = true;
                    FX.Gulp(transform.position, Ship.Stats.Length / 10f);
                    LastLight.Audio.Sfx.PlayAt("wreck_sink", transform.position, 0.6f);
                }
                if (sinkT > 2.6f)
                {
                    // Let the last smoke drift off after the hull is gone.
                    if (fireSmoke != null) { fireSmoke.transform.SetParent(null, true); Destroy(fireSmoke.gameObject, 9f); fireSmoke = null; }
                    Finished = true;
                }
                return;
            }
            // Arrived: sail on into the harbour basin, or out to sea, and fade.
            var st = Ship.Stats;
            var fwd = Geo.Dir(curHeading);
            if (Ship.Route.ToHarbor && dockIndex < 2)
            {
                var map = MapData.Load();
                var target = map.Dock[Mathf.Min(dockIndex, map.Dock.Length - 1)];
                var to = target - curPos;
                if (to.magnitude < 3f) dockIndex++;
                float want = Geo.Bearing(to);
                curHeading += Mathf.Clamp(Geo.DeltaAngle(curHeading, want), -dt * 1.2f, dt * 1.2f);
                fwd = Geo.Dir(curHeading);
            }
            curPos += fwd * st.Speed * 0.55f * dt;
            fade = Mathf.MoveTowards(fade, 0f, dt / (Ship.Route.ToHarbor ? 5f : 3f));
            if (fade <= 0f) Finished = true;
            hull.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, fade);
        }
    }
}
