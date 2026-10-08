using System.Collections;
using System.Collections.Generic;
using LastLight.Core;
using LastLight.Sim;
using LastLight.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastLight.Automation
{
    /// <summary>
    /// The lettering tour (-llScript lettering, with -llFresh): measures every scripted radio call
    /// and ship's hail against the HUD's radio box, and every briefing against Ianto's speech box,
    /// in the typewriter and in Plain; chooses Plain in Settings with the keyboard; then shows the
    /// longest call on night VI in both letterings, at 100% and 130% HUD text, and the pause menu's
    /// radio log in Plain.
    /// </summary>
    public static class LetteringTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["lettering"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        static float Height(Text box, string text)
        {
            var size = box.rectTransform.rect.size;
            var settings = box.GetGenerationSettings(new Vector2(size.x, 0f));
            return box.cachedTextGeneratorForLayout.GetPreferredHeight(text, settings) / box.pixelsPerUnit;
        }

        static int Lines(Text box, string text)
        {
            var gen = new TextGenerator();
            gen.Populate(text, box.GetGenerationSettings(new Vector2(box.rectTransform.rect.width, 0f)));
            return gen.lineCount;
        }

        /// <summary>How many texts run past <paramref name="room"/> (the box, or the panel below it),
        /// how many past the box itself, and the tallest.</summary>
        static (int over, int pastBox, float tallest, string longest, int lines) Fit(Text box, IEnumerable<string> texts, System.Func<string, float> room)
        {
            int over = 0, pastBox = 0;
            float tallest = 0f;
            string longest = "";
            foreach (var s in texts)
            {
                float h = Height(box, s);
                if (h > tallest) { tallest = h; longest = s; }
                if (h > room(s) + 0.5f) over++;
                if (h > box.rectTransform.rect.height + 0.5f) pastBox++;
            }
            return (over, pastBox, tallest, longest, Lines(box, longest));
        }

        static IEnumerator Press(Key key)
        {
            TourScripts.Key(key, true);
            yield return null;
            yield return null;
            TourScripts.Key(key, false);
            yield return null;
            yield return null;
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var g = Game.Instance;
            var save = SaveData.Current;
            yield return Tour.Wait(4f);

            // Every call the nights script, and every briefing.
            var calls = new List<string>();
            var briefings = new List<string>();
            foreach (var m in MissionLibrary.All)
            {
                if (m.radio != null) foreach (var c in m.radio) if (!string.IsNullOrEmpty(c.text)) calls.Add(c.text);
                if (m.ships != null) foreach (var s in m.ships) if (!string.IsNullOrEmpty(s.hail)) calls.Add(s.hail);
                if (!string.IsNullOrEmpty(m.briefing)) briefings.Add(m.briefing);
            }
            var radioBox = g.Hud.TourRadioText;
            var briefing = (BriefingScreen)g.TourScreen("briefing");
            var speechBox = briefing.TourSpeech;
            string longestCall = "";
            foreach (bool plain in new[] { false, true })
            {
                save.plainRadio = plain;
                UiKit.SetRadioLettering(radioBox, 23);
                UiKit.SetRadioLettering(speechBox, 28);
                Canvas.ForceUpdateCanvases();
                // Since round 12 a long call grows the panel, so each must fit the text box the panel
                // gives it, margin and all.
                var r = Fit(radioBox, calls, g.Hud.RadioRoomFor);
                var b = Fit(speechBox, briefings, _ => speechBox.rectTransform.rect.height);
                string name = plain ? "Plain" : "Typewriter";
                t.Log($"{name}: radio {radioBox.font.name} {radioBox.fontSize}, briefing {speechBox.font.name} {speechBox.fontSize}");
                Check(t, r.over == 0, $"{name}: all {calls.Count} calls fit the radio's text box, clear of the panel's margin (tallest {r.tallest:0} units in a box of {g.Hud.RadioRoomFor(r.longest):0}, {r.lines} lines; {r.pastBox} would have run past the usual 88-unit box, so the panel grows for them: \"{r.longest}\")");
                Check(t, b.over == 0, $"{name}: all {briefings.Count} briefings fit Ianto's box (tallest {b.tallest:0} of {speechBox.rectTransform.rect.height:0} units, {b.lines} lines)");
                if (!plain) longestCall = r.longest;
            }
            save.plainRadio = false;
            save.Apply(display: false);

            // Settings ▸ Radio lettering, one step right with the keyboard: Plain.
            g.TourShowSettings();
            yield return Tour.Wait(1f);
            var settings = (SettingsScreen)g.TourScreen("settings");
            EventSystem.current.SetSelectedGameObject(settings.RowControl("Radio lettering").gameObject);
            yield return null;
            yield return Press(Key.RightArrow);
            yield return Tour.Wait(0.4f);
            Check(t, save.plainRadio && radioBox.font == UiKit.BodyMedium && settings.AboutShown == "Radio lettering",
                $"→ on Radio lettering chooses Plain (plain {save.plainRadio}, the HUD's radio in {radioBox.font.name}, described: {settings.AboutShown})");
            yield return t.Shot("lettering_setting");
            yield return Press(Key.LeftArrow);
            Check(t, !save.plainRadio && radioBox.font == UiKit.Radio, $"← puts the typewriter back ({radioBox.font.name})");
            g.TourHideAll();

            // Night VI with the longest call on the radio, in each lettering.
            g.AutoPlay = true;
            g.TourBriefing(6);
            yield return Tour.Wait(3.5f);
            g.TourBegin();
            yield return Tour.Wait(2f);
            foreach (var (plain, scale) in new[] { (false, 1f), (true, 1f), (true, 1.3f) })
            {
                save.plainRadio = plain;
                save.hudScale = scale;
                save.Apply(display: false);
                while (g.Radio.Busy) yield return null;
                g.Radio.Say("ianto", longestCall, 5);
                yield return Tour.Wait(7f);
                float h = Height(radioBox, longestCall), room = radioBox.rectTransform.rect.height;
                Check(t, radioBox.text == longestCall && h <= room + 0.5f,
                    $"{(plain ? "Plain" : "Typewriter")} at {scale * 100:0}%: the longest call is on the radio, typed out and inside its text box, clear of the margin ({h:0} of {room:0} units, {Lines(radioBox, longestCall)} lines; the panel stands {g.Hud.TourRadioPanel.sizeDelta.y:0} tall)");
                yield return t.Shot($"lettering_radio_{(plain ? "plain" : "typewriter")}_{scale * 100:0}");
            }
            g.TourPause();
            yield return Tour.Wait(1f);
            yield return t.Shot("lettering_radio_log_plain");
            g.TourResume();
            save.plainRadio = false;
            save.hudScale = 1f;
            save.Apply(display: false);
            t.Log($"real save untouched: {Game.HasArg("-llFresh")}");
            t.Log($"lettering {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
