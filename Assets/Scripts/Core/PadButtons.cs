using System;
using UnityEngine.InputSystem;

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

        public static string West => Others(Style).west;
        public static string LeftShoulder => Others(Style).leftShoulder;
        public static string RightShoulder => Others(Style).rightShoulder;

        public static readonly string[] Choices = { "Auto", "Xbox", "PlayStation", "Nintendo" };
    }
}
