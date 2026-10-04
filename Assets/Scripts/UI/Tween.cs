using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastLight.UI
{
    /// <summary>
    /// A tiny unscaled-time tween runner for UI motion (fades, slides, punches). Tweens are keyed by
    /// target + channel so starting a new one replaces the old.
    /// </summary>
    public sealed class Tween : MonoBehaviour
    {
        sealed class Item
        {
            public object Target;
            public string Channel;
            public float Delay, Duration, T;
            public Action<float> Apply;
            public Func<float, float> Ease;
            public Action Done;
        }

        static Tween instance;
        readonly List<Item> items = new List<Item>();
        readonly List<Item> finished = new List<Item>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        static Tween Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("Tween");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Tween>();
                return instance;
            }
        }

        public static void Run(object target, string channel, float duration, Action<float> apply, float delay = 0f, Func<float, float> ease = null, Action done = null)
        {
            var list = Instance.items;
            list.RemoveAll(i => ReferenceEquals(i.Target, target) && i.Channel == channel);
            list.Add(new Item { Target = target, Channel = channel, Duration = Mathf.Max(0.0001f, duration), Delay = delay, Apply = apply, Ease = ease ?? EaseOutCubic, Done = done });
            if (delay <= 0f) apply((ease ?? EaseOutCubic)(0f));
        }

        public static void Kill(object target)
        {
            if (instance != null) instance.items.RemoveAll(i => ReferenceEquals(i.Target, target));
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            finished.Clear();
            for (int k = 0; k < items.Count; k++)
            {
                var i = items[k];
                if (i.Target is UnityEngine.Object o && o == null) { finished.Add(i); continue; }
                if (i.Delay > 0f) { i.Delay -= dt; continue; }
                i.T = Mathf.Min(1f, i.T + dt / i.Duration);
                try { i.Apply(i.Ease(i.T)); }
                catch (Exception e) { Debug.LogException(e); finished.Add(i); continue; }
                if (i.T >= 1f) finished.Add(i);
            }
            foreach (var i in finished)
            {
                items.Remove(i);
                if (i.T >= 1f) i.Done?.Invoke();
            }
        }

        // ---------------------------------------------------------------- easing

        public static float Linear(float t) => t;
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float EaseInOut(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        public static float EaseOutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }
        public static float EaseIn(float t) => t * t * t;

        // ---------------------------------------------------------------- helpers

        public static void Fade(CanvasGroup g, float to, float duration, float delay = 0f, Action done = null)
        {
            float from = g.alpha;
            Run(g, "fade", duration, t => g.alpha = Mathf.Lerp(from, to, t), delay, Linear, done);
        }

        public static void Move(RectTransform rt, Vector2 to, float duration, float delay = 0f, Func<float, float> ease = null)
        {
            var from = rt.anchoredPosition;
            Run(rt, "move", duration, t => rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t), delay, ease);
        }

        public static void Scale(Transform tr, float from, float to, float duration, float delay = 0f, Func<float, float> ease = null)
        {
            Run(tr, "scale", duration, t => tr.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, t), delay, ease);
        }

        public static void Punch(Transform tr, float amount = 0.15f, float duration = 0.35f)
        {
            Run(tr, "scale", duration, t => tr.localScale = Vector3.one * (1f + amount * Mathf.Sin(t * Mathf.PI) * (1f - t)), 0f, Linear);
        }

        public static void Color(UnityEngine.UI.Graphic g, Color to, float duration, float delay = 0f)
        {
            var from = g.color;
            Run(g, "color", duration, t => g.color = UnityEngine.Color.Lerp(from, to, t), delay, Linear);
        }
    }
}
