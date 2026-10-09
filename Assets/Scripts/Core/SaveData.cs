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

    /// <summary>A Night Watch still under way, as it stood at its last checkpoint. It's written
    /// every so often during a watch, so a watch cut short by a crash or a power cut can be kept
    /// the next time the game starts.</summary>
    [Serializable]
    public sealed class WatchUnderway
    {
        public bool active;
        public int score, ships, seconds;
        public bool hard;
        public int speed;                        // as in WatchRecord: percent when below full, 0 = full speed
        public List<string> names = new List<string>();   // the ships home so far
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
        public WatchUnderway watchUnderway = new WatchUnderway();     // a watch being played (see CheckpointWatch)

        // Settings
        public float master = 0.9f, music = 0.75f, sfx = 1f, radio = 1f, ambience = 0.9f;
        public bool muteInBackground;            // silence the game while its window is out of focus
        public bool mono;                        // everything in both ears alike (Settings ▸ Sound)
        public bool fullscreen = true;
        public int resWidth, resHeight;          // 0 = the desktop's own resolution
        public float turnSpeed = 1f;             // keyboard lens turn speed, 0.5..1.25
        public int quality = DefaultQuality;     // Graphics fidelity: 0 Low, 1 Medium, 2 High, 3 Ultra (see Fidelity; once Fog and haze quality, Low to High)
        /// <summary>A new save's Graphics fidelity: High, the game as released; in a browser, which
        /// draws through WebGL at some cost, Medium.</summary>
        public static int DefaultQuality => Platform.IsWeb ? Fidelity.Medium : Fidelity.High;
        public float renderScale = 1f;           // the 3D scene's resolution (the UI stays native)
        public int frameCap;                     // frames per second: 0 = the display's rate (at least 60), or 60 or 30
        public bool reduceFlashing;              // lightning and impact flashes much dimmer
        public int brightness;                   // the scene's exposure, in steps from -2 to 2 (0 = as graded)
        public int difficulty;                   // 0 Standard (the game as tuned), 1 Hard
        public float hudScale = 1f;              // HUD and radio text size: 1, 1.15 or 1.3
        public float gameSpeed = 1f;             // an assist: the simulation at 1, 0.85 or 0.7 of full speed
        public Difficulty Difficulty => difficulty == 1 ? Difficulty.Hard : Difficulty.Standard;
        public bool focusToggle;                 // focus: hold the button (false) or press to switch it on and off
        public int padStyle;                     // the pad's button names: 0 Auto, 1 Xbox, 2 PlayStation, 3 Nintendo (see PadButtons)
        public KeyBindings keys = new KeyBindings();   // the keyboard's keys for turning, focus and the horn
        public PadBindings pad = new PadBindings();    // the gamepad's buttons for focus and the horn
        public bool shake = true;
        public float textSpeed = 1f;             // multiplier
        public bool plainRadio;                  // the radio's calls in the plain sans-serif rather than the typewriter
        public bool hints = true;
        public List<string> hintsSeen = new List<string>();   // each onboarding hint shows once per save

        static SaveData current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { current = null; LoadProblem = WriteProblem = writeReason = null; writeBlocked = false; }

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
                        current.watches = new List<WatchRecord>
                        {
                            new WatchRecord { score = 9600, ships = 57, seconds = 954 },
                            new WatchRecord { score = 7450, ships = 44, seconds = 781 },
                            new WatchRecord { score = 5100, ships = 31, seconds = 602 },
                        };
                    }
                    return current;
                }
                current = Load(SaveStore.Dir, out var problem, out writeBlocked, out bool migrate);
                LoadProblem = problem;
                if (migrate && !Application.isEditor) current.Save();
                return current;
            }
        }

        /// <summary>Trouble reading the save at startup, in words for the keeper (null if none).</summary>
        public static string LoadProblem { get; private set; }
        /// <summary>Why the last save couldn't be written (null once one has been).</summary>
        public static string WriteProblem { get; private set; }
        /// <summary>The save is there but couldn't be opened: this session never writes over it.</summary>
        static bool writeBlocked;
        static string writeReason;

        /// <summary>For the dawn card: why the night just kept wasn't saved (null if it was).</summary>
        public static string Unsaved => writeBlocked ? "This night wasn't saved: the save couldn't be opened when the game started."
            : writeReason != null ? $"This night couldn't be saved ({writeReason}). The title says where the save should be." : null;

        /// <summary>What to tell the keeper about the save, if anything: the title shows it, and the
        /// dawn card shows the part about progress not being kept.</summary>
        public static string Trouble => LoadProblem != null && WriteProblem != null && !writeBlocked ? LoadProblem + "\n" + WriteProblem : LoadProblem ?? WriteProblem;

        /// <summary>Reads the save in <paramref name="dir"/>. A save that can't be opened leaves the
        /// keeper a blank season and <paramref name="blocked"/> set, so it's never written over; one
        /// that opens but can't be read is kept aside as save.unreadable.json and a season begins
        /// afresh. Either way <paramref name="problem"/> says so in words.</summary>
        public static SaveData Load(string dir, out string problem, out bool blocked, out bool migrate)
        {
            var data = new SaveData();
            problem = null;
            blocked = migrate = false;
            string json, from;
            try { json = SaveStore.Read(dir, out from); }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] could not open the save: " + e.Message);
                blocked = true;
                problem = $"Your save couldn't be opened ({SaveStore.Reason(e)}), so this season won't be kept. The save itself is left as it was, in\n{dir}";
                return data;
            }
            if (!SaveStore.TryParse(json, data, out var error))
            {
                // Start afresh, but keep the damaged text: the next save would overwrite it.
                Debug.LogWarning($"[Save] could not read the save ({error}); kept it as {SaveStore.UnreadableName}");
                data = new SaveData();
                try
                {
                    SaveStore.Write(dir, SaveStore.UnreadableName, json);
                    problem = $"Your save couldn't be read, so a new season has begun. The damaged save was kept as {SaveStore.UnreadableName}, in\n{dir}";
                }
                catch (Exception e)
                {
                    // It couldn't be kept aside either: don't write over the only copy.
                    Debug.LogWarning("[Save] could not keep the damaged save: " + e.Message);
                    blocked = true;
                    problem = $"Your save couldn't be read or copied ({SaveStore.Reason(e)}), so this season won't be kept. The save itself is left as it was, in\n{dir}";
                }
            }
            else if (from == "prefs" && !string.IsNullOrEmpty(json))
            {
                // A save from an older build: carry it over to the file (the old entry stays put).
                Debug.Log("[Save] carried the save over from PlayerPrefs to " + SaveStore.FileName);
                migrate = true;
            }
            if (data.lamps == null || data.lamps.Length != 12) data.lamps = new int[12];
            if (data.best == null || data.best.Length != 12) data.best = new int[12];
            data.homeNames ??= new List<string>();
            data.hintsSeen ??= new List<string>();
            data.watches ??= new List<WatchRecord>();
            data.watchUnderway ??= new WatchUnderway();
            data.watchUnderway.names ??= new List<string>();
            data.keys ??= new KeyBindings();
            data.keys.Validate();
            data.pad ??= new PadBindings();
            data.pad.Validate();
            // Saves from before the table: the one best watch becomes its first entry.
            if (data.watches.Count == 0 && data.watchBest > 0)
                data.watches.Add(new WatchRecord { score = data.watchBest, ships = data.watchShips, seconds = data.watchSeconds });
            return data;
        }

        public void Save()
        {
            if (Game.HasArg("-llFresh")) return;
            SaveTo(SaveStore.Dir);
        }

        /// <summary>Writes the save to <paramref name="dir"/>, unless this session mustn't (see
        /// <see cref="Load"/>); a failure is kept in <see cref="WriteProblem"/> for the keeper.</summary>
        public bool SaveTo(string dir)
        {
            if (writeBlocked) return false;
            try
            {
                SaveStore.Write(dir, SaveStore.FileName, JsonUtility.ToJson(this));
                WriteProblem = writeReason = null;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] could not write the save: " + e.Message);
                writeReason = SaveStore.Reason(e);
                WriteProblem = $"Progress isn't being saved ({writeReason}). The save should be in\n{dir}";
                return false;
            }
        }

        /// <summary>For tests: forget this session's save trouble.</summary>
        public static void ClearTrouble(bool blocked = false)
        {
            LoadProblem = WriteProblem = writeReason = null;
            writeBlocked = blocked;
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
            watchUnderway = new WatchUnderway();
            hintsSeen = new List<string>();
        }

        /// <summary>"Start a new season": the season so far is kept as save.previous.json, then
        /// cleared and saved.</summary>
        public void NewSeason()
        {
            if (!Game.HasArg("-llFresh") && !writeBlocked)
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

        /// <summary>Records a finished watch and saves; returns its place in the table of best
        /// watches (1 = best), or 0 if it didn't make the table.</summary>
        public int RecordWatch(int score, int ships, float seconds, IEnumerable<string> names, int speed = 100)
        {
            int rank = AddWatch(score, ships, seconds, names, difficulty == 1, speed);
            Save();
            return rank;
        }

        /// <summary>Puts a finished watch in the table (without saving) and clears the watch under
        /// way; returns its place, 1 = best, or 0 if it didn't make the table.</summary>
        public int AddWatch(int score, int ships, float seconds, IEnumerable<string> names, bool hard, int speed = 100)
        {
            var record = new WatchRecord { score = score, ships = ships, seconds = Mathf.RoundToInt(seconds), hard = hard, speed = speed > 0 && speed < 100 ? speed : 0 };
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
            watchUnderway = new WatchUnderway();
            return rank;
        }

        /// <summary>Notes where a watch under way stands (without saving), so it can be kept if the
        /// game never gets to close properly.</summary>
        public void CheckpointWatch(int score, int ships, float seconds, IEnumerable<string> names, int speed = 100)
        {
            watchUnderway = new WatchUnderway
            {
                active = true, score = score, ships = ships, seconds = Mathf.RoundToInt(seconds),
                hard = difficulty == 1, speed = speed < 100 ? speed : 0, names = new List<string>(names),
            };
        }

        /// <summary>Forgets the watch under way (it was thrown away); doesn't save.</summary>
        public void DropWatchUnderway() => watchUnderway = new WatchUnderway();

        /// <summary>A watch still under way in the save when the game starts was cut short (a crash,
        /// a power cut, a killed process): it's put in the table as it stood at its last checkpoint,
        /// once. Returns the record and its place (0 if it missed the table), or null if there was
        /// none. Doesn't save.</summary>
        public WatchRecord RecoverWatch(out int rank)
        {
            rank = 0;
            var u = watchUnderway;
            if (u == null || !u.active) return null;
            rank = AddWatch(u.score, u.ships, u.seconds, u.names ?? new List<string>(), u.hard, u.speed > 0 ? u.speed : 100);
            return new WatchRecord { score = u.score, ships = u.ships, seconds = u.seconds, hard = u.hard, speed = u.speed };
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
            MonoMix.On = mono;
            Platform.SetMono(mono);   // the browser has no OnAudioFilterRead: the page's output folds instead
            quality = Mathf.Clamp(quality, Fidelity.Low, Fidelity.Ultra);
            Fidelity.Set(quality, steps: Game.Arg("-llSteps", -1) <= 0);
            Stage.ApplyFidelity();
            if (Game.Instance != null) Game.Instance.OnFidelity();
            ShaderGlobals.FlashScale = FlashFx.Scale = reduceFlashing ? 0.12f : 1f;
            // In a browser, Display is the page's own pace (requestAnimationFrame, -1); a number
            // there would pace the game by timer instead.
            Application.targetFrameRate = Game.Arg("-llFps", Platform.IsWeb && frameCap != 30 && frameCap != 60 ? -1 : FrameRate);
            brightness = Mathf.Clamp(brightness, -2, 2);
            Stage.SetBrightness(brightness);
            if (Game.Instance != null && Game.Instance.Hud != null) { Game.Instance.Hud.SetScale(hudScale); Game.Instance.Hud.SetRadioLettering(); }
            // The pipeline asset is shared with the editor, so only the player changes it.
            if (!Application.isEditor && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.renderScale = Mathf.Clamp(renderScale, 0.5f, 1f);
            // A page has no window to size, and fullscreen is the browser's to grant on a click
            // (Settings ▸ Display and ScreenMode ask for it there).
            if (display && !Application.isEditor && !Platform.IsWeb)
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
