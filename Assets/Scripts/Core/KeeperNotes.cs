using System.Collections.Generic;

namespace LastLight.Core
{
    /// <summary>
    /// The keeper's notes: the rules in short entries, for a player who missed a hint or is coming
    /// back after a while. An entry shows once the night that brings its idea is open, so the notes
    /// never give the season away. The words follow the device in use, the Focus setting, the keys
    /// the keeper has chosen and the difficulty.
    /// </summary>
    public static class KeeperNotes
    {
        public readonly struct Entry
        {
            public readonly string Id, Title, Body;
            public readonly int Night;   // the night that opens it (13: the Night Watch)
            public Entry(string id, int night, string title, string body) { Id = id; Night = night; Title = title; Body = body; }
        }

        /// <summary>Every entry the save has reached, in the order the season brings them.</summary>
        public static List<Entry> For(SaveData save, bool pad)
        {
            var all = All(save, pad);
            var open = new List<Entry>();
            foreach (var e in all)
                if (e.Night <= 12 ? save.unlocked >= e.Night : save.WatchUnlocked) open.Add(e);
            return open;
        }

        /// <summary>How many entries the season still holds back.</summary>
        public static int Sealed(SaveData save) => All(save, false).Count - For(save, false).Count;

        static List<Entry> All(SaveData save, bool pad)
        {
            bool hard = save.difficulty == 1, toggle = save.focusToggle;
            var keys = save.keys ?? new KeyBindings();
            string turnKeys = keys.TurnPair(), focusKey = keys.First(KeeperAction.Focus), hornKey = keys.First(KeeperAction.Horn);
            string turn = pad ? "Point the right stick to turn the light."
                : turnKeys != "" ? $"Move the mouse to turn the light, or use {turnKeys}." : "Move the mouse to turn the light.";
            string focus = pad
                ? (toggle ? "Press the right trigger to focus, and again to widen the beam." : "Hold the right trigger to focus.")
                : (toggle ? "Click the left button" + (focusKey != "" ? $" or press {focusKey}" : "") + " to focus, and again to widen the beam."
                          : "Hold the left button" + (focusKey != "" ? $" or {focusKey}" : "") + " to focus.");
            string horn = pad ? "Press A" : hornKey != "" ? $"Press {hornKey} or click the right button" : "Click the right button";
            string pause = pad ? "Start" : "Esc";
            int chart = hard ? 15 : 22, buoy = hard ? 20 : 28;

            return new List<Entry>
            {
                new Entry("light", 1, "The light",
                    $"{turn} The lens is heavy: it gathers speed, swings a little past and settles, so start a turn early.\n\n" +
                    $"{focus} A focused beam is narrow and bright, reaches the far lanes and cuts through fog, but it turns more slowly.\n\n" +
                    $"{pause} pauses the night. The pause menu keeps tonight's radio calls."),
                new Entry("ships", 1, "Ships and their nerve",
                    "Each ship has a ring of confidence. It fills while the ship is in your beam and drains in the dark.\n\n" +
                    "When it runs out, the captain is Lost: the ring turns red, a ? bobs over the ship, and it slows, wanders and drifts toward the shore until you light it again.\n\n" +
                    "The ships in the top bar carry the same marks, with a tick once a ship is home and a cross if it's wrecked."),
                new Entry("lamps", 1, "Lamps and the Board",
                    "Dawn lights up to three lamps: one for keeping the light through the night, one for losing no ship, and one for a steady hand, when no ship was ever Lost or Lured. The lamps under the score show what's still in play: the third goes out when a ship first loses its way, the second at the first wreck.\n\n" +
                    "The briefing says how many wrecks the Board allows tonight, and the hulls under the night's title count them down. One wreck more ends the night at once.\n\n" +
                    "The logbook keeps your best for every night, and a night's briefing says which lamp is still to earn. At dawn, Chart shows where every ship went, and Replay plays the night back with your light."),
                new Entry("names", 1, "Names on the water",
                    "When a ship is on the radio, its name shows under its hull.\n\n" +
                    "A place the radio names, such as a buoy, a cliff or Porthkell harbour, is labelled on the water for a moment. A reef gets its name the first time you chart it each night."),
                new Entry("reefs", 2, "Hidden reefs",
                    "Captains steer straight over rocks they can't see. Sweep the light over the water ahead of a ship to chart the reefs there: white water breaks over them, and captains steer around them.\n\n" +
                    $"A chart fades about {chart} seconds after the light leaves it, so stay just ahead of each ship rather than lighting the whole bay."),
                new Entry("breakers", 2, "Breakers ahead",
                    (hard ? "On Hard the crew give no warning. On Standard, a" : "A") +
                    " few seconds before a ship reaches an uncharted rock, its crew hear the surf: its ring flickers white and a pale ring pulses round it. Light the water in front of it.\n\n" +
                    "Chart a rock too close for the captain to turn and they ring for full astern, three short blasts, and the ship slows hard while it swings clear. A late chart can still be a near miss."),
                new Entry("buoys", 3, "Buoys",
                    $"Sweep the light over a buoy to light it. A burning buoy keeps nearby ships steady for about {buoy} seconds while you tend to others."),
                new Entry("steamers", 4, "Steamers and sandbanks",
                    "Collier steamers are big and slow and turn wide, so warn them early.\n\n" +
                    "They run aground on sandbanks that trawlers sail over. Light the sands to chart them, as you would a reef."),
                new Entry("fog", 5, "Fog and the foghorn",
                    "Sea fret drains ships faster and swallows the wide beam. Focus punches through it.\n\n" +
                    $"{horn} to sound the foghorn. Its wave spreads across the water and steadies every ship in earshot. It needs 14 seconds before it can sound again; the gauge at the top right shows when it's ready."),
                new Entry("ferry", 6, "The ferry",
                    "The Evening Star carries passengers, with rows of lit windows. She's worth more than the rest of the night put together."),
                new Entry("dark", 7, "Ships running dark",
                    "A damaged ship carries no lights. Find it from the red distress flares it sends up (a red ring marks each one, even at the screen's edge), and keep it in your beam."),
                new Entry("storms", 8, "Storms",
                    "A storm's current pushes ships off course, and the rain shortens your beam. Captains steer against the current while they can see your light; a Lost ship drifts with it, toward the rocks.\n\n" +
                    "Every lightning strike lights the whole bay and charts every reef for a moment. Use it."),
                new Entry("wreckers", 9, "Wreckers and false lights",
                    "The Corleys sweep lanterns from the cliffs to imitate your light. A ship caught in a false beam while you're elsewhere is Lured: an amber ring, a lantern over it, and a dashed line to the false light, as it steers for the rocks.\n\n" +
                    "Hold your beam on the lantern to douse it, or light the ship to break the spell. A lantern burning just past the edge of the screen shows a marker there."),
                new Entry("mimic", 11, "The mimic",
                    "One false light copies your every sweep. Don't chase it every time. Stay with your ships."),
                new Entry("watch", 13, "The Night Watch",
                    "An endless watch with every reef, sandbank and buoy out and no end to the ships. Each watch brings its own fog, squalls and wreckers, and Ianto gives the forecast in the briefing.\n\n" +
                    "The third wreck ends it. To stop sooner, choose End the watch in the pause menu: the watch is kept and ranked. The game keeps your five best."),
                new Entry("assists", 1, "If it's too much",
                    "Settings can slow the whole night (Game speed), make focus a press instead of a hold, enlarge the HUD and radio text, soften the lightning, brighten the scene, and change the keyboard's keys.\n\n" +
                    "Settings ▸ Show hints again brings back the one-time hints."),
            };
        }
    }
}
