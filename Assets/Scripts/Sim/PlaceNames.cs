using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Sim
{
    /// <summary>
    /// The places and ships a radio call names, so the HUD can point at them. Only what the keeper
    /// can already see is named: buoys, the wreckers' cliffs, the sea stacks and Porthkell harbour
    /// always, a reef group or sandbank only while it's charted, so a call never gives a hidden rock
    /// away. Names match as whole words, longest first ("Teeth Bell" is the buoy, not the Teeth).
    /// </summary>
    public static class PlaceNames
    {
        public readonly struct Place
        {
            public readonly string Key, Label;
            public readonly Vector3 World;
            public Place(string key, string label, Vector3 world) { Key = key; Label = label; World = world; }
        }

        /// <summary>A reef group's name for a label: "The Merrow Teeth".</summary>
        public static string GroupLabel(string group)
        {
            string n = SimWorld.ReefGroupName(group);
            return char.ToUpperInvariant(n[0]) + n.Substring(1);
        }

        /// <summary>Where a reef group's name goes: over the middle of tonight's reefs in it, just
        /// north of the furthest one, so the label sits above the white water rather than on it.</summary>
        public static Vector3 GroupCentre(SimWorld w, string group)
        {
            float sumX = 0f, top = float.MinValue;
            int n = 0;
            foreach (var r in w.Reefs)
                if (r.Group == group) { sumX += r.Pos.x; top = Mathf.Max(top, r.Pos.y + r.Radius); n++; }
            return n > 0 ? new Vector3(sumX / n, 1f, top + 2f) : new Vector3(0f, 1f, 0f);
        }

        static bool GroupCharted(SimWorld w, string group)
        {
            foreach (var r in w.Reefs) if (r.Group == group && r.Charted) return true;
            return false;
        }

        // The names a call might use for each reef group, besides "the Merrow Teeth" and the like.
        static readonly Dictionary<string, string[]> GroupAliases = new Dictionary<string, string[]>
        {
            ["teeth"] = new[] { "Merrow Teeth", "Teeth" },
            ["widow"] = new[] { "Widow's Ledge" },
            ["hens"] = new[] { "Hen's Chicks" },
            ["collar"] = new[] { "Gannet's Collar" },
            ["outer"] = new[] { "Outer Ground" },
        };

        /// <summary>The ship names a call might use: the whole name, without "SS", and its last
        /// word ("the Auk", "the Calloway").</summary>
        public static IEnumerable<string> ShipAliases(string name)
        {
            yield return name;
            string bare = name.StartsWith("SS ") ? name.Substring(3) : name;
            if (bare != name) yield return bare;
            int space = bare.LastIndexOf(' ');
            if (space > 0 && bare.Length - space - 1 >= 3) yield return bare.Substring(space + 1);
        }

        /// <summary>The places named in <paramref name="text"/> that can be shown tonight.</summary>
        public static void FindPlaces(SimWorld w, string text, List<Place> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(text)) return;
            var candidates = new List<(string alias, Place place)>();
            foreach (var b in w.Buoys)
                candidates.Add((b.Name, new Place("buoy:" + b.Id, b.Name, new Vector3(b.Pos.x, 1.5f, b.Pos.y))));
            foreach (var site in w.Map.WreckerSites.Values)
            {
                var p = new Place("site:" + site.Id, site.Name, new Vector3(site.Pos.x, site.Height + 2f, site.Pos.y));
                candidates.Add((site.Name, p));
                // "a false light at Corley", "the Sentinels"
                int space = site.Name.IndexOf(' ');
                if (site.Name.StartsWith("The ")) candidates.Add((site.Name.Substring(4), p));
                else if (space > 0 && (site.Name.EndsWith(" Cove"))) candidates.Add((site.Name.Substring(0, space), p));
            }
            foreach (var s in w.Map.Stacks)
                candidates.Add((s.Name, new Place("stack:" + s.Id, s.Name, new Vector3(s.Pos.x, s.Height + 1f, s.Pos.y))));
            var harbour = new Place("harbour", "Porthkell", new Vector3(w.Map.Harbor.x, 1f, w.Map.Harbor.y));
            candidates.Add(("Porthkell", harbour));
            candidates.Add(("harbour", harbour));
            candidates.Add(("Harbour", harbour));
            foreach (var kv in GroupAliases)
            {
                if (!GroupCharted(w, kv.Key)) continue;
                var p = new Place("reefs:" + kv.Key, GroupLabel(kv.Key), GroupCentre(w, kv.Key));
                foreach (var a in kv.Value) candidates.Add((a, p));
            }
            foreach (var s in w.Shoals)
            {
                var p = new Place("shoal:" + s.Def.Id, s.Def.Name, new Vector3(s.Def.Pos.x, 1f, s.Def.Pos.y));
                // An uncharted bank still claims its name, so "Long Sands" can't fall through to
                // a shorter match; it just isn't shown.
                candidates.Add((s.Def.Name, s.Charted ? p : default));
            }
            // An uncharted reef group claims its names too ("the Teeth" before the Teeth Bell is lit).
            foreach (var kv in GroupAliases)
                if (!GroupCharted(w, kv.Key))
                    foreach (var a in kv.Value) candidates.Add((a, default));

            candidates.Sort((a, b) => b.alias.Length.CompareTo(a.alias.Length));
            var taken = new bool[text.Length];
            foreach (var (alias, place) in candidates)
            {
                if (!Claim(text, alias, taken)) continue;
                if (place.Key == null) continue;
                bool dup = false;
                foreach (var q in into) if (q.Key == place.Key || q.Label == place.Label) dup = true;
                if (!dup) into.Add(place);
            }
        }

        /// <summary>The ships afloat that <paramref name="text"/> names.</summary>
        public static void FindShips(SimWorld w, string text, List<SimShip> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(text)) return;
            var candidates = new List<(string alias, SimShip ship)>();
            foreach (var s in w.Ships)
                if (s.Active)
                    foreach (var a in ShipAliases(s.Name)) candidates.Add((a, s));
            candidates.Sort((a, b) => b.alias.Length.CompareTo(a.alias.Length));
            var taken = new bool[text.Length];
            foreach (var (alias, ship) in candidates)
                if (!into.Contains(ship) && Claim(text, alias, taken)) into.Add(ship);
        }

        /// <summary>Marks every whole-word occurrence of <paramref name="word"/> not already
        /// claimed by a longer name; true if there was one.</summary>
        static bool Claim(string text, string word, bool[] taken)
        {
            bool found = false;
            for (int at = text.IndexOf(word, System.StringComparison.Ordinal); at >= 0; at = text.IndexOf(word, at + 1, System.StringComparison.Ordinal))
            {
                int end = at + word.Length;
                bool startOk = at == 0 || !IsWordChar(text[at - 1]);
                bool endOk = end == text.Length || !char.IsLetterOrDigit(text[end]);
                if (!startOk || !endOk) continue;
                bool free = true;
                for (int i = at; i < end; i++) if (taken[i]) { free = false; break; }
                if (!free) continue;
                for (int i = at; i < end; i++) taken[i] = true;
                found = true;
            }
            return found;
        }

        static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '\'' || c == '’';
    }
}
