using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastLight.EditorTools
{
    /// <summary>
    /// Reproducible project wiring: shader materials in Resources (so shaders ship), the volumetric
    /// atmosphere full-screen pass, URP and player settings, and the Main scene.
    /// Menu: Last Light > Apply Project Setup, or -executeMethod LastLight.EditorTools.ProjectSetup.ApplyBatch
    /// </summary>
    public static class ProjectSetup
    {
        const string MaterialDir = "Assets/Resources/Materials";
        const string AtmosphereFeature = "LL Atmosphere";
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Last Light/Apply Project Setup")]
        public static void Apply()
        {
            Directory.CreateDirectory(MaterialDir);
            var atmosphere = EnsureMaterial("LL_Atmosphere", "LL/Atmosphere");
            EnsureMaterial("LL_Water", "LL/Water");
            EnsureMaterial("LL_Sky", "LL/Sky");
            EnsureMaterial("LL_Lit", "LL/Lit").enableInstancing = true;
            EnsureMaterial("LL_Glow", "LL/Glow").enableInstancing = true;
            EnsureMaterial("LL_Ring", "LL/Ring");
            EnsureMaterial("LL_Foam", "LL/Foam");
            EnsureMaterial("LL_BeamCore", "LL/BeamCore");
            EnsureMaterial("LL_ParticleLit", "LL/ParticleLit").enableInstancing = true;
            EnsureMaterial("LL_ParticleAdd", "LL/ParticleAdd").enableInstancing = true;

            foreach (var path in new[] { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" })
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data != null) ConfigureRenderer(data, atmosphere);
            }
            foreach (var path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
                ConfigurePipeline(path);

            ConfigurePlayer();
            EnsureScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] applied");
        }

        public static void ApplyBatch()
        {
            try { Apply(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static Material EnsureMaterial(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.Exception($"missing shader {shaderName}");
            var path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void ConfigureRenderer(UniversalRendererData data, Material atmosphere)
        {
            var feature = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == AtmosphereFeature);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = AtmosphereFeature;
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedProperties();
            }
            feature.passMaterial = atmosphere;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.requirements = ScriptableRenderPassInput.Depth;
            feature.fetchColorBuffer = true;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);

            // SSAO does nothing for this look and costs a lot. Remove it outright: a disabled feature
            // still gets Create() in the player, where its stripped shaders make it throw.
            var ssao = data.rendererFeatures.Where(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion").ToList();
            if (ssao.Count > 0)
            {
                var so = new SerializedObject(data);
                var features = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                for (int i = features.arraySize - 1; i >= 0; i--)
                {
                    var obj = features.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (obj == null || !ssao.Contains(obj as ScriptableRendererFeature)) continue;
                    features.DeleteArrayElementAtIndex(i);
                    map.DeleteArrayElementAtIndex(i);
                }
                so.ApplyModifiedProperties();
                foreach (var f in ssao) AssetDatabase.RemoveObjectFromAsset(f);
            }
            EditorUtility.SetDirty(data);
        }

        static void ConfigurePipeline(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null) return;
            var so = new SerializedObject(asset);
            void Set(string prop, float value)
            {
                var p = so.FindProperty(prop);
                if (p == null) { Debug.LogWarning($"[ProjectSetup] {path}: no {prop}"); return; }
                if (p.propertyType == SerializedPropertyType.Float) p.floatValue = value;
                else if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value > 0;
                else p.intValue = (int)value;
            }
            Set("m_RequireDepthTexture", 1);
            Set("m_RequireOpaqueTexture", 0);
            Set("m_SupportsHDR", 1);
            Set("m_HDRColorBufferPrecision", 1);   // 64-bit: no banding in the dark gradients
            Set("m_MSAA", 1);
            Set("m_ShadowDistance", 120);
            Set("m_MainLightShadowsSupported", 0);
            Set("m_AdditionalLightShadowsSupported", 0);
            Set("m_AdditionalLightsPerObjectLimit", 8);
            Set("m_SoftShadowsSupported", 0);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Gannet Head";
            PlayerSettings.productName = "Last Light";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            // The game switches fullscreen itself on F11 and Alt+Enter (see ScreenMode), so the
            // player's own Alt+Enter would switch it straight back.
            PlayerSettings.allowFullscreenSwitch = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.visibleInBackground = true;
            try { PlayerSettings.SplashScreen.show = false; } catch { }
            PlayerSettings.SplashScreen.showUnityLogo = false;
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            var profile = "Assets/Settings/SampleSceneProfile.asset";
            if (File.Exists(profile)) AssetDatabase.DeleteAsset(profile);
        }
    }
}
