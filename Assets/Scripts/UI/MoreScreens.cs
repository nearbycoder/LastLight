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
        Text number, title, date, speech, newTitle, newText, prompt;
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
            number = Label(card, "", UiKit.BodyBold, 24, UiKit.Brass, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, -20), new Vector2(800, 34));
            number.rectTransform.pivot = new Vector2(0, 0.5f);
            title = Label(card, "", UiKit.Title, 104, UiKit.Paper, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, -95), new Vector2(900, 120));
            title.rectTransform.pivot = new Vector2(0, 0.5f);
            title.Shadowed(0.85f, 3f);
            date = Label(card, "", UiKit.Italic, 28, UiKit.Muted, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, -165), new Vector2(800, 40));
            date.rectTransform.pivot = new Vector2(0, 0.5f);
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

            var start = UiButton.Create(card, "Begin the watch", UiKit.Heading, 46, () => { if (ready) OnStart?.Invoke(); });
            ((RectTransform)start.transform).Pin(new Vector2(0, 0), new Vector2(0, 0), new Vector2(22, 40), new Vector2(520, 64));
            FirstSelected = start;
            prompt = Label(card, PromptText(), UiKit.Italic, 24, UiKit.Muted, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(330, 72), new Vector2(360, 30));
            prompt.rectTransform.pivot = new Vector2(0, 0.5f);
            InputMode.Changed += () =>
            {
                prompt.text = PromptText();
                if (newThing != null && NewThings.TryGetValue(newThing, out var n)) { newText.text = NewText(newThing, n.text); SetNewIcon(n.icon); }
            };
        }

        static string PromptText() => InputMode.Pick("click, or press Space", "press A");

        // The new-thing cards that name a control, in gamepad words.
        static readonly Dictionary<string, string> PadNewThings = new Dictionary<string, string>
        {
            ["aim"] = "Point the right stick to turn the light.",
            ["fog"] = "Fog swallows the light. Hold RT to focus, A for the horn.",
        };

        static string NewText(string id, string keys) => InputMode.Pad && PadNewThings.TryGetValue(id, out var pad) ? pad : keys;

        static readonly Dictionary<string, (string title, string text, string icon)> NewThings = new Dictionary<string, (string, string, string)>
        {
            ["aim"] = ("TONIGHT", "Move the mouse to turn the light.", "mouse"),
            ["chart"] = ("NEW: HIDDEN REEFS", "Sweep the light ahead of a ship to chart the rocks.", "reef"),
            ["buoy"] = ("NEW: BUOYS", "Light a buoy and it guides ships for a while.", "buoy"),
            ["shoal"] = ("NEW: STEAMERS AND SANDBANKS", "Deep hulls run aground on shoals. Chart the sands for them.", "steamer"),
            ["fog"] = ("NEW: SEA FRET", "Fog swallows the light. Hold to focus, Space for the horn.", "lmb"),
            ["ferry"] = ("NEW: THE FERRY", "The Evening Star carries passengers. Worth the most, lost the hardest.", "ferry"),
            ["damaged"] = ("NEW: DAMAGED SHIPS", "No lamps. Find them by their flares, then light them home.", "flare"),
            ["storm"] = ("NEW: STORM", "The current pushes ships ashore. Lightning shows the rocks.", "storm"),
            ["wrecker"] = ("NEW: FALSE LIGHTS", "Wreckers lure ships with lanterns. Hold your beam on one to douse it.", "lantern"),
            ["mimic"] = ("NEW: THE MIMIC", "A false light that turns like yours. Stay on your ships.", "twin"),
            ["finale"] = ("THE LAST NIGHT", "Everyone is out. Bring them all home.", "lamp"),
            ["watch"] = ("ENDLESS", "Every hazard out, ships without end. The third wreck ends the watch.", "lamp"),
        };

        /// <summary>Fill the card for a night; `watch` carries the Night Watch records.</summary>
        public void Setup(MissionDef def, SaveData watch = null)
        {
            number.text = UiKit.Spaced(def.endless ? "AFTER THE SEASON" : "NIGHT " + UiKit.Roman(def.night) + " OF XII");
            title.text = def.title;
            date.text = def.date;
            if (def.endless && watch != null && watch.watchShips > 0)
                date.text = $"Longest watch {UiKit.Clock(watch.watchSeconds)}  ·  {watch.watchShips} ships home  ·  best {watch.watchBest:N0}";
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
        public Action OnBack;
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
            panel = UiKit.Rect("Panel", Root).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1660, 820));
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
            Row("Text speed", UiStepper.Create(panel, new[] { "Slow", "Normal", "Fast" }, save.textSpeed < 0.9f ? 0 : save.textSpeed > 1.1f ? 2 : 1, i => { save.textSpeed = i == 0 ? 0.7f : i == 2 ? 1.5f : 1f; }));
            Row("Hints", UiStepper.Create(panel, new[] { "Off", "On" }, save.hints ? 1 : 0, i => save.hints = i == 1));
            UiButton replay = null;
            replay = UiButton.Create(panel, "Show hints again", UiKit.BodyMedium, 26, () =>
            {
                save.hintsSeen.Clear();
                save.Save();
                replay.Label.text = "Hints will show again";
            }, TextAnchor.MiddleCenter);
            Row("Seen hints", replay);

            row = 0;
            column = 410f;
            Row("Screen shake", UiStepper.Create(panel, new[] { "Off", "On" }, save.shake ? 1 : 0, i => save.shake = i == 1));
            Row("Lens turn speed (keys)", UiSlider.Create(panel, Mathf.InverseLerp(0.5f, 1.25f, save.turnSpeed), v => save.turnSpeed = Mathf.Lerp(0.5f, 1.25f, v)));
            Row("Display", UiStepper.Create(panel, new[] { "Windowed", "Fullscreen" }, save.fullscreen ? 1 : 0, i => { save.fullscreen = i == 1; save.Apply(); }));
            var sizes = Resolutions();
            int current = sizes.FindIndex(r => r.x == save.resWidth && r.y == save.resHeight);
            var names = sizes.ConvertAll(r => r.x == 0 ? "Native" : $"{r.x} × {r.y}").ToArray();
            Row("Resolution", UiStepper.Create(panel, names, Mathf.Max(0, current), i => { save.resWidth = sizes[i].x; save.resHeight = sizes[i].y; save.Apply(); }));
            Row("Fog and haze quality", UiStepper.Create(panel, new[] { "Low", "Medium", "High" }, save.quality, i => { save.quality = i; save.Apply(); }));
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
            FirstSelected = back;
        }
    }

    // ==================================================================== results

    /// <summary>Dawn: how the night went, lamps lighting up one by one.</summary>
    public sealed class ResultsScreen : UiScreen
    {
        public Action OnNext, OnRetry, OnLogbook;
        Text heading, title, verdict, stats, scoreLine, best, debrief;
        readonly Image[] lamps = new Image[3];
        readonly Text[] lampCaptions = new Text[3];
        UiButton next, retry, logbook;
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
        }

        public void Setup(MissionDef def, SimWorld w, int previousBest, bool hasNext, bool finale)
        {
            bool won = w.Outcome == MissionOutcome.Won;
            heading.text = UiKit.Spaced(won ? "DAWN  ·  NIGHT " + UiKit.Roman(def.night) : "NIGHT " + UiKit.Roman(def.night));
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
        public void SetupWatch(SimWorld w, int previousBest)
        {
            heading.text = UiKit.Spaced("DAWN  ·  THE NIGHT WATCH");
            title.text = "The watch ends";
            verdict.text = w.Arrivals == 0 ? "Not one ship home. The Board will hear of it."
                : w.Arrivals == 1 ? $"One ship home in {UiKit.Clock(w.Time)}." : $"{w.Arrivals} ships home in {UiKit.Clock(w.Time)}.";
            stats.text = $"Ships home  <b>{w.Arrivals}</b>          Wrecked  <b>{w.Wrecks}</b>          Watch kept  <b>{UiKit.Clock(w.Time)}</b>";
            scoreLine.text = w.Score.ToString("N0");
            best.text = w.Score > previousBest && previousBest > 0 ? UiKit.Spaced("NEW BEST") : previousBest > 0 ? $"best {previousBest:N0}" : "";
            var m = NightWatch.Milestones;
            for (int i = 0; i < 3; i++) lampCaptions[i].text = $"{m[i]} ships home";
            ShowDebrief(Debrief.Lines(w, wrecksOnly: true));
            ResetLamps(w.Lamps);
            next.gameObject.SetActive(false);
            retry.Label.text = "Keep watch again";
            FirstSelected = retry;
        }

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

        /// <summary>Space the visible buttons evenly along the bottom of the card.</summary>
        void LayoutButtons()
        {
            var shown = new List<UiButton>();
            foreach (var b in new[] { next, retry, logbook }) if (b.gameObject.activeSelf) shown.Add(b);
            for (int i = 0; i < shown.Count; i++)
                ((RectTransform)shown[i].transform).anchoredPosition = new Vector2((i - (shown.Count - 1) * 0.5f) * 300f, 50f);
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
