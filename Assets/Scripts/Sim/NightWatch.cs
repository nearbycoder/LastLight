using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Sim
{
    /// <summary>
    /// The Night Watch, unlocked after the last night: an endless watch over the whole bay. Every
    /// reef, sandbank and buoy is out, and ships keep coming, closer and closer together. Each watch
    /// draws its own weather from its seed: fog banks in different places, squalls blowing through,
    /// and wreckers lighting up at their own times. The third wreck ends the watch.
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

        /// <summary>On Hard, ships come about 15% closer together.</summary>
        public static MissionDef Generate(MapData map, int seed, float hours = 1f, bool hard = false)
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
                t += Interval(t) * (0.75f + 0.5f * (float)rng.NextDouble()) * (hard ? 0.85f : 1f);
            }

            // The night's weather and wreckers come from their own stream, so a seed's ships stay put.
            var wx = new System.Random(seed * 7919 + 13);
            var fog = Fog(wx, out string fogWhere);
            var squalls = Squalls(wx, hours * 3600f);
            var wreckers = Wreckers(wx);
            var radio = new List<RadioCue>
            {
                new RadioCue { t = 4f, who = "ianto", text = "Long night ahead. Keep them coming home." },
                new RadioCue { t = wreckers[0].start - 4f, who = "ianto", text = "Lanterns on the cliffs again. Douse them before they draw anyone in." },
                new RadioCue { t = wreckers[2].start - 4f, who = "ianto", text = "That one off the Sentinels turns just like yours, keeper. Stay on your ships." },
                new RadioCue { t = 600f, who = "ianto", text = "Ten minutes and the light's still burning. Good." },
                new RadioCue { t = 1200f, who = "ianto", text = "Busiest night I've seen. Steady, keeper." },
                new RadioCue { on = "firstWreck", who = "ianto", text = "One lost. The Board allows three. Mind the rest." },
                new RadioCue { on = "fail", who = "ianto", text = "That's three. The watch is over, keeper. Come down and get some sleep." },
            };
            for (int i = 0; i < squalls.Length; i++)
            {
                var q = squalls[i];
                string line = i == 0 ? $"First squall of the night, coming in from the {q.from}. It'll set them {Towards(q)}."
                    : wx.Next(2) == 0 ? $"Another squall from the {q.from}. Mind the current." : $"Here's more weather off the {q.from}, keeper. Hold them steady.";
                radio.Add(new RadioCue { t = Mathf.Max(5f, q.t - 6f), who = "ianto", text = line });
            }

            string forecast = $"Tonight: fret {fogWhere}, squalls from the {squalls[0].from} after about {Words(Mathf.RoundToInt(squalls[0].t / 60f))} minutes, and the Corleys out {(wreckers[0].start < 260f ? "early" : "later on")}.";
            return new MissionDef
            {
                id = "nightwatch", night = Number, title = "Night Watch", date = "Every night after",
                briefing = "No more schedules, keeper: they'll come as the sea sends them. Three wrecks and the Board sends someone else. " + forecast,
                newThing = "watch", allowedWrecks = WrecksAllowed, drainScale = 1f, endless = true,
                reefGroups = new List<string>(groups).ToArray(), shoals = shoals.ToArray(), buoys = buoys.ToArray(),
                foghorn = true, haze = 1.15f,
                fog = fog,
                squalls = squalls,
                wreckers = wreckers,
                ships = ships.ToArray(),
                radio = radio.ToArray(),
                hints = new HintCue[0],
            };
        }

        // Places a fog bank can roll in, named for the forecast.
        static readonly (string name, float x, float z)[] FogPlaces =
        {
            ("off Black Hen", -50f, 70f), ("over the Teeth", 55f, 62f), ("on Widow's Ledge", 92f, 28f),
            ("on the Long Sands", -62f, 22f), ("off West Point", -105f, 30f), ("out in the open sea", 5f, 100f),
            ("round the Sentinels", 28f, 22f),
        };

        static FogDef[] Fog(System.Random wx, out string where)
        {
            int count = 1 + wx.Next(3);
            var order = new List<int>();
            for (int i = 0; i < FogPlaces.Length; i++) order.Insert(wx.Next(order.Count + 1), i);
            var banks = new FogDef[count];
            var names = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var p = FogPlaces[order[i]];
                float ang = (float)(wx.NextDouble() * Mathf.PI * 2.0);
                float speed = Mathf.Lerp(0.5f, 1.0f, (float)wx.NextDouble());
                banks[i] = new FogDef
                {
                    x = p.x + Mathf.Lerp(-8f, 8f, (float)wx.NextDouble()), z = p.z + Mathf.Lerp(-8f, 8f, (float)wx.NextDouble()),
                    r = Mathf.Lerp(22f, 32f, (float)wx.NextDouble()), density = Mathf.Lerp(0.6f, 0.85f, (float)wx.NextDouble()),
                    vx = Mathf.Cos(ang) * speed, vz = Mathf.Sin(ang) * speed * 0.5f,
                };
                names.Add(p.name);
            }
            where = names.Count == 1 ? names[0] : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
            return banks;
        }

        // Where squalls blow from, with the way the current sets (bearing convention: 0 = north).
        static readonly (string from, float deg)[] Winds =
        {
            ("north", 0f), ("north-east", 45f), ("east", 90f), ("west", 270f), ("north-west", 315f),
        };

        /// <summary>Squalls through the watch: the first after five to nine minutes, then every four to
        /// seven, each a minute or two long, and stronger as the night wears on.</summary>
        static SquallDef[] Squalls(System.Random wx, float until)
        {
            var list = new List<SquallDef>();
            var wind = Winds[wx.Next(Winds.Length)];
            float t = Mathf.Lerp(300f, 540f, (float)wx.NextDouble());
            while (t < until)
            {
                float dur = Mathf.Lerp(60f, 120f, (float)wx.NextDouble());
                float deg = wind.deg + Mathf.Lerp(-25f, 25f, (float)wx.NextDouble());
                float strength = Mathf.Lerp(0.55f, 0.85f, Mathf.Clamp01(t / 1800f));
                // The current runs downwind: away from where the squall blows from.
                var set = Geo.Dir((deg + 180f) * Mathf.Deg2Rad) * strength;
                list.Add(new SquallDef
                {
                    t = t, dur = dur, cx = set.x, cz = set.y,
                    rain = Mathf.Lerp(0.6f, 0.9f, (float)wx.NextDouble()), from = wind.from,
                });
                t += dur + Mathf.Lerp(240f, 420f, (float)wx.NextDouble());
            }
            return list.ToArray();
        }

        static string Towards(SquallDef q) => q.cz < -0.2f ? "down onto the coast" : q.cx > 0.2f ? "east, towards the Ledge" : q.cx < -0.2f ? "west, towards the Sands" : "out to sea";

        static WreckerDef[] Wreckers(System.Random wx)
        {
            var sites = new List<string> { "corley", "blackhen", "westpoint", "sentinels" };
            var shuffled = new List<string>();
            foreach (var site in sites) shuffled.Insert(wx.Next(shuffled.Count + 1), site);
            return new[]
            {
                new WreckerDef { sites = new[] { shuffled[0], shuffled[1], shuffled[2] }, start = Mathf.Lerp(180f, 360f, (float)wx.NextDouble()), relight = Mathf.Lerp(35f, 50f, (float)wx.NextDouble()), sweep = Mathf.Lerp(15f, 19f, (float)wx.NextDouble()) },
                new WreckerDef { sites = new[] { shuffled[3], shuffled[1], shuffled[0] }, start = Mathf.Lerp(600f, 900f, (float)wx.NextDouble()), relight = Mathf.Lerp(50f, 65f, (float)wx.NextDouble()), sweep = Mathf.Lerp(18f, 22f, (float)wx.NextDouble()) },
                // Late in a long watch, the Corleys bring out the light that turns like yours.
                new WreckerDef { sites = new[] { "sentinels", "sentinels" }, start = Mathf.Lerp(1200f, 1500f, (float)wx.NextDouble()), relight = 60f, sweep = 24f, mimic = true },
            };
        }

        static string Words(int n) => n switch { 3 => "three", 4 => "four", 5 => "five", 6 => "six", 7 => "seven", 8 => "eight", 9 => "nine", 10 => "ten", _ => n.ToString() };

        static string Roman(int n) => n switch { 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", _ => n.ToString() };
    }
}
