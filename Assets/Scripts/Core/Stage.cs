using LastLight.View;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastLight.Core
{
    /// <summary>Creates the camera, the moon and the post-processing stack in code.</summary>
    public static class Stage
    {
        public static Light Moon { get; private set; }
        public static Volume Post { get; private set; }
        public static Bloom Bloom { get; private set; }
        public static ColorAdjustments Color { get; private set; }
        public static Vignette Vignette { get; private set; }
        public static ChromaticAberration Chroma { get; private set; }
        public static LensDistortion Lens { get; private set; }

        public static readonly Vector3 MoonDirection = new Vector3(-0.3f, 0.48f, 0.82f).normalized;
        public static readonly Color MoonColor = new Color(0.62f, 0.72f, 0.92f);
        public const float MoonIntensity = 1.1f;

        public static CameraRig BuildCamera()
        {
            var existing = Camera.main;
            if (existing != null) Object.Destroy(existing.gameObject);
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 4000f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            cam.allowMSAA = false;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.requiresDepthTexture = true;
            data.requiresColorTexture = false;
            go.AddComponent<AudioListener>();
            var rig = go.AddComponent<CameraRig>();
            rig.Snap(CameraRig.PlayPose);
            return rig;
        }

        public static void BuildMoon()
        {
            foreach (var l in Object.FindObjectsByType<Light>())
                if (l.type == LightType.Directional) Object.Destroy(l.gameObject);
            var go = new GameObject("Moon");
            Moon = go.AddComponent<Light>();
            Moon.type = LightType.Directional;
            Moon.color = MoonColor;
            Moon.intensity = MoonIntensity;
            Moon.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.LookRotation(-MoonDirection, Vector3.up);
            RenderSettings.sun = Moon;
        }

        public static void BuildPost()
        {
            var go = new GameObject("Post");
            Post = go.AddComponent<Volume>();
            Post.isGlobal = true;
            Post.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            Bloom = profile.Add<Bloom>(true);
            Bloom.threshold.Override(0.8f);
            Bloom.intensity.Override(0.95f);
            Bloom.scatter.Override(0.74f);
            Bloom.highQualityFiltering.Override(true);
            Bloom.tint.Override(new UnityEngine.Color(1f, 0.93f, 0.85f));

            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);

            Color = profile.Add<ColorAdjustments>(true);
            Color.postExposure.Override(0.45f);
            Color.contrast.Override(12f);
            Color.saturation.Override(6f);

            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new UnityEngine.Color(0.36f, 0.47f, 0.62f));
            split.highlights.Override(new UnityEngine.Color(0.72f, 0.58f, 0.38f));
            split.balance.Override(-12f);

            var lgg = profile.Add<LiftGammaGain>(true);
            lgg.lift.Override(new Vector4(0.98f, 1.0f, 1.04f, 0.0f));

            Vignette = profile.Add<Vignette>(true);
            Vignette.intensity.Override(0.34f);
            Vignette.smoothness.Override(0.5f);
            Vignette.color.Override(new UnityEngine.Color(0.0f, 0.01f, 0.03f));

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.2f);
            grain.response.Override(0.8f);

            Chroma = profile.Add<ChromaticAberration>(true);
            Chroma.intensity.Override(0.07f);

            Lens = profile.Add<LensDistortion>(true);
            Lens.intensity.Override(0f);

            Post.sharedProfile = profile;
        }
    }
}
