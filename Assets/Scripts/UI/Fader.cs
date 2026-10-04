using System;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>Full-screen black for fades and dips between scenes.</summary>
    public sealed class Fader : MonoBehaviour
    {
        Image image;
        CanvasGroup group;

        public static Fader Create(Transform canvas)
        {
            var rt = UiKit.Rect("Fader", canvas).Fill();
            var f = rt.gameObject.AddComponent<Fader>();
            f.image = rt.gameObject.AddComponent<Image>();
            f.image.color = new Color(0.005f, 0.01f, 0.02f, 1f);
            f.image.raycastTarget = false;
            f.group = rt.Group(0f);
            f.group.blocksRaycasts = false;
            return f;
        }

        void LateUpdate() => transform.SetAsLastSibling();

        public void FadeFrom(float alpha, float time)
        {
            group.alpha = alpha;
            Tween.Fade(group, 0f, time);
        }

        /// <summary>Fade to black, run the action, fade back.</summary>
        public void Dip(float time, Action middle)
        {
            group.blocksRaycasts = true;
            Tween.Fade(group, 1f, time, 0f, () =>
            {
                try { middle?.Invoke(); }
                finally
                {
                    Tween.Fade(group, 0f, time * 1.4f, 0.15f, () => group.blocksRaycasts = false);
                }
            });
        }

        public void To(float alpha, float time) => Tween.Fade(group, alpha, time);
    }
}
