using System.Collections.Generic;
using LastLight.Audio;
using LastLight.Sim;
using LastLight.UI;
using LastLight.View;
using UnityEngine;

namespace LastLight.Core
{
    /// <summary>
    /// Turns simulation events into the game's feedback: sound, effects, camera, radio chatter,
    /// scripted radio cues, onboarding hints, music tension and the lens's mechanical voice.
    /// One instance per night.
    /// </summary>
    public sealed class Feedback
    {
        readonly MissionRunner runner;
        readonly Hud hud;
        readonly Radio radio;
        readonly HashSet<string> fired = new HashSet<string>();
        readonly HashSet<int> cuesDone = new HashSet<int>();
        readonly HashSet<int> hintsDone = new HashSet<int>();
        float swept;               // total beam rotation, for the "aim" hint
        float focusHeld;
        float swingPeak, lastBrake = -9f;
        float hitPause;
        float thunderAt = -1f;
        public static Sfx.Loop Whirr, Focus, Sea, Wind, Rain, Static;

        public Feedback(MissionRunner runner, Hud hud, Radio radio)
        {
            this.runner = runner;
            this.hud = hud;
            this.radio = radio;
            runner.OnEvent += Handle;
            InputMode.Changed += RefreshHint;
            EnsureLoops();
            var storm = runner.Def.storm;
            bool stormy = storm != null && storm.enabled;
            Wind.Set(stormy ? 0.85f : 0.25f);
            Rain.Set(stormy ? 0.7f * storm.rain + 0.2f : 0f);
            Waves.Scale = stormy ? 0.75f : 0.35f;
            Waves.Choppiness = stormy ? 1.25f : 1f;
            MaterialLibrary.Water.SetFloat("_WaveScale", Waves.Scale);
            MaterialLibrary.Water.SetFloat("_Choppiness", Waves.Choppiness);
        }

        public static void EnsureLoops()
        {
            Sea ??= Sfx.StartLoop("amb_sea", Bus.Ambience, 0.75f);
            Wind ??= Sfx.StartLoop("amb_wind", Bus.Ambience, 0.25f);
            Rain ??= Sfx.StartLoop("amb_rain", Bus.Ambience, 0f);
            Whirr ??= Sfx.StartLoop("lens_whirr", Bus.Sfx, 0f);
            Focus ??= Sfx.StartLoop("lens_focus", Bus.Sfx, 0f);
            Static ??= Sfx.StartLoop("radio_static", Bus.Radio, 0f);
            Whirr.FadeSpeed = 6f;
            Focus.FadeSpeed = 4f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Whirr = Focus = Sea = Wind = Rain = Static = null; }

        public void Detach()
        {
            runner.OnEvent -= Handle;
            InputMode.Changed -= RefreshHint;
        }

        bool First(string key) => fired.Add(key);

        void Cue(string on)
        {
            var radioCues = runner.Def.radio;
            if (radioCues != null)
                for (int i = 0; i < radioCues.Length; i++)
                    if (radioCues[i].on == on && cuesDone.Add(i)) radio.Say(radioCues[i].who, radioCues[i].text, 3);
            var hints = runner.Def.hints;
            if (hints != null)
                for (int i = 0; i < hints.Length; i++)
                    if (hints[i].on == on && hintsDone.Add(i)) ShowHint(hints[i].id);
        }

        // Each hint in mouse-and-keyboard words, then in gamepad words (keycap icons for the pad).
        static readonly Dictionary<string, (string text, string icon, string padText, string padIcon)> HintText = new Dictionary<string, (string, string, string, string)>
        {
            ["aim"] = ("Move the mouse to turn the light.", "mouse", "Point the right stick to turn the light.", "RS"),
            ["ships"] = ("Ships lose their nerve in the dark. Keep them in your light.", "ring", null, null),
            ["chart"] = ("Sweep the light ahead of a ship to chart the hidden reefs.", "ring", null, null),
            ["focus"] = ("Hold the left button to focus: a narrow beam that reaches further.", "lmb", "Hold the right trigger to focus: a narrow beam that reaches further.", "RT"),
            ["buoy"] = ("Sweep the light over a buoy to light it. It guides ships for a while.", "lamp", null, null),
            ["horn"] = ("Fog! Press SPACE to sound the foghorn.", "SPACE", "Fog! Press A to sound the foghorn.", "A"),
            ["flare"] = ("A ship with no lamps. Watch for its flares.", "ring", null, null),
            ["douse"] = ("A false light! Hold your beam on its lantern to douse it.", "lmb", "A false light! Hold your beam on its lantern to douse it.", "RT"),
            ["shoal"] = ("Steamers run aground on sandbanks. Light the sands to chart them.", "ring", null, null),
            ["storm"] = ("The storm pushes ships towards the rocks. Lightning reveals them.", "ring", null, null),
        };

        static (string text, string icon) HintFor(string id)
        {
            var h = HintText[id];
            return InputMode.Pad && h.padText != null ? (h.padText, h.padIcon) : (h.text, h.icon);
        }

        /// <summary>Each hint shows once per save (Settings can bring them all back).</summary>
        void ShowHint(string id)
        {
            var save = SaveData.Current;
            if (!save.hints || !HintText.ContainsKey(id) || save.hintsSeen.Contains(id)) return;
            save.hintsSeen.Add(id);
            save.Save();
            var h = HintFor(id);
            hud.ShowHint(id, h.text, h.icon, id == "aim" ? 12f : 9f);
        }

        /// <summary>The keeper picked up the other device: reword the hint on screen.</summary>
        void RefreshHint()
        {
            foreach (var id in HintText.Keys)
                if (hud.HintShowing(id)) { var h = HintFor(id); hud.SetHintContent(h.text, h.icon); return; }
        }

        void Dismiss(string id)
        {
            if (hud.HintShowing(id)) hud.HideHint();
        }

        // ---------------------------------------------------------------- events

        void Handle(SimEvent e)
        {
            var w = runner.World;
            var pos3 = new Vector3(e.Pos.x, 0.5f, e.Pos.y);
            switch (e.Type)
            {
                case SimEventType.ShipIncoming:
                    var def = w.Schedule[e.Index];
                    hud.Incoming(e.Pos, def.type);
                    break;
                case SimEventType.ShipEntered:
                {
                    var s = e.Ship;
                    var horn = s.Type switch { ShipType.Steamer => "horn_steamer", ShipType.Ferry => "horn_ferry", _ => "horn_trawler" };
                    Sfx.PlayAt(horn, pos3, 0.45f);
                    var spawn = FindSpawn(w, s);
                    if (spawn != null && !string.IsNullOrEmpty(spawn.hail)) radio.Say(string.IsNullOrEmpty(s.Captain) ? "crew" : s.Captain, spawn.hail, 2, s.Name);
                    if (First("entered")) Cue("firstEntered");
                    if (Vector2.Distance(s.Pos, w.Beam.Origin) > SimBeam.WideRange * 0.85f && First("far")) Cue("firstFar");
                    if (s.Damaged && First("damaged")) Cue("firstDamaged");
                    break;
                }
                case SimEventType.ShipLit:
                    Sfx.PlayAt("ship_lit", pos3, 0.4f, e.Ship.Type == ShipType.Steamer ? 0.8f : e.Ship.Type == ShipType.Ferry ? 1.12f : 1f, 0.15f);
                    if (!e.Ship.Damaged) Sfx.PlayAt("ship_answer", pos3, 0.3f, Random.Range(0.95f, 1.05f), 0.15f);
                    break;
                case SimEventType.ShipLost:
                    Sfx.PlayAt("ship_lost", pos3, 0.75f);
                    radio.React(e, w);
                    if (First("lost")) Cue("firstLost");
                    ShowHint("ships");
                    break;
                case SimEventType.ShipFound:
                    Sfx.PlayAt("ship_found", pos3, 0.55f);
                    radio.React(e, w);
                    break;
                case SimEventType.ShipLured:
                    Sfx.PlayAt("lured", pos3, 0.7f);
                    radio.React(e, w);
                    if (First("lured")) Cue("firstLured");
                    break;
                case SimEventType.ShipFreed:
                    Sfx.PlayAt("ship_found", pos3, 0.6f, 1.15f);
                    radio.React(e, w);
                    break;
                case SimEventType.ShipArrived:
                {
                    var s = e.Ship;
                    Sfx.PlayAt("arrive", pos3, 0.75f);
                    Sfx.PlayAt(s.Type switch { ShipType.Steamer => "horn_steamer", ShipType.Ferry => "horn_ferry", _ => "horn_trawler" }, pos3, 0.5f);
                    if (s.Route.ToHarbor) FX.ArrivalGlow(pos3);
                    hud.FloatText(new Vector3(e.Pos.x, 4f, e.Pos.y), "+" + e.Index, s.SteadyHand ? UiKit.BrassBright : UiKit.Paper);
                    radio.React(e, w);
                    break;
                }
                case SimEventType.ShipWrecked:
                {
                    var s = e.Ship;
                    Sfx.PlayAt("wreck", pos3, 1f);
                    Sfx.Duck(0.5f, 1.2f);
                    float size = s.Type == ShipType.Trawler ? 0.8f : 1.2f;
                    FX.Splash(pos3, size);
                    FX.Impact(pos3, size);
                    FX.Debris(pos3, s.Velocity, true);
                    hud.FloatText(new Vector3(e.Pos.x, 4f, e.Pos.y), s.Name, UiKit.Danger);
                    Shake(0.55f);
                    if (SaveData.Current.shake && CameraRig.Instance != null) CameraRig.Instance.Punch(pos3, 1f);
                    hitPause = 0.35f;
                    radio.React(e, w);
                    if (First("wreck")) Cue("firstWreck");
                    break;
                }
                case SimEventType.ShipFlare:
                    FX.Flare(new Vector3(e.Pos.x, 2f, e.Pos.y));
                    hud.Flare(e.Pos);
                    radio.React(e, w);
                    if (First("flare")) { Cue("firstFlare"); ShowHint("flare"); }
                    break;
                case SimEventType.ReefCharted:
                    Sfx.PlayAt("chart", pos3, 0.5f, Random.Range(0.92f, 1.1f), 0.08f);
                    FX.ChartPing(pos3, w.Reefs[e.Index].Radius);
                    if (First("charted")) { Cue("firstCharted"); Dismiss("chart"); }
                    break;
                case SimEventType.ShoalCharted:
                    Sfx.PlayAt("chart", pos3, 0.55f, 0.8f, 0.08f);
                    if (First("shoal")) { Cue("firstShoal"); Dismiss("shoal"); }
                    break;
                case SimEventType.BuoyLit:
                {
                    var b = w.Buoys[e.Index];
                    Sfx.PlayAt(b.Kind == "bell" ? "buoy_bell" : "buoy_lit", pos3, 0.65f);
                    if (First("buoy")) { Cue("firstBuoy"); Dismiss("buoy"); }
                    break;
                }
                case SimEventType.BuoyOut:
                    Sfx.PlayAt("buoy_out", pos3, 0.35f);
                    break;
                case SimEventType.Horn:
                    Sfx.Play("foghorn", 1f, 1f, 0f);
                    Sfx.Duck(0.45f, 2.2f);
                    FX.HornWave(new Vector3(w.Beam.Origin.x, 0f, w.Beam.Origin.y));
                    Shake(0.3f);
                    Dismiss("horn");
                    break;
                case SimEventType.Lightning:
                    thunderAt = runner.World.Time + Random.Range(0.35f, 1.3f);
                    Shake(0.12f);
                    break;
                case SimEventType.WreckerLit:
                    Sfx.PlayAt("wrecker_lit", pos3, 0.6f);
                    radio.React(e, w);
                    if (First("wrecker")) { Cue("firstWrecker"); ShowHint("douse"); }
                    break;
                case SimEventType.WreckerDousing:
                    Sfx.PlayAt("douse_sizzle", pos3, 0.5f);
                    break;
                case SimEventType.WreckerDoused:
                {
                    var site = w.Wreckers[e.Index].Site;
                    var p = new Vector3(site.Pos.x, site.Height + 2f, site.Pos.y);
                    Sfx.PlayAt("doused", p, 0.8f);
                    FX.Sparks(p, new Color(2.5f, 1.1f, 0.35f), 70);
                    Shake(0.15f);
                    radio.React(e, w);
                    if (First("doused")) { Cue("firstDoused"); Dismiss("douse"); }
                    break;
                }
            }
        }

        static SpawnDef FindSpawn(SimWorld w, SimShip s)
        {
            foreach (var d in w.Schedule)
                if (d.name == s.Name && d.route == s.Route.Id) return d;
            return null;
        }

        void Shake(float amount)
        {
            if (SaveData.Current.shake && CameraRig.Instance != null) CameraRig.Instance.Shake(amount);
        }

        // ---------------------------------------------------------------- per frame

        public void Update(float dt)
        {
            var w = runner.World;
            if (w == null) return;

            // Scripted radio and hints by time.
            var cues = runner.Def.radio;
            if (cues != null)
                for (int i = 0; i < cues.Length; i++)
                    if (cues[i].t >= 0 && w.Time >= cues[i].t && cuesDone.Add(i)) radio.Say(cues[i].who, cues[i].text, 3);
            var hints = runner.Def.hints;
            if (hints != null)
                for (int i = 0; i < hints.Length; i++)
                    if (hints[i].t >= 0 && w.Time >= hints[i].t && hintsDone.Add(i)) ShowHint(hints[i].id);

            // Lens voice: whirr follows angular speed, arc sizzle while focused.
            float speed = Mathf.Abs(w.Beam.AngularVelocity) * Mathf.Rad2Deg;
            Whirr.Set(Mathf.Clamp01(speed / 160f) * 0.55f + 0.06f, 0.7f + Mathf.Clamp01(speed / 200f) * 0.7f);
            Focus.Set(w.Beam.Focus * 0.35f, 0.9f + w.Beam.Focus * 0.2f);
            swingPeak = Mathf.Max(swingPeak, speed);
            if (speed < 25f)
            {
                if (swingPeak > 110f && w.Time - lastBrake > 0.4f)
                {
                    float v = 0.22f + 0.3f * Mathf.InverseLerp(110f, 200f, swingPeak);
                    Sfx.Play("lens_brake", v, Random.Range(0.93f, 1.05f), 0f);
                    lastBrake = w.Time;
                }
                swingPeak = 0f;
            }
            Static.Set(radio.Busy ? 0.08f : 0f);
            swept += speed * dt;
            if (swept > 90f) Dismiss("aim");
            if (w.Beam.Focus > 0.9f) { focusHeld += dt; if (focusHeld > 0.8f) Dismiss("focus"); }

            // Hints that depend on what is on the water.
            if (w.Fog.Count > 0 && runner.Def.foghorn && w.Time > 6f && First("hornhint")) ShowHint("horn");
            if (w.Shoals.Count > 0 && w.Time > 3f)
                foreach (var s in w.Ships)
                    if (s.Active && s.Stats.DeepDraught && s.Inside && First("shoalhint")) { ShowHint("shoal"); break; }

            if (thunderAt > 0f && w.Time >= thunderAt)
            {
                thunderAt = -1f;
                Sfx.Play("thunder", Random.Range(0.7f, 1f), Random.Range(0.9f, 1.05f), Random.Range(-0.4f, 0.4f));
            }

            // Music tension from trouble on the water.
            float tension = 0f;
            foreach (var s in w.Ships)
            {
                if (!s.Active || !s.Inside) continue;
                if (s.State == ShipState.Lost || s.State == ShipState.Lured) tension += 0.55f;
                else if (s.Confidence < 0.35f) tension += 0.25f;
            }
            foreach (var wr in w.Wreckers) if (wr.Burning) tension += 0.2f;
            Music.SetTension(tension);

            // A short hit-pause after a wreck.
            if (hitPause > 0f)
            {
                hitPause -= Unscaled.Delta;
                runner.TimeScale = hitPause > 0.2f ? 0.15f : Mathf.Lerp(1f, 0.15f, hitPause / 0.2f);
                if (hitPause <= 0f) runner.TimeScale = 1f;
            }
        }
    }
}
