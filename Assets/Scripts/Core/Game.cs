using System;
using System.IO;
using LastLight.Sim;
using LastLight.View;
using UnityEngine;

namespace LastLight.Core
{
    /// <summary>
    /// Entry point. Built automatically after the scene loads: sets up the stage and the world,
    /// then runs the game flow. Command-line switches (also readable from Temp/ll_boot.txt in the
    /// editor): -llNight N (start a night directly), -llAuto (the AutoKeeper plays).
    /// </summary>
    public sealed class Game : MonoBehaviour
    {
        public static Game Instance { get; private set; }
        public WorldView World { get; private set; }
        public CameraRig Rig { get; private set; }
        public MissionRunner Runner { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            new GameObject("Game").AddComponent<Game>();
        }

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            Rig = Stage.BuildCamera();
            Stage.BuildMoon();
            Stage.BuildPost();
            ShaderGlobals.PushMood();
            Look.Apply();
            World = WorldView.Build(MapData.Load());
        }

        void Start()
        {
            int night = Arg("-llNight", 1);
            bool auto = HasArg("-llAuto");
            StartNight(Mathf.Clamp(night, 1, MissionLibrary.All.Count), auto);
        }

        public void StartNight(int night, bool auto)
        {
            if (Runner != null) Runner.Teardown();
            var def = MissionLibrary.All[night - 1];
            Runner = MissionRunner.Begin(def, World, auto);
            Debug.Log($"[Game] night {night} '{def.title}' auto={auto}");
        }

        void Update()
        {
            ShaderGlobals.PushMood();
        }

        // ------------------------------------------------------------------ arguments

        static string[] args;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { args = null; Instance = null; }

        static string[] Args
        {
            get
            {
                if (args != null) return args;
                var list = new System.Collections.Generic.List<string>(Environment.GetCommandLineArgs());
#if UNITY_EDITOR
                var boot = Path.Combine(Application.dataPath, "../Temp/ll_boot.txt");
                if (File.Exists(boot)) list.AddRange(File.ReadAllText(boot).Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries));
#endif
                args = list.ToArray();
                return args;
            }
        }

        public static bool HasArg(string name) => Array.IndexOf(Args, name) >= 0;

        public static int Arg(string name, int fallback)
        {
            int i = Array.IndexOf(Args, name);
            return i >= 0 && i + 1 < Args.Length && int.TryParse(Args[i + 1], out int v) ? v : fallback;
        }

        public static string ArgString(string name, string fallback)
        {
            int i = Array.IndexOf(Args, name);
            return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : fallback;
        }
    }
}
