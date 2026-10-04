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
        ResultsScreen results;
        Fader fader;
        Feedback feedback;
        State settingsReturn;
        State logbookReturn;
        float outcomeTimer = -1f;
        bool recorded;
        int previousBest;
        Ending ending;
        ParticleSystem rainFx;
        MissionDef watchDef;

        bool Watching => Night == NightWatch.Number;

        /// <summary>The night's mission; the Night Watch is generated afresh for each watch.</summary>
        MissionDef DefFor(int night, bool fresh)
        {
            if (night != NightWatch.Number) return MissionLibrary.All[Mathf.Clamp(night, 1, MissionLibrary.All.Count) - 1];
            if (fresh || watchDef == null) watchDef = NightWatch.Generate(World.Map, Arg("-llSeed", UnityEngine.Random.Range(1, 100000)));
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
            int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = Arg("-llFps", Mathf.Max(60, hz));
            Time.timeScale = 1f;
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
        }

        void BuildUi()
        {
            UiKit.EnsureEventSystem();
            Hud = Hud.Create();
            menus = UiKit.MakeCanvas("Menus", 20);
            title = TitleScreen.Create(menus.transform);
            logbook = LogbookScreen.Create(menus.transform);
            briefing = BriefingScreen.Create(menus.transform);
            pause = PauseScreen.Create(menus.transform);
            settings = SettingsScreen.Create(menus.transform);
            results = ResultsScreen.Create(menus.transform);
            fader = Fader.Create(menus.transform);

            title.OnBegin = () => ShowBriefing(SaveData.Current.unlocked);
            title.OnWatch = () => ShowBriefing(NightWatch.Number);
            title.OnLogbook = () => ShowLogbook(State.Title);
            title.OnSettings = () => ShowSettings(State.Title);
            title.OnQuit = Quit;
            logbook.OnPick = n => { logbook.Hide(); ShowBriefing(n); };
            logbook.OnBack = () => { logbook.Hide(); ShowTitle(false); };
            briefing.OnStart = BeginWatch;
            pause.OnResume = Resume;
            pause.OnRestart = () => { Time.timeScale = 1f; pause.Hide(); RestartNight(); };
            pause.OnSettings = () => { pause.Hide(); ShowSettings(State.Paused); };
            pause.OnLogbook = () => { pause.Hide(); Time.timeScale = 1f; ShowLogbook(State.Title); };
            pause.OnTitle = () => { Time.timeScale = 1f; pause.Hide(); ShowTitle(); };
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
            briefing.Setup(def, Watching ? SaveData.Current : null);
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
            Runner = MissionRunner.Begin(def, World, AutoPlay);
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
        }

        void BeginWatch()
        {
            if (Current == State.Playing) return;   // a pad's A can both submit the button and start the briefing
            briefing.Hide();
            Current = State.Playing;
            Runner.Holding = false;
            Hud.Show(true, 1f);
            Sfx.Play("ui_begin", 0.7f);
            previousBest = Watching ? SaveData.Current.watchBest : SaveData.Current.best[Night - 1];
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
            pause.Show();
            Sfx.Play("ui_page", 0.4f, 1.2f);
        }

        void Resume()
        {
            pause.Hide();
            Time.timeScale = 1f;
            Current = State.Playing;
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
                if (Watching) SaveData.Current.RecordWatch(w.Score, w.Arrivals, w.Time, names);
                else if (won) SaveData.Current.RecordNight(Night, w.Lamps, w.Score, names);
            }
            Hud.Show(false, 1.2f);
            if (Watching) results.SetupWatch(w, previousBest);
            else results.Setup(Runner.Def, w, previousBest, Night < MissionLibrary.All.Count, Night >= 12 && won);
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

        void StartEnding()
        {
            Current = State.Ending;
            Hud.Show(false);
            foreach (var s in new UiScreen[] { title, logbook, briefing, pause, settings, results })
                if (s.Visible) s.Hide();
            if (ending == null) ending = gameObject.AddComponent<Ending>();
            ending.Play(this, () =>
            {
                SaveData.Current.endingSeen = true;
                SaveData.Current.Save();
                fader.Dip(1.5f, () => { ShowTitle(false); Rig.Snap(CameraRig.TitlePose); });
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
            float dt = Time.deltaTime;
            Radio.TextSpeed = SaveData.Current.textSpeed;
            if (Current == State.Playing || Current == State.Results || Current == State.Ending) Radio.Update(dt);
            feedback?.Update(dt);

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool back = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) || (pad != null && pad.startButton.wasPressedThisFrame);
            // B backs out of menus (but never pauses: it's too easy to hit mid-watch).
            if (pad != null && pad.buttonEast.wasPressedThisFrame && Current != State.Playing) back = true;
            if (back)
            {
                if (Current == State.Playing) Pause();
                else if (Current == State.Paused && pause.Visible) Resume();
                else if (Current == State.Paused && settings.Visible) { settings.Hide(); SaveData.Current.Save(); pause.Show(); }
                else if (Current == State.Briefing) { briefing.Hide(); ShowTitle(); }
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
        public void TourHideAll() { logbook.Hide(0f); settings.Hide(0f); title.Hide(0f); if (results.Visible) results.Hide(0f); }
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
        public bool ShowingResults => Current == State.Results;
        public bool TourPaused => Current == State.Paused;
    }
}
