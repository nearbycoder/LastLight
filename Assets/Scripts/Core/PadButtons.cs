using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LastLight.Core
{
    /// <summary>Whose names the gamepad's buttons go by in prompts.</summary>
    public enum PadStyle { Auto, Xbox, PlayStation, Nintendo }

    /// <summary>
    /// The pad's buttons as the keeper's pad labels them. Settings ▸ Pad buttons chooses Xbox (A, B,
    /// RT, Start), PlayStation (Cross, Circle, R2, Options) or Nintendo (B, A, ZR, +; by position, so
    /// the bottom button is B), or Auto, which goes by the pad's name where Unity reports one.
    /// </summary>
    public static class PadButtons
    {
        /// <summary>The style in use: the keeper's choice, or the pad in hand's on Auto.</summary>
        public static PadStyle Style => StyleFor(SaveData.Current);

        public static PadStyle StyleFor(SaveData save)
        {
            var chosen = (PadStyle)UnityEngine.Mathf.Clamp(save.padStyle, 0, 3);
            return chosen != PadStyle.Auto ? chosen : Detect(Gamepad.current);
        }

        /// <summary>A guess from the pad's layout and names: Sony's pads read PlayStation, Nintendo's
        /// Nintendo, anything else Xbox (the usual layout on PC, and Steam Input's).</summary>
        public static PadStyle Detect(Gamepad pad)
        {
            if (pad == null) return PadStyle.Xbox;
            if (pad is UnityEngine.InputSystem.DualShock.DualShockGamepad) return PadStyle.PlayStation;
            return Detect(pad.name, pad.displayName, pad.description.product, pad.description.manufacturer, pad.layout);
        }

        public static PadStyle Detect(params string[] names)
        {
            string all = string.Join(" ", names ?? Array.Empty<string>()).ToLowerInvariant();
            // Microsoft's pads are "Wireless Controller"s too.
            foreach (var n in new[] { "xbox", "x-box", "microsoft" })
                if (all.Contains(n)) return PadStyle.Xbox;
            foreach (var n in new[] { "dualsense", "dualshock", "playstation", "sony", "ps4", "ps5", "wireless controller" })
                if (all.Contains(n)) return PadStyle.PlayStation;
            foreach (var n in new[] { "nintendo", "switch", "pro controller", "joy-con", "joycon" })
                if (all.Contains(n)) return PadStyle.Nintendo;
            return PadStyle.Xbox;
        }

        public static string South => For(Style).south;
        public static string East => For(Style).east;
        public static string RightTrigger => For(Style).trigger;
        public static string Start => For(Style).start;

        public static (string south, string east, string trigger, string start) For(PadStyle style) => style switch
        {
            PadStyle.PlayStation => ("Cross", "Circle", "R2", "Options"),
            PadStyle.Nintendo => ("B", "A", "ZR", "+"),
            _ => ("A", "B", "RT", "Start"),
        };

        /// <summary>The left face button and the shoulder buttons, which only the replay uses.</summary>
        public static (string west, string leftShoulder, string rightShoulder) Others(PadStyle style) => style switch
        {
            PadStyle.PlayStation => ("Square", "L1", "R1"),
            PadStyle.Nintendo => ("Y", "L", "R"),
            _ => ("X", "LB", "RB"),
        };

        /// <summary>Any bindable button by the style's name for it ("RB", "R1" or "R").</summary>
        public static string Name(GamepadButton b, PadStyle style)
        {
            var main = For(style);
            var others = Others(style);
            bool ps = style == PadStyle.PlayStation, nin = style == PadStyle.Nintendo;
            return b switch
            {
                GamepadButton.South => main.south,
                GamepadButton.East => main.east,
                GamepadButton.West => others.west,
                GamepadButton.North => ps ? "Triangle" : nin ? "X" : "Y",
                GamepadButton.LeftShoulder => others.leftShoulder,
                GamepadButton.RightShoulder => others.rightShoulder,
                GamepadButton.LeftTrigger => ps ? "L2" : nin ? "ZL" : "LT",
                GamepadButton.RightTrigger => main.trigger,
                GamepadButton.LeftStick => ps ? "L3" : "LS",
                GamepadButton.RightStick => ps ? "R3" : "RS",
                GamepadButton.DpadUp => "D-pad ↑",
                GamepadButton.DpadDown => "D-pad ↓",
                GamepadButton.DpadLeft => "D-pad ←",
                GamepadButton.DpadRight => "D-pad →",
                GamepadButton.Start => main.start,
                GamepadButton.Select => ps ? "Create" : nin ? "−" : "View",
                _ => b.ToString(),
            };
        }

        /// <summary>The foghorn's and focus's first bound buttons, as the pad in use names them.</summary>
        public static string Horn => Name(SaveData.Current.pad.First(KeeperAction.Horn), Style);
        public static string Focus => Name(SaveData.Current.pad.First(KeeperAction.Focus), Style);

        /// <summary>Focus's button in a sentence: "the right trigger" (as the prompts always said), or its name.</summary>
        public static string FocusWords => FocusWordsFor(SaveData.Current);

        public static string FocusWordsFor(SaveData save) => (save.pad ?? new PadBindings()).First(KeeperAction.Focus) switch
        {
            GamepadButton.RightTrigger => "the right trigger",
            GamepadButton.LeftTrigger => "the left trigger",
            var b => Name(b, StyleFor(save)),
        };

        /// <summary>The button that empties a pad slot in Settings ▸ Keys and buttons.</summary>
        public static string Select => Name(GamepadButton.Select, Style);

        public static string West => Others(Style).west;
        public static string LeftShoulder => Others(Style).leftShoulder;
        public static string RightShoulder => Others(Style).rightShoulder;

        public static readonly string[] Choices = { "Auto", "Xbox", "PlayStation", "Nintendo" };
    }
}
