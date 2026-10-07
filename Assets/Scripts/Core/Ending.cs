using System;
using System.Collections;
using System.Text;
using LastLight.Audio;
using LastLight.Automation;
using LastLight.UI;
using LastLight.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastLight.Core
{
    /// <summary>
    /// The end of the season. Dawn comes up over Merrow Bay, the Calloway steams home, its captain
    /// thanks the keeper, the Board orders the light extinguished, and the player holds to put it
    /// out. The lens winds down, the title returns, and the credits list every ship brought home.
    /// </summary>
    public sealed class Ending : MonoBehaviour
    {
        Canvas canvas;
        CanvasGroup promptGroup, cardGroup, creditsGroup;
        Image holdFill;
        Text promptText;
        RectTransform credits;
        Text creditsText;
        bool holding;
        // Skipping: Esc, Start or B once arms it ("Press again to skip"), a second press within a
        // few seconds goes straight back to the title.
        CanvasGroup skipGroup;
        float skipArmed;
        bool skipping;
        const float SkipWindow = 3f;

        /// <summary>The "Press again to skip" prompt is showing; for tours.</summary>
        public bool SkipPrompted => skipArmed > 0f;
        public bool Playing { get; private set; }
        public bool CreditsRolling { get; private set; }
        /// <summary>The last ending was skipped rather than watched to the end.</summary>
        public bool Skipped { get; private set; }

        public void Play(Game game, Action done) => StartCoroutine(Run(game, done));

        static bool BackPressed()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && (pad.startButton.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame));
        }

        void Update()
        {
            if (!Playing || skipping) return;
            if (BackPressed())
            {
                if (skipArmed > 0f) { skipping = true; skipArmed = 0f; }
                else { skipArmed = SkipWindow; Tween.Fade(skipGroup, 1f, 0.25f); }
            }
            else if (skipArmed > 0f)
            {
                skipArmed -= Unscaled.Delta;
                if (skipArmed <= 0f) Tween.Fade(skipGroup, 0f, 0.6f);
            }
        }

        static string PromptText() => InputMode.Pick("Hold the left button, or Space, to put out the light", "Hold A to put out the light");

        void Build()
        {
            if (canvas != null) return;
            canvas = UiKit.MakeCanvas("Ending", 25);
            var prompt = UiKit.Rect("Prompt", canvas.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(900, 120));
            promptGroup = prompt.Group(0f);
            var ring = UiKit.Image("Ring", prompt, SpriteFactory.Ring, new Color(1, 1, 1, 0.2f));
            ring.rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(80, 80));
            holdFill = UiKit.Image("Fill", prompt, SpriteFactory.Ring, UiKit.BrassBright);
            holdFill.rectTransform.Pin(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(80, 80));
            holdFill.type = Image.Type.Filled;
            holdFill.fillMethod = Image.FillMethod.Radial360;
            holdFill.fillOrigin = (int)Image.Origin360.Top;
            holdFill.fillAmount = 0f;
            promptText = UiKit.Text("Text", prompt, PromptText(), UiKit.Italic, 30, UiKit.Paper, TextAnchor.MiddleCenter).Shadowed(0.8f, 2f);
            InputMode.Changed += () => { if (promptText != null) promptText.text = PromptText(); };
            promptText.rectTransform.Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(900, 40));

            var card = UiKit.Rect("Card", canvas.transform).Fill();
            cardGroup = card.Group(0f);
            var t = UiKit.Text("Title", card, "LAST LIGHT", UiKit.Title, 160, UiKit.Paper, TextAnchor.MiddleCenter).Shadowed(0.7f, 4f);
            t.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1400, 200));
            var sub = UiKit.Text("Sub", card, "Gannet Head Light  ·  1871 – 1950", UiKit.Italic, 34, new Color(0.9f, 0.86f, 0.8f), TextAnchor.MiddleCenter).Shadowed();
            sub.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1200, 50));

            var shade = UiKit.Image("Shade", canvas.transform, null, new Color(0.01f, 0.015f, 0.03f, 0.55f));
            shade.rectTransform.Fill();
            creditsGroup = shade.Group(0f);
            credits = UiKit.Rect("Credits", shade.transform).Pin(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(1300, 3000));
            creditsText = UiKit.Text("Text", credits, "", UiKit.Body, 30, UiKit.Paper, TextAnchor.UpperCenter).Shadowed();
            creditsText.rectTransform.Fill();
            creditsText.lineSpacing = 1.25f;

            var skip = UiKit.Text("Skip", canvas.transform, "Press again to skip", UiKit.Italic, 26, UiKit.Paper, TextAnchor.MiddleRight).Shadowed(0.8f, 2f);
            skip.rectTransform.Pin(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -44), new Vector2(600, 40));   // clear of the radio panel
            skipGroup = skip.rectTransform.Group(0f);
        }

        IEnumerator Wait(float s)
        {
            float t = 0f;
            while (t < s && !skipping) { t += Time.deltaTime; yield return null; }
        }

        IEnumerator Run(Game game, Action done)
        {
            Build();
            Playing = true;
            skipping = false;
            skipArmed = 0f;
            skipGroup.alpha = 0f;
            var body = Body(game);
            while (body.MoveNext())
            {
                if (skipping) break;
                yield return body.Current;
            }
            if (skipping)
            {
                // Straight to the end: stop the dawn and the lens winding down, clear the cards.
                Tween.Kill(this);
                Tween.Fade(skipGroup, 0f, 0.3f);
                foreach (var g in new[] { promptGroup, cardGroup, creditsGroup }) Tween.Fade(g, 0f, 0.6f);
                Music.Stop(1.5f);
            }
            ShaderGlobals.AmbientColor = new Color(0.06f, 0.085f, 0.13f);
            Stage.Moon.intensity = Stage.MoonIntensity;
            if (game.Runner != null) game.Runner.World.Beam.Power = 1f;
            Playing = false;
            CreditsRolling = false;
            Skipped = skipping;
            skipArmed = 0f;
            done?.Invoke();
        }

        IEnumerator Body(Game game)
        {
            var radio = game.Radio;
            radio.Clear();
            game.Fader.Dip(1.2f, () =>
            {
                game.StartEndingRunner();
                game.Rig.Snap(CameraRig.EndingPose);
                Stage.MoonTowards(true, 0.01f);   // the setting moon hangs beside the tower
                game.Hud.ShowRadioOnly(radio);
            });
            yield return Wait(1.4f);
            Music.PlayTrack("music_dawn", 4f);

            // Dawn rises across the whole sequence.
            Tween.Run(this, "dawn", 40f, t =>
            {
                ShaderGlobals.DawnAmount = Mathf.Lerp(0.1f, 0.85f, t);
                Stage.Moon.color = Color.Lerp(Stage.MoonColor, new Color(1f, 0.72f, 0.5f), t);
                Stage.Moon.intensity = Mathf.Lerp(Stage.MoonIntensity, 1.8f, t);
                ShaderGlobals.AmbientColor = Color.Lerp(new Color(0.06f, 0.085f, 0.13f), new Color(0.2f, 0.17f, 0.18f), t);
            }, 0f, Tween.EaseInOut);

            yield return Wait(2.5f);
            radio.Say("pryce", "Gannet Head, Calloway. That's us home. Last of the night.", 3, "SS Calloway");
            yield return Wait(8.5f);
            radio.Say("pryce", "Forty years I've come round this Head on your light, keeper. ...Thank you for the light.", 3, "SS Calloway");
            yield return Wait(10f);
            radio.Say("ianto", "That's the lot. Every one of them home, keeper.", 3);
            yield return Wait(6.5f);
            radio.Say("board", "Gannet Head Light: the Board of Lights confirms the radio beacon is commissioned as of six o'clock. You may extinguish the light.", 3);
            yield return Wait(9f);

            // The keeper puts out the light.
            Tween.Fade(promptGroup, 1f, 1.2f);
            float held = 0f, waited = 0f;
            while (held < 2.4f && !skipping)
            {
                var kb = Keyboard.current;
                var mouse = Mouse.current;
                var pad = Gamepad.current;
                holding = (kb != null && kb.spaceKey.isPressed) || (mouse != null && mouse.leftButton.isPressed) || (pad != null && pad.buttonSouth.isPressed);
                if (Tour.Active && waited > 2f) holding = true;
                held = holding ? held + Time.deltaTime : Mathf.Max(0f, held - Time.deltaTime * 2f);
                waited += Time.deltaTime;
                holdFill.fillAmount = held / 2.4f;
                promptText.color = Color.Lerp(UiKit.Paper, UiKit.BrassBright, held / 2.4f);
                var runner = game.Runner;
                if (runner != null) runner.World.Beam.Power = 1f - 0.25f * held / 2.4f;
                yield return null;
            }
            Tween.Fade(promptGroup, 0f, 0.8f);
            Sfx.Play("switch_off", 1f);
            Sfx.Duck(0.6f, 2f);
            Music.Stop(3f);
            var r = game.Runner;
            float startTurn = r != null ? r.AttractTurn : 0f;
            Tween.Run(this, "out", 6f, t =>
            {
                if (r == null) return;
                r.World.Beam.Power = Mathf.Lerp(0.75f, 0f, Mathf.Clamp01(t * 1.6f));
                r.AttractTurn = Mathf.Lerp(startTurn, 0f, t);
            }, 0f, Tween.EaseInOut);
            Sfx.Play("lens_stop", 0.8f);
            yield return Wait(4.5f);
            Music.PlayTrack("music_ending", 3f, false);
            yield return Wait(2f);
            Tween.Fade(cardGroup, 1f, 3f);
            yield return Wait(7.5f);
            Tween.Fade(cardGroup, 0f, 2f);
            yield return Wait(1.5f);

            // Credits: every ship the keeper brought home.
            var save = SaveData.Current;
            var sb = new StringBuilder();
            sb.Append("<size=60><color=#E9DFC7>Thank you for the light</color></size>\n\n\n");
            if (save.homeNames.Count > 0)
                sb.Append($"<color=#C9A35A>YOU BROUGHT {Mathf.Max(save.shipsHome, save.homeNames.Count)} SHIPS HOME</color>\n\n");
            for (int i = 0; i < save.homeNames.Count; i++)
            {
                sb.Append(save.homeNames[i]);
                sb.Append(i % 3 == 2 ? "\n" : "      ·      ");
            }
            sb.Append("\n\n\n<color=#C9A35A>ON THE RADIO</color>\n\nIanto Rees, harbourmaster\nMaren Holt of the Little Auk\nCapt. Edwin Pryce of the SS Calloway\nDot Okafor of the Evening Star\nand the Corleys, who never got their wreck\n\n\n");
            sb.Append("<color=#C9A35A>MADE WITH</color>\n\nUnity 6 and Blender\nEvery model, sound and note made by script\n\nCormorant Garamond and Alegreya Sans (SIL Open Font License)\nSpecial Elite (Apache License 2.0)\n\n\n\n");
            sb.Append("<size=40><i>For the keepers.</i></size>\n");
            creditsText.text = sb.ToString();
            credits.anchoredPosition = new Vector2(0, -60);
            Tween.Fade(creditsGroup, 1f, 2f);
            float h = creditsText.preferredHeight + 1200f;
            float speed = 70f;
            float y = -60f;
            CreditsRolling = true;
            while (y < h && !skipping)
            {
                y += speed * Time.deltaTime * (Tour.Active && !TourSlowCredits ? 6f : 1f);
                credits.anchoredPosition = new Vector2(0, y);
                yield return null;
            }
            Tween.Fade(creditsGroup, 0f, 2f);
        }

        /// <summary>Tours that skip the credits let them roll at reading speed.</summary>
        public static bool TourSlowCredits;
    }
}
