using LastLight.Sim;
using UnityEngine;

namespace LastLight.View
{
    /// <summary>
    /// Static scenery of Merrow Bay: the sea, the coast, the stacks, Porthkell and the lighthouse.
    /// Uses the Blender models when they exist, otherwise simple stand-ins built from the map data.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        public MapData Map { get; private set; }
        public LighthouseView Lighthouse { get; private set; }
        public Material WaterMaterial { get; private set; }
        public Transform HarborLights { get; private set; }

        public static WorldView Build(MapData map)
        {
            var go = new GameObject("World");
            var view = go.AddComponent<WorldView>();
            view.Map = map;
            view.BuildSea();
            view.BuildCoast();
            view.BuildStacks();
            view.BuildHarbor();
            view.Lighthouse = LighthouseView.Build(map, go.transform);
            RenderSettings.skybox = MaterialLibrary.Sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = false;
            return view;
        }

        void BuildSea()
        {
            var go = new GameObject("Sea");
            go.transform.SetParent(transform, false);
            var area = Rect.MinMaxRect(Map.Bounds.xMin - 50f, Map.Bounds.yMin - 30f, Map.Bounds.xMax + 50f, Map.Bounds.yMax + 60f);
            go.AddComponent<MeshFilter>().sharedMesh = Meshes.SeaGrid(area, 1.25f, 2200f);
            var r = go.AddComponent<MeshRenderer>();
            WaterMaterial = MaterialLibrary.Water;
            r.sharedMaterial = WaterMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void BuildCoast()
        {
            var model = ModelLibrary.Spawn("merrow_bay", transform);
            if (model != null) return;
            foreach (var poly in Map.Land)
            {
                var go = new GameObject("Coast");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = Meshes.Extrude(poly, -4f, 5f, new Color(0.22f, 0.26f, 0.2f), new Color(0.32f, 0.31f, 0.3f));
                go.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Lit(Color.white, 1f, 0.05f, 0.6f);
            }
        }

        void BuildStacks()
        {
            var parent = new GameObject("Stacks").transform;
            parent.SetParent(transform, false);
            foreach (var s in Map.Stacks)
            {
                var go = ModelLibrary.SpawnPart("rocks", "stack_" + s.Id, parent);
                if (go == null)
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(go.GetComponent<Collider>());
                    go.transform.SetParent(parent, false);
                    go.transform.localScale = new Vector3(s.Radius * 2f, s.Height * 0.5f, s.Radius * 2f);
                    go.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Lit(new Color(0.3f, 0.29f, 0.28f), 1.2f, 0.05f, 1f);
                    go.transform.position = new Vector3(s.Pos.x, s.Height * 0.5f - 1.5f, s.Pos.y);
                }
                else go.transform.position = new Vector3(s.Pos.x, 0f, s.Pos.y);
                go.name = s.Name;
            }
        }

        void BuildHarbor()
        {
            HarborLights = new GameObject("Porthkell Lights").transform;
            HarborLights.SetParent(transform, false);
            // Harbour-mouth lights on the breakwater heads (red to port, green to starboard coming in).
            Glows.Glow("Breakwater W", HarborLights, new Vector3(-98.5f, 4.5f, -20.5f), 3.2f, new Color(3f, 0.4f, 0.3f));
            Glows.Glow("Breakwater E", HarborLights, new Vector3(-87.5f, 4.5f, -23f), 3.2f, new Color(0.3f, 2.6f, 0.9f));
            Glows.PointLight("Harbour Light", HarborLights, new Vector3(-93f, 6f, -34f), new Color(1f, 0.7f, 0.4f), 6f, 30f);
            if (ModelLibrary.Has("merrow_bay")) return;
            // Placeholder windows of the town.
            var rng = new System.Random(4);
            for (int i = 0; i < 26; i++)
            {
                float x = -112f + (float)rng.NextDouble() * 36f;
                float z = -50f + (float)rng.NextDouble() * 10f;
                Glows.Glow("Window", HarborLights, new Vector3(x, 6.5f + (float)rng.NextDouble() * 3f, z), 1.4f, new Color(2.2f, 1.3f, 0.5f));
            }
        }
    }
}
