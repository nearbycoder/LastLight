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

            newCard = UiKit.Rect("New", card).Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -500), new Vector2(760, 110));
            var nbg = UiKit.Image("Bg", newCard, SpriteFactory.Rounded, new Color(1f, 0.85f, 0.55f, 0.08f), true);
            nbg.rectTransform.Fill();
            newIcon = UiKit.Image("Icon", newCard, SpriteFactory.Lamp, UiKit.BrassBright);
            newIcon.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(62, 0), new Vector2(66, 66));
            newTitle = Label(newCard, "", UiKit.BodyBold, 22, UiKit.Brass, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(120, -28), new Vector2(620, 30));
            newTitle.rectTransform.pivot = new Vector2(0, 0.5f);
            newText = Label(newCard, "", UiKit.BodyMedium, 27, UiKit.Paper, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(120, -68), new Vector2(620, 40));
            newText.rectTransform.pivot = new Vector2(0, 0.5f);

            var start = UiButton.Create(card, "Begin the watch", UiKit.Heading, 46, () => { if (ready) OnStart?.Invoke(); });
            ((RectTransform)start.transform).Pin(new Vector2(0, 0), new Vector2(0, 0), new Vector2(22, 40), new Vector2(520, 64));
            FirstSelected = start;
            prompt = Label(card, "click, or press Space", UiKit.Italic, 22, UiKit.Muted, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(22, 10), new Vector2(520, 30));
            prompt.rectTransform.pivot = new Vector2(0, 0.5f);
        }

        static readonly Dictionary<string, (string title, string text, string icon)> NewThings = new Dictionary<string, (string, string, string)>
        {
            ["aim"] = ("TONIGHT", "Move the mouse to turn the light.", "mouse"),
            ["chart"] = ("NEW: HIDDEN REEFS", "Sweep the light ahead of a ship to chart the rocks.", "ring"),
            ["buoy"] = ("NEW: BUOYS", "Light a buoy and it guides ships for a while.", "lamp"),
            ["shoal"] = ("NEW: STEAMERS AND SANDBANKS", "Deep hulls run aground on shoals. Chart the sands for them.", "ring"),
            ["fog"] = ("NEW: SEA FRET", "Fog swallows the light. Hold to focus, Space for the horn.", "lmb"),
            ["ferry"] = ("NEW: THE FERRY", "The Evening Star carries passengers. Worth the most, lost the hardest.", "ring"),
            ["damaged"] = ("NEW: DAMAGED SHIPS", "No lamps. Find them by their flares, then light them home.", "ring"),
            ["storm"] = ("NEW: STORM", "The current pushes ships ashore. Lightning shows the rocks.", "ring"),
            ["wrecker"] = ("NEW: FALSE LIGHTS", "Wreckers lure ships with lanterns. Hold your beam on one to douse it.", "lmb"),
            ["mimic"] = ("NEW: THE MIMIC", "A false light that turns like yours. Stay on your ships.", "ring"),
            ["finale"] = ("THE LAST NIGHT", "Everyone is out. Bring them all home.", "lamp"),
        };

        public void Setup(MissionDef def)
        {
            number.text = UiKit.Spaced("NIGHT " + UiKit.Roman(def.night) + " OF XII");
            title.text = def.title;
            date.text = def.date;
            full = def.briefing ?? "";
            typed = 0f;
            speech.text = "";
            if (!string.IsNullOrEmpty(def.newThing) && NewThings.TryGetValue(def.newThing, out var n))
            {
                newCard.gameObject.SetActive(true);
                newTitle.text = UiKit.Spaced(n.title);
                newText.text = n.text;
                newIcon.sprite = n.icon switch
                {
                    "mouse" => SpriteFactory.Mouse(""),
                    "lmb" => SpriteFactory.Mouse("left"),
                    "ring" => SpriteFactory.ThinRing,
                    _ => SpriteFactory.Lamp,
                };
                newIcon.rectTransform.sizeDelta = n.icon == "mouse" || n.icon == "lmb" ? new Vector2(46, 62) : new Vector2(66, 66);
            }
            else newCard.gameObject.SetActive(false);
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
                typed = Mathf.Min(full.Length, typed + Time.unscaledDeltaTime * 48f * SaveData.Current.textSpeed);
                int now = (int)typed;
                speech.text = full.Substring(0, now) + "<color=#00000000>" + full.Substring(now) + "</color>";
                if (now / 3 != before / 3) Sfx.Play("radio_tick", 0.1f, UnityEngine.Random.Range(0.9f, 1.1f), 0, Bus.Ui, 0.02f);
            }
            prompt.color = new Color(UiKit.Muted.r, UiKit.Muted.g, UiKit.Muted.b, 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 2.5f));
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
            panel = UiKit.Rect("Panel", Root).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 860));
            Label(panel, "Settings", UiKit.Title, 80, UiKit.Paper, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(800, 100)).Shadowed();
            var save = SaveData.Current;
            int row = 0;
            void Row(string label, Component control)
            {
                float y = -150 - row * 62;
                var l = Label(panel, label, UiKit.BodyMedium, 28, UiKit.Paper, TextAnchor.MiddleLeft, new Vector2(0.5f, 1), new Vector2(-190, y), new Vector2(380, 50));
                l.Shadowed();
                ((RectTransform)control.transform).Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(200, y), new Vector2(380, 46));
                row++;
            }
            Row("Master volume", UiSlider.Create(panel, save.master, v => { save.master = v; save.Apply(); }));
            Row("Music", UiSlider.Create(panel, save.music, v => { save.music = v; save.Apply(); }));
            Row("Sound effects", UiSlider.Create(panel, save.sfx, v => { save.sfx = v; save.Apply(); }));
            Row("Radio voices", UiSlider.Create(panel, save.radio, v => { save.radio = v; save.Apply(); Sfx.Play("voice_ianto", 0.4f, 0.95f, 0, Bus.Radio, 0.5f); }));
            Row("Sea and wind", UiSlider.Create(panel, save.ambience, v => { save.ambience = v; save.Apply(); }));
            Row("Text speed", UiStepper.Create(panel, new[] { "Slow", "Normal", "Fast" }, save.textSpeed < 0.9f ? 0 : save.textSpeed > 1.1f ? 2 : 1, i => { save.textSpeed = i == 0 ? 0.7f : i == 2 ? 1.5f : 1f; }));
            Row("Fog and haze quality", UiStepper.Create(panel, new[] { "Low", "Medium", "High" }, save.quality, i => { save.quality = i; save.Apply(); }));
            Row("Screen shake", UiStepper.Create(panel, new[] { "Off", "On" }, save.shake ? 1 : 0, i => save.shake = i == 1));
            Row("Hints", UiStepper.Create(panel, new[] { "Off", "On" }, save.hints ? 1 : 0, i => save.hints = i == 1));
            Row("Display", UiStepper.Create(panel, new[] { "Windowed", "Fullscreen" }, save.fullscreen ? 1 : 0, i => { save.fullscreen = i == 1; save.Apply(); }));
            var back = UiButton.Create(panel, "Done", UiKit.Heading, 44, () => { SaveData.Current.Save(); OnBack?.Invoke(); }, TextAnchor.MiddleCenter);
            ((RectTransform)back.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(300, 60));
            FirstSelected = back;
        }
    }

    // ==================================================================== results

    /// <summary>Dawn: how the night went, lamps lighting up one by one.</summary>
    public sealed class ResultsScreen : UiScreen
    {
        public Action OnNext, OnRetry, OnLogbook;
        Text heading, title, verdict, stats, scoreLine, best;
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
            var bg = UiKit.Image("Bg", card, SpriteFactory.Rounded, new Color(0.03f, 0.045f, 0.06f, 0.8f), true);
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
            scoreLine = Label(card, "", UiKit.Heading, 54, UiKit.BrassBright, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -620), new Vector2(900, 70));
            best = Label(card, "", UiKit.BodyBold, 22, UiKit.Brass, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -668), new Vector2(900, 32));
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
            foreach (var s in w.Ships) if (!s.SteadyHand) lost++;
            if (!won) verdict.text = "Too many ships were lost. The Board will hear of it.";
            else if (w.Lamps == 3) verdict.text = "Every ship home, and not one lost its way.";
            else if (w.Lamps == 2) verdict.text = lost == 1 ? "Every ship home, though one lost its way for a while." : $"Every ship home, though {lost} lost their way for a while.";
            else verdict.text = w.Wrecks == 1 ? "The light was kept, but one ship never made it." : $"The light was kept, but {w.Wrecks} ships never made it.";
            stats.text = $"Ships home  <b>{w.Arrivals} / {w.TotalShips}</b>          Wrecked  <b>{w.Wrecks}</b>          Steady hands  <b>{w.SteadyArrivals}</b>";
            scoreLine.text = won ? w.Score.ToString("N0") : "";
            best.text = won && w.Score > previousBest && previousBest > 0 ? UiKit.Spaced("NEW BEST") : (previousBest > 0 ? $"best {previousBest:N0}" : "");
            lampCount = w.Lamps;
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
            next.gameObject.SetActive(won && (hasNext || finale));
            next.Label.text = finale ? "Dawn" : "Next night";
            retry.Label.text = won ? "Play again" : "Try again";
            FirstSelected = won && (hasNext || finale) ? next : retry;
        }

        public override void Show()
        {
            base.Show();
            card.localScale = Vector3.one * 0.94f;
            Tween.Scale(card, 0.94f, 1f, 0.6f, 0f, Tween.EaseOutBack);
        }

        void Update()
        {
            if (!Visible || lampShown >= lampCount) return;
            lampTimer -= Time.unscaledDeltaTime;
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
