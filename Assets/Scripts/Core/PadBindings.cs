using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace LastLight.Core
{
    /// <summary>
    /// The gamepad's share of the keeper's controls: two buttons each for focus and the foghorn,
    /// kept in the save (the sticks always aim). A button belongs to one slot at most, so binding it
    /// somewhere moves it from wherever it was. Start always pauses and View (Select) empties a slot
    /// while one is waiting for a button, so neither can be bound; and each action keeps a button.
    /// </summary>
    [Serializable]
    public sealed class PadBindings
    {
        public const int Slots = 2;
        public const int Empty = -1;
        public static readonly KeeperAction[] Actions = { KeeperAction.Focus, KeeperAction.Horn };

        /// <summary>The buttons that can be bound, in the order a waiting slot looks for them.</summary>
        public static readonly GamepadButton[] Bindable =
        {
            GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North,
            GamepadButton.LeftShoulder, GamepadButton.RightShoulder, GamepadButton.LeftTrigger, GamepadButton.RightTrigger,
            GamepadButton.LeftStick, GamepadButton.RightStick,
            GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight,
        };

        // GamepadButton values; -1 is an empty slot (0 is the d-pad's up).
        public int[] focus, horn;

        public PadBindings() => Reset();

        public void Reset()
        {
            focus = new[] { (int)GamepadButton.RightTrigger, (int)GamepadButton.LeftTrigger };
            horn = new[] { (int)GamepadButton.South, Empty };
        }

        int[] Row(KeeperAction a) => a == KeeperAction.Horn ? horn : focus;
        KeeperAction Other(KeeperAction a) => a == KeeperAction.Horn ? KeeperAction.Focus : KeeperAction.Horn;

        public static bool CanBind(KeeperAction a) => a == KeeperAction.Focus || a == KeeperAction.Horn;

        /// <summary>The button in a slot, or null when it's empty.</summary>
        public GamepadButton? Get(KeeperAction a, int slot)
        {
            int v = Row(a)[slot];
            return v == Empty ? (GamepadButton?)null : (GamepadButton)v;
        }

        public static bool IsBindable(GamepadButton b) => Array.IndexOf(Bindable, b) >= 0;

        int Count(KeeperAction a)
        {
            int n = 0;
            foreach (var v in Row(a)) if (v != Empty) n++;
            return n;
        }

        /// <summary>Puts a button in a slot. A button the other action has moves over; if it was
        /// that action's only button, the button this slot held goes to it in exchange (and with
        /// nothing to exchange, the bind is refused). Returns false if the button can't be bound.</summary>
        public bool Bind(KeeperAction a, int slot, GamepadButton button, out KeeperAction? movedFrom, out GamepadButton? swapped)
        {
            movedFrom = null;
            swapped = null;
            if (!CanBind(a) || !IsBindable(button)) return false;
            var row = Row(a);
            int previous = row[slot];
            var other = Other(a);
            var otherRow = Row(other);
            int at = Array.IndexOf(otherRow, (int)button);
            if (at >= 0)
            {
                if (Count(other) == 1)
                {
                    // Taking the other action's last button: it gets this slot's in exchange.
                    if (previous == Empty) return false;
                    otherRow[at] = previous;
                    swapped = (GamepadButton)previous;
                }
                else otherRow[at] = Empty;
                movedFrom = other;
            }
            // Within the action, the button leaves its other slot.
            for (int i = 0; i < Slots; i++) if (i != slot && row[i] == (int)button) row[i] = Empty;
            row[slot] = (int)button;
            return true;
        }

        /// <summary>Empties a slot, unless it holds the action's last button.</summary>
        public bool Clear(KeeperAction a, int slot)
        {
            var row = Row(a);
            if (row[slot] == Empty) return true;
            if (Count(a) == 1) return false;
            row[slot] = Empty;
            return true;
        }

        /// <summary>The bound buttons of an action, in slot order.</summary>
        public IEnumerable<GamepadButton> Buttons(KeeperAction a)
        {
            foreach (var v in Row(a)) if (v != Empty) yield return (GamepadButton)v;
        }

        /// <summary>The first button of an action (every action has one once validated).</summary>
        public GamepadButton First(KeeperAction a)
        {
            foreach (var b in Buttons(a)) return b;
            return a == KeeperAction.Horn ? GamepadButton.South : GamepadButton.RightTrigger;
        }

        /// <summary>Repairs a loaded save: two slots each, only bindable buttons, none twice, and a
        /// button for each action (or the defaults, if that can't be had).</summary>
        public void Validate()
        {
            var seen = new HashSet<int>();
            int[] Fix(int[] row)
            {
                var fixedRow = new[] { Empty, Empty };
                for (int i = 0; i < Slots && row != null && i < row.Length; i++)
                    if (Enum.IsDefined(typeof(GamepadButton), row[i]) && IsBindable((GamepadButton)row[i]) && seen.Add(row[i])) fixedRow[i] = row[i];
                return fixedRow;
            }
            focus = Fix(focus);
            horn = Fix(horn);
            if (Count(KeeperAction.Focus) == 0 || Count(KeeperAction.Horn) == 0) Reset();
        }

        static ButtonControl Control(Gamepad pad, GamepadButton b)
        {
            if (pad == null) return null;
            try { return pad[b]; }
            catch (ArgumentOutOfRangeException) { return null; }
        }

        static bool IsTrigger(GamepadButton b) => b == GamepadButton.LeftTrigger || b == GamepadButton.RightTrigger;

        /// <summary>A trigger counts as held from a light touch, as it always has; a button when it's down.</summary>
        public bool Held(Gamepad pad, KeeperAction a)
        {
            foreach (var b in Buttons(a))
                if (Control(pad, b) is ButtonControl c && (IsTrigger(b) ? c.ReadValue() > 0.3f : c.isPressed)) return true;
            return false;
        }

        public bool Pressed(Gamepad pad, KeeperAction a)
        {
            foreach (var b in Buttons(a))
                if (Control(pad, b) is ButtonControl c && c.wasPressedThisFrame) return true;
            return false;
        }

        /// <summary>The bindable button pressed this frame on the pad, if any.</summary>
        public static GamepadButton? PressedNow(Gamepad pad)
        {
            foreach (var b in Bindable)
                if (Control(pad, b) is ButtonControl c && c.wasPressedThisFrame) return b;
            return null;
        }
    }
}
