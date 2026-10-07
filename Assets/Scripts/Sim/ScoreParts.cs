namespace LastLight.Sim
{
    /// <summary>
    /// Where a night's score comes from, for the dawn card: each ship home earns its hull's points
    /// (a trawler 100, a steamer 150, the ferry 250), twice that if it ran dark, and 50 more for a
    /// steady hand (never lost or lured). The parts always add up to <see cref="SimWorld.Score"/>.
    /// </summary>
    public readonly struct ScoreParts
    {
        public const int SteadyBonus = 50;

        public readonly int Ships, HullPoints;        // ships home, and their hulls' points
        public readonly int Dark, DarkPoints;         // ships home that ran dark, and the extra they earned
        public readonly int Steady, SteadyPoints;     // ships home with a steady hand, and the bonus

        public int Total => HullPoints + DarkPoints + SteadyPoints;

        ScoreParts(int ships, int hull, int dark, int darkPoints, int steady)
        {
            Ships = ships; HullPoints = hull; Dark = dark; DarkPoints = darkPoints;
            Steady = steady; SteadyPoints = steady * SteadyBonus;
        }

        /// <summary>What one ship earns on reaching home.</summary>
        public static int ShipPoints(SimShip s) => s.Stats.Points * (s.Damaged ? 2 : 1) + (s.SteadyHand ? SteadyBonus : 0);

        public static ScoreParts Of(SimWorld w)
        {
            int ships = 0, hull = 0, dark = 0, darkPoints = 0, steady = 0;
            foreach (var s in w.Ships)
            {
                if (s.State != ShipState.Arrived) continue;
                ships++;
                hull += s.Stats.Points;
                if (s.Damaged) { dark++; darkPoints += s.Stats.Points; }
                if (s.SteadyHand) steady++;
            }
            return new ScoreParts(ships, hull, dark, darkPoints, steady);
        }

        /// <summary>The parts in a line: "5 ships home 600  ·  1 ran dark, double +100  ·  4 steady hands +200".</summary>
        public string Line()
        {
            if (Ships == 0) return "";
            string line = $"{(Ships == 1 ? "1 ship" : Ships + " ships")} home  {HullPoints:N0}";
            if (Dark > 0) line += $"   ·   {(Dark == 1 ? "1 ran" : Dark + " ran")} dark, double  +{DarkPoints:N0}";
            if (Steady > 0) line += $"   ·   {(Steady == 1 ? "1 steady hand" : Steady + " steady hands")}  +{SteadyPoints:N0}";
            return line;
        }
    }
}
