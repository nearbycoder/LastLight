using System.Collections;
using LastLight.Core;
using LastLight.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LastLight.Automation
{
    /// <summary>
    /// The endingskip tour (-llScript endingskip): skips the ending three ways, with Esc early in the
    /// dawn scene, with a pad's Start in the credits, and with a pad's B. One press only arms the
    /// skip; the second goes back to the title.
    /// </summary>
    public static class EndingSkipTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["endingskip"] = Run;

        static IEnumerator KeyPress(Key key)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
            yield return null;
        }

        static IEnumerator PadPress(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            yield return null;
        }

        static IEnumerator BackAtTitle(Tour t, string how)
        {
            var g = Game.Instance;
            float waited = 0f;
            while (!g.TourShowingTitle && waited < 8f) { waited += Time.unscaledDeltaTime; yield return null; }
            yield return Tour.Wait(2f);
            bool ok = g.TourShowingTitle && g.TourEndingScene.Skipped && ShaderGlobals.DawnAmount == 0f && g.Runner.Attract && g.Runner.Def.id == "attract"
                      && g.Runner.World.Beam.Power == 1f && SaveData.Current.endingSeen;
            t.Log($"{(ok ? "PASS" : "FAIL")} {how}: back at the title {waited:0.0}s after the second press (dawn {ShaderGlobals.DawnAmount:0.00}, title sea {g.Runner.Def.id}, lens power {g.Runner.World.Beam.Power:0.00}, ending seen {SaveData.Current.endingSeen})");
        }

        static IEnumerator Run(Tour t)
        {
            var g = Game.Instance;
            yield return Tour.Wait(3f);

            // 1. Esc in the dawn scene: once arms, and lapses; twice skips.
            g.TourEnding();
            yield return Tour.Wait(12f);
            yield return KeyPress(Key.Escape);
            yield return Tour.Wait(0.4f);
            bool armed = g.TourEndingScene.SkipPrompted && g.Current == Game.State.Ending;
            yield return t.Shot("01_skip_prompt");
            t.Log($"{(armed ? "PASS" : "FAIL")} one Esc shows \"Press again to skip\" and the ending goes on");
            yield return Tour.Wait(3.5f);
            bool lapsed = !g.TourEndingScene.SkipPrompted && g.Current == Game.State.Ending;
            t.Log($"{(lapsed ? "PASS" : "FAIL")} the prompt lapses after a few seconds and the ending goes on");
            yield return KeyPress(Key.Escape);
            yield return Tour.Wait(0.3f);
            yield return KeyPress(Key.Escape);
            yield return BackAtTitle(t, "Esc twice in the dawn scene");
            yield return t.Shot("02_title_after_skip");

            // 2. A pad's Start, twice, in the credits (rolling at reading speed).
            var pad = InputSystem.AddDevice<Gamepad>("TourPad");
            yield return null;
            Ending.TourSlowCredits = true;
            g.TourEnding();
            float waited = 0f;
            while (!g.TourEndingScene.CreditsRolling && waited < 120f) { waited += Time.unscaledDeltaTime; yield return null; }
            yield return Tour.Wait(4f);
            yield return t.Shot("03_credits");
            yield return PadPress(pad, GamepadButton.Start);
            yield return Tour.Wait(0.4f);
            t.Log($"{(g.TourEndingScene.SkipPrompted ? "PASS" : "FAIL")} Start in the credits arms the skip");
            yield return t.Shot("04_credits_skip_prompt");
            yield return PadPress(pad, GamepadButton.Start);
            yield return BackAtTitle(t, "Start twice in the credits");

            // 3. A pad's B, twice, early on.
            g.TourEnding();
            yield return Tour.Wait(6f);
            yield return PadPress(pad, GamepadButton.East);
            yield return Tour.Wait(0.3f);
            yield return PadPress(pad, GamepadButton.East);
            yield return BackAtTitle(t, "B twice in the dawn scene");
            InputSystem.RemoveDevice(pad);
        }
    }
}
