using System;
using System.Collections.Generic;
using LastLight.Audio;
using LastLight.Sim;
using UnityEngine;

namespace LastLight.Core
{
    public sealed class Speaker
    {
        public string Id, Name, Role, Initials;
        public Color Color;
        public float Pitch = 1f;
    }

    public sealed class RadioMessage
    {
        public Speaker Speaker;
        public string Title;     // shown under the name (ship name / role)
        public string Text;
        public int Priority;     // scripted 3, important reactions 2, chatter 1
    }

    /// <summary>The night's calls as they went out, for reading back in the pause menu.</summary>
    public sealed class RadioLog
    {
        public readonly struct Entry
        {
            public readonly string Name, Title, Text;
            public Entry(string name, string title, string text) { Name = name; Title = title; Text = text; }
        }

        public const int Capacity = 40;
        readonly List<Entry> entries = new List<Entry>();
        public IReadOnlyList<Entry> Entries => entries;
        public int Count => entries.Count;

        public void Add(string name, string title, string text)
        {
            entries.Add(new Entry(name, title, text));
            if (entries.Count > Capacity) entries.RemoveRange(0, entries.Count - Capacity);
        }

        public void Clear() => entries.Clear();
    }

    /// <summary>
    /// The VHF: who is speaking, what they say about what just happened, and a small queue so the
    /// chatter never drowns out the scripted lines. The RadioPanel displays the current message.
    /// </summary>
    public sealed class Radio
    {
        public static readonly Dictionary<string, Speaker> Speakers = new Dictionary<string, Speaker>
        {
            ["ianto"] = new Speaker { Id = "ianto", Name = "Ianto Rees", Role = "Harbourmaster, Porthkell", Initials = "IR", Color = new Color(0.79f, 0.64f, 0.35f), Pitch = 0.95f },
            ["maren"] = new Speaker { Id = "maren", Name = "Maren Holt", Role = "Trawler Little Auk", Initials = "MH", Color = new Color(0.45f, 0.75f, 0.85f), Pitch = 1.18f },
            ["pryce"] = new Speaker { Id = "pryce", Name = "Capt. Edwin Pryce", Role = "Collier SS Calloway", Initials = "EP", Color = new Color(0.85f, 0.5f, 0.38f), Pitch = 0.78f },
            ["dot"] = new Speaker { Id = "dot", Name = "Dot Okafor", Role = "Ferry Evening Star", Initials = "DO", Color = new Color(0.95f, 0.75f, 0.45f), Pitch = 1.1f },
            ["corley"] = new Speaker { Id = "corley", Name = "Unknown", Role = "Open channel", Initials = "??", Color = new Color(1f, 0.45f, 0.25f), Pitch = 0.7f },
            ["board"] = new Speaker { Id = "board", Name = "The Board of Lights", Role = "By letter", Initials = "BL", Color = new Color(0.7f, 0.72f, 0.78f), Pitch = 0.9f },
            ["crew"] = new Speaker { Id = "crew", Name = "", Role = "", Initials = "", Color = new Color(0.75f, 0.8f, 0.86f), Pitch = 1f },
        };

        public event Action<RadioMessage> Started;
        /// <summary>Every call that went out since the last <see cref="Clear"/> (one night).</summary>
        public RadioLog Log { get; } = new RadioLog();
        public RadioMessage Current { get; private set; }
        public bool Busy => Current != null;
        readonly List<RadioMessage> queue = new List<RadioMessage>();
        float timer, gap, lastChatter = -99f;
        public float TextSpeed = 1f;
        public float Clock;
        readonly System.Random rng = new System.Random(5);

        public float TypeDuration(RadioMessage m) => m.Text.Length / (42f * TextSpeed);
        public float HoldDuration(RadioMessage m) => TypeDuration(m) + 2.2f + m.Text.Length * 0.025f;

        public void Say(string who, string text, int priority = 3, string title = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!Speakers.TryGetValue(who ?? "crew", out var sp)) sp = Speakers["crew"];
            if (priority <= 1)
            {
                if (Clock - lastChatter < 6f || queue.Count >= 2) return;
                lastChatter = Clock;
            }
            if (queue.Count >= 4) queue.RemoveAll(m => m.Priority <= 1);
            var msg = new RadioMessage { Speaker = sp, Title = title ?? sp.Role, Text = text, Priority = priority };
            // Higher priority jumps the queue (but never interrupts what is being said).
            int at = queue.FindIndex(m => m.Priority < priority);
            if (at < 0) queue.Add(msg); else queue.Insert(at, msg);
        }

        public void Clear()
        {
            Log.Clear();
            queue.Clear();
            Current = null;
            timer = 0f;
        }

        public void Update(float dt)
        {
            Clock += dt;
            if (Current != null)
            {
                timer -= dt;
                if (timer > 0f) return;
                Current = null;
                gap = 0.45f;
                Sfx.RadioDucking(false);
            }
            if (gap > 0f) { gap -= dt; return; }
            if (queue.Count == 0) return;
            Current = queue[0];
            queue.RemoveAt(0);
            timer = HoldDuration(Current);
            // The crew's own shouts have no name: the ship's name stands in.
            Log.Add(string.IsNullOrEmpty(Current.Speaker.Name) ? Current.Title : Current.Speaker.Name, Current.Title, Current.Text);
            Sfx.RadioDucking(true);
            Started?.Invoke(Current);
        }

        // ---------------------------------------------------------------- reactions

        string Pick(params string[] options) => options[rng.Next(options.Length)];

        static string Who(SimShip s) => string.IsNullOrEmpty(s.Captain) || !Speakers.ContainsKey(s.Captain) ? "crew" : s.Captain;

        public void React(SimEvent e, SimWorld world)
        {
            var s = e.Ship;
            switch (e.Type)
            {
                case SimEventType.ShipLost:
                    Say(Who(s), LostLine(s), 2, s.Name);
                    break;
                case SimEventType.ShipFound:
                    if (s.StateTime < 0.1f) Say(Who(s), FoundLine(s), 1, s.Name);
                    break;
                case SimEventType.ShipArrived:
                    Say(Who(s), ArrivedLine(s), s.Captain != "" ? 2 : 1, s.Name);
                    break;
                case SimEventType.ShipWrecked:
                    Say(Who(s), WreckLine(s, e.Text), 3, s.Name);
                    Say("ianto", Pick("Lifeboat's away for them. Mind the others, keeper.", "The lifeboat's launched. Keep your light on the rest.", "Porthkell lifeboat is going out. Don't lose another."), 3);
                    break;
                case SimEventType.ShipLured:
                    Say(Who(s), Pick("There's the light. Steering for it.", "Gannet Head's light, fine on the bow. Coming round.", "Ah, there you are, keeper. Following you in."), 2, s.Name);
                    break;
                case SimEventType.ShipFreed:
                    Say(Who(s), Pick("Wait. That wasn't Gannet Head! Hard about!", "Two lights? That one's false! Coming away!", "Thanks, keeper. That other light nearly had us."), 2, s.Name);
                    break;
                case SimEventType.ShipDanger:
                    Say(Who(s), s.Captain switch
                    {
                        "maren" => Pick("Keeper! White water ahead, I can hear it!", "Breakers off the bow! Where are the rocks?"),
                        "pryce" => Pick("Breakers ahead. Show me the rock, keeper!", "Hear that? Surf where there shouldn't be any."),
                        "dot" => Pick("Ooh, is that surf? Keeper, what's in front of us?", "Breakers ahead! Light the water for us, love!"),
                        _ => Pick("Breakers ahead! Light the water, keeper!", $"{s.Name} here. Surf on the bow, can't see what's breaking!", "White water dead ahead! Where's the rock?"),
                    }, s.Captain != "" ? 2 : 1, s.Name);
                    break;
                case SimEventType.ShipAstern:
                    Say(Who(s), s.Captain switch
                    {
                        "maren" => Pick("Hard over! Full astern!", "Whoa! Back her off, back her off!"),
                        "pryce" => Pick("Full astern! ...Hold her. Hold her.", "Rock! Full astern, and be quick about it!"),
                        "dot" => Pick("Everyone hold on to something!", "Full astern! Sorry, folks, bit of a bump coming!"),
                        _ => Pick("Rock ahead! Full astern!", "Full astern! Hard over!", "There it is! Back her down!"),
                    }, 2, s.Name);
                    break;
                case SimEventType.ShipFlare:
                    Say(Who(s), Pick("Gannet Head, " + s.Name + ". No lamps, taking water. Flare's up!", "We've fired a flare. Can you see us, keeper?", s.Name + " here, dark and drifting. Look for the flare!"), 2, s.Name);
                    break;
                case SimEventType.WreckerDoused:
                    Say("corley", Pick("Douse it! The keeper's seen us!", "Blast that light. Move the lantern.", "Put it out, quick. Down the cliff path."), 2);
                    break;
                case SimEventType.WreckerLit:
                    Say("corley", Pick("Light her up, lads.", "There. Let them come to us.", "Swing the lantern slow. Like the real one."), 1);
                    break;
            }
        }

        string LostLine(SimShip s) => s.Captain switch
        {
            "maren" => Pick("Keeper! I can't see a blessed thing out here!", "Gannet Head, Auk. We're blind. Where are you?"),
            "pryce" => Pick("Gannet Head, Calloway. We've lost the marks. Where's your light, man?", "Lost her. Can't make out a thing. Light, keeper!"),
            "dot" => Pick("Ooh, it's gone very dark, keeper. The passengers are asking.", "Evening Star here, we've lost you! Bit of a fright in the saloon."),
            _ => Pick($"{s.Name} here. We're blind, Gannet Head!", $"Gannet Head, {s.Name}. Lost our bearings. Light, please!", $"{s.Name}. Can't see the marks. Help us, keeper."),
        };

        string FoundLine(SimShip s) => s.Captain switch
        {
            "maren" => Pick("There you are! Bless you.", "That's better. Thought you'd nodded off."),
            "pryce" => Pick("About time.", "Hm. Got you."),
            "dot" => Pick("And there's the light! Everyone wave!", "Lovely. Back on course."),
            _ => Pick("Got your light. Thank you.", "Light's on us again. Steady now.", "That's it, we see you."),
        };

        string ArrivedLine(SimShip s)
        {
            bool harbour = s.Route.ToHarbor;
            return s.Captain switch
            {
                "maren" => harbour ? Pick("Little Auk's home. Drinks are on me, keeper. Well, on my tab.", "Home and dry. Thank you, keeper!") : "Auk's clear of the bay. Ta-ra, keeper!",
                "pryce" => harbour ? Pick("Calloway alongside. Adequate, Gannet Head. Adequate.", "Calloway's in. You'll do.") : "Calloway clear. Carry on.",
                "dot" => harbour ? Pick("Evening Star in Porthkell. Mind the gap, everyone!", "We're in! A round of applause for the lighthouse!") : "Evening Star clear of the bay. Goodnight, keeper!",
                _ => harbour ? Pick($"{s.Name} safe in harbour. Thanks for the light.", $"{s.Name} alongside. Much obliged, Gannet Head.", $"{s.Name} home.") : Pick($"{s.Name} clear of the bay. Goodnight, Gannet Head.", $"{s.Name} out past the Head. Thank you."),
            };
        }

        string WreckLine(SimShip s, string cause) => s.Captain switch
        {
            "maren" => "We've struck! We've... Mayday, mayday, mayday!",
            "pryce" => "Calloway's aground! All hands! All hands!",
            "dot" => "We've hit something! Everybody stay calm, lifejackets on!",
            _ => $"Mayday! {s.Name} has struck {cause}!",
        };
    }
}
