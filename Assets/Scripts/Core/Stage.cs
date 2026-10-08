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
        public static FilmGrain Grain { get; private set; }
        public static DepthOfField Focus { get; private set; }
        public static UniversalAdditionalCameraData CameraData { get; private set; }

        /// <summary>In play the moon hangs north-west so its path lies across the bay; on the title it
        /// rises north-east, beside the lighthouse. The camera swoop hides the move.</summary>
        public static readonly Vector3 MoonDirection = new Vector3(-0.3f, 0.48f, 0.82f).normalized;
        public static readonly Vector3 MoonDirectionTitle = new Vector3(0.36f, 0.36f, 0.86f).normalized;

        public static void MoonTowards(bool title, float seconds)
        {
            if (Moon == null) return;
            var from = Moon.transform.rotation;
            var to = Quaternion.LookRotation(-(title ? MoonDirectionTitle : MoonDirection), Vector3.up);
            UI.Tween.Run(Moon, "moon", seconds, t => Moon.transform.rotation = Quaternion.Slerp(from, to, t), 0f, UI.Tween.EaseInOut);
        }
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
            data.dithering = true;   // no banding in the long dark gradients and the dawn sky
            CameraData = data;
            go.AddComponent<AudioListener>();
            go.AddComponent<LastLight.Audio.MonoMix>();
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

        /// <summary>The scene's exposure for a Brightness step (-2..2; 0 is the game as graded).
        /// Post-processing touches only the 3D scene, so the menus and HUD keep their look.</summary>
        public static float Exposure(int brightness) => 0.45f + brightness switch { -2 => -0.5f, -1 => -0.25f, 1 => 0.5f, 2 => 1f, _ => 0f };

        /// <summary>Settings: Brightness.</summary>
        public static void SetBrightness(int brightness)
        {
            if (Color != null) Color.postExposure.Override(Exposure(brightness));
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
            Color.postExposure.Override(Exposure(SaveData.Current.brightness));
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

            var grain = Grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.2f);
            grain.response.Override(0.8f);

            Chroma = profile.Add<ChromaticAberration>(true);
            Chroma.intensity.Override(0.07f);

            Lens = profile.Add<LensDistortion>(true);
            Lens.intensity.Override(0f);

            // Off unless Ultra's title close-up (or a menu over the night) asks for it.
            Focus = profile.Add<DepthOfField>(true);
            Focus.mode.Override(DepthOfFieldMode.Off);

            Post.sharedProfile = profile;
            ApplyFidelity();
        }

        // Ultra's moon shadows: four cascades over the whole bay as the play camera sees it.
        const float ShadowReach = 520f;

        /// <summary>Settings ▸ Graphics fidelity: anti-aliasing, bloom, grain, the moon's shadows
        /// and the title's depth of field. High is the game as first graded.</summary>
        public static void ApplyFidelity()
        {
            int level = Fidelity.Level;
            if (CameraData != null)
            {
                var cam = CameraData.GetComponent<Camera>();
                switch (level)
                {
                    case Fidelity.Low:
                        CameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                        break;
                    case Fidelity.Medium:
                        CameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                        CameraData.antialiasingQuality = AntialiasingQuality.Medium;
                        break;
                    case Fidelity.Ultra:
                        CameraData.antialiasing = AntialiasingMode.TemporalAntiAliasing;
                        CameraData.taaSettings.quality = TemporalAAQuality.High;
                        CameraData.taaSettings.baseBlendFactor = 0.9f;
                        CameraData.taaSettings.jitterScale = 1f;
                        CameraData.taaSettings.contrastAdaptiveSharpening = 0.35f;
                        break;
                    default:
                        CameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                        CameraData.antialiasingQuality = AntialiasingQuality.High;
                        break;
                }
                CameraData.resetHistory = true;
                if (cam != null) cam.allowMSAA = false;
            }
            if (Bloom != null)
            {
                Bloom.highQualityFiltering.Override(level >= Fidelity.High);
                Bloom.downscale.Override(level == Fidelity.Low ? BloomDownscaleMode.Quarter : BloomDownscaleMode.Half);
                Bloom.maxIterations.Override(level == Fidelity.Ultra ? 8 : level == Fidelity.Low ? 5 : 6);
            }
            if (Grain != null) Grain.active = level > Fidelity.Low;
            if (Moon != null)
            {
                Moon.shadows = level == Fidelity.Ultra ? LightShadows.Soft : LightShadows.None;
                Moon.shadowStrength = 0.85f;
            }
            // The pipeline asset is shared with the editor, so only the player changes it.
            if (!Application.isEditor && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = ShadowReach;
                urp.shadowCascadeCount = 4;
                urp.mainLightShadowmapResolution = 4096;
            }
        }

        // The lantern the title's and the ending's close-ups look at.
        static readonly Vector3 Lantern = new Vector3(0f, 17f, 30f);

        /// <summary>Depth of field, each frame from the camera rig: on Ultra, when the camera is
        /// down at the lighthouse (the title and the ending), the far sea and the sky soften behind
        /// the tower while the tower, the cliffs and the beam's pool stay sharp; it fades out as the
        /// camera rises to the play view. Otherwise off.</summary>
        public static void UpdateFocus(Vector3 camera)
        {
            if (Focus == null) return;
            float close = 1f - Mathf.Clamp01((camera.y - 14f) / 26f);
            if (Fidelity.Level != Fidelity.Ultra || close <= 0.02f)
            {
                if (Focus.mode.value != DepthOfFieldMode.Off) Focus.mode.Override(DepthOfFieldMode.Off);
                return;
            }
            float tower = Vector3.Distance(camera, Lantern);
            Focus.mode.Override(DepthOfFieldMode.Gaussian);
            Focus.gaussianStart.Override(tower + 90f);
            Focus.gaussianEnd.Override(tower + 700f);
            Focus.gaussianMaxRadius.Override(Mathf.Lerp(0.5f, 1.1f, close));
            Focus.highQualitySampling.Override(true);
        }
    }
}
