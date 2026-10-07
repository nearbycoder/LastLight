using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace LastLight.Core
{
    /// <summary>The keyboard's share of the keeper's controls (the mouse and the pad stay fixed).</summary>
    public enum KeeperAction { TurnLeft, TurnRight, Focus, Horn }

    /// <summary>
    /// Which keys do what: three slots per action, kept in the save. A key belongs to one slot at
    /// most, so binding it somewhere moves it from wherever it was. Esc and P always pause.
    /// </summary>
    [Serializable]
    public sealed class KeyBindings
    {
        public const int Slots = 3;
        public static readonly KeeperAction[] Actions = { KeeperAction.TurnLeft, KeeperAction.TurnRight, KeeperAction.Focus, KeeperAction.Horn };

        // Key values (UnityEngine.InputSystem.Key); 0 is an empty slot.
        public int[] turnLeft, turnRight, focus, horn;

        public KeyBindings() => Reset();

        public void Reset()
        {
            turnLeft = new[] { (int)Key.A, (int)Key.LeftArrow, 0 };
            turnRight = new[] { (int)Key.D, (int)Key.RightArrow, 0 };
            focus = new[] { (int)Key.LeftShift, (int)Key.W, (int)Key.UpArrow };
            horn = new[] { (int)Key.Space, 0, 0 };
        }

        int[] Row(KeeperAction a) => a switch
        {
            KeeperAction.TurnLeft => turnLeft,
            KeeperAction.TurnRight => turnRight,
            KeeperAction.Focus => focus,
            _ => horn,
        };

        public Key Get(KeeperAction a, int slot) => (Key)Row(a)[slot];

        /// <summary>Esc and P pause the game, so they can't be given to anything else.</summary>
        public static bool Reserved(Key k) => k == Key.Escape || k == Key.P;

        /// <summary>Puts a key in a slot, taking it from any other slot. Returns the action it was
        /// taken from (if it was another), or false if the key can't be bound.</summary>
        public bool Bind(KeeperAction a, int slot, Key key, out KeeperAction? movedFrom)
        {
            movedFrom = null;
            if (key == Key.None || Reserved(key) || !Enum.IsDefined(typeof(Key), key) || key == Key.IMESelected) return false;
            foreach (var other in Actions)
            {
                var row = Row(other);
                for (int i = 0; i < Slots; i++)
                    if (row[i] == (int)key && !(other == a && i == slot))
                    {
                        row[i] = 0;
                        if (other != a) movedFrom = other;
                    }
            }
            Row(a)[slot] = (int)key;
            return true;
        }

        public void Clear(KeeperAction a, int slot) => Row(a)[slot] = 0;

        /// <summary>The bound keys of an action, in slot order.</summary>
        public IEnumerable<Key> Keys(KeeperAction a)
        {
            foreach (var k in Row(a)) if (k != 0) yield return (Key)k;
        }

        /// <summary>Repairs a loaded save: three slots each, no reserved, unknown or repeated keys.</summary>
        public void Validate()
        {
            var seen = new HashSet<int>();
            int[] Fix(int[] row)
            {
                var fixedRow = new int[Slots];
                for (int i = 0; i < Slots && row != null && i < row.Length; i++)
                {
                    var k = (Key)row[i];
                    if (row[i] != 0 && Enum.IsDefined(typeof(Key), k) && !Reserved(k) && k != Key.IMESelected && seen.Add(row[i])) fixedRow[i] = row[i];
                }
                return fixedRow;
            }
            turnLeft = Fix(turnLeft);
            turnRight = Fix(turnRight);
            focus = Fix(focus);
            horn = Fix(horn);
        }

        public bool Held(Keyboard kb, KeeperAction a)
        {
            foreach (var k in Row(a)) if (k != 0 && Control(kb, (Key)k) is KeyControl c && c.isPressed) return true;
            return false;
        }

        public bool Pressed(Keyboard kb, KeeperAction a)
        {
            foreach (var k in Row(a)) if (k != 0 && Control(kb, (Key)k) is KeyControl c && c.wasPressedThisFrame) return true;
            return false;
        }

        static KeyControl Control(Keyboard kb, Key k)
        {
            if (kb == null) return null;
            try { return kb[k]; }
            catch (ArgumentOutOfRangeException) { return null; }
        }

        // ---------------------------------------------------------------- names

        public static string ActionName(KeeperAction a) => a switch
        {
            KeeperAction.TurnLeft => "Turn left",
            KeeperAction.TurnRight => "Turn right",
            KeeperAction.Focus => "Focus",
            _ => "Foghorn",
        };

        /// <summary>A key as the keyboard's own layout labels it ("Q" for the A key on AZERTY).</summary>
        public static string KeyName(Key k)
        {
            switch (k)
            {
                case Key.None: return "—";
                case Key.Space: return "Space";
                case Key.LeftShift: return "Shift";
                case Key.RightShift: return "Right Shift";
                case Key.LeftCtrl: return "Ctrl";
                case Key.RightCtrl: return "Right Ctrl";
                case Key.LeftAlt: return "Alt";
                case Key.RightAlt: return "Alt Gr";
                case Key.Enter: return "Enter";
                case Key.Tab: return "Tab";
                case Key.Backspace: return "Backspace";
                case Key.LeftArrow: return "←";
                case Key.RightArrow: return "→";
                case Key.UpArrow: return "↑";
                case Key.DownArrow: return "↓";
            }
            var c = Control(Keyboard.current, k);
            string name = c != null && !string.IsNullOrWhiteSpace(c.displayName) ? c.displayName : k.ToString();
            return name.Length == 1 ? name.ToUpperInvariant() : name;
        }

        /// <summary>The first key of an action ("Space"), or "" when it has none.</summary>
        public string First(KeeperAction a)
        {
            foreach (var k in Keys(a)) return KeyName(k);
            return "";
        }

        /// <summary>The turn keys as a pair ("A / D"), from the first slot each has.</summary>
        public string TurnPair()
        {
            string l = First(KeeperAction.TurnLeft), r = First(KeeperAction.TurnRight);
            return l == "" ? r : r == "" ? l : $"{l} / {r}";
        }
    }
}
