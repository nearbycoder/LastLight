using System.Collections.Generic;
using UnityEngine;

namespace LastLight.View
{
    /// <summary>
    /// Shared materials. Templates live in Resources/Materials (created by ProjectSetup) so their
    /// shaders ship in builds; coloured variants are cached by name. Blender materials are named
    /// <c>col_RRGGBB</c>, <c>glow_RRGGBB</c>, <c>wet_RRGGBB</c>, <c>metal_RRGGBB</c>, <c>glass_RRGGBB</c>
    /// and are swapped for these on load.
    /// </summary>
    public static class MaterialLibrary
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        static Material Template(string name)
        {
            if (cache.TryGetValue("@" + name, out var m) && m != null) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null)
            {
                Debug.LogError($"[MaterialLibrary] missing Resources/Materials/{name}.mat (run Last Light > Apply Project Setup)");
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            }
            cache["@" + name] = m;
            return m;
        }

        public static Material Water => Template("LL_Water");
        public static Material Sky => Template("LL_Sky");
        public static Material Atmosphere => Template("LL_Atmosphere");
        public static Material GlowTemplate => Template("LL_Glow");
        public static Material RingTemplate => Template("LL_Ring");
        public static Material FoamTemplate => Template("LL_Foam");
        public static Material BeamCoreTemplate => Template("LL_BeamCore");

        /// <summary>Lit material with a base colour (vertex colours multiply on top).</summary>
        public static Material Lit(Color color, float rim = 1f, float specular = 0.03f, float wet = 0f)
        {
            string key = $"lit_{ColorUtility.ToHtmlStringRGB(color)}_{rim:0.00}_{specular:0.00}_{wet:0.00}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Template("LL_Lit")) { name = key };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_RimStrength", rim);
            m.SetFloat("_Specular", specular);
            m.SetFloat("_Wet", wet);
            m.enableInstancing = true;
            cache[key] = m;
            return m;
        }

        /// <summary>Emissive (self-lit) surface such as windows and lamp lenses.</summary>
        public static Material Emissive(Color color, float intensity)
        {
            string key = $"emit_{ColorUtility.ToHtmlStringRGB(color)}_{intensity:0.00}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Template("LL_Lit")) { name = key };
            m.SetColor("_BaseColor", Color.black);
            m.SetColor("_EmissionColor", color * intensity);
            cache[key] = m;
            return m;
        }

        public static Material NewGlow(Color hdr)
        {
            var m = new Material(GlowTemplate);
            m.SetColor("_Color", hdr);
            return m;
        }

        public static Material NewRing(Color hdr) { var m = new Material(RingTemplate); m.SetColor("_Color", hdr); return m; }

        public static Material NewFoam() => new Material(FoamTemplate);

        /// <summary>Maps a Blender material name to a project material.</summary>
        public static Material FromBlenderName(string name)
        {
            name = name.Replace(" (Instance)", "");
            int dot = name.IndexOf('.');
            if (dot > 0) name = name.Substring(0, dot);
            int us = name.IndexOf('_');
            if (us < 0 || !ColorUtility.TryParseHtmlString("#" + name.Substring(us + 1, Mathf.Min(6, name.Length - us - 1)), out var c))
                return Lit(new Color(0.6f, 0.6f, 0.6f));
            string prefix = name.Substring(0, us);
            return prefix switch
            {
                "glow" => Emissive(c, 2.2f),
                "lamp" => Emissive(c, 6f),
                "wet" => Lit(c, 1f, 0.12f, 1f),
                "metal" => Lit(c, 1.2f, 0.5f),
                "glass" => Lit(c, 1.5f, 0.8f),
                _ => Lit(c),
            };
        }
    }
}
