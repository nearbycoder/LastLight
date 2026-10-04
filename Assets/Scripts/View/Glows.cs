using UnityEngine;

namespace LastLight.View
{
    /// <summary>Factory for camera-facing lamp glows and flat ring decals.</summary>
    public static class Glows
    {
        static MaterialPropertyBlock block;
        static readonly int InstColor = Shader.PropertyToID("_InstColor");

        public static MeshRenderer Glow(string name, Transform parent, Vector3 localPos, float size, Color hdr)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = Meshes.QuadXY;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = MaterialLibrary.GlowTemplate;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            SetColor(r, hdr);
            return r;
        }

        public static void SetColor(Renderer r, Color hdr)
        {
            block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            hdr.a = 1f;
            block.SetVector(InstColor, hdr);
            r.SetPropertyBlock(block);
        }

        public static MeshRenderer Ring(string name, Transform parent, float radius, Color hdr, Vector4 ring, Vector4 arc)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0.18f, 0);
            go.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            go.AddComponent<MeshFilter>().sharedMesh = Meshes.QuadXZ;
            var r = go.AddComponent<MeshRenderer>();
            var m = MaterialLibrary.NewRing(hdr);
            m.SetVector("_Ring", ring);
            m.SetVector("_Arc", arc);
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        public static Light PointLight(string name, Transform parent, Vector3 localPos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }
    }
}
