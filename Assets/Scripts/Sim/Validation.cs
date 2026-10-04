using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Sim
{
    /// <summary>
    /// Automated proofs about the content, used by the EditMode tests and Tools/validate.sh:
    /// the data is consistent, every route is safe for every hull once its hazards are charted,
    /// no hazard sits where the beam can never reach, and every night can be won by the AutoKeeper.
    /// </summary>
    public static class Validation
    {
        const float Dt = 1f / 60f;

        public static MapData FreshMap() => MapData.Parse(Resources.Load<TextAsset>("Data/merrow_bay").text);

        public static List<MissionDef> FreshMissions() =>
            new List<MissionDef>(JsonUtility.FromJson<MissionFile>(Resources.Load<TextAsset>("Data/missions").text).missions);

        /// <summary>Every reference in the missions resolves against the map.</summary>
        public static List<string> DataIntegrity(MapData map, List<MissionDef> missions)
        {
            var errors = new List<string>();
            var groups = new HashSet<string>();
            foreach (var r in map.Reefs) groups.Add(r.group);
            var buoys = new HashSet<string>();
            foreach (var b in map.Buoys) buoys.Add(b.id);
            var shoals = new HashSet<string>();
            foreach (var s in map.Shoals) shoals.Add(s.Id);
            if (missions.Count != 12) errors.Add($"expected 12 nights, found {missions.Count}");
            for (int i = 0; i < missions.Count; i++)
            {
                var m = missions[i];
                if (m.night != i + 1) errors.Add($"{m.id}: night number {m.night} out of order");
                if (m.ships == null || m.ships.Length == 0) errors.Add($"{m.id}: no ships");
                foreach (var s in m.ships ?? new SpawnDef[0])
                {
                    if (!map.Routes.ContainsKey(s.route)) errors.Add($"{m.id}: ship {s.name} has unknown route {s.route}");
                    if (s.type != "trawler" && s.type != "steamer" && s.type != "ferry") errors.Add($"{m.id}: ship {s.name} has unknown type {s.type}");
                }
                foreach (var g in m.reefGroups ?? new string[0]) if (!groups.Contains(g)) errors.Add($"{m.id}: unknown reef group {g}");
                foreach (var b in m.buoys ?? new string[0]) if (!buoys.Contains(b)) errors.Add($"{m.id}: unknown buoy {b}");
                foreach (var s in m.shoals ?? new string[0]) if (!shoals.Contains(s)) errors.Add($"{m.id}: unknown shoal {s}");
                foreach (var w in m.wreckers ?? new WreckerDef[0])
                    foreach (var site in w.sites) if (!map.WreckerSites.ContainsKey(site)) errors.Add($"{m.id}: unknown wrecker site {site}");
                if (m.allowedWrecks < 0 || m.allowedWrecks >= (m.ships?.Length ?? 0)) errors.Add($"{m.id}: odd wreck allowance {m.allowedWrecks}");
            }
            return errors;
        }

        /// <summary>Hazards and buoys must be reachable by the beam (not in a stack's shadow, not off range).</summary>
        public static List<string> Reachability(MapData map)
        {
            var errors = new List<string>();
            var def = new MissionDef { id = "reach", reefGroups = AllGroups(map), ships = new SpawnDef[0] };
            var w = new SimWorld(map, def);
            foreach (var r in w.Reefs)
            {
                if (w.Occlusion(map.Lighthouse, r.Pos) < 0.5f) errors.Add($"reef {r.Id} at {r.Pos} is in a beam shadow");
                if (Vector2.Distance(r.Pos, map.Lighthouse) > SimBeam.FocusRange * 0.85f) errors.Add($"reef {r.Id} is beyond the focused beam");
            }
            foreach (var b in map.Buoys)
            {
                var p = new Vector2(b.x, b.z);
                if (w.Occlusion(map.Lighthouse, p) < 0.5f) errors.Add($"buoy {b.id} is in a beam shadow");
            }
            return errors;
        }

        static string[] AllGroups(MapData map)
        {
            var set = new HashSet<string>();
            foreach (var r in map.Reefs) set.Add(r.group);
            return new List<string>(set).ToArray();
        }

        /// <summary>
        /// Each route, sailed alone by each hull with every hazard permanently charted and the
        /// captain fully confident, must reach its destination: the steering can always find the way.
        /// </summary>
        public static List<string> RouteSafety(MapData map)
        {
            var errors = new List<string>();
            var shoalIds = new List<string>();
            foreach (var s in map.Shoals) shoalIds.Add(s.Id);
            foreach (var route in map.Routes.Keys)
                foreach (var type in new[] { "trawler", "steamer", "ferry" })
                {
                    var def = new MissionDef
                    {
                        id = "route", drainScale = 0f, reefGroups = AllGroups(map), shoals = shoalIds.ToArray(),
                        ships = new[] { new SpawnDef { t = 0, type = type, route = route, name = "Test" } },
                    };
                    var w = new SimWorld(map, def);
                    string result = "did not arrive within 400 s";
                    for (int i = 0; i < 60 * 400; i++)
                    {
                        foreach (var r in w.Reefs) r.ChartTimer = 999f;
                        foreach (var sh in w.Shoals) sh.ChartTimer = 999f;
                        w.Step(Dt, default);
                        if (w.Ships.Count == 0) continue;
                        var s = w.Ships[0];
                        if (s.State == ShipState.Wrecked) { result = $"wrecked on {s.WreckCause} at {s.Pos}"; break; }
                        if (s.State == ShipState.Arrived) { result = null; break; }
                    }
                    if (result != null) errors.Add($"{route} / {type}: {result}");
                }
            return errors;
        }

        public struct NightResult
        {
            public MissionDef Def;
            public MissionOutcome Outcome;
            public int Lamps, Wrecks, Arrivals, Total, Score;
            public float Time;
        }

        /// <summary>The AutoKeeper plays a night to the end.</summary>
        public static NightResult PlayNight(MapData map, MissionDef def, int seed = 7)
        {
            var w = new SimWorld(map, def, seed);
            var bot = new AutoKeeper();
            for (int i = 0; i < 60 * 900 && w.Outcome == MissionOutcome.Running; i++)
                w.Step(Dt, bot.Decide(w, Dt));
            return new NightResult { Def = def, Outcome = w.Outcome, Lamps = w.Lamps, Wrecks = w.Wrecks, Arrivals = w.Arrivals, Total = w.TotalShips, Score = w.Score, Time = w.Time };
        }

        /// <summary>One-line summary of everything, for Tools/validate.sh.</summary>
        public static string Report()
        {
            var map = FreshMap();
            var missions = FreshMissions();
            var sb = new System.Text.StringBuilder();
            void Section(string name, List<string> errors)
            {
                sb.Append(errors.Count == 0 ? $"PASS {name}\n" : $"FAIL {name}\n");
                foreach (var e in errors) sb.Append("     " + e + "\n");
            }
            Section("data integrity", DataIntegrity(map, missions));
            Section("beam reachability", Reachability(map));
            Section("route safety (42 route/hull pairs)", RouteSafety(map));
            int won = 0;
            foreach (var m in missions)
            {
                var r = PlayNight(map, m);
                if (r.Outcome == MissionOutcome.Won) won++;
                sb.Append($"{(r.Outcome == MissionOutcome.Won ? "PASS" : "FAIL")} night {m.night,2} {m.title,-18} {r.Outcome,-7} lamps {r.Lamps}  home {r.Arrivals}/{r.Total}  wrecks {r.Wrecks}  score {r.Score}  ({r.Time:0}s)\n");
            }
            sb.Append($"{won}/{missions.Count} nights won by the AutoKeeper\n");
            return sb.ToString();
        }
    }
}
