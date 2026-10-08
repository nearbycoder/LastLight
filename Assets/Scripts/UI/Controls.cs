using System;
using LastLight.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>A brass slider: drag or click the track; left/right with keys or pad.</summary>
    public sealed class UiSlider : Selectable, IDragHandler, IPointerDownHandler
    {
        public Action<float> Changed;
        /// <summary>How far one press of left or right moves it.</summary>
        public float Step = 0.05f;
        public Color KnobIdle = UiKit.Paper, KnobHot = UiKit.BrassBright;
        RectTransform track;
        Image fill, knob;
        float value;
        float lastTick;

        public float Value
        {
            get => value;
            set { this.value = Mathf.Clamp01(value); Refresh(); }
        }

        public static UiSlider Create(Transform parent, float initial, Action<float> changed)
        {
            var rt = UiKit.Rect("Slider", parent);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var s = rt.gameObject.AddComponent<UiSlider>();
            s.targetGraphic = hit;
            s.transition = Transition.None;
            s.track = UiKit.Rect("Track", rt);
            s.track.Stretch(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(12, -3), new Vector2(-12, 3));
            UiKit.Image("Bg", s.track, SpriteFactory.Pill, new Color(1, 1, 1, 0.15f), true).rectTransform.Fill();
            s.fill = UiKit.Image("Fill", s.track, SpriteFactory.Pill, UiKit.Brass, true);
            s.knob = UiKit.Image("Knob", s.track, SpriteFactory.Disc, UiKit.Paper);
            s.knob.rectTransform.sizeDelta = new Vector2(22, 22);
            s.value = initial;
            s.Changed = changed;
            s.Refresh();
            return s;
        }

        void Refresh()
        {
            if (fill == null) return;
            fill.rectTransform.anchorMin = new Vector2(0, 0);
            fill.rectTransform.anchorMax = new Vector2(value, 1);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(value, 0.5f);
            knob.rectTransform.anchoredPosition = Vector2.zero;
        }

        void SetFromPointer(PointerEventData e)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out var local))
            {
                var r = track.rect;
                Value = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
                Changed?.Invoke(value);
                if (Unscaled.Time - lastTick > 0.06f) { lastTick = Unscaled.Time; Sfx.Play("ui_tick", 0.3f, 0.8f + value * 0.5f, 0, Bus.Ui, 0f); }
            }
        }

        public void OnDrag(PointerEventData e) => SetFromPointer(e);

        public override void OnPointerDown(PointerEventData e)
        {
            base.OnPointerDown(e);
            SetFromPointer(e);
        }

        public override void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left || e.moveDir == MoveDirection.Right)
            {
                Value += e.moveDir == MoveDirection.Left ? -Step : Step;
                Changed?.Invoke(value);
                Sfx.Play("ui_tick", 0.3f, 0.8f + value * 0.5f, 0, Bus.Ui, 0f);
                return;
            }
            base.OnMove(e);
        }

        void Update()
        {
            if (knob == null) return;
            bool hot = currentSelectionState == SelectionState.Highlighted || currentSelectionState == SelectionState.Selected || currentSelectionState == SelectionState.Pressed;
            knob.color = Color.Lerp(knob.color, hot ? KnobHot : KnobIdle, Unscaled.Delta * 10f);
        }
    }

    /// <summary>"&lt; Option &gt;" stepper for discrete settings.</summary>
    public sealed class UiStepper : Selectable, IPointerClickHandler
    {
        public string[] Options;
        public int Index;
        public Action<int> Changed;
        Text label;

        public static UiStepper Create(Transform parent, string[] options, int index, Action<int> changed)
        {
            var rt = UiKit.Rect("Stepper", parent);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var s = rt.gameObject.AddComponent<UiStepper>();
            s.targetGraphic = hit;
            s.transition = Transition.None;
            s.Options = options;
            s.Index = Mathf.Clamp(index, 0, options.Length - 1);
            s.Changed = changed;
            s.label = UiKit.Text("Label", rt, "", UiKit.BodyMedium, 26, UiKit.Paper, TextAnchor.MiddleCenter);
            s.label.rectTransform.Fill();
            s.Refresh();
            return s;
        }

        void Refresh() => label.text = $"<color=#C9A35A>‹</color>   {Options[Index]}   <color=#C9A35A>›</color>";

        /// <summary>Shows a choice made elsewhere, without acting on it.</summary>
        public void Set(int index)
        {
            Index = Mathf.Clamp(index, 0, Options.Length - 1);
            Refresh();
        }

        /// <summary>The choice as shown; for tours.</summary>
        public string Shown => Options[Index];

        void Step(int d)
        {
            Index = (Index + d + Options.Length) % Options.Length;
            Refresh();
            Tween.Punch(label.transform, 0.08f, 0.2f);
            Sfx.Play("ui_click", 0.45f, 1.1f, 0, Bus.Ui);
            Changed?.Invoke(Index);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var local))
                Step(local.x < 0 ? -1 : 1);
        }

        public override void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) { Step(-1); return; }
            if (e.moveDir == MoveDirection.Right) { Step(1); return; }
            base.OnMove(e);
        }

        void Update()
        {
            bool hot = currentSelectionState == SelectionState.Highlighted || currentSelectionState == SelectionState.Selected;
            label.color = Color.Lerp(label.color, hot ? UiKit.BrassBright : UiKit.Paper, Unscaled.Delta * 10f);
        }
    }
}
