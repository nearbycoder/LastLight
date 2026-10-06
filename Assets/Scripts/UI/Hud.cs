using System.Collections.Generic;
using LastLight.Audio;
using LastLight.Core;
using LastLight.Sim;
using LastLight.View;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight.UI
{
    /// <summary>
    /// The in-night HUD: title, manifest of ships, score, the radio panel, foghorn dial, hint
    /// prompts, incoming chevrons at the screen edge, distress markers and floating score text.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        Canvas canvas;
        RectTransform root;
        CanvasGroup group;
        Text nightLabel, titleLabel, scoreLabel;
        RectTransform manifest;
        readonly List<(Image icon, Image mark, SpawnDef def)> manifestIcons = new List<(Image, Image, SpawnDef)>();
        // The Night Watch strip: ships home and the clock, and a hull for each wreck the Board allows.
        Text watchText;
        readonly List<Image> watchHulls = new List<Image>();
        int watchWrecksShown;
        // Radio
        RectTransform radioPanel;
        CanvasGroup radioGroup;
        Text radioName, radioRole, radioText, radioInitials;
        Image radioMedallion, radioPulse;
        string radioFull = "";
        float radioTyped;
        float radioTypeRate;
        AudioSource voice;
        // Foghorn
        RectTransform hornPanel;
        Image hornFill, hornIcon;
        Text hornKey;
        float hornReadyFlash;
        // Hints
        RectTransform hintPanel;
        CanvasGroup hintGroup;
        Text hintText;
        Image hintIcon;
        Text hintKey;
        string hintId;
        float hintTimer;
        // Markers
        RectTransform markerLayer;
        readonly List<Marker> markers = new List<Marker>();

        MissionRunner runner;
        Radio radio;
        int shownScore;
        float scoreAnim;

        sealed class Marker
        {
            public RectTransform Rt;
            public Image Image;
            public Text Text;
            public Vector3 World;
            public float Lift;
            public float Life, MaxLife;
            public bool EdgeClamp;
            public Color Color;
            public float Rise;
        }

        public static Hud Create()
        {
            var canvas = UiKit.MakeCanvas("HUD", 10);
            var hud = canvas.gameObject.AddComponent<Hud>();
            hud.canvas = canvas;
            hud.Build();
            return hud;
        }

        void Build()
        {
            root = UiKit.Rect("Root", canvas.transform).Fill();
            group = root.Group(0f);
            group.blocksRaycasts = false;
            markerLayer = UiKit.Rect("Markers", root).Fill();

            // Top-left: the night.
            var tl = UiKit.Rect("Night", root).Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(46, -34), new Vector2(560, 120));
            nightLabel = UiKit.Text("Number", tl, "", UiKit.BodyBold, 20, UiKit.Brass, TextAnchor.UpperLeft).Shadowed();
            nightLabel.rectTransform.Stretch(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -26), new Vector2(0, 0));
            titleLabel = UiKit.Text("Title", tl, "", UiKit.Heading, 44, UiKit.Paper, TextAnchor.UpperLeft).Shadowed(0.8f, 2f);
            titleLabel.rectTransform.Stretch(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -80), new Vector2(0, -22));

            // Top-centre: the manifest.
            manifest = UiKit.Rect("Manifest", root).Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -38), new Vector2(900, 44));

            // Top-right: score.
            var tr = UiKit.Rect("Score", root).Pin(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-46, -34), new Vector2(300, 90));
            var scoreCaption = UiKit.Text("Caption", tr, UiKit.Spaced("TONIGHT"), UiKit.BodyBold, 18, UiKit.Brass, TextAnchor.UpperRight).Shadowed();
            scoreCaption.rectTransform.Stretch(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -24), Vector2.zero);
            scoreLabel = UiKit.Text("Value", tr, "0", UiKit.BodyBold, 40, UiKit.Paper, TextAnchor.UpperRight).Shadowed(0.8f, 2f);
            scoreLabel.rectTransform.Stretch(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -80), new Vector2(0, -20));

            BuildRadio();
            BuildHorn();
            BuildHint();
        }

        void BuildRadio()
        {
            radioPanel = UiKit.Rect("Radio", root).Pin(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 36), new Vector2(720, 150));
            radioGroup = radioPanel.Group(0f);
            var bg = UiKit.Image("Bg", radioPanel, SpriteFactory.Rounded, new Color(0.03f, 0.05f, 0.07f, 0.82f), true);
            bg.rectTransform.Fill();
            var edge = UiKit.Image("Edge", radioPanel, SpriteFactory.Bar, new Color(UiKit.Brass.r, UiKit.Brass.g, UiKit.Brass.b, 0.5f));
            edge.rectTransform.Stretch(new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -3), new Vector2(-20, 1));
            radioPulse = UiKit.Image("Pulse", radioPanel, SpriteFactory.Glow, new Color(1, 0.8f, 0.5f, 0f));
            radioPulse.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(78, 0), new Vector2(150, 150));
            radioMedallion = UiKit.Image("Medallion", radioPanel, SpriteFactory.Medallion, Color.white);
            radioMedallion.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(78, 0), new Vector2(104, 104));
            radioInitials = UiKit.Text("Initials", radioMedallion.transform, "", UiKit.Heading, 38, UiKit.Paper, TextAnchor.MiddleCenter);
            radioInitials.rectTransform.Fill();
            radioName = UiKit.Text("Name", radioPanel, "", UiKit.BodyBold, 22, UiKit.Brass, TextAnchor.UpperLeft);
            radioName.rectTransform.Stretch(new Vector2(0, 1), new Vector2(1, 1), new Vector2(150, -44), new Vector2(-20, -14));
            radioRole = UiKit.Text("Role", radioPanel, "", UiKit.Italic, 18, UiKit.Muted, TextAnchor.UpperRight);
            radioRole.rectTransform.Stretch(new Vector2(0, 1), new Vector2(1, 1), new Vector2(150, -42), new Vector2(-22, -16));
            radioText = UiKit.Text("Text", radioPanel, "", UiKit.Radio, 23, UiKit.Paper, TextAnchor.UpperLeft);
            radioText.rectTransform.Stretch(new Vector2(0, 0), new Vector2(1, 1), new Vector2(150, 12), new Vector2(-24, -50));
            radioText.lineSpacing = 1.12f;
        }

        void BuildHorn()
        {
            hornPanel = UiKit.Rect("Foghorn", root).Pin(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -130), new Vector2(120, 140));
            var ring = UiKit.Image("Ring", hornPanel, SpriteFactory.Ring, new Color(1, 1, 1, 0.25f));
            ring.rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(96, 96));
            hornFill = UiKit.Image("Fill", hornPanel, SpriteFactory.Ring, UiKit.Brass);
            hornFill.rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(96, 96));
            hornFill.type = Image.Type.Filled;
            hornFill.fillMethod = Image.FillMethod.Radial360;
            hornFill.fillOrigin = (int)Image.Origin360.Top;
            hornFill.fillClockwise = true;
            hornIcon = UiKit.Image("Icon", hornPanel, SpriteFactory.Glow, UiKit.Beam);
            hornIcon.rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -48), new Vector2(60, 60));
            var label = UiKit.Text("Label", hornPanel, "HORN", UiKit.BodyBold, 16, UiKit.Muted, TextAnchor.MiddleCenter);
            label.rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -48), new Vector2(100, 30));
            hornKey = UiKit.Text("Key", hornPanel, InputMode.Pick("SPACE", "A"), UiKit.BodyBold, 18, UiKit.Paper, TextAnchor.MiddleCenter).Shadowed();
            hornKey.rectTransform.Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(120, 30));
            InputMode.Changed += () => hornKey.text = InputMode.Pick("SPACE", "A");
            hornPanel.gameObject.SetActive(false);
        }

        void BuildHint()
        {
            hintPanel = UiKit.Rect("Hint", root).Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(820, 86));
            hintGroup = hintPanel.Group(0f);
            var bg = UiKit.Image("Bg", hintPanel, SpriteFactory.Pill, new Color(0.03f, 0.05f, 0.07f, 0.72f), true);
            bg.rectTransform.Fill();
            hintIcon = UiKit.Image("Icon", hintPanel, SpriteFactory.Mouse(""), UiKit.Paper);
            hintIcon.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(58, 0), new Vector2(44, 58));
            hintKey = UiKit.Text("Key", hintPanel, "", UiKit.BodyBold, 20, UiKit.Paper, TextAnchor.MiddleCenter);
            hintKey.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(58, 0), new Vector2(90, 40));
            hintText = UiKit.Text("Text", hintPanel, "", UiKit.BodyMedium, 27, UiKit.Paper, TextAnchor.MiddleLeft);
            hintText.rectTransform.Stretch(Vector2.zero, Vector2.one, new Vector2(112, 0), new Vector2(-28, 0));
        }

        // ---------------------------------------------------------------- binding

        public void Bind(MissionRunner r, Radio radio)
        {
            runner = r;
            this.radio = radio;
            var def = r.Def;
            nightLabel.text = UiKit.Spaced((def.endless ? "ENDLESS" : "NIGHT " + UiKit.Roman(def.night)) + (r.World.Hard ? "  ·  HARD" : ""));
            titleLabel.text = def.title;
            shownScore = 0;
            scoreLabel.text = "0";
            foreach (Transform c in root) c.gameObject.SetActive(true);
            foreach (Transform c in manifest) Destroy(c.gameObject);
            manifestIcons.Clear();
            watchHulls.Clear();
            watchText = null;
            var sched = def.endless ? new SpawnDef[0] : r.World.Schedule;
            if (def.endless) BuildWatchStrip(def);
            float w = 66f, gap = 10f;
            float total = sched.Length * w + (sched.Length - 1) * gap;
            for (int i = 0; i < sched.Length; i++)
            {
                var icon = UiKit.Image("Ship", manifest, SpriteFactory.Ship(sched[i].type), new Color(1, 1, 1, 0.25f));
                icon.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-total / 2 + w / 2 + i * (w + gap), 0), new Vector2(w, w * 0.4f));
                var mark = UiKit.Image("Mark", icon.transform, SpriteFactory.Disc, new Color(0, 0, 0, 0));
                mark.rectTransform.Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(8, 8));
                manifestIcons.Add((icon, mark, sched[i]));
            }
            hornPanel.gameObject.SetActive(def.foghorn);
            radioGroup.alpha = 0f;
            radio.Started -= OnRadio;
            radio.Started += OnRadio;
            HideHint(true);
            foreach (var m in markers) if (m.Rt != null) Destroy(m.Rt.gameObject);
            markers.Clear();
        }

        void BuildWatchStrip(MissionDef def)
        {
            int allowed = def.allowedWrecks + 1;
            for (int i = 0; i < allowed; i++)
            {
                var hull = UiKit.Image("Hull", manifest, SpriteFactory.Ship("trawler"), new Color(0.92f, 0.94f, 1f, 0.9f));
                hull.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-250 + i * 64, 0), new Vector2(56, 22));
                watchHulls.Add(hull);
            }
            watchText = UiKit.Text("Tally", manifest, "", UiKit.BodyBold, 26, UiKit.Paper, TextAnchor.MiddleLeft).Shadowed();
            watchText.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(-40, 0), new Vector2(420, 40));
            watchWrecksShown = 0;
        }

        void UpdateWatchStrip(SimWorld w)
        {
            watchText.text = $"{w.Arrivals} <size=19><color=#C9A35A>HOME</color></size>     {UiKit.Clock(w.Time)}";
            for (int i = 0; i < watchHulls.Count; i++)
            {
                bool lost = i < w.Wrecks;
                var c = lost ? new Color(0.9f, 0.35f, 0.3f, 0.55f) : new Color(0.92f, 0.94f, 1f, 0.9f);
                watchHulls[i].color = Color.Lerp(watchHulls[i].color, c, Unscaled.Delta * 6f);
            }
            if (w.Wrecks > watchWrecksShown && w.Wrecks - 1 < watchHulls.Count)
                Tween.Punch(watchHulls[w.Wrecks - 1].transform, 0.5f, 0.6f);
            watchWrecksShown = w.Wrecks;
        }

        public void Show(bool on, float time = 0.6f) => Tween.Fade(group, on ? 1f : 0f, time);

        /// <summary>
        /// The ending: only the radio panel, so the last calls can be read over the dawn scene.
        /// The next <see cref="Bind"/> brings the rest of the HUD back.
        /// </summary>
        public void ShowRadioOnly(Radio radio)
        {
            runner = null;
            if (this.radio != null) this.radio.Started -= OnRadio;
            this.radio = radio;
            radio.Started += OnRadio;
            radioGroup.alpha = 0f;
            HideHint(true);
            foreach (Transform c in root) c.gameObject.SetActive(c == radioPanel);
            Show(true, 0.5f);
        }

        // ---------------------------------------------------------------- radio

        void OnRadio(RadioMessage m)
        {
            var sp = m.Speaker;
            radioName.text = string.IsNullOrEmpty(sp.Name) ? m.Title : sp.Name;
            radioRole.text = string.IsNullOrEmpty(sp.Name) ? "" : m.Title;
            radioInitials.text = string.IsNullOrEmpty(sp.Initials) ? Initials(m.Title) : sp.Initials;
            radioInitials.color = sp.Color;
            radioFull = m.Text;
            radioTyped = 0f;
            radioTypeRate = 42f * radio.TextSpeed;
            radioText.text = "";
            Tween.Fade(radioGroup, 1f, 0.25f);
            Tween.Punch(radioMedallion.transform, 0.12f, 0.3f);
            Sfx.Play("radio_squelch", 0.5f, Random.Range(0.95f, 1.05f), -0.4f, Bus.Radio);
            if (voice != null) voice.Stop();
            voice = Sfx.Play(sp.Id == "board" ? "radio_letter" : "voice_" + (sp.Id == "crew" ? "crew" : sp.Id), 0.75f, sp.Pitch * Random.Range(0.97f, 1.03f), -0.35f, Bus.Radio, 0f);
        }

        static string Initials(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            var parts = title.Replace("SS ", "").Split(' ');
            return parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}" : title.Substring(0, Mathf.Min(2, title.Length)).ToUpper();
        }

        void UpdateRadio(float dt)
        {
            if (radio == null) return;
            if (radio.Current == null)
            {
                if (radioGroup.alpha > 0.99f) Tween.Fade(radioGroup, 0f, 0.6f);
                if (voice != null && voice.isPlaying) voice.volume = Mathf.MoveTowards(voice.volume, 0f, dt * 2f);
                radioPulse.color = new Color(1, 0.8f, 0.5f, 0f);
                return;
            }
            if (radioTyped < radioFull.Length)
            {
                int before = (int)radioTyped;
                radioTyped = Mathf.Min(radioFull.Length, radioTyped + dt * radioTypeRate);
                int now = (int)radioTyped;
                radioText.text = radioFull.Substring(0, now) + "<color=#00000000>" + radioFull.Substring(now) + "</color>";
                if (now / 3 != before / 3 && now < radioFull.Length && radioFull[now] != ' ') Sfx.Play("radio_tick", 0.12f, Random.Range(0.9f, 1.1f), -0.4f, Bus.Ui, 0.02f);
                radioPulse.color = new Color(1, 0.8f, 0.5f, 0.18f + 0.12f * Mathf.Sin(Unscaled.Time * 18f));
                if (radioTyped >= radioFull.Length && voice != null) voice.volume *= 0.5f;
            }
            else
            {
                radioText.text = radioFull;
                radioPulse.color = new Color(1, 0.8f, 0.5f, 0f);
                if (voice != null && voice.isPlaying) voice.volume = Mathf.MoveTowards(voice.volume, 0f, dt * 2f);
            }
        }

        // ---------------------------------------------------------------- hints

        public void ShowHint(string id, string text, string icon, float duration = 9f)
        {
            hintId = id;
            hintTimer = duration;
            SetHintContent(text, icon);
            hintPanel.anchoredPosition = new Vector2(0, -76);
            Tween.Fade(hintGroup, 1f, 0.4f);
            Tween.Move(hintPanel, new Vector2(0, -96), 0.5f, 0f, Tween.EaseOutBack);
            Sfx.Play("ui_hint", 0.45f);
        }

        /// <summary>The hint's words and icon, without the entrance (for a change of input device).</summary>
        public void SetHintContent(string text, string icon)
        {
            hintText.text = text;
            hintIcon.gameObject.SetActive(true);
            hintKey.text = "";
            switch (icon)
            {
                case "mouse": hintIcon.sprite = SpriteFactory.Mouse(""); break;
                case "lmb": hintIcon.sprite = SpriteFactory.Mouse("left"); break;
                case "rmb": hintIcon.sprite = SpriteFactory.Mouse("right"); break;
                case "ring": hintIcon.sprite = SpriteFactory.ThinRing; break;
                case "lamp": hintIcon.sprite = SpriteFactory.Lamp; break;
                default:
                    hintIcon.sprite = SpriteFactory.KeyCap;
                    hintKey.text = icon;
                    hintIcon.rectTransform.sizeDelta = new Vector2(Mathf.Max(56, icon.Length * 14 + 26), 50);
                    break;
            }
            if (!(icon.Length > 0 && hintKey.text != "")) hintIcon.rectTransform.sizeDelta = icon == "ring" || icon == "lamp" ? new Vector2(54, 54) : new Vector2(42, 56);
        }

        public void HideHint(bool instant = false)
        {
            hintId = null;
            if (instant) hintGroup.alpha = 0f;
            else Tween.Fade(hintGroup, 0f, 0.5f);
        }

        public bool HintShowing(string id) => hintId == id;

        /// <summary>The words of the hint on screen, or null (for the tours).</summary>
        public string HintOnScreen => hintId != null ? hintText.text : null;

        // ---------------------------------------------------------------- markers

        Marker AddMarker(Vector3 world, Sprite sprite, Color color, float size, float life, bool clamp, string text = null, float rise = 0f)
        {
            var rt = UiKit.Rect("Marker", markerLayer);
            rt.sizeDelta = new Vector2(size, size);
            var img = UiKit.Image("Img", rt, sprite, color);
            img.rectTransform.Fill();
            Text t = null;
            if (text != null)
            {
                img.enabled = sprite != null;
                t = UiKit.Text("Text", rt, text, UiKit.Heading, 34, color, TextAnchor.MiddleCenter).Shadowed(0.85f, 2f);
                t.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 60));
            }
            var m = new Marker { Rt = rt, Image = img, Text = t, World = world, Life = life, MaxLife = life, EdgeClamp = clamp, Color = color, Rise = rise };
            markers.Add(m);
            return m;
        }

        public void Incoming(Vector2 entry, string type)
        {
            var m = AddMarker(new Vector3(entry.x, 2f, entry.y), SpriteFactory.Ship(type), new Color(1f, 0.92f, 0.75f, 1f), 60f, 4.5f, true);
            m.Rt.sizeDelta = new Vector2(66, 28);
            var ring = UiKit.Image("Ring", m.Rt, SpriteFactory.ThinRing, new Color(1f, 0.9f, 0.7f, 0.7f));
            ring.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84));
        }

        /// <summary>Breakers ahead of a ship: a pale ring pulses round it (or at the screen edge).</summary>
        public void Danger(Vector2 pos)
        {
            for (int i = 0; i < 2; i++)
                AddMarker(new Vector3(pos.x, 1f, pos.y), SpriteFactory.ThinRing, new Color(0.92f, 0.96f, 1f, 1f), 56f + i * 26f, 1.6f + i * 0.4f, true);
        }

        public void Flare(Vector2 pos) => AddMarker(new Vector3(pos.x, 1f, pos.y), SpriteFactory.ThinRing, new Color(1f, 0.35f, 0.25f, 1f), 70f, 6f, true);

        public void FloatText(Vector3 world, string text, Color color)
        {
            // Ships arriving together share a harbour mouth: stack their scores instead of overlapping.
            float lift = 0f;
            foreach (var other in markers)
                if (other.Text != null && other.MaxLife - other.Life < 1.2f && (other.World - world).sqrMagnitude < 400f)
                    lift = Mathf.Max(lift, other.Lift + 42f);
            var m = AddMarker(world, null, color, 10f, 2.2f, false, text, 70f);
            m.Lift = lift;
        }

        void UpdateMarkers(float dt)
        {
            var cam = CameraRig.Instance != null ? CameraRig.Instance.Cam : Camera.main;
            if (cam == null) return;
            var canvasRect = (RectTransform)canvas.transform;
            float scale = canvasRect.rect.width / Mathf.Max(1, Screen.width);
            for (int i = markers.Count - 1; i >= 0; i--)
            {
                var m = markers[i];
                m.Life -= dt;
                if (m.Life <= 0f || m.Rt == null)
                {
                    if (m.Rt != null) Destroy(m.Rt.gameObject);
                    markers.RemoveAt(i);
                    continue;
                }
                var sp = cam.WorldToScreenPoint(m.World);
                var p = new Vector2(sp.x, sp.y) * scale;
                float W = canvasRect.rect.width, H = canvasRect.rect.height;
                if (m.EdgeClamp)
                {
                    p.x = Mathf.Clamp(p.x, 70, W - 70);
                    p.y = Mathf.Clamp(p.y, 70, H - 150);
                }
                float age = m.MaxLife - m.Life;
                p.y += m.Rise * (age / m.MaxLife) + m.Lift;
                m.Rt.anchorMin = m.Rt.anchorMax = Vector2.zero;
                m.Rt.anchoredPosition = p;
                float a = Mathf.Clamp01(m.Life / 0.6f) * Mathf.Clamp01(age / 0.25f);
                float pulse = m.EdgeClamp ? 0.75f + 0.25f * Mathf.Sin(age * 8f) : 1f;
                var c = m.Color;
                c.a *= a * pulse;
                if (m.Image != null) m.Image.color = c;
                if (m.Text != null) m.Text.color = c;
                foreach (Transform child in m.Rt)
                    if (child.name == "Ring") child.localScale = Vector3.one * (1f + 0.25f * Mathf.Repeat(age * 1.2f, 1f));
            }
        }

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            float dt = Unscaled.Delta;
            UpdateRadio(dt);
            UpdateMarkers(dt);
            if (runner == null || runner.World == null) return;
            var w = runner.World;

            // Score counts up.
            if (shownScore != w.Score)
            {
                scoreAnim += dt * 8f;
                int step = Mathf.Max(1, (w.Score - shownScore) / 8);
                if (scoreAnim > 1f) { scoreAnim = 0f; shownScore = Mathf.Min(w.Score, shownScore + step); scoreLabel.text = shownScore.ToString(); }
            }

            if (watchText != null) UpdateWatchStrip(w);

            // Manifest states.
            int spawned = w.SpawnedShips;
            for (int i = 0; i < manifestIcons.Count; i++)
            {
                var (icon, mark, def) = manifestIcons[i];
                Color c = new Color(1, 1, 1, 0.22f);
                Color mc = new Color(0, 0, 0, 0);
                SimShip s = i < spawned ? FindShip(w, def) : null;
                if (s != null)
                {
                    switch (s.State)
                    {
                        case ShipState.Arrived: c = new Color(1f, 0.86f, 0.55f, 1f); mc = new Color(1f, 0.86f, 0.55f, 1f); break;
                        case ShipState.Wrecked: c = new Color(0.9f, 0.35f, 0.3f, 0.75f); mc = UiKit.Danger; break;
                        case ShipState.Lost: c = Color.Lerp(UiKit.Danger, Color.white, 0.5f + 0.5f * Mathf.Sin(Unscaled.Time * 8f)); break;
                        case ShipState.Lured: c = Color.Lerp(UiKit.Lure, Color.white, 0.5f + 0.5f * Mathf.Sin(Unscaled.Time * 8f)); break;
                        default: c = new Color(0.92f, 0.94f, 1f, 0.95f); break;
                    }
                }
                icon.color = Color.Lerp(icon.color, c, dt * 8f);
                mark.color = mc;
            }

            // Foghorn readiness.
            if (hornPanel.gameObject.activeSelf)
            {
                float ready = 1f - w.HornCooldown / SimWorld.HornCooldownTime;
                hornFill.fillAmount = ready;
                bool isReady = w.HornCooldown <= 0f;
                if (isReady && hornReadyFlash <= 0f) { hornReadyFlash = 1f; Tween.Punch(hornPanel, 0.12f, 0.35f); }
                if (!isReady) hornReadyFlash = 0f;
                hornFill.color = isReady ? UiKit.BrassBright : new Color(UiKit.Brass.r, UiKit.Brass.g, UiKit.Brass.b, 0.6f);
                hornIcon.color = isReady ? new Color(1f, 0.85f, 0.55f, 0.45f + 0.15f * Mathf.Sin(Unscaled.Time * 3f)) : new Color(1, 1, 1, 0.08f);
                hornKey.color = isReady ? UiKit.Paper : UiKit.Muted;
            }

            if (hintId != null)
            {
                hintTimer -= dt;
                if (hintTimer <= 0f) HideHint();
            }
        }

        readonly Dictionary<SpawnDef, SimShip> shipBySpawn = new Dictionary<SpawnDef, SimShip>();

        SimShip FindShip(SimWorld w, SpawnDef def)
        {
            if (shipBySpawn.TryGetValue(def, out var s) && w.Ships.Contains(s)) return s;
            foreach (var ship in w.Ships)
                if (ship.Name == def.name && ship.Route.Id == def.route && !shipBySpawn.ContainsValue(ship))
                {
                    shipBySpawn[def] = ship;
                    return ship;
                }
            return null;
        }

        public void ClearBindings() => shipBySpawn.Clear();
    }
}
