using System;
using System.Collections.Generic;
using System.IO;
using LastLight.Audio;
using LastLight.Sim;
using LastLight.UI;
using LastLight.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight.Core
{
    /// <summary>
    /// Entry point and game flow: title, logbook, briefing, the watch, pause, dawn results and the
    /// ending. Built automatically after the scene loads; everything else is created from code.
    /// Command-line switches (also read from Temp/ll_boot.txt in the editor):
    ///   -llNight N   jump straight into night N (13: the Night Watch; -llSeed S fixes its ships)
    ///   -llAuto      the AutoKeeper plays
    ///   -llFresh     ignore and don't write the save -llTour d screenshot tour (see Tour)
    /// </summary>
    public sealed class Game : MonoBehaviour
    {
        public enum State { Title, Logbook, Briefing, Playing, Paused, Results, Ending }

        public static Game Instance { get; private set; }
        public WorldView World { get; private set; }
        public CameraRig Rig { get; private set; }
        public MissionRunner Runner { get; private set; }
        public State Current { get; private set; } = State.Title;
        public Radio Radio { get; } = new Radio();
        public Hud Hud { get; private set; }
        public int Night { get; private set; }
        public bool AutoPlay;

        Canvas menus;
        TitleScreen title;
        LogbookScreen logbook;
        BriefingScreen briefing;
        PauseScreen pause;
        SettingsScreen settings;
        NotesScreen notes;
        State notesReturn;
        ResultsScreen results;
        ChartScreen chart;
        Fader fader;
        Feedback feedback;
        State settingsReturn;
        State logbookReturn;
        float outcomeTimer = -1f;
        bool recorded;
        int previousBest;
        int failNight, failStreak;   // the same night failed this many times running
        int watchRank;
        Ending ending;
        ParticleSystem rainFx;
        MissionDef watchDef;

        bool Watching => Night == NightWatch.Number;

        /// <summary>The night's mission; the Night Watch is generated afresh for each watch.</summary>
        MissionDef DefFor(int night, bool fresh)
        {
            if (night != NightWatch.Number) return MissionLibrary.All[Mathf.Clamp(night, 1, MissionLibrary.All.Count) - 1];
            if (fresh || watchDef == null) watchDef = NightWatch.Generate(World.Map, Arg("-llSeed", UnityEngine.Random.Range(1, 100000)), 1f, SaveData.Current.difficulty == 1);
            return watchDef;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            new GameObject("Game").AddComponent<Game>();
        }

        void Awake()
        {
            Instance = this;
            // Paced by targetFrameRate rather than vsync: some Wayland compositors throttle
            // FIFO presentation to ~12 Hz, and they composite without tearing anyway.
            QualitySettings.vSyncCount = Arg("-llVsync", 0);
            // The rate itself comes from Settings ▸ Frame rate (SaveData.Apply), or -llFps.
            Time.timeScale = 1f;
            // Tours can set the comfort settings (with -llFresh the save is a blank one).
            int scale = Arg("-llRenderScale", 0);
            if (scale > 0) SaveData.Current.renderScale = scale / 100f;
            if (HasArg("-llReduceFlashing")) SaveData.Current.reduceFlashing = true;
            if (HasArg("-llHard")) SaveData.Current.difficulty = 1;
            int hudScale = Arg("-llHudScale", 0);
            if (hudScale > 0) SaveData.Current.hudScale = hudScale / 100f;
            SaveData.Current.Apply(display: !HasArg("-screen-width"));
            int steps = Arg("-llSteps", -1);
            if (steps > 0) ShaderGlobals.Steps = steps;
            Rig = Stage.BuildCamera();
            Stage.BuildMoon();
            Stage.BuildPost();
            ShaderGlobals.PushMood();
            Look.Apply();
            World = WorldView.Build(MapData.Load());
            BuildUi();
            Feedback.EnsureLoops();
            AutoPlay = HasArg("-llAuto");
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        void OnDestroy() => InputSystem.onDeviceChange -= OnDeviceChange;

        // Don't let the night run on unattended: pause when the window loses focus (tours run
        // unfocused, so they call FocusLost themselves) or when the pad in use goes away.
        void OnApplicationFocus(bool focused)
        {
            if (!HasArg("-llTour")) FocusChanged(focused);
        }

        /// <summary>The window lost or regained focus: pause the night, and fall silent if the
        /// keeper asked for that (Settings ▸ Sound in background).</summary>
        public void FocusChanged(bool focused)
        {
            AudioListener.volume = !focused && SaveData.Current.muteInBackground ? 0f : 1f;
            if (!focused) FocusLost();
        }

        public void FocusLost()
        {
            if (Current == State.Playing && !AutoPlay) Pause();
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad && InputMode.Pad && (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected))
            {
                InputMode.Set(false);
                if (Current == State.Playing && !AutoPlay) Pause();
            }
        }

        void BuildUi()
        {
            UiKit.EnsureEventSystem();
            Hud = Hud.Create();
            Hud.SetScale(SaveData.Current.hudScale);
            menus = UiKit.MakeCanvas("Menus", 20);
            title = TitleScreen.Create(menus.transform);
            logbook = LogbookScreen.Create(menus.transform);
            briefing = BriefingScreen.Create(menus.transform);
            pause = PauseScreen.Create(menus.transform);
            settings = SettingsScreen.Create(menus.transform);
            notes = NotesScreen.Create(menus.transform);
            results = ResultsScreen.Create(menus.transform);
            chart = ChartScreen.Create(menus.transform);
            fader = Fader.Create(menus.transform);

            title.OnBegin = () => ShowBriefing(SaveData.Current.unlocked);
            title.OnWatch = () => ShowBriefing(NightWatch.Number);
            title.OnLogbook = () => ShowLogbook(State.Title);
            title.OnSettings = () => ShowSettings(State.Title);
            title.OnNotes = () => ShowNotes(State.Title);
            notes.OnBack = CloseNotes;
            title.OnQuit = Quit;
            logbook.OnPick = n => { logbook.Hide(); ShowBriefing(n); };
            logbook.OnBack = () => { logbook.Hide(); ShowTitle(false); };
            logbook.OnNewSeason = () =>
            {
                SaveData.Current.NewSeason();
                logbook.Refresh(MissionLibrary.All, SaveData.Current);
                title.RefreshFooter();
                Sfx.Play("ui_begin", 0.5f);
            };
            briefing.OnStart = BeginWatch;
            pause.OnResume = Resume;
            pause.OnRestart = () => { Time.timeScale = 1f; pause.Hide(); RestartNight(); };
            pause.OnEndWatch = EndWatch;
            pause.OnSettings = () => { pause.Hide(); ShowSettings(State.Paused); };
            pause.OnNotes = () => { pause.Hide(); ShowNotes(State.Paused); };
            pause.OnLogbook = () => { pause.Hide(); Time.timeScale = 1f; ShowLogbook(State.Title); };
            pause.OnTitle = () => { Time.timeScale = 1f; pause.Hide(); ShowTitle(); };
            settings.OnFocusMode = title.RefreshFooter;
            settings.OnBack = () =>
            {
                settings.Hide();
                if (settingsReturn == State.Paused) { pause.Show(); Current = State.Paused; }
                else ShowTitle(false);
            };
            results.OnNext = () =>
            {
                results.Hide();
                if (Watching) ShowBriefing(NightWatch.Number);
                else if (Night >= 12) StartEnding();
                else ShowBriefing(Night + 1);
            };
            results.OnRetry = () => { results.Hide(); RestartNight(); };
            results.OnLogbook = () => { results.Hide(); ShowLogbook(State.Title); };
            results.OnChart = ShowChart;
            chart.OnBack = CloseChart;
        }

        /// <summary>The night's chart, from the dawn card.</summary>
        void ShowChart()
        {
            if (Runner == null || Current != State.Results) return;
            var w = Runner.World;
            string title = Watching ? "The Night Watch" : $"Night {UiKit.Roman(Night)}  ·  {Runner.Def.title}";
            string home = Watching ? $"{w.Arrivals} ships home in {UiKit.Clock(w.Time)}" : $"{w.Arrivals} of {w.TotalShips} ships home";
            if (w.Wrecks > 0) home += w.Wrecks == 1 ? ", one wrecked" : $", {w.Wrecks} wrecked";
            results.Hide(0.2f);
            chart.Setup(World.Map, w, Runner.Log, title, home);
            chart.Show();
        }

        void CloseChart()
        {
            if (!chart.Visible) return;
            chart.Hide(0.2f);
            if (Current == State.Results) results.ShowAgain();
        }

        void Start()
        {
            int night = Arg("-llNight", 0);
            if (night > 0)
            {
                Night = night == NightWatch.Number ? night : Mathf.Clamp(night, 1, MissionLibrary.All.Count);
                StartRunner(DefFor(Night, true), false);
                Rig.Snap(CameraRig.PlayPose);
                Stage.MoonTowards(false, 0.01f);
                BeginWatch();
                return;
            }
            Rig.Snap(CameraRig.TitlePose);
            ShowTitle(false);
            fader.FadeFrom(1f, 2.5f);
        }

        // ---------------------------------------------------------------- flow

        void ShowTitle(bool moveCamera = true)
        {
            Current = State.Title;
            Hud.Show(false);
            Time.timeScale = 1f;
            if (Runner == null || !Runner.Attract) StartAttract();
            if (moveCamera) Rig.BlendTo(CameraRig.TitlePose, 3f);
            Stage.MoonTowards(true, moveCamera ? 3f : 0.01f);
            var save = SaveData.Current;
            title.SetBeginLabel(save.unlocked > 1 || save.lamps[0] > 0 ? $"Continue: night {UiKit.Roman(save.unlocked)}" : "Begin the watch");
            title.SetWatchUnlocked(save.WatchUnlocked);
            title.SetNotice(SaveData.Trouble);
            title.Show();
            Music.PlayTrack("music_title", 3f);
            ShaderGlobals.DawnAmount = 0f;
            Stage.Moon.color = Stage.MoonColor;
            Radio.Clear();
        }

        void ShowLogbook(State from)
        {
            logbookReturn = from;
            title.Hide();
            if (Current != State.Title && Current != State.Logbook)
            {
                // Leaving a night for the logbook: go back to the title scene behind it.
                Current = State.Title;
                if (Runner == null || !Runner.Attract) StartAttract();
                Rig.BlendTo(CameraRig.TitlePose, 3f);
                Hud.Show(false);
                Music.PlayTrack("music_title", 3f);
            }
            Current = State.Logbook;
            logbook.Refresh(MissionLibrary.All, SaveData.Current);
            logbook.Show();
        }

        void ShowSettings(State from)
        {
            settingsReturn = from;
            title.Hide();
            settings.Show();
        }

        /// <summary>The keeper's notes. From the pause menu the night waits, and the book opens on
        /// the idea tonight brings.</summary>
        void ShowNotes(State from)
        {
            notesReturn = from;
            title.Hide();
            string first = null;
            if (from == State.Paused)
                foreach (var e in KeeperNotes.For(SaveData.Current, InputMode.Pad))
                    if (e.Night == Night) { first = e.Id; break; }
            notes.Refresh(SaveData.Current, first);
            notes.Show();
        }

        void CloseNotes()
        {
            if (!notes.Visible) return;
            notes.Hide();
            if (notesReturn == State.Paused) { pause.Show(); Current = State.Paused; }
            else ShowTitle(false);
        }

        void ShowBriefing(int night)
        {
            title.Hide();
            Night = night == NightWatch.Number ? night : Mathf.Clamp(night, 1, MissionLibrary.All.Count);
            var def = DefFor(Night, true);
            float bearing = Runner != null ? Runner.Bearing : 0f;
            StartRunner(def, true);
            Runner.SetInitialBearing(bearing);
            Current = State.Briefing;
            Rig.BlendTo(CameraRig.PlayPose, 3.2f);
            Stage.MoonTowards(false, 3.2f);
            briefing.Setup(def, SaveData.Current);
            briefing.Show();
            Music.PlayTrack("music_night", 4f);
            Music.SetTension(0f);
            Hud.Show(false);
            ShaderGlobals.DawnAmount = 0f;
            Stage.Moon.color = Stage.MoonColor;
        }

        void StartRunner(MissionDef def, bool holding)
        {
            if (feedback != null) { feedback.Detach(); feedback = null; }
            if (Runner != null) Runner.Teardown();
            Radio.Clear();
            Runner = MissionRunner.Begin(def, World, AutoPlay, difficulty: SaveData.Current.Difficulty);
            Runner.Holding = holding;
            int neglect = Arg("-llNeglect", -1);
            if (neglect >= 0 && neglect < def.ships.Length) TourNeglect(def.ships[neglect].name);
            Hud.ClearBindings();
            Hud.Bind(Runner, Radio);
            feedback = new Feedback(Runner, Hud, Radio);
            outcomeTimer = -1f;
            recorded = false;
            ApplyMood(def);
        }

        void StartAttract()
        {
            if (feedback != null) { feedback.Detach(); feedback = null; }
            float bearing = Runner != null ? Runner.Bearing : 4.2f;
            if (Runner != null) Runner.Teardown();
            Runner = MissionRunner.Begin(AttractMission(), World, false, UnityEngine.Random.Range(1, 1000));
            Runner.Attract = true;
            Runner.SetInitialBearing(bearing);
            ApplyMood(Runner.Def);
        }

        static MissionDef AttractMission() => new MissionDef
        {
            id = "attract", night = 0, title = "", drainScale = 0f, haze = 1.1f,
            ships = new[]
            {
                new SpawnDef { t = 0f, type = "ferry", route = "e1_w1", name = "Evening Star" },
                new SpawnDef { t = 6f, type = "trawler", route = "harbor_n2", name = "Kittiwake" },
                new SpawnDef { t = 30f, type = "steamer", route = "w1_e1", name = "SS Calloway" },
                new SpawnDef { t = 52f, type = "trawler", route = "n1_harbor", name = "Little Auk" },
                new SpawnDef { t = 80f, type = "trawler", route = "w2_e2", name = "Saint Brannoc" },
            },
        };

        void ApplyMood(MissionDef def)
        {
            ShaderGlobals.HazeAmount = def.haze;
            bool stormy = def.storm != null && def.storm.enabled;
            Stage.Moon.intensity = stormy ? Stage.MoonIntensity * 0.45f : Stage.MoonIntensity;
            ShaderGlobals.MoonBrightness = stormy ? 0.35f : 1f;
            if (rainFx != null) Destroy(rainFx.gameObject);
            if (stormy && def.storm.rain > 0f) rainFx = FX.RainSheet(def.storm.rain);
            // Squalls (the Night Watch): the weather follows the sim's storm strength, frame by frame.
            squally = !stormy && def.squalls != null && def.squalls.Length > 0;
            if (!stormy)
            {
                // Calm again (a watch may have ended mid-squall).
                Waves.Scale = 0.35f;
                Waves.Choppiness = 1f;
                MaterialLibrary.Water.SetFloat("_WaveScale", Waves.Scale);
                MaterialLibrary.Water.SetFloat("_Choppiness", Waves.Choppiness);
                Feedback.Wind?.Set(0.25f);
                Feedback.Rain?.Set(0f);
            }
            if (squally) { rainFx = FX.RainSheet(1f); SetRain(0f); }
        }

        bool squally;

        void SetRain(float amount)
        {
            if (rainFx == null) return;
            var em = rainFx.emission;
            em.rateOverTime = 3500f * amount;
        }

        /// <summary>A squall blowing through: rain, wind, swell and moonlight follow its strength.</summary>
        void UpdateSquall()
        {
            if (!squally || Runner == null || Runner.Attract) return;
            var w = Runner.World;
            float k = w.StormStrength;
            SetRain(w.Rain);
            Stage.Moon.intensity = Stage.MoonIntensity * Mathf.Lerp(1f, 0.45f, k);
            ShaderGlobals.MoonBrightness = Mathf.Lerp(1f, 0.35f, k);
            Feedback.Wind?.Set(Mathf.Lerp(0.25f, 0.85f, k));
            Feedback.Rain?.Set(k > 0.02f ? 0.7f * w.Rain + 0.2f * k : 0f);
            Waves.Scale = Mathf.Lerp(0.35f, 0.75f, k);
            Waves.Choppiness = Mathf.Lerp(1f, 1.25f, k);
            MaterialLibrary.Water.SetFloat("_WaveScale", Waves.Scale);
            MaterialLibrary.Water.SetFloat("_Choppiness", Waves.Choppiness);
        }

        void BeginWatch()
        {
            if (Current == State.Playing) return;   // a pad's A can both submit the button and start the briefing
            briefing.Hide();
            Current = State.Playing;
            Runner.Holding = false;
            Runner.Controls.IgnorePresses();
            Hud.Show(true, 1f);
            Sfx.Play("ui_begin", 0.7f);
            previousBest = Watching ? SaveData.Current.watchBest : SaveData.Current.best[Night - 1];
            Hud.SetBest(previousBest, Watching);
        }

        void RestartNight()
        {
            fader.Dip(0.5f, () =>
            {
                var def = DefFor(Night, true);
                StartRunner(def, false);
                Rig.Snap(CameraRig.PlayPose);
                Music.PlayTrack("music_night", 1f);
                ShaderGlobals.DawnAmount = 0f;
                Stage.Moon.color = Stage.MoonColor;
                BeginWatch();
            });
        }

        void Pause()
        {
            if (Current != State.Playing) return;
            Current = State.Paused;
            Time.timeScale = 0f;
            pause.SetWatch(Watching);
            pause.Show();
            pause.SetRadioLog(Radio.Log);
            Sfx.Play("ui_page", 0.4f, 1.2f);
        }

        void Resume()
        {
            pause.Hide();
            Time.timeScale = 1f;
            Current = State.Playing;
            Runner?.Controls.IgnorePresses();
        }

        /// <summary>The keeper stands a Night Watch down: dawn now, and the watch is kept and ranked.</summary>
        void EndWatch()
        {
            if (!Watching || Runner == null || Current != State.Paused) return;
            pause.Hide();
            Time.timeScale = 1f;
            Runner.World.StandDown();
            Radio.Say("ianto", "Right you are, keeper. I'll write the watch up in the log.", 3);
            ShowResults();
        }

        void ShowResults()
        {
            Current = State.Results;
            var w = Runner.World;
            bool won = w.Outcome == MissionOutcome.Won;
            if (!recorded)
            {
                recorded = true;
                var names = new List<string>();
                foreach (var s in w.Ships) if (s.State == ShipState.Arrived) names.Add(s.Name);
                if (Watching) watchRank = SaveData.Current.RecordWatch(w.Score, w.Arrivals, w.Time, names, SpeedPercent);
                else if (won) SaveData.Current.RecordNight(Night, w.Lamps, w.Score, names);
                if (!Watching)
                {
                    failStreak = won ? 0 : failNight == Night ? failStreak + 1 : 1;
                    failNight = Night;
                }
            }
            Hud.Show(false, 1.2f);
            if (Watching) results.SetupWatch(w, previousBest, watchRank, SpeedPercent);
            else results.Setup(Runner.Def, w, previousBest, Night < MissionLibrary.All.Count, Night >= 12 && won, SpeedPercent);
            // Only a night that was kept (or a watch) tried to save.
            results.SetSaveNote(Watching || won ? SaveData.Unsaved : null);
            results.SetHelpNote(!Watching && !won ? HelpNote(failStreak, SaveData.Current) : null);
            results.Show();
            if (Watching) won = true;   // every watch ends in a wreck too many; it still ends at dawn
            Music.PlayTrack(won ? "music_dawn" : "music_title", 2.5f);
            if (won)
                Tween.Run(this, "dawn", 6f, t =>
                {
                    ShaderGlobals.DawnAmount = t * 0.35f;
                    Stage.Moon.color = Color.Lerp(Stage.MoonColor, new Color(0.95f, 0.75f, 0.6f), t * 0.35f);
                }, 0f, Tween.EaseInOut);
        }

        /// <summary>After the same night fails twice running, a word on the assists that are there
        /// and not yet in use: Difficulty when it's on Hard, otherwise Game speed. Nothing changes
        /// by itself, and nothing is said once both are as easy as they go.</summary>
        public static string HelpNote(int failsRunning, SaveData save)
        {
            if (failsRunning < 2) return null;
            if (save.difficulty == 1) return "If you'd like it gentler: Settings ▸ Difficulty ▸ Standard gives ships more nerve and charts longer.";
            if (save.gameSpeed > 0.75f) return $"If you'd like more time: Settings ▸ Game speed slows the whole night ({(save.gameSpeed > 0.9f ? "85% or 70%" : "70%")}).";
            return null;
        }

        /// <summary>The slowest game speed this night was played at, in percent.</summary>
        int SpeedPercent => Runner != null ? Mathf.RoundToInt(Runner.SlowestSpeed * 100f) : 100;

        void StartEnding()
        {
            Current = State.Ending;
            Hud.Show(false);
            foreach (var s in new UiScreen[] { title, logbook, briefing, pause, settings, notes, results, chart })
                if (s.Visible) s.Hide();
            if (ending == null) ending = gameObject.AddComponent<Ending>();
            ending.Play(this, () =>
            {
                SaveData.Current.endingSeen = true;
                SaveData.Current.Save();
                fader.Dip(1.5f, () =>
                {
                    // Skipped early, the ending's own dawn scene would linger behind the title.
                    if (ending.Skipped) StartAttract();
                    ShowTitle(false);
                    Rig.Snap(CameraRig.TitlePose);
                });
            });
        }

        /// <summary>The dawn scene of the ending: the Calloway steaming home, the lens still turning.</summary>
        public MissionRunner StartEndingRunner()
        {
            if (feedback != null) { feedback.Detach(); feedback = null; }
            float bearing = Runner != null ? Runner.Bearing : 0f;
            if (Runner != null) Runner.Teardown();
            var def = new MissionDef
            {
                id = "dawn", night = 13, title = "", drainScale = 0f, haze = 0.8f,
                ships = new[] { new SpawnDef { t = 0f, type = "steamer", route = "e2_harbor", name = "SS Calloway" } },
            };
            Runner = MissionRunner.Begin(def, World, false);
            Runner.Attract = true;
            Runner.AttractTurn = 0.12f;
            Runner.SetInitialBearing(bearing);
            ApplyMood(def);
            return Runner;
        }

        public Fader Fader => fader;

        // Closing the game (the window's close button, Alt+F4, a logout) during a Night Watch keeps
        // the watch, as End the watch does. A night of the twelve is simply left, as before.
        void OnApplicationQuit() => KeepWatchOnQuit();

        /// <summary>A watch under way (playing, paused, or just ended and waiting for dawn) is stood
        /// down and recorded before the game closes. True if one was kept.</summary>
        public bool KeepWatchOnQuit()
        {
            // A watch the bot played from the command line (-llAuto) isn't the keeper's to keep.
            if (!Watching || Runner == null || Runner.Attract || recorded || HasArg("-llAuto")) return false;
            if (Current != State.Playing && Current != State.Paused) return false;
            var w = Runner.World;
            if (w.Outcome == MissionOutcome.Running) w.StandDown();
            recorded = true;
            var names = new List<string>();
            foreach (var s in w.Ships) if (s.State == ShipState.Arrived) names.Add(s.Name);
            watchRank = SaveData.Current.RecordWatch(w.Score, w.Arrivals, w.Time, names, SpeedPercent);
            Debug.Log($"[Game] the watch was kept as the game closed: {w.Score} points, {w.Arrivals} ships, {UiKit.Clock(w.Time)}, rank {watchRank}");
            return true;
        }

        void Quit()
        {
            SaveData.Current.Save();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            ShaderGlobals.PushMood();
            InputMode.Update();
            // The pointer means nothing to a pad player: hide it until the mouse moves again.
            if (Cursor.visible == InputMode.Pad) Cursor.visible = !InputMode.Pad;
            float dt = Time.deltaTime;
            Radio.TextSpeed = SaveData.Current.textSpeed;
            if (Current == State.Playing || Current == State.Results || Current == State.Ending) Radio.Update(dt);
            feedback?.Update(dt);
            UpdateSquall();

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool back = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) || (pad != null && pad.startButton.wasPressedThisFrame);
            // B backs out of menus (but never pauses: it's too easy to hit mid-watch).
            if (pad != null && pad.buttonEast.wasPressedThisFrame && Current != State.Playing) back = true;
            if (back)
            {
                if (Current == State.Playing) Pause();
                else if (Current == State.Paused && pause.Visible && pause.Confirming) pause.Cancel();
                else if (Current == State.Paused && pause.Visible) Resume();
                else if (chart.Visible) CloseChart();
                else if (settings.Visible && settings.Back()) { }
                else if (notes.Visible) CloseNotes();
                else if (Current == State.Paused && settings.Visible) { settings.Hide(); SaveData.Current.Save(); pause.Show(); }
                else if (Current == State.Briefing) { briefing.Hide(); ShowTitle(); }
                else if (Current == State.Logbook && logbook.Visible && logbook.Confirming) logbook.Cancel();
                else if (Current == State.Logbook && logbook.Visible) { logbook.Hide(); ShowTitle(false); }
                else if (Current == State.Title && settings.Visible) { settings.Hide(); SaveData.Current.Save(); ShowTitle(false); }
            }

            if (Current == State.Playing && Runner != null)
            {
                var o = Runner.World.Outcome;
                if (o != MissionOutcome.Running && outcomeTimer < 0f)
                {
                    outcomeTimer = o == MissionOutcome.Won ? 3.5f : 3f;
                    var cues = Runner.Def.radio;
                    if (cues != null)
                        foreach (var c in cues)
                            if (c.on == (o == MissionOutcome.Won ? "end" : "fail")) Radio.Say(c.who, c.text, 3);
                }
                if (outcomeTimer >= 0f)
                {
                    outcomeTimer -= Unscaled.Delta;
                    if ((outcomeTimer <= 0f && !Radio.Busy) || outcomeTimer < -6f) ShowResults();
                }
            }

            if (Current == State.Title && Runner != null && Runner.Attract && Runner.World.ShipsDone) StartAttract();
        }

        // ---------------------------------------------------------------- arguments

        static string[] args;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { args = null; Instance = null; }

        static string[] Args
        {
            get
            {
                if (args != null) return args;
                var list = new List<string>(Environment.GetCommandLineArgs());
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

        // ---------------------------------------------------------------- automation hooks

        public void TourShowLogbook() => ShowLogbook(State.Title);
        public void TourShowSettings() => ShowSettings(State.Title);
        public void TourHideAll() { logbook.Hide(0f); settings.Hide(0f); notes.Hide(0f); title.Hide(0f); if (results.Visible) results.Hide(0f); if (chart.Visible) chart.Hide(0f); }
        public void TourShowNotes() => ShowNotes(State.Title);
        public NotesScreen TourNotes => notes;
        public void TourBriefing(int night) => ShowBriefing(night);
        public void TourWatch() => ShowBriefing(NightWatch.Number);
        public void TourDip(float time, Action middle) => fader.Dip(time, middle);
        public void TourBegin() => BeginWatch();
        /// <summary>The AutoKeeper leaves this ship to its fate (stages a wreck for captures).</summary>
        public void TourNeglect(string ship) { if (Runner != null) Runner.Bot.Ignore = s => s.Name == ship; }
        public void TourPause() => Pause();
        public void TourResume() => Resume();
        public void TourEnding() => StartEnding();
        public void TourTitle() => ShowTitle();
        public UiScreen TourScreen(string name) => name switch
        {
            "title" => title, "logbook" => logbook, "settings" => settings, "briefing" => briefing,
            "pause" => pause, "results" => results, "notes" => notes, "chart" => chart, _ => null,
        };
        public ChartScreen TourChart => chart;
        public ResultsScreen TourResults => results;
        public void TourShowChart() => ShowChart();
        public bool ShowingResults => Current == State.Results;
        public bool TourPaused => Current == State.Paused;
        public bool TourShowingTitle => Current == State.Title;
        public Ending TourEndingScene => ending;
        public (int shown, string newest) TourPauseLog => (pause.LogShown, pause.LogNewest);
        public string TourPauseItem(int i) => pause.ItemLabel(i);
        public string TourPauseControls => pause.ControlsShown;
        public void TourPauseChoose(int i) => pause.Choose(i);
        public bool TourPauseConfirming => pause.Confirming;
        public string TourPauseConfirmHeading => pause.ConfirmHeading;
        public string TourPauseConfirmLabel(int i) => pause.ConfirmLabel(i);
        public void TourPauseConfirm(int i) => pause.ConfirmChoose(i);
    }
}
