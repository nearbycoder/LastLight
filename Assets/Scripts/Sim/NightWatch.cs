using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Sim
{
    /// <summary>
    /// The Night Watch, unlocked after the last night: an endless watch over the whole bay. Every
    /// reef, sandbank and buoy is out, fog drifts through, wreckers light up later on, and ships
    /// keep coming, closer and closer together. The third wreck ends the watch.
    /// </summary>
    public static class NightWatch
    {
        public const int Number = 13;
        public const int WrecksAllowed = 2;               // the third one ends it
        public static readonly int[] Milestones = { 12, 35, 70 };    // ships home for each lamp

        public static int LampsFor(int shipsHome)
        {
            int n = 0;
            foreach (var m in Milestones) if (shipsHome >= m) n++;
            return n;
        }

        /// <summary>Seconds between ships at a given time into the watch.</summary>
        public static float Interval(float t) =>
            t < 900f ? Mathf.Lerp(22f, 10f, t / 900f) : Mathf.Lerp(10f, 7f, Mathf.Clamp01((t - 900f) / 900f));

        static readonly string[] Trawlers =
        {
            "Kittiwake", "Little Auk", "Curlew", "Dunlin", "Fulmar", "Guillemot", "Petrel", "Puffin",
            "Razorbill", "Shearwater", "Saint Brannoc", "Morwenna", "Gannet", "Cormorant", "Tern", "Whimbrel",
            "Sanderling", "Godwit", "Skua", "Merlin", "Bonny Jean", "Saint Piran", "Lowenna", "Kerensa",
            "Wren", "Sea Pink", "Jenifer", "Tamsin", "Endeavour", "Our Daisy",
        };

        static readonly string[] Steamers =
        {
            "SS Calloway", "SS Hesper", "SS Maud Ellen", "SS Penhallow", "SS Trevose", "SS Carn Brea",
            "SS Lizard Point", "SS Helford", "SS Godrevy", "SS Pendeen", "SS Zennor", "SS Boscastle",
        };

        static readonly string[] Ferries = { "Evening Star", "Morning Star", "Scillonian", "Lady of Merrow" };

        // Routes each hull keeps to (the same pairs the twelve nights use, all proven safe once charted).
        static readonly string[] SteamerRoutes = { "e1_harbor", "e2_harbor", "e2_w2", "harbor_e2", "n1_harbor", "n2_harbor", "n3_harbor", "w1_e1", "w1_harbor", "w2_e2" };
        static readonly string[] FerryRoutes = { "e1_harbor", "e2_harbor", "e2_w2", "harbor_e2", "w1_e1", "w2_e2" };

        public static MissionDef Generate(MapData map, int seed, float hours = 1f)
        {
            var rng = new System.Random(seed);
            var routes = new List<string>(map.Routes.Keys);
            routes.Sort();
            var groups = new SortedSet<string>();
            foreach (var r in map.Reefs) groups.Add(r.group);
            var shoals = new List<string>();
            foreach (var s in map.Shoals) shoals.Add(s.Id);
            var buoys = new List<string>();
            foreach (var b in map.Buoys) buoys.Add(b.id);

            var ships = new List<SpawnDef>();
            var lastOnRoute = new Dictionary<string, float>();
            var used = new Dictionary<string, int>();
            float t = 3f;
            while (t < hours * 3600f)
            {
                double roll = rng.NextDouble();
                // Steamers and the ferries join once the watch is under way.
                string type = t < 60f || roll < 0.6 ? "trawler" : roll < 0.85 ? "steamer" : "ferry";
                var pool = type == "steamer" ? SteamerRoutes : type == "ferry" ? FerryRoutes : routes.ToArray();
                string route = pool[rng.Next(pool.Length)];
                // Keep ships on the same route well apart, so they don't sail through each other.
                for (int tries = 0; tries < 6 && lastOnRoute.TryGetValue(route, out float last) && t - last < 30f; tries++)
                    route = pool[rng.Next(pool.Length)];
                lastOnRoute[route] = t;
                var names = type == "steamer" ? Steamers : type == "ferry" ? Ferries : Trawlers;
                string name = names[rng.Next(names.Length)];
                used.TryGetValue(name, out int count);
                used[name] = count + 1;
                if (count > 0) name += " " + Roman(count + 1);
                ships.Add(new SpawnDef
                {
                    t = t, type = type, route = route, name = name, captain = "",
                    damaged = type == "trawler" && t > 240f && rng.NextDouble() < 0.1,
                });
                t += Interval(t) * (0.75f + 0.5f * (float)rng.NextDouble());
            }

            return new MissionDef
            {
                id = "nightwatch", night = Number, title = "Night Watch", date = "Every night after",
                briefing = "The Board's kept you on, keeper. No more schedules: they'll come as the sea sends them, all night, every night. Three wrecks and they'll send someone else.",
                newThing = "watch", allowedWrecks = WrecksAllowed, drainScale = 1f, endless = true,
                reefGroups = new List<string>(groups).ToArray(), shoals = shoals.ToArray(), buoys = buoys.ToArray(),
                foghorn = true, haze = 1.15f,
                fog = new[]
                {
                    new FogDef { x = -60f, z = 95f, r = 30f, density = 0.8f, vx = 0.9f, vz = -0.25f },
                    new FogDef { x = 70f, z = 20f, r = 26f, density = 0.7f, vx = -0.7f, vz = 0.3f },
                },
                wreckers = new[]
                {
                    new WreckerDef { sites = new[] { "corley", "blackhen", "westpoint" }, start = 240f, relight = 40f, sweep = 16f },
                    new WreckerDef { sites = new[] { "sentinels", "westpoint", "corley" }, start = 720f, relight = 55f, sweep = 20f },
                },
                ships = ships.ToArray(),
                radio = new[]
                {
                    new RadioCue { t = 4f, who = "ianto", text = "Long night ahead. Keep them coming home." },
                    new RadioCue { t = 236f, who = "ianto", text = "Lanterns on the cliffs again. Douse them before they draw anyone in." },
                    new RadioCue { t = 600f, who = "ianto", text = "Ten minutes and the light's still burning. Good." },
                    new RadioCue { t = 1200f, who = "ianto", text = "Busiest night I've seen. Steady, keeper." },
                    new RadioCue { on = "firstWreck", who = "ianto", text = "One lost. The Board allows three. Mind the rest." },
                    new RadioCue { on = "fail", who = "ianto", text = "That's three. The watch is over, keeper. Come down and get some sleep." },
                },
                hints = new HintCue[0],
            };
        }

        static string Roman(int n) => n switch { 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", _ => n.ToString() };
    }
}
