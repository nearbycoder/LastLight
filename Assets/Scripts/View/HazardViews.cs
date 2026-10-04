using LastLight.Sim;
using UnityEngine;

namespace LastLight.View
{
    /// <summary>A hidden reef: dark rock just under the surface, plus breaking foam once charted.</summary>
    public sealed class ReefView : MonoBehaviour
    {
        [System.NonSerialized] public SimReef Reef;
        Material foam;
        float shown;
        float bloom;

        public static ReefView Create(SimReef reef, Transform parent)
        {
            var go = new GameObject("Reef " + reef.Id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(reef.Pos.x, 0f, reef.Pos.y);
            var v = go.AddComponent<ReefView>();
            v.Reef = reef;
            var rock = ModelLibrary.SpawnPart("rocks", "reef_" + (reef.Index % 5), go.transform);
            if (rock == null)
            {
                rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(rock.GetComponent<Collider>());
                rock.transform.SetParent(go.transform, false);
                rock.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Lit(new Color(0.2f, 0.2f, 0.19f), 1f, 0.2f, 1f);
                rock.transform.localScale = new Vector3(reef.Radius * 2.1f, 1.6f, reef.Radius * 1.8f);
            }
            else rock.transform.localScale = Vector3.one * reef.Radius / 2.4f;
            rock.transform.localPosition = new Vector3(0, -2.7f, 0);
            rock.transform.localRotation = Quaternion.Euler(0, reef.Index * 67f, 0);

            var decal = new GameObject("Foam");
            decal.transform.SetParent(go.transform, false);
            decal.transform.localPosition = new Vector3(0, 0.3f, 0);
            decal.transform.localScale = new Vector3(reef.Radius * 4.6f, 1f, reef.Radius * 4.6f);
            decal.AddComponent<MeshFilter>().sharedMesh = Meshes.QuadXZ;
            var r = decal.AddComponent<MeshRenderer>();
            v.foam = MaterialLibrary.NewFoam();
            v.foam.SetFloat("_Seed", reef.Index * 1.7f);
            v.foam.SetVector("_Shape", new Vector4(0.2f, 1.0f, 1, 0));
            r.sharedMaterial = v.foam;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return v;
        }

        public void OnCharted() => bloom = 1f;

        void Update()
        {
            float target = Reef.Charted ? Mathf.Clamp01(Reef.ChartTimer / 5f) : 0f;
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * (target > shown ? 4f : 0.8f));
            bloom = Mathf.Max(0f, bloom - Time.deltaTime * 1.5f);
            foam.SetFloat("_Amount", shown * (1f + bloom * 1.5f));
            foam.SetVector("_Shape", new Vector4(0.2f, 1.0f + bloom * 0.25f, 1, 0));
        }
    }

    /// <summary>A sandbank: a pale shallows shape under the surface, foam along it once charted.</summary>
    public sealed class ShoalView : MonoBehaviour
    {
        [System.NonSerialized] public SimShoal Shoal;
        Material foam;
        float shown;

        public static ShoalView Create(SimShoal shoal, Transform parent)
        {
            var d = shoal.Def;
            var go = new GameObject("Shoal " + d.Id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(d.Pos.x, 0f, d.Pos.y);
            go.transform.rotation = Quaternion.Euler(0, -d.Angle, 0);
            var v = go.AddComponent<ShoalView>();
            v.Shoal = shoal;
            var sand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sand.GetComponent<Collider>());
            sand.transform.SetParent(go.transform, false);
            sand.transform.localPosition = new Vector3(0, -1.4f, 0);
            sand.transform.localScale = new Vector3(d.Rx * 2f, 1.8f, d.Rz * 2f);
            sand.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Lit(new Color(0.5f, 0.45f, 0.33f), 1f, 0.1f, 1f);

            var decal = new GameObject("Foam");
            decal.transform.SetParent(go.transform, false);
            decal.transform.localPosition = new Vector3(0, 0.3f, 0);
            decal.transform.localScale = new Vector3(d.Rx * 2.3f, 1f, d.Rz * 2.6f);
            decal.AddComponent<MeshFilter>().sharedMesh = Meshes.QuadXZ;
            var r = decal.AddComponent<MeshRenderer>();
            v.foam = MaterialLibrary.NewFoam();
            v.foam.SetFloat("_Seed", 5.5f);
            v.foam.SetVector("_Shape", new Vector4(0.45f, 0.95f, 1, 0));
            r.sharedMaterial = v.foam;
            return v;
        }

        void Update()
        {
            float target = Shoal.Charted ? Mathf.Clamp01(Shoal.ChartTimer / 5f) : 0f;
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * (target > shown ? 4f : 0.8f));
            foam.SetFloat("_Amount", shown * 0.8f);
        }
    }

    /// <summary>A channel buoy: bobbing body, lamp that burns down, and its guidance aura.</summary>
    public sealed class BuoyView : MonoBehaviour
    {
        [System.NonSerialized] public SimBuoy Buoy;
        MeshRenderer lamp;
        Light light;
        MeshRenderer aura;
        Material auraMat;
        Transform body;
        Color lampColor;
        float flare;

        public static BuoyView Create(SimBuoy buoy, Transform parent)
        {
            var go = new GameObject("Buoy " + buoy.Id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(buoy.Pos.x, 0f, buoy.Pos.y);
            var v = go.AddComponent<BuoyView>();
            v.Buoy = buoy;
            v.lampColor = buoy.Kind switch
            {
                "red" => new Color(3.2f, 0.45f, 0.3f),
                "green" => new Color(0.35f, 3f, 1.0f),
                _ => new Color(3f, 2.3f, 1.1f),
            };
            v.body = new GameObject("Body").transform;
            v.body.SetParent(go.transform, false);
            var model = ModelLibrary.Spawn("buoy_" + buoy.Kind, v.body);
            if (model == null)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(c.GetComponent<Collider>());
                c.transform.SetParent(v.body, false);
                c.transform.localPosition = new Vector3(0, 1.2f, 0);
                c.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
                var col = buoy.Kind == "red" ? new Color(0.65f, 0.12f, 0.1f) : buoy.Kind == "green" ? new Color(0.1f, 0.45f, 0.22f) : new Color(0.7f, 0.55f, 0.15f);
                c.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Lit(col, 1.3f, 0.3f, 0.5f);
            }
            var anchor = model != null ? ModelLibrary.Find(model.transform, "lamp_top") : null;
            var lampPos = anchor != null ? v.body.InverseTransformPoint(anchor.position) : new Vector3(0, 4.4f, 0);
            v.lamp = Glows.Glow("Lamp", v.body, lampPos, 3f, Color.black);
            v.light = Glows.PointLight("Lamp Light", v.body, lampPos + Vector3.up * 0.2f, v.lampColor.linear * 0.3f, 0f, 16f);
            v.light.color = new Color(v.lampColor.r, v.lampColor.g, v.lampColor.b) / Mathf.Max(v.lampColor.r, Mathf.Max(v.lampColor.g, v.lampColor.b));
            v.aura = Glows.Ring("Aura", go.transform, SimBuoy.AuraRadius, Color.black, new Vector4(0.972f, 0.985f, 0.006f, 0), new Vector4(1, 60, 0, 0));
            v.auraMat = v.aura.sharedMaterial;
            return v;
        }

        public void OnLit() => flare = 1f;

        void Update()
        {
            float t = Time.time;
            var p = Buoy.Pos;
            float h = Waves.Height(p, t);
            body.localPosition = new Vector3(0, h * 0.9f, 0);
            body.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.1f + Buoy.Index) * 6f, 0, Mathf.Cos(t * 0.9f + Buoy.Index * 2f) * 6f);
            flare = Mathf.Max(0f, flare - Time.deltaTime * 1.6f);
            float charge = Buoy.Charge;
            // Lamps flash in their characteristic rhythm while burning.
            float rhythm = Buoy.Kind == "bell" ? 0.75f + 0.25f * Mathf.Sin(t * 3f) : (Mathf.Repeat(t + Buoy.Index * 0.7f, 2.5f) < 1.2f ? 1f : 0.35f);
            float on = charge > 0f ? Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(charge * 3f)) * rhythm : 0f;
            Glows.SetColor(lamp, lampColor * (on + flare * 2f) + new Color(0.06f, 0.06f, 0.07f));
            light.intensity = on * 4f + flare * 6f;
            float auraOn = charge > 0f ? Mathf.Clamp01(charge * 4f) : 0f;
            auraMat.SetColor("_Color", lampColor * 0.075f * auraOn * (0.6f + 0.4f * charge) + lampColor * 0.3f * flare);
            aura.transform.localScale = new Vector3(SimBuoy.AuraRadius * 2f * (1f + flare * 0.15f), 1, SimBuoy.AuraRadius * 2f * (1f + flare * 0.15f));
            aura.transform.localRotation = Quaternion.Euler(0, t * 6f, 0);
        }
    }

    /// <summary>The wreckers' false lantern on a cliff top or islet.</summary>
    public sealed class WreckerView : MonoBehaviour
    {
        [System.NonSerialized] public SimWrecker Wrecker;
        MeshRenderer glow;
        Light light;
        float shown;

        public static WreckerView Create(SimWrecker w, Transform parent)
        {
            var go = new GameObject("Wrecker " + w.Index);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<WreckerView>();
            v.Wrecker = w;
            v.glow = Glows.Glow("False Lantern", go.transform, Vector3.zero, 8f, Color.black);
            v.light = Glows.PointLight("False Light", go.transform, Vector3.zero, new Color(1f, 0.5f, 0.2f), 0f, 30f);
            return v;
        }

        void Update()
        {
            var site = Wrecker.Site;
            transform.position = new Vector3(site.Pos.x, site.Height + 2f, site.Pos.y);
            float target = Wrecker.Burning ? 1f - Wrecker.DouseProgress * 0.7f : 0f;
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * (target > shown ? 1.5f : 5f));
            float flick = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 9f, Wrecker.Index * 4.2f);
            if (Wrecker.DouseProgress > 0f) flick *= 0.6f + 0.4f * Mathf.PerlinNoise(Time.time * 30f, 1.3f);
            Glows.SetColor(glow, new Color(3f, 1.4f, 0.5f) * shown * flick);
            light.intensity = 7f * shown * flick;
        }
    }
}
