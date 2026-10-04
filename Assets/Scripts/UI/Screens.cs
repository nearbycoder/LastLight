using System;
using System.Collections.Generic;
using LastLight.Audio;
using LastLight.Core;
using LastLight.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
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
            if (FirstSelected != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
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
        public Action OnBegin, OnLogbook, OnSettings, OnQuit;
        readonly List<Text> letters = new List<Text>();
        Text tagline, footer;
        UiButton begin;
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
            var bar = UiKit.Image("Bar", Root, SpriteFactory.Bar, new Color(UiKit.Brass.r, UiKit.Brass.g, UiKit.Brass.b, 0.8f));
            bar.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, 62), new Vector2(560, 4));

            menu = UiKit.Rect("Menu", Root).Pin(new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(160, 20), new Vector2(560, 360));
            begin = AddItem("Begin the watch", () => OnBegin?.Invoke(), 0);
            AddItem("Keeper's logbook", () => OnLogbook?.Invoke(), 1);
            AddItem("Settings", () => OnSettings?.Invoke(), 2);
            AddItem("Quit", () => OnQuit?.Invoke(), 3);
            FirstSelected = begin;

            footer = Label(Root, "Mouse  turn the light     ·     Hold left button  focus     ·     Space  foghorn     ·     Esc  pause", UiKit.Body, 22, new Color(0.65f, 0.7f, 0.76f, 0.85f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(1600, 40));
            footer.Shadowed();
        }

        UiButton AddItem(string label, Action click, int i)
        {
            var b = UiButton.Create(menu, label, UiKit.Heading, 46, click);
            ((RectTransform)b.transform).Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * 70), new Vector2(560, 64));
            return b;
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
            var dim = UiKit.Image("Dim", Root, null, new Color(0, 0.01f, 0.02f, 0.55f));
            dim.rectTransform.Fill();
            page = UiKit.Rect("Page", Root).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1240, 900));
            var paper = UiKit.Image("Paper", page, SpriteFactory.Paper, Color.white, true);
            paper.rectTransform.Fill();
            var shadow = paper.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(8, -10);
            Label(page, "Keeper's Log", UiKit.Title, 64, UiKit.PaperInk, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(900, 80));
            Label(page, "Gannet Head Light  ·  Merrow Bay  ·  the last season", UiKit.Italic, 24, new Color(0.35f, 0.28f, 0.2f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -138), new Vector2(900, 34));
            var rule = UiKit.Image("Rule", page, SpriteFactory.Bar, new Color(0.35f, 0.26f, 0.16f, 0.7f));
            rule.rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -168), new Vector2(980, 3));
            for (int i = 0; i < 12; i++)
            {
                int col = i / 6, row = i % 6;
                var e = UiKit.Rect("Entry" + i, page).Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(col == 0 ? -282 : 282, -192 - row * 100), new Vector2(540, 92));
                entries.Add(e);
            }
            summary = Label(page, "", UiKit.BodyMedium, 24, new Color(0.3f, 0.24f, 0.17f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 92), new Vector2(1000, 36));
            var back = UiButton.Create(page, "Back", UiKit.Heading, 38, () => OnBack?.Invoke(), TextAnchor.MiddleCenter);
            back.Normal = UiKit.PaperInk;
            back.Hover = new Color(0.55f, 0.3f, 0.1f);
            back.Label.GetComponent<Shadow>().enabled = false;
            ((RectTransform)back.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(240, 54));
        }

        public void Refresh(List<MissionDef> missions, SaveData save)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                foreach (Transform c in e) Destroy(c.gameObject);
                bool open = i < missions.Count && i + 1 <= save.unlocked;
                var m = i < missions.Count ? missions[i] : null;
                var ink = open ? UiKit.PaperInk : new Color(0.45f, 0.38f, 0.3f, 0.55f);
                var b = UiButton.Create(e, "", UiKit.Heading, 30, null);
                ((RectTransform)b.transform).Fill();
                b.Slide = 8f;
                b.Label.enabled = false;
                b.Marker.rectTransform.anchoredPosition = new Vector2(-14, 0);
                b.Marker.color = new Color(0.9f, 0.6f, 0.2f, 0f);
                int night = i + 1;
                b.OnClick = () => OnPick?.Invoke(night);
                b.SetInteractable(open);
                var content = b.transform.Find("Content");
                var num = Label(content, UiKit.Roman(night), UiKit.Title, 42, open ? new Color(0.45f, 0.25f, 0.1f) : ink, TextAnchor.MiddleCenter, new Vector2(0, 0.5f), new Vector2(44, 0), new Vector2(90, 70));
                num.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var title = Label(content, open && m != null ? m.title : "sealed", UiKit.Heading, 34, ink, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(100, 13), new Vector2(300, 44));
                title.rectTransform.pivot = new Vector2(0, 0.5f);
                var date = Label(content, open && m != null ? m.date : "", UiKit.Italic, 19, new Color(ink.r, ink.g, ink.b, ink.a * 0.75f), TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(101, -21), new Vector2(300, 28));
                date.rectTransform.pivot = new Vector2(0, 0.5f);
                for (int k = 0; k < 3; k++)
                {
                    bool lit = save.lamps[i] > k;
                    var lamp = UiKit.Image("Lamp", content, lit ? SpriteFactory.Lamp : SpriteFactory.LampEmpty, lit ? new Color(0.78f, 0.5f, 0.12f) : new Color(0.45f, 0.38f, 0.3f, open ? 0.5f : 0.25f));
                    lamp.rectTransform.Pin(new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-96 + k * 32, 10), new Vector2(34, 34));
                }
                if (save.best[i] > 0)
                    Label(content, save.best[i].ToString("N0"), UiKit.BodyMedium, 19, new Color(0.35f, 0.28f, 0.2f), TextAnchor.MiddleCenter, new Vector2(1, 0.5f), new Vector2(-64, -22), new Vector2(120, 26));
                _ = num; _ = title; _ = date;
            }
            summary.text = $"Lamps lit  {save.TotalLamps} / 36          Ships brought home  {save.shipsHome}";
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
        public Action OnResume, OnRestart, OnSettings, OnLogbook, OnTitle;

        public static PauseScreen Create(Transform canvas)
        {
            var s = canvas.gameObject.AddComponent<PauseScreen>();
            s.Init(canvas, "Pause");
            s.Build();
            return s;
        }

        void Build()
        {
            var dim = UiKit.Image("Dim", Root, null, new Color(0, 0.01f, 0.02f, 0.62f));
            dim.rectTransform.Fill();
            Label(Root, "The light burns on", UiKit.Italic, 28, UiKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(800, 40));
            Label(Root, "Paused", UiKit.Title, 96, UiKit.Paper, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 175), new Vector2(800, 120)).Shadowed(0.8f, 3f);
            var items = new (string, Action)[]
            {
                ("Resume", () => OnResume?.Invoke()),
                ("Restart the night", () => OnRestart?.Invoke()),
                ("Settings", () => OnSettings?.Invoke()),
                ("Keeper's logbook", () => OnLogbook?.Invoke()),
                ("Leave the lighthouse", () => OnTitle?.Invoke()),
            };
            for (int i = 0; i < items.Length; i++)
            {
                var (label, act) = items[i];
                var b = UiButton.Create(Root, label, UiKit.Heading, 42, act, TextAnchor.MiddleCenter);
                ((RectTransform)b.transform).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60 - i * 66), new Vector2(520, 60));
                if (i == 0) FirstSelected = b;
            }
        }
    }
}
