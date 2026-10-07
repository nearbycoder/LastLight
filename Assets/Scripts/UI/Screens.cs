using System;
using System.Collections.Generic;
using LastLight.Audio;
using LastLight.Core;
using LastLight.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>A full-screen panel that fades in and out.</summary>
    public abstract class UiScreen : MonoBehaviour
    {
        protected RectTransform Root;
        protected CanvasGroup Group;
        public bool Visible { get; private set; }
        protected Selectable FirstSelected;
        /// <summary>The panels that must fit on screen at any shape (checked by the screens tour).</summary>
        public readonly List<RectTransform> Frames = new List<RectTransform>();

        protected void Init(Transform canvas, string name)
        {
            Root = UiKit.Rect(name, canvas).Fill();
            Group = Root.Group(0f);
            Group.blocksRaycasts = false;
            Group.interactable = false;
            Root.gameObject.SetActive(false);
        }

        public virtual void Show()
        {
            Visible = true;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            Group.blocksRaycasts = true;
            Group.interactable = true;
            Tween.Fade(Group, 1f, 0.45f);
            // With a pad in hand, start on the first item; with a mouse, nothing is highlighted
            // until the pointer or a key asks for it.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(Gamepad.current != null && FirstSelected != null ? FirstSelected.gameObject : null);
        }

        /// <summary>
        /// Pad and keyboard navigation need something selected to move from: the first d-pad,
        /// stick or arrow press on an open menu selects its first item.
        /// </summary>
        protected virtual void LateUpdate()
        {
            if (!Visible || FirstSelected == null || EventSystem.current == null) return;
            var sel = EventSystem.current.currentSelectedGameObject;
            if (sel != null && sel.activeInHierarchy && sel.transform.IsChildOf(Root)) return;
            if (NavPressed()) EventSystem.current.SetSelectedGameObject(FirstSelected.gameObject);
        }

        static bool NavPressed()
        {
            var pad = Gamepad.current;
            if (pad != null && (pad.dpad.ReadValue().sqrMagnitude > 0.25f || pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.buttonSouth.wasPressedThisFrame))
                return true;
            var kb = Keyboard.current;
            return kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame);
        }

        public virtual void Hide(float time = 0.35f)
        {
            Visible = false;
            Group.blocksRaycasts = false;
            Group.interactable = false;
            Tween.Fade(Group, 0f, time, 0f, () => { if (!Visible) Root.gameObject.SetActive(false); });
        }

        protected static Text Label(Transform parent, string text, Font font, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
        {
            var t = UiKit.Text("Label", parent, text, font, size, color, align);
            t.rectTransform.Pin(anchor, anchor, pos, sizeDelta);
            return t;
        }
    }

    // ==================================================================== title

    public sealed class TitleScreen : UiScreen
    {
        public Action OnBegin, OnWatch, OnLogbook, OnNotes, OnSettings, OnQuit;
        readonly List<Text> letters = new List<Text>();
        readonly List<UiButton> items = new List<UiButton>();
        Text tagline, footer;
        UiButton begin, watch;
        RectTransform menu;

        public static TitleScreen Create(Transform canvas)
        {
            var s = canvas.gameObject.AddComponent<TitleScreen>();
            s.Init(canvas, "Title");
            s.Build();
            return s;
        }

        void Build()
        {
            var shade = UiKit.Image("Shade", Root, SpriteFactory.FadeH, new Color(0, 0.01f, 0.02f, 0.82f));
            shade.rectTransform.Stretch(Vector2.zero, new Vector2(0.72f, 1), Vector2.zero, Vector2.zero);

            const string word = "LAST LIGHT";
            float x = 150f;
            var title = UiKit.Rect("TitleWord", Root).Pin(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 190), new Vector2(1200, 200));
            var font = UiKit.Title;
            font.RequestCharactersInTexture(word, 168, FontStyle.Normal);
            foreach (char ch in word)
            {
                var t = UiKit.Text("L", title, ch.ToString(), font, 168, UiKit.Paper, TextAnchor.MiddleLeft).Shadowed(0.85f, 4f);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(160, 200));
                font.GetCharacterInfo(ch, out var info, 168, FontStyle.Normal);
                x += (ch == ' ' ? 46f : info.advance) + 14f;
                letters.Add(t);
            }
            tagline = Label(Root, "Twelve nights at Gannet Head", UiKit.Italic, 32, new Color(0.8f, 0.82f, 0.86f), TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(158, 95), new Vector2(900, 50));
            tagline.Shadowed();
            Frames.Add(title);
            var bar = UiKit.Image("Bar", Root, SpriteFactory.Bar, new Color(UiKit.Brass.r, UiKit.Brass.g, UiKit.Brass.b, 0.8f));
            bar.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, 62), new Vector2(560, 4));

            menu = UiKit.Rect("Menu", Root).Pin(new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(160, 20), new Vector2(560, 360));
            begin = AddItem("Begin the watch", () => OnBegin?.Invoke());
            watch = AddItem("Night Watch", () => OnWatch?.Invoke());
            AddItem("Keeper's logbook", () => OnLogbook?.Invoke());
            AddItem("Keeper's notes", () => OnNotes?.Invoke());
            AddItem("Settings", () => OnSettings?.Invoke());
            AddItem("Quit", () => OnQuit?.Invoke());
            SetWatchUnlocked(false);
            FirstSelected = begin;

            footer = Label(Root, FooterText(), UiKit.Body, 22, new Color(0.65f, 0.7f, 0.76f, 0.85f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(1600, 40));
            footer.Shadowed();
            Frames.Add(menu);
            Frames.Add(footer.rectTransform);
            InputMode.Changed += () => footer.text = FooterText();
        }

        static string FooterText()
        {
            string horn = SaveData.Current.keys.First(KeeperAction.Horn);
            if (horn == "") horn = "Right button";
            return SaveData.Current.focusToggle
                ? InputMode.Pick(
                    $"Mouse  turn the light     ·     Left button  focus on and off     ·     {horn}  foghorn     ·     Esc  pause",
                    "Right stick  turn the light     ·     RT  focus on and off     ·     A  foghorn     ·     Start  pause")
                : InputMode.Pick(
                    $"Mouse  turn the light     ·     Hold left button  focus     ·     {horn}  foghorn     ·     Esc  pause",
                    "Right stick  turn the light     ·     Hold RT  focus     ·     A  foghorn     ·     Start  pause");
        }

        /// <summary>The control strip as shown; for tours.</summary>
        public string FooterShown => footer.text;

        /// <summary>Settings changed the focus mode or the keys: reword the control strip.</summary>
        public void RefreshFooter() => footer.text = FooterText();

        UiButton AddItem(string label, Action click)
        {
            var b = UiButton.Create(menu, label, UiKit.Heading, 46, click);
            items.Add(b);
            return b;
        }

        /// <summary>The Night Watch entry appears once the season is done; the menu closes up around it.</summary>
        public void SetWatchUnlocked(bool on)
        {
            watch.gameObject.SetActive(on);
            int row = 0;
            foreach (var b in items)
            {
                if (!b.gameObject.activeSelf) continue;
                ((RectTransform)b.transform).Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -row * 70), new Vector2(560, 64));
                row++;
            }
        }

        public void SetBeginLabel(string text) => begin.Label.text = text;

        public override void Show()
        {
            base.Show();
            for (int i = 0; i < letters.Count; i++)
            {
                var t = letters[i];
                var rt = t.rectTransform;
                var basePos = rt.anchoredPosition;
                var c = t.color;
                t.color = new Color(c.r, c.g, c.b, 0f);
                float delay = 0.35f + i * 0.09f;
                Tween.Run(t, "reveal", 1.1f, k =>
                {
                    t.color = new Color(c.r, c.g, c.b, k);
                    rt.anchoredPosition = new Vector2(basePos.x, -18f * (1f - k));
                }, delay, Tween.EaseOutCubic);
            }
            var tg = tagline.Group(0f);
            Tween.Fade(tg, 1f, 1.2f, 1.4f);
            var mg = menu.Group(0f);
            Tween.Fade(mg, 1f, 1f, 1.8f);
            var fg = footer.Group(0f);
            Tween.Fade(fg, 1f, 1.2f, 2.4f);
        }
    }

    // ==================================================================== logbook

    public sealed class LogbookScreen : UiScreen
    {
        public Action<int> OnPick;
        public Action OnBack;
        RectTransform page;
        readonly List<RectTransform> entries = new List<RectTransform>();
        Text summary;

        public static LogbookScreen Create(Transform canvas)
        {
            var s = canvas.gameObject.AddComponent<LogbookScreen>();
            s.Init(canvas, "Logbook");
            s.Build();
            return s;
        }

        void Build()
        {
            var dim = UiKit.Image("Dim", Root, null, new Color(0, 0.01f, 0.02f, 0.6f));
            dim.rectTransform.Fill();
            page = UiKit.Rect("Book", Root).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(1320, 960));
            // Leather boards just proud of the pages, then the open spread.
            var cover = UiKit.Image("Cover", page, SpriteFactory.Rounded, new Color(0.2f, 0.09f, 0.06f), true);
            cover.rectTransform.Fill(-18f);
            Frames.Add(cover.rectTransform);
            var coverShadow = cover.gameObject.AddComponent<Shadow>();
            coverShadow.effectColor = new Color(0, 0, 0, 0.55f);
            coverShadow.effectDistance = new Vector2(10, -14);
            var paper = UiKit.Image("Pages", page, SpriteFactory.BookSpread, Color.white);
            paper.rectTransform.Fill();

            // Left page: the title block. Right page: the season's tally.
            Centered(Label(page, "Keeper's Log", UiKit.Title, 62, UiKit.PaperInk, TextAnchor.MiddleCenter, new Vector2(0.25f, 1), new Vector2(0, -44), new Vector2(560, 76)));
            Centered(Label(page, "Gannet Head Light  ·  Merrow Bay", UiKit.Italic, 23, new Color(0.38f, 0.3f, 0.21f), TextAnchor.MiddleCenter, new Vector2(0.25f, 1), new Vector2(0, -118), new Vector2(560, 32)));
            Centered(Label(page, "The last season", UiKit.Title, 40, UiKit.PaperInk, TextAnchor.MiddleCenter, new Vector2(0.75f, 1), new Vector2(0, -56), new Vector2(560, 60)));
            summary = Centered(Label(page, "", UiKit.Italic, 23, new Color(0.38f, 0.3f, 0.21f), TextAnchor.MiddleCenter, new Vector2(0.75f, 1), new Vector2(0, -118), new Vector2(560, 32)));
            for (int side = 0; side < 2; side++)
            {
                var rule = UiKit.Image("Rule", page, SpriteFactory.Bar, new Color(0.35f, 0.26f, 0.16f, 0.65f));
                rule.rectTransform.Pin(new Vector2(side == 0 ? 0.25f : 0.75f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -164), new Vector2(500, 3));
            }
            for (int i = 0; i < 12; i++)
            {
                int col = i / 6, row = i % 6;
                var e = UiKit.Rect("Entry" + i, page).Pin(new Vector2(col == 0 ? 0.25f : 0.75f, 1), new Vector2(0.5f, 1), new Vector2(0, -184 - row * 104), new Vector2(540, 96));
                // A faint ruled line under each entry, as in a ledger.
                var line = UiKit.Image("Ruling", page, null, new Color(0.35f, 0.42f, 0.55f, 0.16f));
                line.rectTransform.Pin(new Vector2(col == 0 ? 0.25f : 0.75f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -184 - row * 104 - 100), new Vector2(520, 2));
                entries.Add(e);
            }
            // The red margin line down each page.
            for (int side = 0; side < 2; side++)
            {
                var margin = UiKit.Image("Margin", page, null, new Color(0.7f, 0.25f, 0.2f, 0.18f));
                margin.rectTransform.Pin(new Vector2(side == 0 ? 0.25f : 0.75f, 1), new Vector2(0.5f, 1), new Vector2(-180, -176), new Vector2(2, 620));
            }
            var back = UiButton.Create(page, "Close the book", UiKit.Heading, 36, () => OnBack?.Invoke(), TextAnchor.MiddleCenter);
            back.Normal = UiKit.PaperInk;
            back.Hover = new Color(0.55f, 0.3f, 0.1f);
            back.Label.GetComponent<Shadow>().enabled = false;
            ((RectTransform)back.transform).Pin(new Vector2(0.75f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(320, 54));
            var hint = Label(page, "Choose a night to keep again", UiKit.Italic, 21, new Color(0.38f, 0.3f, 0.21f, 0.8f), TextAnchor.MiddleCenter, new Vector2(0.25f, 0), new Vector2(0, 52), new Vector2(500, 30));
            hint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        static Text Centered(Text t)
        {
            t.rectTransform.pivot = new Vector2(0.5f, t.rectTransform.pivot.y);
            return t;
        }

        public void Refresh(List<MissionDef> missions, SaveData save)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                foreach (Transform c in e) Destroy(c.gameObject);
                bool open = i < missions.Count && i + 1 <= save.unlocked;
                var m = i < missions.Count ? missions[i] : null;
                var ink = open ? UiKit.PaperInk : new Color(0.4f, 0.33f, 0.25f, 0.7f);
                var b = UiButton.Create(e, "", UiKit.Heading, 30, null);
                ((RectTransform)b.transform).Fill();
                b.Slide = 8f;
                b.Label.enabled = false;
                b.Marker.rectTransform.anchoredPosition = new Vector2(-14, 0);
                b.Marker.color = new Color(0.9f, 0.6f, 0.2f, 0f);
                int night = i + 1;
                b.OnClick = () => OnPick?.Invoke(night);
                b.SetInteractable(open);
                if (open) FirstSelected = b;     // the latest night open
                var content = b.transform.Find("Content");
                var num = Label(content, UiKit.Roman(night), UiKit.Title, 42, open ? new Color(0.5f, 0.22f, 0.1f) : ink, TextAnchor.MiddleCenter, new Vector2(0, 0.5f), new Vector2(44, 0), new Vector2(90, 70));
                num.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                if (open && m != null)
                {
                    var title = Label(content, m.title, UiKit.Heading, 34, ink, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(104, 13), new Vector2(300, 44));
                    title.rectTransform.pivot = new Vector2(0, 0.5f);
                    var date = Label(content, m.date, UiKit.Italic, 19, new Color(ink.r, ink.g, ink.b, 0.7f), TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(105, -21), new Vector2(300, 28));
                    date.rectTransform.pivot = new Vector2(0, 0.5f);
                    bool played = save.lamps[i] > 0 || save.best[i] > 0;
                    for (int k = 0; k < 3; k++)
                    {
                        bool lit = save.lamps[i] > k;
                        var lamp = UiKit.Image("Lamp", content, lit ? SpriteFactory.Lamp : SpriteFactory.LampEmpty, lit ? new Color(0.78f, 0.5f, 0.12f) : new Color(0.42f, 0.35f, 0.27f, played ? 0.55f : 0.35f));
                        lamp.rectTransform.Pin(new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-96 + k * 32, 10), new Vector2(34, 34));
                    }
                    if (save.best[i] > 0)
                        Label(content, save.best[i].ToString("N0"), UiKit.BodyMedium, 19, new Color(0.35f, 0.28f, 0.2f), TextAnchor.MiddleCenter, new Vector2(1, 0.5f), new Vector2(-64, -22), new Vector2(120, 26));
                    else
                        Label(content, "not yet kept", UiKit.Italic, 18, new Color(0.38f, 0.3f, 0.21f, 0.75f), TextAnchor.MiddleCenter, new Vector2(1, 0.5f), new Vector2(-64, -22), new Vector2(140, 26));
                }
                else
                {
                    var title = Label(content, "sealed", UiKit.Italic, 28, ink, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(104, 0), new Vector2(300, 44));
                    title.rectTransform.pivot = new Vector2(0, 0.5f);
                    var seal = UiKit.Image("Seal", content, SpriteFactory.WaxSeal, new Color(1f, 1f, 1f, 0.92f));
                    seal.rectTransform.Pin(new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-64, 0), new Vector2(58, 58));
                    seal.rectTransform.localEulerAngles = new Vector3(0, 0, (i * 37) % 40 - 20);
                }
            }
            summary.text = $"{save.TotalLamps} of 36 lamps lit  ·  {save.shipsHome} ships brought home";
        }

        public override void Show()
        {
            base.Show();
            page.localScale = Vector3.one * 0.96f;
            Tween.Scale(page, 0.96f, 1f, 0.5f, 0f, Tween.EaseOutBack);
            Sfx.Play("ui_page", 0.6f);
        }
    }

    // ==================================================================== pause

    public sealed class PauseScreen : UiScreen
    {
        public Action OnResume, OnRestart, OnEndWatch, OnSettings, OnNotes, OnLogbook, OnTitle;
        readonly List<UiButton> buttons = new List<UiButton>();
        bool watch;
        Text controls;
        RectTransform logPanel;
        Text logText;
        // The calls' area at most: the panel hangs from beside the title and stops short of the
        // HUD's radio panel, even at 130% HUD text.
        const float LogHeight = 500f;

        /// <summary>How many of the night's calls the log is showing; for tours.</summary>
        public int LogShown { get; private set; }
        public string LogNewest { get; private set; }

        public static PauseScreen Create(Transform canvas)
        {
            var s = canvas.gameObject.AddComponent<PauseScreen>();
            s.Init(canvas, "Pause");
            s.Build();
            return s;
        }

        void Build()
        {
            var dim = UiKit.Image("Dim", Root, null, new Color(0, 0.01f, 0.02f, 0.5f));
            dim.rectTransform.Fill();
            // A pool of shadow behind the menu, so it reads even over the beam.
            var pool = UiKit.Image("Pool", Root, SpriteFactory.Glow, new Color(0, 0.008f, 0.016f, 0.9f));
            pool.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1500, 1100));
            var core = UiKit.Image("PoolCore", Root, SpriteFactory.Glow, new Color(0, 0.008f, 0.016f, 0.8f));
            core.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1000, 860));
            Label(Root, "The light burns on", UiKit.Italic, 28, UiKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(800, 40));
            Label(Root, "Paused", UiKit.Title, 96, UiKit.Paper, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 175), new Vector2(800, 120)).Shadowed(0.8f, 3f);
            // Restarting, leaving or ending throws the night away (or ends a watch), so those ask first.
            var items = new (string, Action)[]
            {
                ("Resume", () => OnResume?.Invoke()),
                ("Restart the night", () =>
                {
                    if (watch) Ask(1, "End the watch now?", "It will be kept and ranked, as if the last wreck had ended it.", ("End the watch", OnEndWatch));
                    else Ask(1, "Start the night again?", "Tonight so far won't be kept.", ("Restart the night", OnRestart));
                }),
                ("Settings", () => OnSettings?.Invoke()),
                ("Keeper's notes", () => OnNotes?.Invoke()),
                ("Keeper's logbook", () => AskLeave(4, "Open the logbook", OnLogbook)),
                ("Leave the lighthouse", () => AskLeave(5, "Leave the lighthouse", OnTitle)),
            };
            for (int i = 0; i < items.Length; i++)
            {
                var (label, act) = items[i];
                var b = UiButton.Create(Root, label, UiKit.Heading, 42, act, TextAnchor.MiddleCenter);
                ((RectTransform)b.transform).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60 - i * 66), new Vector2(520, 60));
                if (i == 0) FirstSelected = b;
                buttons.Add(b);
                Frames.Add((RectTransform)b.transform);
            }

            // The question, in the menu's place: what will happen, with Stay first.
            confirm = UiKit.Rect("Confirm", Root).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(900, 330));
            confirmHeading = Label(confirm, "", UiKit.Heading, 44, UiKit.Paper, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(900, 60)).Shadowed(0.8f, 2f);
            confirmBody = Label(confirm, "", UiKit.Italic, 27, UiKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(900, 40));
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var b = UiButton.Create(confirm, "", UiKit.Heading, 40, () => confirmActions[index]?.Invoke(), TextAnchor.MiddleCenter);
                ((RectTransform)b.transform).Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -160 - i * 62), new Vector2(560, 58));
                confirmButtons.Add(b);
            }
            confirm.gameObject.SetActive(false);
            Frames.Add(confirm);

            // The controls, for a keeper who forgot: a card left of the menu, mirroring the radio log.
            var card = UiKit.Rect("Controls", Root).Pin(new Vector2(0.5f, 0.5f), new Vector2(1, 1), new Vector2(-340, 290), new Vector2(500, 200));
            var cardBg = UiKit.Image("Bg", card, SpriteFactory.Rounded, new Color(0.03f, 0.05f, 0.07f, 0.78f), true);
            cardBg.rectTransform.Fill();
            var cardEdge = UiKit.Image("Edge", card, SpriteFactory.Bar, new Color(UiKit.Brass.r, UiKit.Brass.g, UiKit.Brass.b, 0.5f));
            cardEdge.rectTransform.Stretch(new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -3), new Vector2(-20, 1));
            var cardCaption = UiKit.Text("Caption", card, UiKit.Spaced("CONTROLS"), UiKit.BodyBold, 18, UiKit.Brass, TextAnchor.MiddleLeft);
            cardCaption.rectTransform.Stretch(new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -50), new Vector2(-20, -16));
            controls = UiKit.Text("Lines", card, "", UiKit.BodyMedium, 24, UiKit.Paper, TextAnchor.UpperLeft);
            controls.rectTransform.Stretch(Vector2.zero, Vector2.one, new Vector2(26, 14), new Vector2(-20, -58));
            controls.lineSpacing = 1.15f;
            InputMode.Changed += () => controls.text = ControlLines();
            Frames.Add(card);

            // The radio log: the night's latest calls, newest at the bottom, beside the menu.
            logPanel = UiKit.Rect("RadioLog", Root).Pin(new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(340, 290), new Vector2(560, LogHeight + 78));
            var bg = UiKit.Image("Bg", logPanel, SpriteFactory.Rounded, new Color(0.03f, 0.05f, 0.07f, 0.78f), true);
            bg.rectTransform.Fill();
            var edge = UiKit.Image("Edge", logPanel, SpriteFactory.Bar, new Color(UiKit.Brass.r, UiKit.Brass.g, UiKit.Brass.b, 0.5f));
            edge.rectTransform.Stretch(new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -3), new Vector2(-20, 1));
            var caption = UiKit.Text("Caption", logPanel, UiKit.Spaced("ON THE RADIO TONIGHT"), UiKit.BodyBold, 18, UiKit.Brass, TextAnchor.MiddleLeft);
            caption.rectTransform.Stretch(new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -50), new Vector2(-20, -16));
            logText = UiKit.Text("Calls", logPanel, "", UiKit.Radio, 21, UiKit.Paper, TextAnchor.UpperLeft);
            logText.rectTransform.Stretch(Vector2.zero, Vector2.one, new Vector2(26, 18), new Vector2(-24, -56));
            logText.lineSpacing = 1.08f;
            logText.verticalOverflow = VerticalWrapMode.Overflow;
            logPanel.gameObject.SetActive(false);
            Frames.Add(logPanel);
        }

        RectTransform confirm;
        Text confirmHeading, confirmBody;
        readonly List<UiButton> confirmButtons = new List<UiButton>();
        readonly Action[] confirmActions = new Action[3];
        int confirmFrom = -1;

        /// <summary>The menu is asking whether to go ahead.</summary>
        public bool Confirming => confirmFrom >= 0;

        /// <summary>Leaving a night loses it; leaving a watch loses it too, but ending it keeps it.</summary>
        void AskLeave(int from, string label, Action act)
        {
            if (watch) Ask(from, "Leave without keeping the watch?", "It won't be kept or ranked. End the watch keeps it.", ("End the watch", OnEndWatch), ("Leave anyway", act));
            else Ask(from, "Leave tonight's watch?", "The night won't be kept.", (label, act));
        }

        /// <summary>Swap the menu for a question. Stay comes first and is selected, so a second
        /// press of the same button changes nothing.</summary>
        void Ask(int from, string heading, string body, params (string label, Action act)[] choices)
        {
            confirmFrom = from;
            confirmHeading.text = heading;
            confirmBody.text = body;
            confirmActions[0] = () => Cancel();
            confirmButtons[0].Label.text = "Stay";
            confirmButtons[0].name = "Button Stay";
            for (int i = 1; i < confirmButtons.Count; i++)
            {
                bool used = i - 1 < choices.Length;
                confirmButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                var act = choices[i - 1].act;
                confirmButtons[i].Label.text = choices[i - 1].label;
                confirmButtons[i].name = "Button " + choices[i - 1].label;
                confirmActions[i] = () => act?.Invoke();
            }
            foreach (var b in buttons) b.gameObject.SetActive(false);
            confirm.gameObject.SetActive(true);
            Tween.Fade(confirm.Group(0f), 1f, 0.25f);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(confirmButtons[0].gameObject);
            Sfx.Play("ui_page", 0.4f, 1.1f);
        }

        /// <summary>Back to the menu, on the item that asked.</summary>
        public void Cancel(bool select = true)
        {
            if (!Confirming) return;
            int from = confirmFrom;
            confirmFrom = -1;
            confirm.gameObject.SetActive(false);
            foreach (var b in buttons) b.gameObject.SetActive(true);
            if (select && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(buttons[from].gameObject);
        }

        /// <summary>The question's heading and its choices as shown; for tours.</summary>
        public string ConfirmHeading => Confirming ? confirmHeading.text : "";
        public string ConfirmLabel(int i) => Confirming && confirmButtons[i].gameObject.activeSelf ? confirmButtons[i].Label.text : "";

        /// <summary>Choose one of the question's answers as a click would (0 is Stay); for tours.</summary>
        public void ConfirmChoose(int i) { if (Confirming) confirmButtons[i].OnClick?.Invoke(); }

        /// <summary>A Night Watch can't be restarted, only ended (and kept); a night only restarted.</summary>
        public void SetWatch(bool on)
        {
            watch = on;
            buttons[1].Label.text = on ? "End the watch" : "Restart the night";
        }

        public override void Show()
        {
            Cancel(false);
            controls.text = ControlLines();   // the Focus setting may have changed
            base.Show();
        }

        /// <summary>The controls for the device in use and the Focus setting, a line each.</summary>
        static string ControlLines()
        {
            bool toggle = SaveData.Current.focusToggle;
            string Line(string keys, string what) => $"<color=#C9A35A>{keys}</color>   {what}";
            var bound = SaveData.Current.keys;
            string turn = bound.TurnPair(), focus = bound.First(KeeperAction.Focus), horn = bound.First(KeeperAction.Horn);
            string focusKeys = (toggle ? "Left button" : "Hold left button") + (focus != "" ? " or " + focus : "");
            var lines = InputMode.Pad
                ? new[] { Line("Right stick", "turn the light"), Line(toggle ? "RT" : "Hold RT", toggle ? "focus on and off" : "focus"), Line("A", "foghorn"), Line("Start", "pause  ·  B  back") }
                : new[] { Line(turn != "" ? "Mouse, or " + turn : "Mouse", "turn the light"), Line(focusKeys, toggle ? "focus on and off" : "focus"), Line(horn != "" ? horn + " or right button" : "Right button", "foghorn"), Line("Esc", "pause") };
            return string.Join("\n", lines);
        }

        /// <summary>The controls as shown; for tours.</summary>
        public string ControlsShown => controls.text;

        /// <summary>The menu's labels, top to bottom; for tours.</summary>
        public string ItemLabel(int i) => buttons[i].Label.text;

        /// <summary>Choose a menu item as a click would; for tours.</summary>
        public void Choose(int i) => buttons[i].OnClick?.Invoke();

        /// <summary>Fill the log from the radio: as many of the latest calls as fit, oldest first.</summary>
        public void SetRadioLog(RadioLog log)
        {
            LogShown = 0;
            LogNewest = null;
            if (log == null || log.Count == 0) { logPanel.gameObject.SetActive(false); return; }
            logPanel.gameObject.SetActive(true);
            string body = "";
            for (int i = log.Count - 1; i >= 0 && LogShown < MaxLog; i--)
            {
                var e = log.Entries[i];
                string who = string.IsNullOrEmpty(e.Title) || e.Title == e.Name ? e.Name : $"{e.Name}  <size=17><i>{e.Title}</i></size>";
                string entry = $"<color=#C9A35A><size=18>{who}</size></color>\n{e.Text}";
                string next = LogShown == 0 ? entry : entry + "\n\n" + body;
                logText.text = next;
                if (LogShown > 0 && logText.preferredHeight > LogHeight) break;
                body = next;
                LogShown++;
                if (LogShown == 1) LogNewest = e.Text;
            }
            logText.text = body;
            logPanel.sizeDelta = new Vector2(560, Mathf.Min(LogHeight, logText.preferredHeight) + 78f);
        }

        const int MaxLog = 6;
    }
}
