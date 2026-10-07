using System;
using System.Collections.Generic;
using LastLight.Audio;
using LastLight.Core;
using LastLight.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.UI
{
    // ==================================================================== briefing

    /// <summary>The night card: number, title, date, the harbourmaster's word and what is new tonight.</summary>
    public sealed class BriefingScreen : UiScreen
    {
        public Action OnStart;
        Text number, title, date, record, speech, newTitle, newText, prompt, stakes;

        /// <summary>The "new tonight" card's words as shown; for tours.</summary>
        public string NewThingShown => newText.text;
        Image newIcon;
        RectTransform card, newCard;
        string full = "";
        string newThing;
        float typed;
        bool ready;

        public static BriefingScreen Create(Transform canvas)
        {
            var s = canvas.gameObject.AddComponent<BriefingScreen>();
            s.Init(canvas, "Briefing");
            s.Build();
            return s;
        }

        void Build()
        {
            var shade = UiKit.Image("Shade", Root, SpriteFactory.FadeH, new Color(0, 0.01f, 0.02f, 0.85f));
            shade.rectTransform.Stretch(Vector2.zero, new Vector2(0.75f, 1), Vector2.zero, Vector2.zero);
            card = UiKit.Rect("Card", Root).Pin(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, 30), new Vector2(900, 760));
            Frames.Add(card);
            number = Label(card, "", UiKit.BodyBold, 24, UiKit.Brass, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, -20), new Vector2(800, 34));
            number.rectTransform.pivot = new Vector2(0, 0.5f);
            title = Label(card, "", UiKit.Title, 104, UiKit.Paper, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, -95), new Vector2(900, 120));
            title.rectTransform.pivot = new Vector2(0, 0.5f);
            title.Shadowed(0.85f, 3f);
            date = Label(card, "", UiKit.Italic, 28, UiKit.Muted, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, -165), new Vector2(800, 40));
            date.rectTransform.pivot = new Vector2(0, 0.5f);
            // A night already kept: its lamps, its best, and what's still to earn (beside the date).
            record = Label(card, "", UiKit.BodyMedium, 23, UiKit.Brass, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, -166), new Vector2(1100, 40));
            record.rectTransform.pivot = new Vector2(0, 0.5f);
            record.horizontalOverflow = HorizontalWrapMode.Overflow;
            record.Shadowed();
            var bar = UiKit.Image("Bar", card, SpriteFactory.Bar, new Color(UiKit.Brass.r, UiKit.Brass.g, UiKit.Brass.b, 0.8f));
            bar.rectTransform.Pin(new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(0, -200), new Vector2(620, 4));

            var med = UiKit.Image("Medallion", card, SpriteFactory.Medallion, Color.white);
            med.rectTransform.Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -232), new Vector2(92, 92));
            var ini = UiKit.Text("Initials", med.transform, "IR", UiKit.Heading, 34, UiKit.Brass, TextAnchor.MiddleCenter);
            ini.rectTransform.Fill();
            Label(card, "Ianto Rees, harbourmaster", UiKit.BodyBold, 22, UiKit.Brass, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(116, -246), new Vector2(700, 30)).rectTransform.pivot = new Vector2(0, 0.5f);
            speech = UiKit.Text("Speech", card, "", UiKit.Radio, 28, UiKit.Paper, TextAnchor.UpperLeft);
            speech.rectTransform.Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(116, -270), new Vector2(740, 200));
            speech.lineSpacing = 1.15f;
            speech.Shadowed(0.7f, 2f);

            newCard = UiKit.Rect("New", card).Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -490), new Vector2(700, 124));
            var nbg = UiKit.Image("Bg", newCard, SpriteFactory.Rounded, new Color(0.02f, 0.03f, 0.045f, 0.72f), true);
            nbg.rectTransform.Fill();
            var edge = UiKit.Image("Edge", newCard, null, new Color(UiKit.Brass.r, UiKit.Brass.g, UiKit.Brass.b, 0.85f));
            edge.rectTransform.Stretch(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 14), new Vector2(4, -14));
            newIcon = UiKit.Image("Icon", newCard, SpriteFactory.Lamp, UiKit.BrassBright);
            newIcon.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(62, 0), new Vector2(66, 66));
            newTitle = Label(newCard, "", UiKit.BodyBold, 22, UiKit.Brass, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(124, -30), new Vector2(560, 30));
            newTitle.rectTransform.pivot = new Vector2(0, 0.5f);
            newText = Label(newCard, "", UiKit.BodyMedium, 27, UiKit.Paper, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(124, -50), new Vector2(556, 66));
            newText.rectTransform.pivot = new Vector2(0, 1);
            newText.lineSpacing = 0.95f;

            // The stakes, just above the button: how many wrecks the Board will stand tonight.
            stakes = Label(card, "", UiKit.Italic, 25, UiKit.Paper, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(24, 124), new Vector2(860, 32));
            stakes.rectTransform.pivot = new Vector2(0, 0.5f);
            stakes.Shadowed();

            var start = UiButton.Create(card, "Begin the watch", UiKit.Heading, 46, () => { if (ready) OnStart?.Invoke(); });
            ((RectTransform)start.transform).Pin(new Vector2(0, 0), new Vector2(0, 0), new Vector2(22, 40), new Vector2(520, 64));
            FirstSelected = start;
            prompt = Label(card, PromptText(), UiKit.Italic, 24, UiKit.Muted, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(330, 72), new Vector2(560, 30));
            prompt.rectTransform.pivot = new Vector2(0, 0.5f);
            InputMode.Changed += () =>
            {
                prompt.text = PromptText();
                if (newThing != null && NewThings.TryGetValue(newThing, out var n)) { newText.text = NewText(newThing, n.text); SetNewIcon(n.icon); }
            };
        }

        static string PromptText() => InputMode.Pick("click, or press Space     ·     Esc to go back", "press A     ·     B to go back");

        /// <summary>The line under the title (the date, or a watch's records); for tours.</summary>
        public string DateLine => date.text;

        // The new-thing cards that name a control, in gamepad words.
        static readonly Dictionary<string, string> PadNewThings = new Dictionary<string, string>
        {
            ["aim"] = "Point the right stick to turn the light.",
            ["fog"] = "Fog swallows the light. Hold RT to focus, A for the horn.",
        };

        static string NewText(string id, string keys)
        {
            string horn = SaveData.Current.keys.First(KeeperAction.Horn);
            if (horn == "") horn = "right-click";
            if (id == "fog" && SaveData.Current.focusToggle)
                return InputMode.Pick($"Fog swallows the light. Click to focus, {horn} for the horn.", "Fog swallows the light. Press RT to focus, A for the horn.");
            return InputMode.Pad && PadNewThings.TryGetValue(id, out var pad) ? pad : keys.Replace("{horn}", horn);
        }

        static readonly Dictionary<string, (string title, string text, string icon)> NewThings = new Dictionary<string, (string, string, string)>
        {
            ["aim"] = ("TONIGHT", "Move the mouse to turn the light.", "mouse"),
            ["chart"] = ("NEW: HIDDEN REEFS", "Sweep the light ahead of a ship to chart the rocks.", "reef"),
            ["buoy"] = ("NEW: BUOYS", "Light a buoy and it guides ships for a while.", "buoy"),
            ["shoal"] = ("NEW: STEAMERS AND SANDBANKS", "Deep hulls run aground on shoals. Chart the sands for them.", "steamer"),
            ["fog"] = ("NEW: SEA FRET", "Fog swallows the light. Hold to focus, {horn} for the horn.", "lmb"),
            ["ferry"] = ("NEW: THE FERRY", "The Evening Star carries passengers. Worth the most, lost the hardest.", "ferry"),
            ["damaged"] = ("NEW: DAMAGED SHIPS", "No lamps. Find them by their flares, then light them home.", "flare"),
            ["storm"] = ("NEW: STORM", "The current pushes ships ashore. Lightning shows the rocks.", "storm"),
            ["wrecker"] = ("NEW: FALSE LIGHTS", "Wreckers lure ships with lanterns. Hold your beam on one to douse it.", "lantern"),
            ["mimic"] = ("NEW: THE MIMIC", "A false light that turns like yours. Stay on your ships.", "twin"),
            ["finale"] = ("THE LAST NIGHT", "Everyone is out. Bring them all home.", "lamp"),
            ["watch"] = ("ENDLESS", "Every hazard out, ships without end. The third wreck ends the watch.", "lamp"),
        };

        /// <summary>Fill the card for a night; `watch` carries the Night Watch records.</summary>
        public void Setup(MissionDef def, SaveData save = null)
        {
            var watch = def.endless ? save : null;
            number.text = UiKit.Spaced(def.endless ? "AFTER THE SEASON" : "NIGHT " + UiKit.Roman(def.night) + " OF XII");
            title.text = def.title;
            date.text = def.date;
            if (def.endless && watch != null && watch.watchShips > 0)
            {
                // The best watches so far, best first.
                var parts = new List<string>();
                int longest = 0;
                foreach (var r in watch.watches) longest = Mathf.Max(longest, r.seconds);
                for (int i = 0; i < watch.watches.Count && i < 3; i++) parts.Add(watch.watches[i].score.ToString("N0") + Marks(watch.watches[i]));
                date.text = $"Best watches  {string.Join("  ·  ", parts)}     longest {UiKit.Clock(longest)}";
            }
            stakes.text = def.endless ? "" : StakesText(def.allowedWrecks);
            record.text = def.endless || save == null || def.night < 1 || def.night > 12 ? "" : RecordText(save.lamps[def.night - 1], save.best[def.night - 1]);
            // Beside the date, which is set in italics at 28.
            record.rectTransform.anchoredPosition = new Vector2(date.preferredWidth + 34f, -166);
            full = def.briefing ?? "";
            typed = 0f;
            speech.text = "";
            newThing = def.newThing;
            if (!string.IsNullOrEmpty(def.newThing) && NewThings.TryGetValue(def.newThing, out var n))
            {
                newCard.gameObject.SetActive(true);
                newTitle.text = UiKit.Spaced(n.title);
                newText.text = NewText(def.newThing, n.text);
                SetNewIcon(n.icon);
            }
            else newCard.gameObject.SetActive(false);
        }

        /// <summary>What a night already kept has earned and what's left: "·  Two lamps, best 880.
        /// Still to earn: a steady hand." Empty for a night not yet kept.</summary>
        public static string RecordText(int lamps, int best)
        {
            if (lamps <= 0) return "";
            string kept = lamps >= 3 ? "All three lamps" : lamps == 2 ? "Two lamps" : "One lamp";
            string left = lamps >= 3 ? "" : lamps == 2 ? "  Still to earn: a steady hand." : "  Still to earn: no ship lost, and a steady hand.";
            return $"·   {kept}, best {best:N0}.{left}";
        }

        /// <summary>The record line beside the date, for tours.</summary>
        public string RecordLine => record.text;

        /// <summary>" (Hard)", " (70%)" or " (Hard, 70%)" after a watch kept that way.</summary>
        public static string Marks(WatchRecord r)
        {
            var marks = new List<string>();
            if (r.hard) marks.Add("Hard");
            if (r.speed > 0 && r.speed < 100) marks.Add(r.speed + "%");
            return marks.Count == 0 ? "" : " (" + string.Join(", ", marks) + ")";
        }

        /// <summary>"The Board allows one wreck tonight. A second ends the night."</summary>
        public static string StakesText(int allowed)
        {
            string[] counts = { "no", "one", "two", "three" }, ordinals = { "first", "second", "third", "fourth" };
            if (allowed < 0 || allowed >= counts.Length) return "";
            return allowed == 0 ? "The Board allows no wrecks tonight. The first ends the night."
                : $"The Board allows {counts[allowed]} wreck{(allowed == 1 ? "" : "s")} tonight. A {ordinals[allowed]} ends the night.";
        }

        void SetNewIcon(string icon)
        {
            // Mouse pictures mean nothing to a pad player: the light's own lamp stands in.
            if (InputMode.Pad && (icon == "mouse" || icon == "lmb")) icon = "lamp";
            newIcon.sprite = icon switch
            {
                "mouse" => SpriteFactory.Mouse(""),
                "lmb" => SpriteFactory.Mouse("left"),
                "steamer" or "ferry" => SpriteFactory.Ship(icon),
                "lamp" => SpriteFactory.Lamp,
                _ => SpriteFactory.Icon(icon),
            };
            newIcon.rectTransform.sizeDelta = icon switch
            {
                "mouse" or "lmb" => new Vector2(46, 62),
                "steamer" or "ferry" => new Vector2(96, 38),
                _ => new Vector2(70, 70),
            };
        }

        public override void Show()
        {
            base.Show();
            ready = false;
            card.anchoredPosition = new Vector2(110, 30);
            Tween.Move(card, new Vector2(150, 30), 0.8f);
            Tween.Run(this, "ready", 0.7f, _ => { }, 0f, null, () => ready = true);
            Sfx.Play("ui_page", 0.5f, 0.9f);
        }

        void Update()
        {
            if (!Visible) return;
            if (typed < full.Length)
            {
                int before = (int)typed;
                typed = Mathf.Min(full.Length, typed + Unscaled.Delta * 48f * SaveData.Current.textSpeed);
                int now = (int)typed;
                speech.text = full.Substring(0, now) + "<color=#00000000>" + full.Substring(now) + "</color>";
                if (now / 3 != before / 3) Sfx.Play("radio_tick", 0.1f, UnityEngine.Random.Range(0.9f, 1.1f), 0, Bus.Ui, 0.02f);
            }
            prompt.color = new Color(UiKit.Muted.r, UiKit.Muted.g, UiKit.Muted.b, 0.55f + 0.35f * Mathf.Sin(Unscaled.Time * 2.5f));
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (ready && ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame)))
            {
                if (typed < full.Length) typed = full.Length;
                else OnStart?.Invoke();
            }
        }
    }

    // ==================================================================== settings

    public sealed class SettingsScreen : UiScreen
    {
        public Action OnBack, OnFocusMode;
        RectTransform panel;

        /// <summary>"Native" first, then the desktop's modes from 1280x720 up, smallest first.</summary>
        static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int> { Vector2Int.zero };
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x >= 1280 && v.y >= 720 && !list.Contains(v)) list.Add(v);
            }
            list.Sort((a, b) => a == Vector2Int.zero ? -1 : b == Vector2Int.zero ? 1 : (a.x * a.y).CompareTo(b.x * b.y));
            return list;
        }

        public static SettingsScreen Create(Transform canvas)
        {
            var s = canvas.gameObject.AddComponent<SettingsScreen>();
            s.Init(canvas, "Settings");
            s.Build();
            return s;
        }

        void Build()
        {
            var dim = UiKit.Image("Dim", Root, null, new Color(0, 0.01f, 0.02f, 0.72f));
            dim.rectTransform.Fill();
            panel = UiKit.Rect("Panel", Root).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1660, 1000));
            Frames.Add(panel);
            var bg = UiKit.Image("Bg", panel, SpriteFactory.Rounded, new Color(0.03f, 0.045f, 0.06f, 0.86f), true);
            bg.rectTransform.Fill();
            Label(panel, "Settings", UiKit.Title, 80, UiKit.Paper, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(800, 100)).Shadowed();
            var rule = UiKit.Image("Rule", panel, SpriteFactory.Bar, new Color(UiKit.Brass.r, UiKit.Brass.g, UiKit.Brass.b, 0.6f));
            rule.rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -146), new Vector2(620, 3));
            var save = SaveData.Current;
            // Two columns: sound and text on the left, play and display on the right. Pads move
            // down the left column, then down the right, then to Done (left and right change values).
            var order = new List<Selectable>();
            int row = 0;
            float column = -410f;
            void Row(string label, Component control)
            {
                // Label and control share a centre line.
                float y = -196 - row * 60;
                var l = Label(panel, label, UiKit.BodyMedium, 28, UiKit.Paper, TextAnchor.MiddleLeft, new Vector2(0.5f, 1), new Vector2(column - 170, y), new Vector2(380, 50));
                l.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                l.Shadowed();
                ((RectTransform)control.transform).Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(column + 210, y), new Vector2(380, 46));
                order.Add((Selectable)control);
                row++;
            }
            Row("Master volume", UiSlider.Create(panel, save.master, v => { save.master = v; save.Apply(); }));
            Row("Music", UiSlider.Create(panel, save.music, v => { save.music = v; save.Apply(); }));
            Row("Sound effects", UiSlider.Create(panel, save.sfx, v => { save.sfx = v; save.Apply(); }));
            Row("Radio voices", UiSlider.Create(panel, save.radio, v => { save.radio = v; save.Apply(); Sfx.Play("voice_ianto", 0.4f, 0.95f, 0, Bus.Radio, 0.5f); }));
            Row("Sea and wind", UiSlider.Create(panel, save.ambience, v => { save.ambience = v; save.Apply(); }));
            Row("Sound in background", UiStepper.Create(panel, new[] { "On", "Off" }, save.muteInBackground ? 1 : 0, i => save.muteInBackground = i == 1));
            Row("Text speed", UiStepper.Create(panel, new[] { "Slow", "Normal", "Fast" }, save.textSpeed < 0.9f ? 0 : save.textSpeed > 1.1f ? 2 : 1, i => { save.textSpeed = i == 0 ? 0.7f : i == 2 ? 1.5f : 1f; }));
            float[] hudScales = { 1f, 1.15f, 1.3f };
            int hudIndex = System.Array.FindIndex(hudScales, v => Mathf.Abs(v - save.hudScale) < 0.01f);
            Row("HUD text size", UiStepper.Create(panel, new[] { "100%", "115%", "130%" }, Mathf.Max(0, hudIndex), i => { save.hudScale = hudScales[i]; save.Apply(); }));
            Row("Hints", UiStepper.Create(panel, new[] { "Off", "On" }, save.hints ? 1 : 0, i => save.hints = i == 1));
            UiButton replay = null;
            replay = UiButton.Create(panel, "Show hints again", UiKit.BodyMedium, 26, () =>
            {
                save.hintsSeen.Clear();
                save.Save();
                replay.Label.text = "Hints will show again";
            }, TextAnchor.MiddleCenter);
            Row("Seen hints", replay);
            keysButton = UiButton.Create(panel, "Change keys", UiKit.BodyMedium, 26, ShowKeys, TextAnchor.MiddleCenter);
            Row("Keyboard keys", keysButton);
            int[] caps = { 0, 60, 30 };
            Row("Frame rate", UiStepper.Create(panel, new[] { "Display", "60", "30" }, Mathf.Max(0, System.Array.IndexOf(caps, save.frameCap)), i => { save.frameCap = caps[i]; save.Apply(display: false); }));

            row = 0;
            column = 410f;
            Row("Difficulty", UiStepper.Create(panel, new[] { "Standard", "Hard" }, save.difficulty, i => save.difficulty = i));
            float[] speeds = { 1f, 0.85f, 0.7f };
            int speedIndex = System.Array.FindIndex(speeds, v => Mathf.Abs(v - save.gameSpeed) < 0.01f);
            Row("Game speed", UiStepper.Create(panel, new[] { "100%", "85%", "70%" }, Mathf.Max(0, speedIndex), i => save.gameSpeed = speeds[i]));
            Row("Screen shake", UiStepper.Create(panel, new[] { "Off", "On" }, save.shake ? 1 : 0, i => save.shake = i == 1));
            Row("Focus", UiStepper.Create(panel, new[] { "Hold", "Toggle" }, save.focusToggle ? 1 : 0, i => { save.focusToggle = i == 1; OnFocusMode?.Invoke(); }));
            Row("Lens turn speed (keys)", UiSlider.Create(panel, Mathf.InverseLerp(0.5f, 1.25f, save.turnSpeed), v => save.turnSpeed = Mathf.Lerp(0.5f, 1.25f, v)));
            Row("Display", UiStepper.Create(panel, new[] { "Windowed", "Fullscreen" }, save.fullscreen ? 1 : 0, i => { save.fullscreen = i == 1; save.Apply(); }));
            var sizes = Resolutions();
            int current = sizes.FindIndex(r => r.x == save.resWidth && r.y == save.resHeight);
            var names = sizes.ConvertAll(r => r.x == 0 ? "Native" : $"{r.x} × {r.y}").ToArray();
            Row("Resolution", UiStepper.Create(panel, names, Mathf.Max(0, current), i => { save.resWidth = sizes[i].x; save.resHeight = sizes[i].y; save.Apply(); }));
            Row("Brightness", UiStepper.Create(panel, new[] { "−2", "−1", "Standard", "+1", "+2" }, Mathf.Clamp(save.brightness, -2, 2) + 2, i => { save.brightness = i - 2; save.Apply(display: false); }));
            Row("Fog and haze quality", UiStepper.Create(panel, new[] { "Low", "Medium", "High" }, save.quality, i => { save.quality = i; save.Apply(); }));
            float[] scales = { 1f, 0.85f, 0.7f, 0.5f };
            int scaleIndex = System.Array.FindIndex(scales, v => Mathf.Abs(v - save.renderScale) < 0.01f);
            Row("Render scale", UiStepper.Create(panel, new[] { "100%", "85%", "70%", "50%" }, Mathf.Max(0, scaleIndex), i => { save.renderScale = scales[i]; save.Apply(); }));
            Row("Reduce flashing", UiStepper.Create(panel, new[] { "Off", "On" }, save.reduceFlashing ? 1 : 0, i => { save.reduceFlashing = i == 1; save.Apply(); }));
            var back = UiButton.Create(panel, "Done", UiKit.Heading, 44, () => { SaveData.Current.Save(); OnBack?.Invoke(); }, TextAnchor.MiddleCenter);
            ((RectTransform)back.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(300, 60));
            order.Add(back);
            for (int i = 0; i < order.Count; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = order[(i + order.Count - 1) % order.Count],
                    selectOnDown = order[(i + 1) % order.Count],
                };
                order[i].navigation = nav;
            }
            FirstSelected = settingsFirst = back;
            BuildKeys();
        }

        // ---------------------------------------------------------------- keys

        // The keyboard's keys for turning, focus and the horn: three slots each. Choose a slot,
        // press a key. The panel takes the settings panel's place while it's open.
        RectTransform keysPanel;
        UiButton keysButton, keysDone, settingsFirst;
        readonly UiButton[,] slots = new UiButton[4, KeyBindings.Slots];
        readonly Image[,] cells = new Image[4, KeyBindings.Slots];
        Text keysNote;
        KeeperAction listenAction;
        int listenSlot = -1, listenFrame, restoreNavFrame = -1;

        /// <summary>The keys panel is open, and whether it's waiting for a key; for tours.</summary>
        public bool KeysOpen => keysPanel != null && keysPanel.gameObject.activeSelf;
        public bool Listening => listenSlot >= 0;

        const string KeysHelp = "Choose a slot, then press a key. Backspace empties it, Esc cancels.\nEsc and P always pause. The mouse and gamepad keep their buttons.";

        void BuildKeys()
        {
            keysPanel = UiKit.Rect("Keys", Root).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1660, 940));
            Frames.Add(keysPanel);
            var bg = UiKit.Image("Bg", keysPanel, SpriteFactory.Rounded, new Color(0.03f, 0.045f, 0.06f, 0.86f), true);
            bg.rectTransform.Fill();
            Label(keysPanel, "Keys", UiKit.Title, 80, UiKit.Paper, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(800, 100)).Shadowed();
            var rule = UiKit.Image("Rule", keysPanel, SpriteFactory.Bar, new Color(UiKit.Brass.r, UiKit.Brass.g, UiKit.Brass.b, 0.6f));
            rule.rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -146), new Vector2(620, 3));
            for (int r = 0; r < 4; r++)
            {
                var action = KeyBindings.Actions[r];
                float y = -236 - r * 92;
                var l = Label(keysPanel, KeyBindings.ActionName(action), UiKit.BodyMedium, 30, UiKit.Paper, TextAnchor.MiddleLeft, new Vector2(0.5f, 1), new Vector2(-560, y), new Vector2(380, 56));
                l.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                l.Shadowed();
                for (int c = 0; c < KeyBindings.Slots; c++)
                {
                    int slot = c;
                    var b = UiButton.Create(keysPanel, "", UiKit.BodyBold, 28, () => Listen(action, slot), TextAnchor.MiddleCenter);
                    b.name = $"Slot {action} {slot + 1}";
                    ((RectTransform)b.transform).Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(-110 + c * 320, y), new Vector2(290, 64));
                    var cell = cells[r, c] = UiKit.Image("Cell", b.transform, SpriteFactory.Rounded, CellIdle, true);
                    cell.rectTransform.Fill();
                    cell.raycastTarget = false;
                    cell.transform.SetAsFirstSibling();
                    slots[r, c] = b;
                }
            }
            keysNote = Label(keysPanel, "", UiKit.Italic, 27, UiKit.BrassBright, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -620), new Vector2(1400, 40));
            var help = Label(keysPanel, KeysHelp, UiKit.BodyMedium, 24, UiKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -700), new Vector2(1400, 80));
            help.lineSpacing = 1.1f;
            var reset = UiButton.Create(keysPanel, "Reset keys", UiKit.Heading, 40, () =>
            {
                StopListening();
                SaveData.Current.keys.Reset();
                RefreshKeys("The keys are back as they were.");
            }, TextAnchor.MiddleCenter);
            ((RectTransform)reset.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-240, 36), new Vector2(340, 60));
            keysDone = UiButton.Create(keysPanel, "Done", UiKit.Heading, 44, () => HideKeys(), TextAnchor.MiddleCenter);
            ((RectTransform)keysDone.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(240, 36), new Vector2(300, 60));

            // A grid for the pad and arrows: across the slots, down the actions, then Reset and Done.
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < KeyBindings.Slots; c++)
                    slots[r, c].navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnLeft = slots[r, (c + KeyBindings.Slots - 1) % KeyBindings.Slots],
                        selectOnRight = slots[r, (c + 1) % KeyBindings.Slots],
                        selectOnUp = r > 0 ? slots[r - 1, c] : keysDone,
                        selectOnDown = r < 3 ? slots[r + 1, c] : c == 0 ? reset : keysDone,
                    };
            reset.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = keysDone, selectOnLeft = keysDone, selectOnUp = slots[3, 0], selectOnDown = slots[0, 0] };
            keysDone.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = reset, selectOnRight = reset, selectOnUp = slots[3, KeyBindings.Slots - 1], selectOnDown = slots[0, 0] };
            keysPanel.gameObject.SetActive(false);
        }

        void RefreshKeys(string note = null)
        {
            var keys = SaveData.Current.keys;
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < KeyBindings.Slots; c++)
                {
                    bool waiting = Listening && listenAction == KeyBindings.Actions[r] && listenSlot == c;
                    var k = keys.Get(KeyBindings.Actions[r], c);
                    slots[r, c].Label.text = waiting ? "<i>press a key…</i>" : k == UnityEngine.InputSystem.Key.None ? "<color=#8A8F96>—</color>" : KeyBindings.KeyName(k);
                }
            if (note != null) keysNote.text = note;
            OnFocusMode?.Invoke();   // the title's control strip names the horn key
        }

        void ShowKeys()
        {
            panel.gameObject.SetActive(false);
            keysPanel.gameObject.SetActive(true);
            FirstSelected = slots[0, 0];
            RefreshKeys("");
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(slots[0, 0].gameObject);
            Sfx.Play("ui_page", 0.4f, 1.1f);
        }

        void HideKeys(bool select = true)
        {
            if (keysPanel == null || !KeysOpen) return;
            StopListening();
            keysPanel.gameObject.SetActive(false);
            panel.gameObject.SetActive(true);
            FirstSelected = settingsFirst;
            if (select && UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(keysButton.gameObject);
        }

        /// <summary>Wait for a key for this slot. Menu navigation is off meanwhile, so an arrow or
        /// Enter can be bound rather than moving the selection.</summary>
        void Listen(KeeperAction action, int slot)
        {
            listenAction = action;
            listenSlot = slot;
            listenFrame = Time.frameCount;
            restoreNavFrame = -1;
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null) es.sendNavigationEvents = false;
            RefreshKeys($"{KeyBindings.ActionName(action)}, key {slot + 1}: press a key.");
        }

        void StopListening(string note = null)
        {
            if (!Listening) return;
            listenSlot = -1;
            restoreNavFrame = Time.frameCount + 1;   // not this frame: the key just pressed mustn't also navigate
            RefreshKeys(note);
        }

        /// <summary>Esc, P or B: stop waiting for a key, or close the keys panel. False when there's
        /// nothing here to back out of (Settings itself then closes).</summary>
        public bool Back()
        {
            if (Listening) { StopListening("Nothing changed."); return true; }
            if (KeysOpen) { HideKeys(); return true; }
            return false;
        }

        static readonly Color CellIdle = new Color(1f, 1f, 1f, 0.045f);
        static readonly Color CellHot = new Color(UiKit.Brass.r, UiKit.Brass.g, UiKit.Brass.b, 0.32f);

        void Update()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (KeysOpen)
            {
                // The selected slot (and the one waiting for a key) lights up, even when empty.
                var sel = es != null ? es.currentSelectedGameObject : null;
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < KeyBindings.Slots; c++)
                    {
                        bool hot = slots[r, c].gameObject == sel || (Listening && listenAction == KeyBindings.Actions[r] && listenSlot == c);
                        cells[r, c].color = Color.Lerp(cells[r, c].color, hot ? CellHot : CellIdle, Unscaled.Delta * 12f);
                    }
            }
            if (restoreNavFrame >= 0 && Time.frameCount >= restoreNavFrame)
            {
                restoreNavFrame = -1;
                if (es != null) es.sendNavigationEvents = true;
            }
            if (!Listening || Time.frameCount <= listenFrame) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            foreach (var c in kb.allKeys)
            {
                if (c == null || !c.wasPressedThisFrame) continue;
                var key = c.keyCode;
                if (KeyBindings.Reserved(key)) return;   // Esc and P back out (see Back)
                var keys = SaveData.Current.keys;
                string what = $"{KeyBindings.ActionName(listenAction)}, key {listenSlot + 1}";
                if (key == UnityEngine.InputSystem.Key.Backspace) { keys.Clear(listenAction, listenSlot); StopListening($"{what} is empty."); return; }
                if (keys.Bind(listenAction, listenSlot, key, out var from))
                    StopListening(from != null ? $"{KeyBindings.KeyName(key)} moved from {KeyBindings.ActionName(from.Value)} to {KeyBindings.ActionName(listenAction)}." : $"{what} is {KeyBindings.KeyName(key)}.");
                else StopListening($"{KeyBindings.KeyName(key)} can't be used.");
                return;
            }
        }

        public override void Show()
        {
            HideKeys(false);
            base.Show();
        }

        public override void Hide(float time = 0.35f)
        {
            StopListening();
            base.Hide(time);
        }

        /// <summary>For tours: open the keys panel, and the label a slot shows.</summary>
        public void TourShowKeys() => ShowKeys();
        public string SlotLabel(KeeperAction a, int slot) => slots[(int)a, slot].Label.text;
        public string KeysNote => keysNote.text;
    }

    // ==================================================================== results

    /// <summary>Dawn: how the night went, lamps lighting up one by one.</summary>
    public sealed class ResultsScreen : UiScreen
    {
        public Action OnNext, OnRetry, OnLogbook, OnChart;
        Text heading, title, verdict, stats, scoreLine, best, debrief;
        readonly Image[] lamps = new Image[3];
        readonly Text[] lampCaptions = new Text[3];
        UiButton next, retry, logbook, chart;
        RectTransform card;
        int lampCount;
        float lampTimer;
        int lampShown;

        public static ResultsScreen Create(Transform canvas)
        {
            var s = canvas.gameObject.AddComponent<ResultsScreen>();
            s.Init(canvas, "Results");
            s.Build();
            return s;
        }

        void Build()
        {
            var dim = UiKit.Image("Dim", Root, null, new Color(0.01f, 0.02f, 0.04f, 0.5f));
            dim.rectTransform.Fill();
            card = UiKit.Rect("Card", Root).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1000, 860));
            Frames.Add(card);
            var bg = UiKit.Image("Bg", card, SpriteFactory.Rounded, new Color(0.03f, 0.045f, 0.06f, 1f), true);
            bg.rectTransform.Fill();
            heading = Label(card, "", UiKit.BodyBold, 26, UiKit.Brass, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(800, 40));
            title = Label(card, "", UiKit.Title, 84, UiKit.Paper, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(900, 100));
            title.Shadowed(0.8f, 3f);
            verdict = Label(card, "", UiKit.Italic, 32, UiKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -218), new Vector2(900, 50));
            string[] captions = { "The light kept", "No ship lost", "A steady hand" };
            for (int i = 0; i < 3; i++)
            {
                var glow = UiKit.Image("Glow", card, SpriteFactory.Glow, new Color(1f, 0.75f, 0.35f, 0f));
                glow.rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(-230 + i * 230, -330), new Vector2(220, 220));
                lamps[i] = UiKit.Image("Lamp", card, SpriteFactory.LampEmpty, new Color(1, 1, 1, 0.25f));
                lamps[i].rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(-230 + i * 230, -330), new Vector2(130, 130));
                lampCaptions[i] = Label(card, captions[i], UiKit.BodyMedium, 24, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(-230 + i * 230, -420), new Vector2(220, 34));
            }
            stats = UiKit.Text("Stats", card, "", UiKit.BodyMedium, 28, UiKit.Paper, TextAnchor.UpperCenter);
            stats.rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -470), new Vector2(900, 120));
            stats.lineSpacing = 1.3f;
            debrief = UiKit.Text("Debrief", card, "", UiKit.BodyMedium, 24, new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.8f), TextAnchor.UpperCenter);
            debrief.rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -522), new Vector2(900, DebriefLine * MaxDebrief));
            debrief.lineSpacing = 1.1f;
            scoreLine = Label(card, "", UiKit.Heading, 54, UiKit.BrassBright, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -620), new Vector2(900, 70));
            best = Label(card, "", UiKit.BodyBold, 22, UiKit.Brass, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -698), new Vector2(900, 32));
            next = UiButton.Create(card, "Next night", UiKit.Heading, 42, () => OnNext?.Invoke(), TextAnchor.MiddleCenter);
            ((RectTransform)next.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-300, 50), new Vector2(300, 60));
            retry = UiButton.Create(card, "Try again", UiKit.Heading, 42, () => OnRetry?.Invoke(), TextAnchor.MiddleCenter);
            ((RectTransform)retry.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(300, 60));
            logbook = UiButton.Create(card, "Logbook", UiKit.Heading, 42, () => OnLogbook?.Invoke(), TextAnchor.MiddleCenter);
            ((RectTransform)logbook.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(300, 50), new Vector2(300, 60));
            chart = UiButton.Create(card, "Chart", UiKit.Heading, 42, () => OnChart?.Invoke(), TextAnchor.MiddleCenter);
            ((RectTransform)chart.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(300, 50), new Vector2(300, 60));
        }

        static string SpeedMark(int speed) => speed < 100 ? $"  ·  {speed}% SPEED" : "";

        /// <summary>The heading's words, for tours.</summary>
        public string Heading => heading.text;

        public void Setup(MissionDef def, SimWorld w, int previousBest, bool hasNext, bool finale, int speed = 100)
        {
            bool won = w.Outcome == MissionOutcome.Won;
            heading.text = UiKit.Spaced((won ? "DAWN  ·  NIGHT " + UiKit.Roman(def.night) : "NIGHT " + UiKit.Roman(def.night)) + (w.Hard ? "  ·  HARD" : "") + SpeedMark(speed));
            title.text = def.title;
            int lost = 0;
            SimShip wavered = null;
            foreach (var s in w.Ships) if (!s.SteadyHand) { lost++; wavered = s; }
            var final = Debrief.FinalWreck(w);
            if (!won) verdict.text = final != null ? $"One wreck too many: the {final.Name} {Debrief.WreckText(final)}." : "Too many ships were lost. The Board will hear of it.";
            else if (w.Lamps == 3) verdict.text = "Every ship home, and not one lost its way.";
            else if (w.Lamps == 2) verdict.text = lost == 1
                ? $"Every ship home, but the {wavered.Name} {(wavered.LuredCount > 0 ? "was lured " + Debrief.Times(wavered.LuredCount) : "lost its way " + Debrief.Times(wavered.LostCount))}."
                : $"Every ship home, though {lost} lost their way for a while.";
            else verdict.text = w.Wrecks == 1 && final != null ? $"The light was kept, but the {final.Name} never made it." : $"The light was kept, but {w.Wrecks} ships never made it.";
            ShowDebrief(Debrief.Lines(w));
            stats.text = $"Ships home  <b>{w.Arrivals} / {w.TotalShips}</b>          Wrecked  <b>{w.Wrecks}</b>          Steady hands  <b>{w.SteadyArrivals}</b>";
            scoreLine.text = won ? w.Score.ToString("N0") : "";
            best.text = won && w.Score > previousBest && previousBest > 0 ? UiKit.Spaced("NEW BEST") : (previousBest > 0 ? $"best {previousBest:N0}" : "");
            string[] captions = { "The light kept", "No ship lost", "A steady hand" };
            for (int i = 0; i < 3; i++) lampCaptions[i].text = captions[i];
            ResetLamps(w.Lamps);
            next.gameObject.SetActive(won && (hasNext || finale));
            next.Label.text = finale ? "Dawn" : "Next night";
            retry.Label.text = won ? "Play again" : "Try again";
            FirstSelected = won && (hasNext || finale) ? next : retry;
        }

        /// <summary>The end of a Night Watch: how long it lasted, and lamps for ships brought home.</summary>
        public void SetupWatch(SimWorld w, int previousBest, int rank = 0, int speed = 100)
        {
            heading.text = UiKit.Spaced("DAWN  ·  THE NIGHT WATCH" + (w.Hard ? "  ·  HARD" : "") + SpeedMark(speed));
            title.text = "The watch ends";
            string home = w.Arrivals == 0 ? "not one ship home" : w.Arrivals == 1 ? "one ship home" : $"{w.Arrivals} ships home";
            verdict.text = w.StoodDown ? $"You stood the watch down after {UiKit.Clock(w.Time)}, {home}."
                : w.Arrivals == 0 ? "Not one ship home. The Board will hear of it."
                : w.Arrivals == 1 ? $"One ship home in {UiKit.Clock(w.Time)}." : $"{w.Arrivals} ships home in {UiKit.Clock(w.Time)}.";
            stats.text = $"Ships home  <b>{w.Arrivals}</b>          Wrecked  <b>{w.Wrecks}</b>          Watch kept  <b>{UiKit.Clock(w.Time)}</b>";
            scoreLine.text = w.Score.ToString("N0");
            best.text = w.Score > previousBest && previousBest > 0 ? UiKit.Spaced("NEW BEST")
                : rank > 1 ? $"your {Ordinal(rank)} best watch  ·  best {previousBest:N0}"
                : previousBest > 0 ? $"best {previousBest:N0}" : "";
            var m = NightWatch.Milestones;
            for (int i = 0; i < 3; i++) lampCaptions[i].text = $"{m[i]} ships home";
            ShowDebrief(Debrief.Lines(w, wrecksOnly: true));
            ResetLamps(w.Lamps);
            next.gameObject.SetActive(false);
            retry.Label.text = "Keep watch again";
            FirstSelected = retry;
        }

        static string Ordinal(int n) => n switch { 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", _ => n.ToString() };

        const int MaxDebrief = 4;
        const float DebriefLine = 30f;

        /// <summary>Up to four lines on what went wrong, most serious first; the card grows to fit.</summary>
        void ShowDebrief(List<Debrief.Line> lines)
        {
            var sb = new System.Text.StringBuilder();
            int shown = lines.Count <= MaxDebrief ? lines.Count : MaxDebrief - 1;
            for (int i = 0; i < shown; i++)
            {
                var l = lines[i];
                string dot = l.Kind == Debrief.Kind.Wreck ? "#FF5A4A" : l.Kind == Debrief.Kind.Lured ? "#FF9A2E" : "#7E8A96";
                if (i > 0) sb.Append('\n');
                sb.Append($"<color={dot}><size=34>•</size></color>  <b>{l.Ship}</b>  {l.Text}");
            }
            if (lines.Count > shown) sb.Append($"\n<color=#7E8A96>and {lines.Count - shown} more</color>");
            debrief.text = sb.ToString();
            int rows = lines.Count == 0 ? 0 : Mathf.Min(lines.Count, MaxDebrief);
            float extra = rows == 0 ? 0f : rows * DebriefLine + 6f - 40f;   // the old card had room for about one line
            extra = Mathf.Max(0f, extra);
            card.sizeDelta = new Vector2(1000, 860 + extra);
            ((RectTransform)scoreLine.transform).anchoredPosition = new Vector2(0, -620 - extra);
            ((RectTransform)best.transform).anchoredPosition = new Vector2(0, -698 - extra);
        }

        void ResetLamps(int count)
        {
            lampCount = count;
            lampShown = 0;
            lampTimer = 1.0f;
            for (int i = 0; i < 3; i++)
            {
                lamps[i].sprite = SpriteFactory.LampEmpty;
                lamps[i].color = new Color(1, 1, 1, 0.22f);
                lampCaptions[i].color = new Color(1, 1, 1, 0.3f);
                var glow = lamps[i].transform.parent.GetChild(lamps[i].transform.GetSiblingIndex() - 1).GetComponent<Image>();
                glow.color = new Color(1f, 0.75f, 0.35f, 0f);
            }
        }

        /// <summary>Space the visible buttons evenly along the bottom of the card, and let the
        /// arrows and the pad move along them.</summary>
        void LayoutButtons()
        {
            var shown = new List<UiButton>();
            foreach (var b in new[] { next, retry, chart, logbook }) if (b.gameObject.activeSelf) shown.Add(b);
            // Four buttons sit closer; "Keep watch again" only ever shares the row with two.
            float step = shown.Count >= 4 ? 236f : 300f;
            for (int i = 0; i < shown.Count; i++)
            {
                var rt = (RectTransform)shown[i].transform;
                rt.anchoredPosition = new Vector2((i - (shown.Count - 1) * 0.5f) * step, 50f);
                rt.sizeDelta = new Vector2(step - 8f, 60f);
                var left = shown[(i + shown.Count - 1) % shown.Count];
                var right = shown[(i + 1) % shown.Count];
                shown[i].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = left, selectOnRight = right, selectOnUp = left, selectOnDown = right };
            }
        }

        /// <summary>Back from the chart: the card as it was, with Chart still chosen.</summary>
        public void ShowAgain()
        {
            Show();
            if (UnityEngine.EventSystems.EventSystem.current != null && (InputMode.Pad || UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null))
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(chart.gameObject);
        }

        /// <summary>The buttons shown along the bottom, left to right; for tours.</summary>
        public string ButtonLabels
        {
            get
            {
                var labels = new List<string>();
                foreach (var b in new[] { next, retry, chart, logbook }) if (b.gameObject.activeSelf) labels.Add(b.Label.text);
                return string.Join(", ", labels);
            }
        }

        public override void Show()
        {
            base.Show();
            LayoutButtons();
            card.localScale = Vector3.one * 0.94f;
            Tween.Scale(card, 0.94f, 1f, 0.6f, 0f, Tween.EaseOutBack);
        }

        void Update()
        {
            if (!Visible || lampShown >= lampCount) return;
            lampTimer -= Unscaled.Delta;
            if (lampTimer > 0f) return;
            int i = lampShown++;
            lampTimer = 0.55f;
            lamps[i].sprite = SpriteFactory.Lamp;
            lamps[i].color = new Color(1f, 0.82f, 0.5f);
            lampCaptions[i].color = UiKit.Paper;
            Tween.Punch(lamps[i].transform, 0.35f, 0.45f);
            var glow = lamps[i].transform.parent.GetChild(lamps[i].transform.GetSiblingIndex() - 1).GetComponent<Image>();
            Tween.Run(glow, "glow", 0.9f, t => glow.color = new Color(1f, 0.75f, 0.35f, 0.55f * (1f - t * 0.45f)), 0f, Tween.EaseOutCubic);
            Sfx.Play("lamp_" + (i + 1), 0.8f, 1f, (i - 1) * 0.3f, Bus.Ui);
        }
    }
}
