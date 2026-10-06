using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LastLight.EditorTools
{
    /// <summary>Menu items and batch-mode entry points for building the player.</summary>
    public static class BuildScript
    {
        static readonly string[] Scenes = { "Assets/Scenes/Main.unity" };
        public const string LinuxPath = "Builds/Linux/LastLight.x86_64";
        public const string MacPath = "Builds/Mac/LastLight.app";
        public const string WindowsPath = "Builds/Windows/LastLight.exe";

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
