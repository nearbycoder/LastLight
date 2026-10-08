using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>Palette, fonts and small builders for uGUI hierarchies made in code.</summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.043f, 0.067f, 0.094f, 0.9f);
        public static readonly Color InkSolid = new Color(0.043f, 0.067f, 0.094f, 1f);
        public static readonly Color Paper = new Color(0.914f, 0.875f, 0.78f);
        public static readonly Color PaperInk = new Color(0.16f, 0.13f, 0.1f);
        public static readonly Color Brass = new Color(0.79f, 0.64f, 0.35f);
        public static readonly Color BrassBright = new Color(1f, 0.84f, 0.52f);
        public static readonly Color Muted = new Color(0.55f, 0.6f, 0.66f);
        public static readonly Color Danger = new Color(0.92f, 0.36f, 0.3f);
        public static readonly Color Lure = new Color(1f, 0.6f, 0.2f);
        public static readonly Color Safe = new Color(0.56f, 0.83f, 0.63f);
        public static readonly Color Beam = new Color(1f, 0.89f, 0.66f);

        static Font title, heading, body, bodyBold, bodyMedium, italic, radio;
        public static Font Title => title ??= Load("CormorantGaramond-Bold");
        public static Font Heading => heading ??= Load("CormorantGaramond-SemiBold");
        public static Font Body => body ??= Load("AlegreyaSans-Regular");
        public static Font BodyMedium => bodyMedium ??= Load("AlegreyaSans-Medium");
        public static Font BodyBold => bodyBold ??= Load("AlegreyaSans-Bold");
        public static Font Italic => italic ??= Load("AlegreyaSans-Italic");
        public static Font Radio => radio ??= Load("SpecialElite-Regular");

        /// <summary>The radio's lettering: the worn typewriter, or with Settings ▸ Radio lettering on
        /// Plain the game's sans-serif, a size larger to match the typewriter's height.</summary>
        public static Font RadioFont => Core.SaveData.Current.plainRadio ? BodyMedium : Radio;
        public static int RadioSize(int typewriter) => Core.SaveData.Current.plainRadio ? Mathf.RoundToInt(typewriter * 1.08f) : typewriter;

        /// <summary>Puts a radio text in the keeper's lettering.</summary>
        public static void SetRadioLettering(Text text, int typewriterSize)
        {
            if (text == null) return;
            text.font = RadioFont;
            text.fontSize = RadioSize(typewriterSize);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { title = heading = body = bodyBold = bodyMedium = italic = radio = null; }

        static Font Load(string name)
        {
            var f = Resources.Load<Font>("Fonts/" + name);
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            // The whole 1920x1080 layout stays on screen at any shape: 16:10, 5:4 and 21:9 add
            // room around it rather than cropping it.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            UnityEngine.Object.DontDestroyOnLoad(go);
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Fill(this RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        /// <summary>Anchor at a point with a fixed size.</summary>
        public static RectTransform Pin(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, bool sliced = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        public static Text Text(string name, Transform parent, string text, Font font, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.lineSpacing = 1.05f;
            return t;
        }

        public static Text Shadowed(this Text t, float alpha = 0.6f, float distance = 2f)
        {
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, alpha);
            s.effectDistance = new Vector2(distance, -distance);
            return t;
        }

        public static CanvasGroup Group(this Component c, float alpha = 1f)
        {
            // Not `??`: in the editor GetComponent returns a fake-null stub that `??` treats as real.
            var g = c.GetComponent<CanvasGroup>();
            if (g == null) g = c.gameObject.AddComponent<CanvasGroup>();
            g.alpha = alpha;
            return g;
        }

        /// <summary>Spaced capitals ("L A S T") for the brass labels.</summary>
        public static string Spaced(string s, int spaces = 1)
        {
            var pad = new string(' ', spaces);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                sb.Append(s[i]);
                if (i < s.Length - 1) sb.Append(s[i] == ' ' ? " " : pad);
            }
            return sb.ToString();
        }

        /// <summary>Minutes and seconds, "14:32".</summary>
        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        public static string Roman(int n)
        {
            string[] r = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };
            return n >= 0 && n < r.Length ? r[n] : n.ToString();
        }
    }
}
