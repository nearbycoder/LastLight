using System;
using System.Collections.Generic;
using LastLight.Audio;
using LastLight.View;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastLight.Core
{
    /// <summary>Progress and preferences, stored as JSON in PlayerPrefs.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public int unlocked = 1;                 // highest night that can be played
        public int[] lamps = new int[12];
        public int[] best = new int[12];
        public int shipsHome;
        public List<string> homeNames = new List<string>();
        public bool endingSeen;
        public bool tutorialSeen;
        public int watchBest, watchShips, watchSeconds;   // Night Watch records

        // Settings
        public float master = 0.9f, music = 0.75f, sfx = 1f, radio = 1f, ambience = 0.9f;
        public bool fullscreen = true;
        public int resWidth, resHeight;          // 0 = the desktop's own resolution
        public float turnSpeed = 1f;             // keyboard lens turn speed, 0.5..1.25
        public int quality = 2;                  // 0 low, 1 medium, 2 high
        public float renderScale = 1f;           // the 3D scene's resolution (the UI stays native)
        public bool reduceFlashing;              // lightning and impact flashes much dimmer
        public bool shake = true;
        public float textSpeed = 1f;             // multiplier
        public bool hints = true;
        public List<string> hintsSeen = new List<string>();   // each onboarding hint shows once per save

        const string Key = "lastlight.save";
        static SaveData current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        public static SaveData Current
        {
            get
            {
                if (current != null) return current;
                current = new SaveData();
                if (Game.HasArg("-llFresh"))
                {
                    // Tours can start mid-season (seven nights kept) to show a filled-in logbook.
                    if (Game.HasArg("-llSampleSave"))
                    {
                        current.unlocked = 8;
                        current.lamps = new[] { 3, 3, 2, 3, 1, 3, 2, 0, 0, 0, 0, 0 };
                        current.best = new[] { 610, 790, 880, 1210, 820, 1560, 1490, 0, 0, 0, 0, 0 };
                        current.shipsHome = 43;
                    }
                    // ... or with the season finished and the Night Watch open.
                    if (Game.HasArg("-llSeasonDone"))
                    {
                        current.unlocked = 12;
                        current.lamps = new[] { 3, 3, 3, 3, 2, 3, 3, 2, 1, 3, 2, 2 };
                        current.best = new[] { 610, 790, 1000, 1210, 1150, 1600, 1700, 1650, 1150, 1800, 1900, 2250 };
                        current.shipsHome = 97;
                        current.endingSeen = true;
                        current.watchBest = 9600;
                        current.watchShips = 57;
                        current.watchSeconds = 954;
                    }
                    return current;
                }
                var json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    try { JsonUtility.FromJsonOverwrite(json, current); }
                    catch (Exception e) { Debug.LogWarning("[Save] could not read save: " + e.Message); }
                }
                if (current.lamps == null || current.lamps.Length != 12) current.lamps = new int[12];
                if (current.best == null || current.best.Length != 12) current.best = new int[12];
                current.homeNames ??= new List<string>();
                current.hintsSeen ??= new List<string>();
                return current;
            }
        }

        public void Save()
        {
            if (Game.HasArg("-llFresh")) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public void RecordNight(int night, int lamps, int score, IEnumerable<string> names)
        {
            int i = night - 1;
            if (i < 0 || i >= 12) return;
            bool won = lamps > 0;
            this.lamps[i] = Mathf.Max(this.lamps[i], lamps);
            if (won) best[i] = Mathf.Max(best[i], score);
            if (won && night >= unlocked) unlocked = Mathf.Min(12, night + 1);
            foreach (var n in names)
            {
                shipsHome++;
                if (!homeNames.Contains(n)) homeNames.Add(n);
            }
            Save();
        }

        /// <summary>The Night Watch opens once the last night has been kept.</summary>
        public bool WatchUnlocked => endingSeen || lamps[11] > 0;

        public void RecordWatch(int score, int ships, float seconds, IEnumerable<string> names)
        {
            watchBest = Mathf.Max(watchBest, score);
            watchShips = Mathf.Max(watchShips, ships);
            watchSeconds = Mathf.Max(watchSeconds, Mathf.RoundToInt(seconds));
            foreach (var n in names)
            {
                shipsHome++;
                if (!homeNames.Contains(n)) homeNames.Add(n);
            }
            Save();
        }

        public int TotalLamps
        {
            get
            {
                int n = 0;
                foreach (var l in lamps) n += l;
                return n;
            }
        }

        /// <summary>Push audio/video preferences into the running game.</summary>
        /// <param name="display">Also apply the window mode and resolution (skipped at startup
        /// when the size was given on the command line, as the dev scripts do).</param>
        public void Apply(bool display = true)
        {
            Sfx.Master = master;
            Sfx.MusicVolume = music;
            Sfx.SfxVolume = sfx;
            Sfx.RadioVolume = radio;
            Sfx.AmbienceVolume = ambience;
            if (Game.Arg("-llSteps", -1) <= 0) ShaderGlobals.Steps = quality switch { 0 => 10, 1 => 16, _ => 24 };
            ShaderGlobals.FlashScale = FlashFx.Scale = reduceFlashing ? 0.12f : 1f;
            // The pipeline asset is shared with the editor, so only the player changes it.
            if (!Application.isEditor && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.renderScale = Mathf.Clamp(renderScale, 0.5f, 1f);
            if (display && !Application.isEditor)
            {
                var mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                var native = Screen.currentResolution;
                int w = resWidth > 0 ? resWidth : fullscreen ? native.width : Mathf.Min(1600, native.width);
                int h = resHeight > 0 ? resHeight : fullscreen ? native.height : Mathf.Min(900, native.height);
                if (Screen.fullScreenMode != mode || Screen.width != w || Screen.height != h) Screen.SetResolution(w, h, mode);
            }
        }
    }
}
