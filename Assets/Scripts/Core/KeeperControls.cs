using LastLight.Sim;
using LastLight.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace LastLight.Core
{
    /// <summary>
    /// Reads the keeper's hands: the mouse aims the lens (with its weight handled by the sim),
    /// LMB, the focus keys or the focus buttons focus, RMB, the horn keys or the horn buttons sound
    /// the foghorn, the turn keys turn the lens directly, and the right stick points it. The keys and
    /// buttons are the player's (see KeyBindings and PadBindings; by default Shift/W/↑, Space and
    /// A-D/arrows, and the triggers and A). With <see cref="ToggleFocus"/> a press switches focus on or
    /// off instead of having to be held.
    /// On a phone or tablet (<see cref="InputMode.Touch"/>) a finger dragged anywhere moves the
    /// point the lens turns towards, as on a trackpad, so it never has to cover the ship being lit,
    /// and the page's on-screen Focus and Horn buttons stand in for the mouse buttons.
    /// </summary>
    public sealed class KeeperControls
    {
        bool usingMouse = true;
        Vector2 lastMouse;
        float lastTarget;
        bool hornLatch;
        public float Sensitivity = 1f;
        public bool ToggleFocus;
        bool focusOn;
        int ignorePressesUntil = -1;
        int touchFocusSeen = -1, touchHornSeen = -1;
        int aimTouch;              // the finger turning the light (Input System touch id), 0 for none
        Vector2 aimPoint, aimFinger;

        /// <summary>A finger moves the aim point this much further than it moves itself.</summary>
        public const float TouchGain = 1.3f;

        /// <summary>The focus the keeper asked for in the last read (for the on-screen button).</summary>
        public bool Focusing { get; private set; }

        /// <summary>The focus switched on (Toggle mode).</summary>
        public bool FocusOn => focusOn;

        /// <summary>The click that begins or resumes a night isn't a focus press.</summary>
        public void IgnorePresses() => ignorePressesUntil = Time.frameCount + 1;

        public KeeperInput Read(Vector2 lighthouse)
        {
            var input = new KeeperInput { TargetBearing = lastTarget };
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool focusHeld = false, focusPressed = false;
            // Behind the dawn card and the chart the bay sails on with the keeper's aim, but Space
            // and the buttons there belong to the menus, not the horn.
            // The press that began or resumed the night (A on Resume is the pad's horn) isn't for the horn either.
            bool playing = (Game.Instance == null || Game.Instance.Current == Game.State.Playing) && Time.frameCount > ignorePressesUntil;

            // A finger: the mouse's position means nothing then (a browser may leave it where a
            // finger last was), and the page's buttons focus and sound the horn.
            if (InputMode.Touch)
            {
                usingMouse = false;
                if (ReadTouchAim(lighthouse)) input.HasTarget = true;
                if (Platform.TouchFocusHeld) focusHeld = true;
                int presses = Platform.TouchFocusPresses;
                if (touchFocusSeen >= 0 && presses != touchFocusSeen) focusPressed = true;
                touchFocusSeen = presses;
                presses = Platform.TouchHornPresses;
                if (touchHornSeen >= 0 && presses != touchHornSeen && playing) hornLatch = true;
                touchHornSeen = presses;
            }
            else
            {
                touchFocusSeen = Platform.TouchFocusPresses;
                touchHornSeen = Platform.TouchHornPresses;
                aimTouch = 0;
            }

            if (mouse != null && !InputMode.Touch)
            {
                var mp = mouse.position.ReadValue();
                if ((mp - lastMouse).sqrMagnitude > 4f) usingMouse = true;
                lastMouse = mp;
                if (usingMouse && CameraRig.Instance != null && CameraRig.Instance.ScreenToSea(mp, out var sea))
                {
                    var rel = new Vector2(sea.x, sea.z) - lighthouse;
                    if (rel.sqrMagnitude > 9f) lastTarget = Geo.Bearing(rel);
                    input.HasTarget = true;
                    input.TargetBearing = lastTarget;
                }
                if (mouse.leftButton.isPressed) focusHeld = true;
                if (mouse.leftButton.wasPressedThisFrame) focusPressed = true;
                if (mouse.rightButton.wasPressedThisFrame && playing) hornLatch = true;
            }

            if (kb != null)
            {
                var keys = SaveData.Current.keys;
                float turn = 0f;
                if (keys.Held(kb, KeeperAction.TurnLeft)) turn -= 1f;
                if (keys.Held(kb, KeeperAction.TurnRight)) turn += 1f;
                if (turn != 0f)
                {
                    usingMouse = false;
                    input.HasTarget = false;
                    input.Turn = turn * Sensitivity;
                }
                else if (!usingMouse)
                {
                    input.HasTarget = false;
                }
                if (keys.Held(kb, KeeperAction.Focus)) focusHeld = true;
                if (keys.Pressed(kb, KeeperAction.Focus)) focusPressed = true;
                if (keys.Pressed(kb, KeeperAction.Horn) && playing) hornLatch = true;
            }

            if (pad != null)
            {
                var stick = pad.rightStick.ReadValue();
                if (stick.sqrMagnitude < 0.09f) stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.16f)
                {
                    usingMouse = false;
                    lastTarget = Geo.Bearing(stick);
                    input.HasTarget = true;
                    input.TargetBearing = lastTarget;
                }
                else if (!usingMouse && input.Turn == 0f)
                {
                    input.HasTarget = true;
                    input.TargetBearing = lastTarget;
                }
                // The keeper's buttons (by default the triggers focus and A sounds the horn). A
                // trigger is held from a light touch, and pressed once it's past halfway.
                var buttons = SaveData.Current.pad;
                if (buttons.Held(pad, KeeperAction.Focus)) focusHeld = true;
                if (buttons.Pressed(pad, KeeperAction.Focus)) focusPressed = true;
                if (buttons.Pressed(pad, KeeperAction.Horn) && playing) hornLatch = true;
            }

            if (focusPressed && playing) focusOn = !focusOn;
            input.Focus = ToggleFocus ? focusOn : focusHeld;
            Focusing = input.Focus;

            if (!input.HasTarget && input.Turn == 0f && usingMouse) input.HasTarget = true;
            if (InputMode.Touch && input.Turn == 0f) { input.HasTarget = true; input.TargetBearing = lastTarget; }
            input.Horn = hornLatch;
            return input;
        }

        /// <summary>A finger on the game (the page's buttons keep their own): the first one down
        /// turns the light. It moves an aim point, which starts on the line the light is turned
        /// to, a third of the screen's height out from the lighthouse; the lens turns towards it.
        /// True while a finger is aiming.</summary>
        bool ReadTouchAim(Vector2 lighthouse)
        {
            var screen = Touchscreen.current;
            var rig = CameraRig.Instance;
            if (screen == null || rig == null || rig.Cam == null) { aimTouch = 0; return false; }
            TouchControl finger = null;
            foreach (var t in screen.touches)
            {
                if (!t.press.isPressed) continue;
                int id = t.touchId.ReadValue();
                if (aimTouch != 0 && id == aimTouch) { finger = t; break; }
                if (aimTouch == 0 && finger == null) finger = t;
            }
            if (finger == null) { aimTouch = 0; return false; }
            var at = finger.position.ReadValue();
            int fingerId = finger.touchId.ReadValue();
            if (fingerId != aimTouch)
            {
                aimTouch = fingerId;
                aimFinger = at;
                var from = rig.Cam.WorldToScreenPoint(new Vector3(lighthouse.x, 0f, lighthouse.y));
                var dir = Geo.Dir(lastTarget);
                var ahead = rig.Cam.WorldToScreenPoint(new Vector3(lighthouse.x + dir.x * 40f, 0f, lighthouse.y + dir.y * 40f));
                var along = (Vector2)(ahead - from);
                if (along.sqrMagnitude < 1f || ahead.z < 0f) along = Vector2.up;
                aimPoint = (Vector2)from + along.normalized * Screen.height * 0.34f;
                return true;
            }
            aimPoint += (at - aimFinger) * TouchGain * Mathf.Clamp(Sensitivity, 0.5f, 2f);
            aimFinger = at;
            // Kept on the screen, so turning back is never a long way off.
            aimPoint.x = Mathf.Clamp(aimPoint.x, 0f, Screen.width);
            aimPoint.y = Mathf.Clamp(aimPoint.y, 0f, Screen.height);
            if (rig.ScreenToSea(aimPoint, out var sea))
            {
                var rel = new Vector2(sea.x, sea.z) - lighthouse;
                if (rel.sqrMagnitude > 9f) lastTarget = Geo.Bearing(rel);
            }
            return true;
        }

        /// <summary>The horn press was consumed by a sim step.</summary>
        public void ConsumeHorn() => hornLatch = false;

        public void SyncTo(float bearing) => lastTarget = bearing;
    }
}
