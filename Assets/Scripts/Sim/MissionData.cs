using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Sim
{
    /// <summary>JSON schema of Resources/Data/missions.json. One entry per night.</summary>
    [Serializable]
    public class MissionFile
    {
        public MissionDef[] missions;
    }

    [Serializable]
    public class MissionDef
    {
        public string id;
        public int night;
        public string title;
        public string date;
        public string briefing;      // the harbourmaster's line on the briefing card
        public string newThing;      // key of the picture card shown in the briefing ("" for none)
        public int allowedWrecks = 1;
        public float drainScale = 1f;
        public string[] reefGroups = new string[0];
        public string[] shoals = new string[0];
        public string[] buoys = new string[0];
        public bool foghorn;
        public float haze = 1f;      // ambient mist multiplier (visual)
        public FogDef[] fog = new FogDef[0];
        public StormDef storm;
        public WreckerDef[] wreckers = new WreckerDef[0];
        public SpawnDef[] ships = new SpawnDef[0];
        public RadioCue[] radio = new RadioCue[0];
        public HintCue[] hints = new HintCue[0];
        public bool finale;
        public bool endless;         // Night Watch: ships without end; the watch ends at the wreck allowance
    }

    [Serializable]
    public class FogDef
    {
        public float x, z, r, density = 1f, vx, vz;
    }

    [Serializable]
    public class StormDef
    {
        public bool enabled;
        public float cx, cz;          // current, units per second
        public float rain;            // 0..1
        public float lightningMin = 10f, lightningMax = 18f;
    }

    [Serializable]
    public class WreckerDef
    {
        public string[] sites;        // cycled in order after each dousing
        public float start;           // seconds into the night
        public float relight = 22f;   // seconds dark after being doused
        public float sweep = 18f;     // degrees per second
        public bool mimic;            // turns full circles like a real light
    }

    [Serializable]
    public class SpawnDef
    {
        public float t;
        public string type;           // trawler | steamer | ferry
        public string route;
        public string name;
        public string captain;        // radio voice id ("maren", "pryce", "dot", ...); "" = generic crew
        public bool damaged;
        public string hail;           // radio line when the ship enters
    }

    [Serializable]
    public class RadioCue
    {
        public float t = -1f;         // seconds into the night, or -1 when triggered by an event
        public string on;             // event trigger: "firstLost", "firstWreck", "firstCharted", "firstLured", "end", ...
        public string who;
        public string text;
    }

    [Serializable]
    public class HintCue
    {
        public string id;             // aim | ships | chart | focus | buoy | horn | flare | douse
        public float t = -1f;
        public string on;
    }

    public static class MissionLibrary
    {
        static List<MissionDef> cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cached = null;

        public static List<MissionDef> All
        {
            get
            {
                if (cached != null) return cached;
                var text = Resources.Load<TextAsset>("Data/missions");
                if (text == null) throw new Exception("Missing Resources/Data/missions.json");
                cached = new List<MissionDef>(JsonUtility.FromJson<MissionFile>(text.text).missions);
                return cached;
            }
        }

        public static MissionDef Parse(string json) => JsonUtility.FromJson<MissionDef>(json);
    }
}
