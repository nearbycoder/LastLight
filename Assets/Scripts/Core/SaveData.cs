using System;
using System.Collections.Generic;
using LastLight.Audio;
using LastLight.View;
using UnityEngine;

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

        // Settings
        public float master = 0.9f, music = 0.75f, sfx = 1f, radio = 1f, ambience = 0.9f;
        public bool fullscreen = true;
        public int quality = 2;                  // 0 low, 1 medium, 2 high
        public bool shake = true;
        public float textSpeed = 1f;             // multiplier
        public bool hints = true;

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
                if (Game.HasArg("-llFresh")) return current;
                var json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    try { JsonUtility.FromJsonOverwrite(json, current); }
                    catch (Exception e) { Debug.LogWarning("[Save] could not read save: " + e.Message); }
                }
                if (current.lamps == null || current.lamps.Length != 12) current.lamps = new int[12];
                if (current.best == null || current.best.Length != 12) current.best = new int[12];
                current.homeNames ??= new List<string>();
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
        public void Apply()
        {
            Sfx.Master = master;
            Sfx.MusicVolume = music;
            Sfx.SfxVolume = sfx;
            Sfx.RadioVolume = radio;
            Sfx.AmbienceVolume = ambience;
            if (Game.Arg("-llSteps", -1) <= 0) ShaderGlobals.Steps = quality switch { 0 => 10, 1 => 16, _ => 24 };
            if (!Application.isEditor)
            {
                var mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
            }
        }
    }
}
