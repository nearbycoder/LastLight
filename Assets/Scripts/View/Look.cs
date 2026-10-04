using UnityEngine;

namespace LastLight.View
{
    /// <summary>
    /// The art direction's numbers in one place: palette and tuning for the sea, the air and the
    /// sky, pushed onto the shared materials at start-up (and again when quality changes).
    /// </summary>
    public static class Look
    {
        public static readonly Color SeaDeep = new Color(0.02f, 0.05f, 0.075f);
        public static readonly Color SeaShallow = new Color(0.05f, 0.13f, 0.15f);
        public static readonly Color SkyZenith = new Color(0.012f, 0.022f, 0.05f);
        public static readonly Color SkyHorizon = new Color(0.075f, 0.115f, 0.175f);
        public static readonly Color ReflectZenith = new Color(0.02f, 0.035f, 0.07f);
        public static readonly Color ReflectHorizon = new Color(0.1f, 0.15f, 0.22f);
        public static readonly Color Foam = new Color(0.75f, 0.82f, 0.9f);

        public static void Apply()
        {
            var water = MaterialLibrary.Water;
            water.SetColor("_DeepColor", SeaDeep);
            water.SetColor("_ShallowColor", SeaShallow);
            water.SetColor("_SkyZenith", ReflectZenith);
            water.SetColor("_SkyHorizon", ReflectHorizon);
            water.SetColor("_FoamColor", Foam);
            water.SetVector("_FarFade", new Vector4(260f, 900f, 0, 0));

            var sky = MaterialLibrary.Sky;
            sky.SetColor("_Zenith", SkyZenith);
            sky.SetColor("_Horizon", SkyHorizon);
            sky.SetColor("_MoonColor", new Color(0.85f, 0.9f, 1f));

            var air = MaterialLibrary.Atmosphere;
            air.SetFloat("_HazeDensity", 0.022f);
            air.SetFloat("_HazeHeight", 6.5f);
            air.SetFloat("_HazeNoise", 0.8f);
            air.SetFloat("_FogDensity", 0.12f);
            air.SetFloat("_ScatterGain", 1.0f);
            air.SetFloat("_BeamScatter", 1.7f);
            air.SetFloat("_Extinction", 0.25f);
            air.SetFloat("_FogExtinction", 1.0f);
            air.SetColor("_AmbientScatter", new Color(0.03f, 0.045f, 0.07f));
            air.SetFloat("_LanternGlow", 1.0f);
        }
    }
}
