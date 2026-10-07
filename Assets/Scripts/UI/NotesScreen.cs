using System;
using System.Collections.Generic;
using LastLight.Audio;
using LastLight.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>
    /// The keeper's notes: the rules the season has taught so far, in a book like the logbook.
    /// Topics down the left page, the chosen one on the right. Opened from the title and from the
    /// pause menu (the night waits); Esc, B or "Close the book" goes back.
    /// </summary>
    public sealed class NotesScreen : UiScreen
    {
        public Action OnBack;
        RectTransform page, topicList;
        Text entryTitle, entryBody, sealedNote;
        UiButton close;
        readonly List<UiButton> topics = new List<UiButton>();
        List<KeeperNotes.Entry> entries = new List<KeeperNotes.Entry>();
        int shown = -1;
        SaveData save;

        static readonly Color Ink = UiKit.PaperInk;
        static readonly Color InkSoft = new Color(0.38f, 0.3f, 0.21f);
        static readonly Color InkRed = new Color(0.5f, 0.22f, 0.1f);
        const float TopicStep = 42f;

        public static NotesScreen Create(Transform canvas)
        {
            var s = canvas.gameObject.AddComponent<NotesScreen>();
            s.Init(canvas, "Notes");
            s.Build();
            return s;
        }

        void Build()
        {
            var dim = UiKit.Image("Dim", Root, null, new Color(0, 0.01f, 0.02f, 0.6f));
            dim.rectTransform.Fill();
            page = UiKit.Rect("Book", Root).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(1320, 960));
            var cover = UiKit.Image("Cover", page, SpriteFactory.Rounded, new Color(0.12f, 0.15f, 0.2f), true);
            cover.rectTransform.Fill(-18f);
            Frames.Add(cover.rectTransform);
            var coverShadow = cover.gameObject.AddComponent<Shadow>();
            coverShadow.effectColor = new Color(0, 0, 0, 0.55f);
            coverShadow.effectDistance = new Vector2(10, -14);
            var paper = UiKit.Image("Pages", page, SpriteFactory.BookSpread, Color.white);
            paper.rectTransform.Fill();

            Centered(Label(page, "Keeper's Notes", UiKit.Title, 62, Ink, TextAnchor.MiddleCenter, new Vector2(0.25f, 1), new Vector2(0, -44), new Vector2(560, 76)));
            Centered(Label(page, "What the light has taught", UiKit.Italic, 23, InkSoft, TextAnchor.MiddleCenter, new Vector2(0.25f, 1), new Vector2(0, -118), new Vector2(560, 32)));
            for (int side = 0; side < 2; side++)
            {
                var rule = UiKit.Image("Rule", page, SpriteFactory.Bar, new Color(0.35f, 0.26f, 0.16f, 0.65f));
                rule.rectTransform.Pin(new Vector2(side == 0 ? 0.25f : 0.75f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -164), new Vector2(500, 3));
            }
            topicList = UiKit.Rect("Topics", page).Pin(new Vector2(0.25f, 1), new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(480, 700));
            sealedNote = Centered(Label(page, "", UiKit.Italic, 21, new Color(InkSoft.r, InkSoft.g, InkSoft.b, 0.8f), TextAnchor.MiddleCenter, new Vector2(0.25f, 0), new Vector2(0, 46), new Vector2(500, 30)));
            sealedNote.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            entryTitle = Centered(Label(page, "", UiKit.Title, 46, Ink, TextAnchor.MiddleCenter, new Vector2(0.75f, 1), new Vector2(0, -50), new Vector2(560, 64)));
            entryBody = UiKit.Text("Body", page, "", UiKit.Body, 27, Ink, TextAnchor.UpperLeft);
            entryBody.rectTransform.Pin(new Vector2(0.75f, 1), new Vector2(0.5f, 1), new Vector2(0, -192), new Vector2(520, 640));
            entryBody.lineSpacing = 1.12f;
            entryBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            entryBody.verticalOverflow = VerticalWrapMode.Overflow;

            close = UiButton.Create(page, "Close the book", UiKit.Heading, 36, () => OnBack?.Invoke(), TextAnchor.MiddleCenter);
            close.Normal = Ink;
            close.Hover = new Color(0.55f, 0.3f, 0.1f);
            close.Label.GetComponent<Shadow>().enabled = false;
            ((RectTransform)close.transform).Pin(new Vector2(0.75f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(320, 54));
            InputMode.Changed += Reword;
        }

        void OnDestroy() => InputMode.Changed -= Reword;

        /// <summary>The keeper picked up the other device with the book open: the same entries in
        /// its words, without rebuilding the topics (the selection stays where it is).</summary>
        void Reword()
        {
            if (!Visible || save == null) return;
            var fresh = KeeperNotes.For(save, InputMode.Pad);
            if (fresh.Count != entries.Count) return;
            entries = fresh;
            if (shown >= 0) entryBody.text = entries[shown].Body;
        }

        static Text Centered(Text t)
        {
            t.rectTransform.pivot = new Vector2(0.5f, t.rectTransform.pivot.y);
            return t;
        }

        /// <summary>Fill the book for the save and the device in use, opening on <paramref name="first"/>
        /// (an entry id) when it's there.</summary>
        public void Refresh(SaveData save, string first = null)
        {
            this.save = save;
            entries = KeeperNotes.For(save, InputMode.Pad);
            foreach (var b in topics) Destroy(b.gameObject);
            topics.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                int index = i;
                var b = UiButton.Create(topicList, entries[i].Title, UiKit.Heading, 30, () => Show(index));
                b.Normal = Ink;
                b.Hover = InkRed;
                b.Slide = 8f;
                b.Label.GetComponent<Shadow>().enabled = false;
                b.Marker.color = new Color(0.9f, 0.6f, 0.2f, 0f);
                b.OnHighlight = () => Show(index);
                ((RectTransform)b.transform).Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -i * TopicStep), new Vector2(440, TopicStep - 2f));
                topics.Add(b);
            }
            // Down the topics, then to Close; up from Close goes back to the last topic.
            for (int i = 0; i < topics.Count; i++)
                topics[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? topics[i - 1] : close,
                    selectOnDown = i < topics.Count - 1 ? topics[i + 1] : close,
                    selectOnRight = close,
                };
            close.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = topics.Count > 0 ? topics[topics.Count - 1] : null,
                selectOnDown = topics.Count > 0 ? topics[0] : null,
                selectOnLeft = topics.Count > 0 ? topics[topics.Count - 1] : null,
            };
            int held = KeeperNotes.Sealed(save);
            sealedNote.text = held > 0 ? "More notes as the season goes on" : "";
            int start = Mathf.Max(0, entries.FindIndex(e => e.Id == first));
            FirstSelected = topics.Count > 0 ? topics[start] : (Selectable)close;
            shown = -1;
            Show(start);
        }

        void Show(int index)
        {
            if (index < 0 || index >= entries.Count) return;
            if (index != shown && shown >= 0) Sfx.Play("ui_page", 0.25f, 1.25f);
            shown = index;
            entryTitle.text = entries[index].Title;
            entryBody.text = entries[index].Body;
            for (int i = 0; i < topics.Count; i++)
            {
                topics[i].Normal = i == index ? InkRed : Ink;
                topics[i].Label.fontStyle = i == index ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        public override void Show()
        {
            base.Show();
            page.localScale = Vector3.one * 0.96f;
            Tween.Scale(page, 0.96f, 1f, 0.5f, 0f, Tween.EaseOutBack);
            Sfx.Play("ui_page", 0.6f);
        }

        // ---------------------------------------------------------------- for tours

        public int TopicCount => topics.Count;
        public string TopicTitle(int i) => entries[i].Title;
        public string ShownId => shown >= 0 ? entries[shown].Id : null;
        public string ShownBody => entryBody.text;
        public bool SealedNoteShowing => sealedNote.text != "";

        /// <summary>The entry body's height against the room on its page (for the screens tour).</summary>
        public (float needed, float room) BodyFit => (entryBody.preferredHeight, entryBody.rectTransform.rect.height);
        public RectTransform BodyRect => entryBody.rectTransform;
        public RectTransform TopicsRect => topicList;
        public float TopicsHeight => topics.Count * TopicStep;
    }
}
