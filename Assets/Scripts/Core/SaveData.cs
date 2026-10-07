using System;
using System.Collections.Generic;
using LastLight.Audio;
using LastLight.Sim;
using LastLight.View;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastLight.Core
{
    /// <summary>One kept Night Watch, for the table of best watches.</summary>
    [Serializable]
    public sealed class WatchRecord
    {
        public int score, ships, seconds;
        public bool hard;
        public int speed;                        // game speed in percent when below full; 0 = full speed
    }

    /// <summary>Progress and preferences, stored as JSON in save.json (see <see cref="SaveStore"/>).</summary>
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
        public List<WatchRecord> watches = new List<WatchRecord>();   // the five best watches, best first
        public const int WatchTable = 5;

        // Settings
        public float master = 0.9f, music = 0.75f, sfx = 1f, radio = 1f, ambience = 0.9f;
        public bool muteInBackground;            // silence the game while its window is out of focus
        public bool fullscreen = true;
        public int resWidth, resHeight;          // 0 = the desktop's own resolution
        public float turnSpeed = 1f;             // keyboard lens turn speed, 0.5..1.25
        public int quality = 2;                  // 0 low, 1 medium, 2 high
        public float renderScale = 1f;           // the 3D scene's resolution (the UI stays native)
        public int frameCap;                     // frames per second: 0 = the display's rate (at least 60), or 60 or 30
        public bool reduceFlashing;              // lightning and impact flashes much dimmer
        public int brightness;                   // the scene's exposure, in steps from -2 to 2 (0 = as graded)
        public int difficulty;                   // 0 Standard (the game as tuned), 1 Hard
        public float hudScale = 1f;              // HUD and radio text size: 1, 1.15 or 1.3
        public float gameSpeed = 1f;             // an assist: the simulation at 1, 0.85 or 0.7 of full speed
        public Difficulty Difficulty => difficulty == 1 ? Difficulty.Hard : Difficulty.Standard;
        public bool focusToggle;                 // focus: hold the button (false) or press to switch it on and off
        public KeyBindings keys = new KeyBindings();   // the keyboard's keys for turning, focus and the horn
        public bool shake = true;
        public float textSpeed = 1f;             // multiplier
        public bool hints = true;
        public List<string> hintsSeen = new List<string>();   // each onboarding hint shows once per save

        static SaveData current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        public static SaveData Current
        {
            get
            {
                if (current != null) return current;
                current = new SaveData();
                bool migrate = false;
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
                        current.watches = new List<WatchRecord>
                        {
                            new WatchRecord { score = 9600, ships = 57, seconds = 954 },
                            new WatchRecord { score = 7450, ships = 44, seconds = 781 },
                            new WatchRecord { score = 5100, ships = 31, seconds = 602 },
                        };
                    }
                    return current;
                }
                string json = "", from = "";
                try { json = SaveStore.Read(SaveStore.Dir, out from); }
                catch (Exception e) { Debug.LogWarning("[Save] could not open the save: " + e.Message); }
                if (!SaveStore.TryParse(json, current, out var error))
                {
                    // Start afresh, but keep the damaged text: the next save would overwrite it.
                    Debug.LogWarning($"[Save] could not read the save ({error}); kept it as {SaveStore.UnreadableName}");
                    current = new SaveData();
                    try { SaveStore.Write(SaveStore.Dir, SaveStore.UnreadableName, json); }
                    catch (Exception e) { Debug.LogWarning("[Save] could not keep the damaged save: " + e.Message); }
                }
                else if (from == "prefs" && !string.IsNullOrEmpty(json))
                {
                    // A save from an older build: carry it over to the file (the old entry stays put).
                    Debug.Log("[Save] carried the save over from PlayerPrefs to " + SaveStore.FileName);
                    migrate = true;
                }
                if (current.lamps == null || current.lamps.Length != 12) current.lamps = new int[12];
                if (current.best == null || current.best.Length != 12) current.best = new int[12];
                current.homeNames ??= new List<string>();
                current.hintsSeen ??= new List<string>();
                current.watches ??= new List<WatchRecord>();
                current.keys ??= new KeyBindings();
                current.keys.Validate();
                // Saves from before the table: the one best watch becomes its first entry.
                if (current.watches.Count == 0 && current.watchBest > 0)
                    current.watches.Add(new WatchRecord { score = current.watchBest, ships = current.watchShips, seconds = current.watchSeconds });
                if (migrate && !Application.isEditor) current.Save();
                return current;
            }
        }

        public void Save()
        {
            if (Game.HasArg("-llFresh")) return;
            try { SaveStore.Write(SaveStore.Dir, SaveStore.FileName, JsonUtility.ToJson(this)); }
            catch (Exception e) { Debug.LogWarning("[Save] could not write the save: " + e.Message); }
        }

        /// <summary>Whether there's a season to clear: a night kept, or a watch on the table.</summary>
        public bool HasProgress => unlocked > 1 || TotalLamps > 0 || shipsHome > 0 || endingSeen || watches.Count > 0 || watchBest > 0;

        /// <summary>Clears the season: the nights, lamps and scores, the ships brought home, the
        /// ending and the Night Watch records; the hints come back. Settings and keys stay. Doesn't
        /// save (see <see cref="NewSeason"/>).</summary>
        public void ClearSeason()
        {
            unlocked = 1;
            lamps = new int[12];
            best = new int[12];
            shipsHome = 0;
            homeNames = new List<string>();
            endingSeen = false;
            tutorialSeen = false;
            watchBest = watchShips = watchSeconds = 0;
            watches = new List<WatchRecord>();
            hintsSeen = new List<string>();
        }

        /// <summary>"Start a new season": the season so far is kept as save.previous.json, then
        /// cleared and saved.</summary>
        public void NewSeason()
        {
            if (!Game.HasArg("-llFresh"))
            {
                try { SaveStore.Write(SaveStore.Dir, SaveStore.PreviousName, JsonUtility.ToJson(this)); }
                catch (Exception e) { Debug.LogWarning("[Save] could not keep the old season: " + e.Message); }
            }
            ClearSeason();
            Save();
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

        /// <summary>Records a finished watch; returns its place in the table of best watches (1 = best),
        /// or 0 if it didn't make the table.</summary>
        public int RecordWatch(int score, int ships, float seconds, IEnumerable<string> names, int speed = 100)
        {
            var record = new WatchRecord { score = score, ships = ships, seconds = Mathf.RoundToInt(seconds), hard = difficulty == 1, speed = speed < 100 ? speed : 0 };
            watches.Add(record);
            watches.Sort((a, b) => b.score != a.score ? b.score.CompareTo(a.score) : b.seconds.CompareTo(a.seconds));
            int rank = watches.IndexOf(record) + 1;
            if (watches.Count > WatchTable) watches.RemoveRange(WatchTable, watches.Count - WatchTable);
            if (rank > WatchTable) rank = 0;
            watchBest = Mathf.Max(watchBest, score);
            watchShips = Mathf.Max(watchShips, ships);
            watchSeconds = Mathf.Max(watchSeconds, Mathf.RoundToInt(seconds));
            foreach (var n in names)
            {
                shipsHome++;
                if (!homeNames.Contains(n)) homeNames.Add(n);
            }
            Save();
            return rank;
        }

        /// <summary>The frame rate to aim for: the display's refresh rate (at least 60), or the cap.</summary>
        public int FrameRate
        {
            get
            {
                if (frameCap == 30 || frameCap == 60) return frameCap;
                int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
                return Mathf.Max(60, hz);
            }
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
            Application.targetFrameRate = Game.Arg("-llFps", FrameRate);
            brightness = Mathf.Clamp(brightness, -2, 2);
            Stage.SetBrightness(brightness);
            if (Game.Instance != null && Game.Instance.Hud != null) Game.Instance.Hud.SetScale(hudScale);
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
