using System;
using LastLight.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>
    /// A text menu item: brightens and slides on hover with a brass lamp marker, punches and
    /// clicks when pressed. Keyboard/gamepad selection uses the same visuals.
    /// </summary>
    public sealed class UiButton : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public Action OnClick;
        /// <summary>The pointer came over it, or keys or a pad selected it.</summary>
        public Action OnHighlight;
        public Text Label;
        public Image Marker;
        public Color Normal = UiKit.Paper;
        public Color Hover = UiKit.BrassBright;
        public Color Disabled = new Color(0.45f, 0.48f, 0.52f, 0.6f);
        public float Slide = 14f;
        RectTransform content;
        bool hovered;
        float hoverT;

        public static UiButton Create(Transform parent, string label, Font font, int size, Action onClick, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = UiKit.Rect("Button " + label, parent);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            hit.raycastTarget = true;
            var b = rt.gameObject.AddComponent<UiButton>();
            b.targetGraphic = hit;
            b.transition = Transition.None;
            b.content = UiKit.Rect("Content", rt).Fill();
            b.Label = UiKit.Text("Label", b.content, label, font, size, UiKit.Paper, align);
            b.Label.rectTransform.Fill();
            b.Label.Shadowed(0.7f, 2f);
            b.Marker = UiKit.Image("Marker", b.content, SpriteFactory.Glow, new Color(1f, 0.85f, 0.55f, 0f));
            float mx = align == TextAnchor.MiddleCenter ? 0 : -22f;
            b.Marker.rectTransform.Pin(new Vector2(align == TextAnchor.MiddleCenter ? 0.5f : 0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(mx, 0), new Vector2(26, 26));
            if (align == TextAnchor.MiddleCenter) b.Slide = 0f;
            b.OnClick = onClick;
            var nav = b.navigation;
            nav.mode = Navigation.Mode.Vertical;
            b.navigation = nav;
            return b;
        }

        public void SetInteractable(bool on)
        {
            interactable = on;
            Label.color = on ? Normal : Disabled;
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (!IsInteractable()) return;
            if (!hovered) Sfx.Play("ui_hover", 0.35f, UnityEngine.Random.Range(0.96f, 1.04f));
            hovered = true;
            OnHighlight?.Invoke();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            hovered = false;
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            if (!(eventData is PointerEventData) && IsInteractable())
            {
                if (!hovered) Sfx.Play("ui_hover", 0.35f);
                hovered = true;
                OnHighlight?.Invoke();
            }
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            hovered = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Press();
        }

        public void OnSubmit(BaseEventData eventData) => Press();

        void Press()
        {
            if (!IsInteractable()) return;
            Sfx.Play("ui_click", 0.6f);
            Tween.Punch(content, 0.08f, 0.25f);
            OnClick?.Invoke();
        }

        void Update()
        {
            if (content == null) return;
            hoverT = Mathf.MoveTowards(hoverT, hovered && IsInteractable() ? 1f : 0f, Unscaled.Delta * 7f);
            float e = Tween.EaseOutCubic(hoverT);
            content.anchoredPosition = new Vector2(Slide * e, 0);
            if (IsInteractable()) Label.color = Color.Lerp(Normal, Hover, e);
            var mc = Marker.color;
            mc.a = e;
            Marker.color = mc;
            Marker.rectTransform.localScale = Vector3.one * (0.7f + 0.3f * e + 0.06f * Mathf.Sin(Unscaled.Time * 5f) * e);
        }
    }
}
