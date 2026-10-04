using LastLight.Sim;
using UnityEngine;

namespace LastLight.View
{
    /// <summary>
    /// Gannet Head Light: the tower, the rotating lens, the lantern glare (which flares when the beam
    /// swings towards the camera) and the bright core of the beam leaving the lens.
    /// </summary>
    public sealed class LighthouseView : MonoBehaviour
    {
        public Transform Lens { get; private set; }
        public Vector3 LensPosition { get; private set; }
        MeshRenderer glare, glareCore;
        MeshRenderer beamCore;
        Material beamCoreMat;
        Light lanternLight;
        float lastBearing;
        public float Power = 1f;

        public static LighthouseView Build(MapData map, Transform parent)
        {
            var go = new GameObject("Gannet Head Light");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(map.Lighthouse.x, 0f, map.Lighthouse.y);
            var v = go.AddComponent<LighthouseView>();
            v.LensPosition = new Vector3(map.Lighthouse.x, map.LensHeight, map.Lighthouse.y);
            v.BuildBody(map);
            return v;
        }

        void BuildBody(MapData map)
        {
            var model = ModelLibrary.Spawn("lighthouse", transform);
            if (model != null)
            {
                Lens = ModelLibrary.Find(model.transform, "Lens");
                var cottage = ModelLibrary.Find(model.transform, "lamp_cottage");
                if (cottage != null) Glows.PointLight("Cottage Light", cottage, Vector3.zero, new Color(1f, 0.72f, 0.42f), 3f, 12f);
                var door = ModelLibrary.Find(model.transform, "lamp_door");
                if (door != null) Glows.PointLight("Door Light", door, Vector3.zero, new Color(1f, 0.75f, 0.45f), 2f, 8f);
            }
            else
            {
                var tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(tower.GetComponent<Collider>());
                tower.transform.SetParent(transform, false);
                tower.transform.localPosition = new Vector3(0, 11f, 0);
                tower.transform.localScale = new Vector3(4.2f, 6f, 4.2f);
                tower.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Lit(new Color(0.85f, 0.84f, 0.8f), 1.3f, 0.2f);
                var lantern = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(lantern.GetComponent<Collider>());
                lantern.transform.SetParent(transform, false);
                lantern.transform.localPosition = new Vector3(0, map.LensHeight, 0);
                lantern.transform.localScale = new Vector3(3.2f, 1.6f, 3.2f);
                lantern.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Emissive(new Color(1f, 0.8f, 0.5f), 1.5f);
                var lens = new GameObject("Lens");
                lens.transform.SetParent(transform, false);
                lens.transform.localPosition = new Vector3(0, map.LensHeight, 0);
                Lens = lens.transform;
            }

            glare = Glows.Glow("Lantern Glare", transform, new Vector3(0, map.LensHeight, 0), 16f, new Color(3f, 2.3f, 1.4f));
            glareCore = Glows.Glow("Lantern Core", transform, new Vector3(0, map.LensHeight, 0), 5f, new Color(6f, 5f, 3.5f));
            lanternLight = Glows.PointLight("Lantern Spill", transform, new Vector3(0, map.LensHeight - 1f, 0), new Color(1f, 0.82f, 0.55f), 9f, 26f);

            var core = new GameObject("Beam Core");
            core.transform.SetParent(transform, false);
            core.transform.localPosition = new Vector3(0, map.LensHeight, 0);
            core.AddComponent<MeshFilter>().sharedMesh = Meshes.Cone(1f, 1f, 24);
            beamCore = core.AddComponent<MeshRenderer>();
            beamCoreMat = new Material(MaterialLibrary.BeamCoreTemplate);
            beamCore.sharedMaterial = beamCoreMat;
            beamCore.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beamCore.enabled = false; // the volumetric pass draws the shaft; the cone read as two hard lines
        }

        /// <summary>Called every frame with the interpolated beam.</summary>
        public void Render(SimBeam beam, float bearing, Camera cam)
        {
            float deg = bearing * Mathf.Rad2Deg;
            if (Lens != null) Lens.rotation = Quaternion.Euler(0f, deg, 0f);

            float power = beam.Power * Power;
            float len = Mathf.Lerp(46f, 70f, beam.Focus);
            float half = beam.HalfAngle;
            float radius = Mathf.Tan(half) * len;
            float drop = Mathf.Atan2(beam.Height * 0.9f, len * 1.6f) * Mathf.Rad2Deg;
            beamCore.transform.rotation = Quaternion.Euler(drop, deg, 0f);
            beamCore.transform.localScale = new Vector3(radius, radius * 0.45f, len);
            beamCoreMat.SetFloat("_Intensity", Mathf.Lerp(0.3f, 0.5f, beam.Focus) * power);

            // The glare flares when the beam points at the camera.
            float facing = 0f;
            if (cam != null)
            {
                var toCam = cam.transform.position - LensPosition;
                var flat = new Vector2(toCam.x, toCam.z).normalized;
                float c = Vector2.Dot(flat, Geo.Dir(bearing));
                facing = Mathf.Pow(Mathf.Clamp01((c - Mathf.Cos(half * 2.2f)) / (1f - Mathf.Cos(half * 2.2f))), 2f);
            }
            float spin = Mathf.Abs(Mathf.DeltaAngle(lastBearing * Mathf.Rad2Deg, deg));
            lastBearing = bearing;
            Glows.SetColor(glare, new Color(2.2f, 1.6f, 0.9f) * power * (0.8f + facing * 1.6f));
            glare.transform.localScale = Vector3.one * (14f + facing * 9f);
            Glows.SetColor(glareCore, new Color(6f, 5f, 3.5f) * power * (1f + facing * 1.2f));
            lanternLight.intensity = 9f * power;
            _ = spin;
        }
    }
}
