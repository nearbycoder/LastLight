using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastLight.EditorTools
{
    /// <summary>Menu items and batch-mode entry points for building the player.</summary>
    public static class BuildScript
    {
        static readonly string[] Scenes = { "Assets/Scenes/Main.unity" };
        public const string LinuxPath = "Builds/Linux/LastLight.x86_64";
        public const string MacPath = "Builds/Mac/LastLight.app";
        public const string WindowsPath = "Builds/Windows/LastLight.exe";
        /// <summary>The browser build: the folder is the site (its name names the files in Build/).
        /// Tools/build-pages.sh adds .nojekyll; serve its parent and open /LastLight/.</summary>
        public const string WebPath = "Builds/Pages/LastLight";

        [MenuItem("Last Light/Build Linux Player")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, LinuxPath);

        /// <summary>A universal (Intel and Apple Silicon) Mono player. Unsigned and un-notarized:
        /// signing needs an Apple Developer account, which is the owner's call.</summary>
        [MenuItem("Last Light/Build macOS Player")]
        public static void BuildMac()
        {
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.x64ARM64;
            Build(BuildTarget.StandaloneOSX, MacPath);
        }

        /// <summary>Needs Unity's Windows Build Support (Mono) module for 6000.6.2f1.</summary>
        [MenuItem("Last Light/Build Windows Player")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, WindowsPath);

        /// <summary>
        /// The browser build for GitHub Pages (Tools/build-pages.sh): WebGL 2 only, no threads (so no
        /// SharedArrayBuffer or COOP/COEP headers), Brotli with the loader's own decompression so
        /// a static host that sends no Content-Encoding still works, the page template in
        /// Assets/WebGLTemplates/LastLight, and the code built for size. The desktop targets don't
        /// read any of these settings. The editor is put back on Linux afterwards.
        /// </summary>
        [MenuItem("Last Light/Build Web Player (GitHub Pages)")]
        public static void BuildWeb()
        {
            var target = BuildTarget.WebGL;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, target))
            {
                Debug.LogError($"[LastLight] WebGL build: this editor has no Web build support. Install the module for Unity {Application.unityVersion} in Unity Hub.");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }
            PlayerSettings.WebGL.template = "PROJECT:LastLight";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;
            PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
            PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.OpenGLES3 });
            SetCodeOptimization(Environment.GetEnvironmentVariable("LL_WEB_OPTIMIZATION") ?? "DiskSizeLTO");
            if (Directory.Exists(WebPath)) Directory.Delete(WebPath, true);
            var report = DoBuild(target, WebPath);
            bool ok = report.summary.result == BuildResult.Succeeded;
            if (Application.isBatchMode)
            {
                // Leave the project on the desktop target the other tools expect.
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        /// <summary>Unity's Code Optimization for the web (BuildTimes, RuntimeSpeed, RuntimeSpeedLTO,
        /// DiskSize, DiskSizeLTO). It lives with the Web module, so it's set by name.</summary>
        static void SetCodeOptimization(string value)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType("UnityEditor.WebGL.UserBuildSettings");
                var prop = type?.GetProperty("codeOptimization", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop == null) continue;
                prop.SetValue(null, Enum.Parse(prop.PropertyType, value));
                Debug.Log($"[LastLight] WebGL code optimization: {value}");
                return;
            }
            Debug.LogWarning("[LastLight] WebGL code optimization setting not found; using the editor's");
        }

        /// <summary>Build from a running editor (e.g. via `unity command eval`); returns a summary.</summary>
        public static string BuildLinuxFromEditor()
        {
            var report = DoBuild(BuildTarget.StandaloneLinux64, LinuxPath);
            return $"{report.summary.result} {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors, {report.summary.totalTime.TotalSeconds:0}s";
        }

        static void Build(BuildTarget target, string path)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
            {
                Debug.LogError($"[LastLight] {target} build: this editor has no build support for {target}. Install the module for Unity {Application.unityVersion} in Unity Hub.");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }
            var report = DoBuild(target, path);
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static BuildReport DoBuild(BuildTarget target, string path)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[LastLight] {target} build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors -> {path}");
            return report;
        }
    }
}
