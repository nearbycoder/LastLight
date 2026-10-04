using LastLight.Sim;
using LastLight.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight.Core
{
    /// <summary>
    /// Reads the keeper's hands: the mouse aims the lens (with its weight handled by the sim),
    /// LMB/Shift/RT focuses, Space/RMB/A sounds the foghorn, A-D/arrows turn the lens directly,
    /// and the right stick points it.
    /// </summary>
    public sealed class KeeperControls
    {
        bool usingMouse = true;
        Vector2 lastMouse;
        float lastTarget;
        bool hornLatch;
        public float Sensitivity = 1f;

        public KeeperInput Read(Vector2 lighthouse)
        {
            var input = new KeeperInput { TargetBearing = lastTarget };
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            var pad = Gamepad.current;

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
                if (mouse.leftButton.isPressed) input.Focus = true;
                if (mouse.rightButton.wasPressedThisFrame) hornLatch = true;
            }

            if (kb != null)
            {
                float turn = 0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) turn -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) turn += 1f;
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
                if (kb.leftShiftKey.isPressed || kb.wKey.isPressed || kb.upArrowKey.isPressed) input.Focus = true;
                if (kb.spaceKey.wasPressedThisFrame) hornLatch = true;
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
                if (pad.rightTrigger.ReadValue() > 0.3f || pad.leftTrigger.ReadValue() > 0.3f) input.Focus = true;
                if (pad.buttonSouth.wasPressedThisFrame) hornLatch = true;
            }

            if (!input.HasTarget && input.Turn == 0f && usingMouse) input.HasTarget = true;
            input.Horn = hornLatch;
            return input;
        }

        /// <summary>The horn press was consumed by a sim step.</summary>
        public void ConsumeHorn() => hornLatch = false;

        public void SyncTo(float bearing) => lastTarget = bearing;
    }
}
