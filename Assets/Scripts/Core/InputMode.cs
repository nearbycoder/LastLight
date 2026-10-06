using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight.Core
{
    /// <summary>
    /// Which hands the keeper is using: a gamepad, or the mouse and keyboard. Prompts and hints
    /// follow the last device that was actually used.
    /// </summary>
    public static class InputMode
    {
        public static bool Pad { get; private set; }
        public static event Action Changed;
        static Vector2 lastMouse = new Vector2(float.NaN, float.NaN);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Pad = false; Changed = null; lastMouse = new Vector2(float.NaN, float.NaN); }

        /// <summary>The text for the device in use.</summary>
        public static string Pick(string keys, string pad) => Pad ? pad : keys;

        public static void Set(bool pad)
        {
            if (Pad == pad) return;
            Pad = pad;
            Changed?.Invoke();
        }

        public static void Update()
        {
            var pad = Gamepad.current;
            if (pad != null && PadUsed(pad)) { Set(true); return; }
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) { Set(false); return; }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var p = mouse.position.ReadValue();
                bool moved = !float.IsNaN(lastMouse.x) && (p - lastMouse).sqrMagnitude > 36f;
                lastMouse = p;
                if (moved || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) Set(false);
            }
        }

        static bool PadUsed(Gamepad pad) =>
            pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
            pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame ||
            pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame ||
            pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame ||
            pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame ||
            pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame ||
            pad.leftTrigger.ReadValue() > 0.5f || pad.rightTrigger.ReadValue() > 0.5f ||
            pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.rightStick.ReadValue().sqrMagnitude > 0.25f;
    }
}
