using LastLight.Sim;
using LastLight.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight.Core
{
    /// <summary>
    /// Reads the keeper's hands: the mouse aims the lens (with its weight handled by the sim),
    /// LMB, the focus keys or the focus buttons focus, RMB, the horn keys or the horn buttons sound
    /// the foghorn, the turn keys turn the lens directly, and the right stick points it. The keys and
    /// buttons are the player's (see KeyBindings and PadBindings; by default Shift/W/↑, Space and
    /// A-D/arrows, and the triggers and A). With <see cref="ToggleFocus"/> a press switches focus on or
    /// off instead of having to be held.
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

            if (mouse != null)
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

            if (!input.HasTarget && input.Turn == 0f && usingMouse) input.HasTarget = true;
            input.Horn = hornLatch;
            return input;
        }

        /// <summary>The horn press was consumed by a sim step.</summary>
        public void ConsumeHorn() => hornLatch = false;

        public void SyncTo(float bearing) => lastTarget = bearing;
    }
}
