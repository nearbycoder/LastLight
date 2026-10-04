using UnityEngine;

namespace LastLight.View
{
    /// <summary>
    /// CPU copy of the Gerstner swell in Water.shader, so ships and buoys ride the same waves the
    /// sea is drawn with. Keep the constants in sync with kWaves in the shader.
    /// </summary>
    public static class Waves
    {
        static readonly Vector4[] kWaves =
        {
            new Vector4(0.32f, 0.95f, 31f, 0.16f),
            new Vector4(-0.55f, 0.83f, 17f, 0.18f),
            new Vector4(0.85f, 0.52f, 11f, 0.14f),
            new Vector4(-0.2f, -0.98f, 7f, 0.10f),
        };

        public static float Scale = 0.35f;
        public static float Choppiness = 1f;

        public static float Height(Vector2 xz, float t)
        {
            float h = 0f;
            for (int i = 0; i < kWaves.Length; i++)
            {
                var w = kWaves[i];
                var d = new Vector2(w.x, w.y).normalized;
                float k = 2f * Mathf.PI / w.z;
                float c = Mathf.Sqrt(9.8f / k) * 0.55f;
                float steep = w.w * Choppiness;
                float a = steep / k * Scale;
                float f = k * (Vector2.Dot(d, xz) - c * t);
                h += a * Mathf.Sin(f);
            }
            return h;
        }

        public static void Apply(MaterialPropertyBlock _, Material water)
        {
            if (water == null) return;
            water.SetFloat("_WaveScale", Scale);
            water.SetFloat("_Choppiness", Choppiness);
        }
    }
}
