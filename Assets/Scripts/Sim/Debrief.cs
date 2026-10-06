using System.Collections.Generic;

namespace LastLight.Sim
{
    /// <summary>
    /// The dawn debrief: what happened to each ship that didn't have a clean night, in plain words,
    /// most serious first. Wrecks, then ships lured by a false light, then ships that lost their way.
    /// </summary>
    public static class Debrief
    {
        public enum Kind { Wreck, Lured, Lost }

        public struct Line
        {
            public Kind Kind;
            public string Ship;
            public string Text;
            public override string ToString() => $"{Ship}: {Text}";
        }

        public static List<Line> Lines(SimWorld w, bool wrecksOnly = false)
        {
            var wrecks = new List<SimShip>();
            var lured = new List<SimShip>();
            var lost = new List<SimShip>();
            foreach (var s in w.Ships)
            {
                if (s.State == ShipState.Wrecked) wrecks.Add(s);
                else if (wrecksOnly) continue;
                else if (s.LuredCount > 0) lured.Add(s);
                else if (s.LostCount > 0) lost.Add(s);
            }
            wrecks.Sort((a, b) => a.WreckTime.CompareTo(b.WreckTime));
            lured.Sort((a, b) => b.LuredCount.CompareTo(a.LuredCount));
            lost.Sort((a, b) => b.LostCount.CompareTo(a.LostCount));

            var lines = new List<Line>();
            foreach (var s in wrecks) lines.Add(new Line { Kind = Kind.Wreck, Ship = s.Name, Text = WreckText(s) });
            foreach (var s in lured) lines.Add(new Line { Kind = Kind.Lured, Ship = s.Name, Text = LuredText(s) });
            foreach (var s in lost) lines.Add(new Line { Kind = Kind.Lost, Ship = s.Name, Text = "lost its way " + Times(s.LostCount) });
            return lines;
        }

        /// <summary>The wreck that broke the night's allowance (the last one), or null.</summary>
        public static SimShip FinalWreck(SimWorld w)
        {
            SimShip last = null;
            foreach (var s in w.Ships)
                if (s.State == ShipState.Wrecked && (last == null || s.WreckTime > last.WreckTime)) last = s;
            return last;
        }

        /// <summary>"struck the Merrow Teeth, uncharted" and the like.</summary>
        public static string WreckText(SimShip s)
        {
            string where = s.WreckShoal ? "the " + s.WreckCause : s.WreckCause;
            string hit = s.WreckShoal ? "ran aground on " + where : where == "the shore" ? "ran ashore" : "struck " + where;
            switch (s.WreckedWhile)
            {
                case ShipState.Lured:
                    return string.IsNullOrEmpty(s.LuredAt) ? $"lured by a false light, {hit}" : $"lured by the false light at {s.LuredAt}, {hit}";
                case ShipState.Lost:
                    return $"lost in the dark, {hit}";
                default:
                    if (where == "the shore") return hit;
                    return !s.WreckCharted ? $"{hit}, uncharted" : s.WreckLate ? $"{hit}, charted too late to turn" : $"{hit} despite the chart";
            }
        }

        static string LuredText(SimShip s)
        {
            string at = string.IsNullOrEmpty(s.LuredAt) ? "a false light" : "the false light at " + s.LuredAt;
            return s.LuredCount == 1 ? $"lured toward {at}, then freed" : $"lured {Times(s.LuredCount)}, last toward {at}";
        }

        public static string Times(int n) => n switch { 1 => "once", 2 => "twice", _ => n + " times" };
    }
}
