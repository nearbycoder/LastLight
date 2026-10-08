using System.Collections.Generic;
using UnityEngine;

namespace LastLight.View
{
    /// <summary>
    /// Loads the Blender-exported FBX models from Resources/Models and swaps their materials for
    /// project materials by name (see MaterialLibrary). Returns null when a model is missing, so
    /// callers can fall back to placeholder geometry.
    /// </summary>
    public static class ModelLibrary
    {
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => prefabs.Clear();

        public static bool Has(string name) => Load(name) != null;

        static GameObject Load(string name)
        {
            if (prefabs.TryGetValue(name, out var p)) return p;
            p = Resources.Load<GameObject>("Models/" + name);
            prefabs[name] = p;
            return p;
        }

        public static GameObject Spawn(string name, Transform parent)
        {
            var prefab = Load(name);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = name;
            Remap(go);
            return go;
        }

        /// <summary>Spawns a single named child of a model file (e.g. one rock variant).</summary>
        public static GameObject SpawnPart(string file, string part, Transform parent)
        {
            var prefab = Load(file);
            if (prefab == null) return null;
            var src = Find(prefab.transform, part);
            if (src == null) return null;
            var go = Object.Instantiate(src.gameObject, parent, false);
            go.name = part;
            go.transform.localPosition = Vector3.zero;
            Remap(go);
            return go;
        }

        public static void Remap(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null) mats[i] = MaterialLibrary.FromBlenderName(mats[i].name);
                r.sharedMaterials = mats;
                // They cast and take the moon's shadows; the moon casts none below Graphics fidelity Ultra.
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }

        public static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var f = Find(c, name);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>All descendants whose name starts with the prefix (lamp anchors etc.).</summary>
        public static List<Transform> FindAll(Transform root, string prefix, List<Transform> into = null)
        {
            into ??= new List<Transform>();
            if (root.name.StartsWith(prefix)) into.Add(root);
            foreach (Transform c in root) FindAll(c, prefix, into);
            return into;
        }
    }
}
