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
        public string PromptShown => prompt.text;
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

        static string PromptText() => InputMode.Pick("click, or press Space     ·     Esc to go back", $"press {PadButtons.South}     ·     {PadButtons.East} to go back");

        /// <summary>The line under the title (the date, or a watch's records); for tours.</summary>
        public string DateLine => date.text;

        // The new-thing cards that name a control, in gamepad words.
        static readonly Dictionary<string, string> PadNewThings = new Dictionary<string, string>
        {
            ["aim"] = "Point the right stick to turn the light.",
            ["fog"] = "Fog swallows the light. Hold {RT} to focus, {A} for the horn.",
        };

        static string NewText(string id, string keys)
        {
            string horn = SaveData.Current.keys.First(KeeperAction.Horn);
            if (horn == "") horn = "right-click";
            if (id == "fog" && SaveData.Current.focusToggle)
                return InputMode.Pick($"Fog swallows the light. Click to focus, {horn} for the horn.", $"Fog swallows the light. Press {PadButtons.Focus} to focus, {PadButtons.Horn} for the horn.");
            return InputMode.Pad && PadNewThings.TryGetValue(id, out var pad) ? pad.Replace("{RT}", PadButtons.Focus).Replace("{A}", PadButtons.Horn) : keys.Replace("{horn}", horn);
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
                prompt.text = PromptText();   // Settings ▸ Pad buttons may have changed
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
        Text about;
        readonly List<(string name, RectTransform label, Selectable control, string about)> rows = new List<(string, RectTransform, Selectable, string)>();

        /// <summary>The row the description is about: the one under the pointer, else the one chosen.</summary>
        int AboutRow()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && !InputMode.Pad)
            {
                var at = mouse.position.ReadValue();
                for (int i = 0; i < rows.Count; i++)
                    if ((rows[i].label != null && RectTransformUtility.RectangleContainsScreenPoint(rows[i].label, at, null)) ||
                        RectTransformUtility.RectangleContainsScreenPoint((RectTransform)rows[i].control.transform, at, null)) return i;
            }
            var es = UnityEngine.EventSystems.EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            for (int i = 0; i < rows.Count; i++) if (sel != null && rows[i].control.gameObject == sel) return i;
            return -1;
        }

        void UpdateAbout()
        {
            if (about == null || !panel.gameObject.activeSelf) return;
            int i = AboutRow();
            AboutShown = i >= 0 ? rows[i].name : null;
            // Named, so it can't be read as the row just above it.
            string text = i >= 0 ? $"<color=#E2C27F><b>{(rows[i].label != null ? rows[i].name : "Done")}</b></color>  ·  {rows[i].about}" : "";
            if (about.text != text) about.text = text;
        }

        /// <summary>For tours: which row the description is about, its words, and every row's.</summary>
        public string AboutShown { get; private set; }
        public string AboutText => about.text;
        public Text AboutLabel => about;
        public IEnumerable<(string name, string about)> AboutAll { get { foreach (var r in rows) yield return (r.name, r.about); } }
        public RectTransform RowLabel(string name) { foreach (var r in rows) if (r.name == name) return r.label; return null; }
        public RectTransform RowControl(string name) { foreach (var r in rows) if (r.name == name) return (RectTransform)r.control.transform; return null; }
        /// <summary>How many rows each column has; for tours walking them with the pad.</summary>
        public int LeftRows { get; private set; }
        public int RightRows { get; private set; }
        // Thirteen rows a column fit between the rule and the description at this spacing.
        const float RowStep = 52f;

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
            rows.Clear();
            void Row(string label, Component control, string about)
            {
                // Label and control share a centre line.
                float y = -196 - row * RowStep;
                var l = Label(panel, label, UiKit.BodyMedium, 28, UiKit.Paper, TextAnchor.MiddleLeft, new Vector2(0.5f, 1), new Vector2(column - 170, y), new Vector2(380, 50));
                l.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                l.Shadowed();
                ((RectTransform)control.transform).Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(column + 210, y), new Vector2(380, 46));
                order.Add((Selectable)control);
                rows.Add((label, l.rectTransform, (Selectable)control, about));
                if (column < 0f) LeftRows++; else RightRows++;
                row++;
            }
            LeftRows = RightRows = 0;
            Row("Master volume", UiSlider.Create(panel, save.master, v => { save.master = v; save.Apply(); }),
                "Everything the game plays, together.");
            Row("Music", UiSlider.Create(panel, save.music, v => { save.music = v; save.Apply(); }),
                "The score: the title waltz, the night's music and dawn.");
            Row("Sound effects", UiSlider.Create(panel, save.sfx, v => { save.sfx = v; save.Apply(); }),
                "The lens, horns, breakers, wrecks and flares, and the menus' clicks.");
            Row("Radio voices", UiSlider.Create(panel, save.radio, v => { save.radio = v; save.Apply(); Sfx.Play("voice_ianto", 0.4f, 0.95f, 0, Bus.Radio, 0.5f); }),
                "The captains' and the harbourmaster's voices. Their words always show as text.");
            Row("Sea and wind", UiSlider.Create(panel, save.ambience, v => { save.ambience = v; save.Apply(); }),
                "The swell, the wind and the rain.");
            Row("Sound", UiStepper.Create(panel, new[] { "Stereo", "Mono" }, save.mono ? 1 : 0, i => { save.mono = i == 1; save.Apply(display: false); }),
                "Mono: every sound in both ears alike, for hearing on one side or a single speaker. Stereo places sounds where they are.");
            Row("Sound in background", UiStepper.Create(panel, new[] { "On", "Off" }, save.muteInBackground ? 1 : 0, i => save.muteInBackground = i == 1),
                "Off: the game falls silent while its window is out of focus. A night pauses either way.");
            Row("Text speed", UiStepper.Create(panel, new[] { "Slow", "Normal", "Fast" }, save.textSpeed < 0.9f ? 0 : save.textSpeed > 1.1f ? 2 : 1, i => { save.textSpeed = i == 0 ? 0.7f : i == 2 ? 1.5f : 1f; }),
                "How fast the radio's calls type out.");
            float[] hudScales = { 1f, 1.15f, 1.3f };
            int hudIndex = System.Array.FindIndex(hudScales, v => Mathf.Abs(v - save.hudScale) < 0.01f);
            Row("HUD text size", UiStepper.Create(panel, new[] { "100%", "115%", "130%" }, Mathf.Max(0, hudIndex), i => { save.hudScale = hudScales[i]; save.Apply(); }),
                "Everything drawn during a night: the radio, hints, names, score and markers. The menus keep their size.");
            Row("Hints", UiStepper.Create(panel, new[] { "Off", "On" }, save.hints ? 1 : 0, i => save.hints = i == 1),
                "Short tips the first time each idea comes up. Each one shows once.");
            UiButton replay = null;
            replay = UiButton.Create(panel, "Show hints again", UiKit.BodyMedium, 26, () =>
            {
                save.hintsSeen.Clear();
                save.Save();
                replay.Label.text = "Hints will show again";
            }, TextAnchor.MiddleCenter);
            Row("Seen hints", replay,
                "Brings back every tip you've already seen.");
            keysButton = UiButton.Create(panel, "Change", UiKit.BodyMedium, 26, ShowKeys, TextAnchor.MiddleCenter);
            Row("Keys and buttons", keysButton,
                "Choose the keys for turning, focus and the foghorn, and the gamepad's buttons for focus and the foghorn.");
            row = 0;
            column = 410f;
            Row("Difficulty", UiStepper.Create(panel, new[] { "Standard", "Hard" }, save.difficulty, i => save.difficulty = i),
                "Hard: ships lose heart faster, charts and buoys fade sooner, and the crew give no breakers warning. From the next night.");
            float[] speeds = { 1f, 0.85f, 0.7f };
            int speedIndex = System.Array.FindIndex(speeds, v => Mathf.Abs(v - save.gameSpeed) < 0.01f);
            Row("Game speed", UiStepper.Create(panel, new[] { "100%", "85%", "70%" }, Mathf.Max(0, speedIndex), i => save.gameSpeed = speeds[i]),
                "Slows the whole night together: ships, the lens, fog, storms and wreckers. Only you gain time. Dawn notes a slowed night.");
            Row("Screen shake", UiStepper.Create(panel, new[] { "Off", "On" }, save.shake ? 1 : 0, i => save.shake = i == 1),
                "The camera's jolt at a wreck, the foghorn, lightning and a doused false light.");
            Row("Focus", UiStepper.Create(panel, new[] { "Hold", "Toggle" }, save.focusToggle ? 1 : 0, i => { save.focusToggle = i == 1; OnFocusMode?.Invoke(); }),
                "Hold: the beam is focused while the button is held. Toggle: press once to focus, again to widen.");
            Row("Lens turn speed (keys)", UiSlider.Create(panel, Mathf.InverseLerp(0.5f, 1.25f, save.turnSpeed), v => save.turnSpeed = Mathf.Lerp(0.5f, 1.25f, v)),
                "How fast the keys turn the lens. The mouse and the sticks point it directly.");
            Row("Pad buttons", UiStepper.Create(panel, PadButtons.Choices, Mathf.Clamp(save.padStyle, 0, 3), i => { save.padStyle = i; OnFocusMode?.Invoke(); }),
                "The names prompts give the gamepad's buttons. Auto goes by the pad's name. Nintendo goes by position: the bottom button is B.");
            Row("Display", UiStepper.Create(panel, new[] { "Windowed", "Fullscreen" }, save.fullscreen ? 1 : 0, i => { save.fullscreen = i == 1; save.Apply(); }),
                "In a window, or filling the screen.");
            var sizes = Resolutions();
            int current = sizes.FindIndex(r => r.x == save.resWidth && r.y == save.resHeight);
            var names = sizes.ConvertAll(r => r.x == 0 ? "Native" : $"{r.x} × {r.y}").ToArray();
            Row("Resolution", UiStepper.Create(panel, names, Mathf.Max(0, current), i => { save.resWidth = sizes[i].x; save.resHeight = sizes[i].y; save.Apply(); }),
                "The window's size, or the screen's when fullscreen. Native is the display's own.");
            int[] caps = { 0, 60, 30 };
            Row("Frame rate", UiStepper.Create(panel, new[] { "Display", "60", "30" }, Mathf.Max(0, System.Array.IndexOf(caps, save.frameCap)), i => { save.frameCap = caps[i]; save.Apply(display: false); }),
                "Display keeps pace with your screen. 60 or 30 saves power and heat on a laptop or handheld.");
            Row("Brightness", UiStepper.Create(panel, new[] { "−2", "−1", "Standard", "+1", "+2" }, Mathf.Clamp(save.brightness, -2, 2) + 2, i => { save.brightness = i - 2; save.Apply(display: false); }),
                "The bay, from half a stop darker to a stop brighter. The menus and the HUD stay as they are.");
            Row("Fog and haze quality", UiStepper.Create(panel, new[] { "Low", "Medium", "High" }, save.quality, i => { save.quality = i; save.Apply(); }),
                "Detail in the fog and the beam's haze. Lower runs faster on fog nights.");
            float[] scales = { 1f, 0.85f, 0.7f, 0.5f };
            int scaleIndex = System.Array.FindIndex(scales, v => Mathf.Abs(v - save.renderScale) < 0.01f);
            Row("Render scale", UiStepper.Create(panel, new[] { "100%", "85%", "70%", "50%" }, Mathf.Max(0, scaleIndex), i => { save.renderScale = scales[i]; save.Apply(); }),
                "Draws the bay at a lower resolution while text stays sharp. 70% helps fog nights on a weaker GPU.");
            Row("Reduce flashing", UiStepper.Create(panel, new[] { "Off", "On" }, save.reduceFlashing ? 1 : 0, i => { save.reduceFlashing = i == 1; save.Apply(); }),
                "The storm's lightning lights the bay at about a tenth of its strength.");
            var back = UiButton.Create(panel, "Done", UiKit.Heading, 44, () => { SaveData.Current.Save(); OnBack?.Invoke(); }, TextAnchor.MiddleCenter);
            ((RectTransform)back.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(300, 60));
            order.Add(back);
            // What the chosen setting does, in the space under the right-hand column.
            // Between the last rows (13 rows end at -820, their boxes at -845) and Done (from -904): two lines across the panel.
            about = Label(panel, "", UiKit.Italic, 23, new Color(0.78f, 0.8f, 0.83f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -873), new Vector2(1440, 56));
            about.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            about.lineSpacing = 1f;
            about.Shadowed(0.7f, 1.5f);
            rows.Add(("Done", null, back, "Settings are kept as you leave."));
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

        // ---------------------------------------------------------------- keys and buttons

        // The keyboard's keys for turning, focus and the horn (three slots each), and the pad's
        // buttons for focus and the horn (two each). Choose a slot, press a key or a button. The
        // panel takes the settings panel's place while it's open.
        RectTransform keysPanel;
        UiButton keysButton, keysDone, keysReset, settingsFirst;
        readonly UiButton[,] slots = new UiButton[4, KeyBindings.Slots];
        readonly Image[,] cells = new Image[4, KeyBindings.Slots];
        readonly UiButton[,] padSlots = new UiButton[2, PadBindings.Slots];
        readonly Image[,] padCells = new Image[2, PadBindings.Slots];
        Text keysNote, keysHelp;
        KeeperAction listenAction;
        int listenSlot = -1, listenFrame, restoreNavFrame = -1, padListenEnded = -1;
        bool listenPad;

        /// <summary>The keys panel is open, and whether it's waiting for a key or a button; for tours.</summary>
        public bool KeysOpen => keysPanel != null && keysPanel.gameObject.activeSelf;
        public bool Listening => listenSlot >= 0;
        public bool ListeningPad => Listening && listenPad;

        /// <summary>The pad's B is a button to bind while a pad slot waits (and on the frame it was
        /// bound), not a way back.</summary>
        public bool PadBackTaken => ListeningPad || Time.frameCount <= padListenEnded;

        // The pad rows are the keys panel's focus and foghorn rows.
        static int PadRow(KeeperAction a) => a == KeeperAction.Focus ? 0 : 1;

        static string KeysHelp() =>
            $"Choose a slot, then press a key or a button. Backspace or the pad's {PadButtons.Select} empties a slot; Esc or {PadButtons.Start} cancels.\n" +
            $"Esc, P and {PadButtons.Start} always pause. The mouse keeps its buttons, and either stick turns the light.";

        void BuildKeys()
        {
            keysPanel = UiKit.Rect("Keys", Root).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1660, 940));
            Frames.Add(keysPanel);
            var bg = UiKit.Image("Bg", keysPanel, SpriteFactory.Rounded, new Color(0.03f, 0.045f, 0.06f, 0.86f), true);
            bg.rectTransform.Fill();
            Label(keysPanel, "Keys and buttons", UiKit.Title, 80, UiKit.Paper, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(1000, 100)).Shadowed();
            var rule = UiKit.Image("Rule", keysPanel, SpriteFactory.Bar, new Color(UiKit.Brass.r, UiKit.Brass.g, UiKit.Brass.b, 0.6f));
            rule.rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -146), new Vector2(620, 3));
            // Columns: the actions, the keyboard's three slots, the pad's two.
            const float keyX = -340f, keyStep = 230f, keyW = 214f, padX = 405f, padStep = 245f, padW = 230f;
            var keysHead = Label(keysPanel, "KEYBOARD", UiKit.BodyBold, 22, UiKit.Brass, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(keyX + keyStep, -200), new Vector2(600, 36));
            keysHead.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var padHead = Label(keysPanel, "GAMEPAD", UiKit.BodyBold, 22, UiKit.Brass, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(padX + padStep / 2, -200), new Vector2(460, 36));
            padHead.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UiButton Slot(string name, float x, float y, float w, Action click, out Image cell)
            {
                var b = UiButton.Create(keysPanel, "", UiKit.BodyBold, 28, click, TextAnchor.MiddleCenter);
                b.name = name;
                ((RectTransform)b.transform).Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, 64));
                cell = UiKit.Image("Cell", b.transform, SpriteFactory.Rounded, CellIdle, true);
                cell.rectTransform.Fill();
                cell.raycastTarget = false;
                cell.transform.SetAsFirstSibling();
                return b;
            }
            for (int r = 0; r < 4; r++)
            {
                var action = KeyBindings.Actions[r];
                float y = -262 - r * 88;
                var l = Label(keysPanel, KeyBindings.ActionName(action), UiKit.BodyMedium, 30, UiKit.Paper, TextAnchor.MiddleLeft, new Vector2(0.5f, 1), new Vector2(-625, y), new Vector2(270, 56));
                l.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                l.Shadowed();
                for (int c = 0; c < KeyBindings.Slots; c++)
                {
                    int slot = c;
                    slots[r, c] = Slot($"Slot {action} {slot + 1}", keyX + c * keyStep, y, keyW, () => Listen(action, slot, false), out cells[r, c]);
                }
                if (PadBindings.CanBind(action))
                    for (int c = 0; c < PadBindings.Slots; c++)
                    {
                        int slot = c;
                        padSlots[PadRow(action), c] = Slot($"Pad {action} {slot + 1}", padX + c * padStep, y, padW, () => Listen(action, slot, true), out padCells[PadRow(action), c]);
                    }
                else
                {
                    // The sticks turn the light; that isn't for changing.
                    var stick = Label(keysPanel, "either stick", UiKit.Italic, 26, UiKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(padX + padStep / 2, y), new Vector2(460, 56));
                    stick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                }
            }
            keysNote = Label(keysPanel, "", UiKit.Italic, 27, UiKit.BrassBright, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -626), new Vector2(1500, 40));
            keysHelp = Label(keysPanel, KeysHelp(), UiKit.BodyMedium, 24, UiKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -706), new Vector2(1500, 80));
            keysHelp.lineSpacing = 1.1f;
            keysReset = UiButton.Create(keysPanel, "Reset to defaults", UiKit.Heading, 40, () =>
            {
                StopListening();
                SaveData.Current.keys.Reset();
                SaveData.Current.pad.Reset();
                RefreshKeys("The keys and buttons are back as they were.");
            }, TextAnchor.MiddleCenter);
            ((RectTransform)keysReset.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-240, 36), new Vector2(400, 60));
            keysDone = UiButton.Create(keysPanel, "Done", UiKit.Heading, 44, () => HideKeys(), TextAnchor.MiddleCenter);
            ((RectTransform)keysDone.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(240, 36), new Vector2(300, 60));

            // A grid for the pad and arrows: across a row's keys and then its buttons, down the
            // columns, then Reset and Done.
            const int columns = KeyBindings.Slots + PadBindings.Slots;
            Selectable At(int r, int c)
            {
                if (c < KeyBindings.Slots) return slots[r, c];
                return PadBindings.CanBind(KeyBindings.Actions[r]) ? padSlots[PadRow(KeyBindings.Actions[r]), c - KeyBindings.Slots] : null;
            }
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < columns; c++)
                {
                    var self = At(r, c);
                    if (self == null) continue;
                    Selectable left = null, right = null, up = null, down = null;
                    for (int d = 1; d < columns && left == null; d++) left = At(r, (c - d + columns) % columns);
                    for (int d = 1; d < columns && right == null; d++) right = At(r, (c + d) % columns);
                    for (int rr = r - 1; rr >= 0 && up == null; rr--) up = At(rr, c);
                    for (int rr = r + 1; rr < 4 && down == null; rr++) down = At(rr, c);
                    self.navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnLeft = left ?? self,
                        selectOnRight = right ?? self,
                        selectOnUp = up ?? keysDone,
                        selectOnDown = down ?? (c == 0 ? (Selectable)keysReset : keysDone),
                    };
                }
            keysReset.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = keysDone, selectOnLeft = keysDone, selectOnUp = slots[3, 0], selectOnDown = slots[0, 0] };
            keysDone.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = keysReset, selectOnRight = keysReset, selectOnUp = padSlots[1, PadBindings.Slots - 1], selectOnDown = slots[0, 0] };
            keysPanel.gameObject.SetActive(false);
        }

        void RefreshKeys(string note = null)
        {
            var keys = SaveData.Current.keys;
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < KeyBindings.Slots; c++)
                {
                    bool waiting = Listening && !listenPad && listenAction == KeyBindings.Actions[r] && listenSlot == c;
                    var k = keys.Get(KeyBindings.Actions[r], c);
                    slots[r, c].Label.text = waiting ? "<i>press a key…</i>" : k == UnityEngine.InputSystem.Key.None ? "<color=#8A8F96>—</color>" : KeyBindings.KeyName(k);
                }
            var pad = SaveData.Current.pad;
            var style = PadButtons.Style;
            foreach (var a in PadBindings.Actions)
                for (int c = 0; c < PadBindings.Slots; c++)
                {
                    bool waiting = ListeningPad && listenAction == a && listenSlot == c;
                    var b = pad.Get(a, c);
                    padSlots[PadRow(a), c].Label.text = waiting ? "<i>press a button…</i>" : b == null ? "<color=#8A8F96>—</color>" : PadButtons.Name(b.Value, style);
                }
            keysHelp.text = KeysHelp();
            if (note != null) keysNote.text = note;
            OnFocusMode?.Invoke();   // the title's control strip names the horn's key and button
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

        static string SlotName(KeeperAction action, int slot, bool pad) => $"{KeyBindings.ActionName(action)}, {(pad ? "button" : "key")} {slot + 1}";

        /// <summary>Wait for a key or a button for this slot. Menu navigation is off meanwhile, so an
        /// arrow, Enter, the d-pad or A can be bound rather than moving the selection.</summary>
        void Listen(KeeperAction action, int slot, bool pad)
        {
            listenAction = action;
            listenSlot = slot;
            listenPad = pad;
            listenFrame = Time.frameCount;
            restoreNavFrame = -1;
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null) es.sendNavigationEvents = false;
            RefreshKeys($"{SlotName(action, slot, pad)}: press {(pad ? "a button on the gamepad" : "a key")}.");
        }

        void StopListening(string note = null)
        {
            if (!Listening) return;
            if (listenPad) padListenEnded = Time.frameCount;
            listenSlot = -1;
            restoreNavFrame = Time.frameCount + 1;   // not this frame: the key just pressed mustn't also navigate
            RefreshKeys(note);
        }

        /// <summary>Esc, P, Start or B: stop waiting for a key, or close the keys panel. False when
        /// there's nothing here to back out of (Settings itself then closes).</summary>
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
            UpdateAbout();
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (KeysOpen)
            {
                // The selected slot (and the one waiting) lights up, even when empty.
                var sel = es != null ? es.currentSelectedGameObject : null;
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < KeyBindings.Slots; c++)
                    {
                        bool hot = slots[r, c].gameObject == sel || (Listening && !listenPad && listenAction == KeyBindings.Actions[r] && listenSlot == c);
                        cells[r, c].color = Color.Lerp(cells[r, c].color, hot ? CellHot : CellIdle, Unscaled.Delta * 12f);
                    }
                foreach (var a in PadBindings.Actions)
                    for (int c = 0; c < PadBindings.Slots; c++)
                    {
                        int r = PadRow(a);
                        bool hot = padSlots[r, c].gameObject == sel || (ListeningPad && listenAction == a && listenSlot == c);
                        padCells[r, c].color = Color.Lerp(padCells[r, c].color, hot ? CellHot : CellIdle, Unscaled.Delta * 12f);
                    }
            }
            if (restoreNavFrame >= 0 && Time.frameCount >= restoreNavFrame)
            {
                restoreNavFrame = -1;
                if (es != null) es.sendNavigationEvents = true;
            }
            if (!Listening || Time.frameCount <= listenFrame) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            string what = SlotName(listenAction, listenSlot, listenPad);
            if (listenPad)
            {
                // A button on the pad; View or Backspace empties the slot. Start and Esc back out (see Back).
                var gamepad = UnityEngine.InputSystem.Gamepad.current;
                var buttons = SaveData.Current.pad;
                bool clear = (gamepad != null && gamepad.selectButton.wasPressedThisFrame) || (kb != null && kb.backspaceKey.wasPressedThisFrame);
                if (clear)
                {
                    StopListening(buttons.Clear(listenAction, listenSlot) ? $"{what} is empty."
                        : $"The {KeyBindings.ActionName(listenAction).ToLowerInvariant()} needs a button on the gamepad, so this one stays.");
                    return;
                }
                var pressed = PadBindings.PressedNow(gamepad);
                if (pressed == null) return;
                var style = PadButtons.Style;
                string name = PadButtons.Name(pressed.Value, style);
                if (buttons.Bind(listenAction, listenSlot, pressed.Value, out var from, out var swapped))
                    StopListening(from == null ? $"{what} is {name}."
                        : swapped != null ? $"{name} moved from {KeyBindings.ActionName(from.Value)} to {KeyBindings.ActionName(listenAction)}, and {PadButtons.Name(swapped.Value, style)} went the other way."
                        : $"{name} moved from {KeyBindings.ActionName(from.Value)} to {KeyBindings.ActionName(listenAction)}.");
                else StopListening($"{name} is the {KeyBindings.ActionName(from ?? (listenAction == KeeperAction.Horn ? KeeperAction.Focus : KeeperAction.Horn)).ToLowerInvariant()}'s only button. Give it another first.");
                return;
            }
            if (kb == null) return;
            foreach (var c in kb.allKeys)
            {
                if (c == null || !c.wasPressedThisFrame) continue;
                var key = c.keyCode;
                if (KeyBindings.Reserved(key)) return;   // Esc and P back out (see Back)
                var keys = SaveData.Current.keys;
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
        public string PadSlotLabel(KeeperAction a, int slot) => padSlots[PadRow(a), slot].Label.text;
        public string KeysNote => keysNote.text;
    }

    // ==================================================================== results

    /// <summary>Dawn: how the night went, lamps lighting up one by one.</summary>
    public sealed class ResultsScreen : UiScreen
    {
        public Action OnNext, OnRetry, OnLogbook, OnChart;
        Text heading, title, verdict, stats, scoreLine, scoreParts, best, debrief, saveNote, helpNote, perfNote;
        float debriefExtra;
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
            // Where the score came from, under it.
            scoreParts = Label(card, "", UiKit.BodyMedium, 22, new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0.62f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -684), new Vector2(940, 30));
            best = Label(card, "", UiKit.BodyBold, 22, UiKit.Brass, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -698), new Vector2(900, 32));
            // Said plainly when the night couldn't be saved.
            saveNote = Label(card, "", UiKit.BodyMedium, 22, new Color(1f, 0.62f, 0.52f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -740), new Vector2(900, 34));
            // After a night that ran slowly: which settings would make it smoother.
            perfNote = Label(card, "", UiKit.Italic, 22, new Color(0.8f, 0.82f, 0.86f, 0.9f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -740), new Vector2(940, 60));
            perfNote.lineSpacing = 1f;
            // After the same night fails twice running: where the assists are.
            helpNote = Label(card, "", UiKit.Italic, 24, new Color(0.8f, 0.82f, 0.86f, 0.9f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -680), new Vector2(900, 34));
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
            scoreParts.text = won ? ScoreParts.Of(w).Line() : "";
            best.text = won && w.Score > previousBest && previousBest > 0 ? UiKit.Spaced("NEW BEST") : (previousBest > 0 ? $"best {previousBest:N0}" : "");
            string[] captions = { "The light kept", "No ship lost", "A steady hand" };
            for (int i = 0; i < 3; i++) lampCaptions[i].text = captions[i];
            ResetLamps(w.Lamps);
            next.gameObject.SetActive(won && (hasNext || finale));
            next.Label.text = finale ? "Dawn" : "Next night";
            retry.Label.text = won ? "Play again" : "Try again";
            FirstSelected = won && (hasNext || finale) ? next : retry;
            Relayout();
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
            scoreParts.text = ScoreParts.Of(w).Line();
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
            Relayout();
        }

        public static string Ordinal(int n) => n switch { 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", _ => n.ToString() };

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
            debriefExtra = extra;
            Relayout();
        }

        /// <summary>The card's lower half: the score, the best, and a line if the night wasn't
        /// saved; the card grows to fit.</summary>
        void Relayout()
        {
            float parts = scoreParts.text != "" ? 40f : 0f;
            float note = saveNote.text != "" ? 44f : 0f;
            float perf = perfNote.text != "" ? 62f : 0f;
            // A failed night has no score, so the offer of help takes the score's place.
            ((RectTransform)helpNote.transform).anchoredPosition = new Vector2(0, -640 - debriefExtra);
            card.sizeDelta = new Vector2(1000, 860 + debriefExtra + parts + note + perf);
            ((RectTransform)scoreLine.transform).anchoredPosition = new Vector2(0, -620 - debriefExtra);
            ((RectTransform)scoreParts.transform).anchoredPosition = new Vector2(0, -684 - debriefExtra);
            ((RectTransform)best.transform).anchoredPosition = new Vector2(0, -698 - debriefExtra - parts);
            ((RectTransform)saveNote.transform).anchoredPosition = new Vector2(0, -740 - debriefExtra - parts);
            ((RectTransform)perfNote.transform).anchoredPosition = new Vector2(0, -752 - debriefExtra - parts - note);
        }

        /// <summary>After a slow night: which settings would make it smoother (null for none).</summary>
        public void SetPerfNote(string text)
        {
            perfNote.text = text ?? "";
            Relayout();
        }

        /// <summary>The frame-rate advice as shown; for tours.</summary>
        public string PerfNoteShown => perfNote.text;

        /// <summary>Why the night just kept wasn't saved, or null when it was.</summary>
        public void SetSaveNote(string text)
        {
            saveNote.text = text ?? "";
            Relayout();
        }

        /// <summary>A word on the assists after the same night has failed twice running (null for none).</summary>
        public void SetHelpNote(string text) => helpNote.text = text ?? "";

        /// <summary>The offer of help as shown; for tours.</summary>
        public string HelpNoteShown => helpNote.text;

        /// <summary>The score's parts as shown; for tours.</summary>
        public string ScorePartsShown => scoreParts.text;

        /// <summary>The save line as shown; for tours.</summary>
        public string SaveNoteShown => saveNote.text;

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
