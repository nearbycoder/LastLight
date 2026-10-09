using System.Collections.Generic;
using UnityEngine;

namespace LastLight.Audio
{
    public enum Bus { Sfx, Music, Radio, Ambience, Ui }

    /// <summary>
    /// All game audio. Clips are synthesized offline (ArtSource/audio) into Resources/Audio. One-shots
    /// come from a pool; ids with numbered variants (id_1, id_2, ...) pick one at random. Positional
    /// sounds pan by where they are on screen. Buses have their own volumes from Settings, and big
    /// moments duck everything else.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        static Sfx instance;
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        public static float Master = 1f, MusicVolume = 0.8f, SfxVolume = 1f, RadioVolume = 1f, AmbienceVolume = 0.9f;
        float duck = 1f, duckTarget = 1f, duckRelease;
        float radioDuck = 1f, radioDuckTarget = 1f;
        readonly List<Loop> loops = new List<Loop>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static Sfx Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("Audio");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Sfx>();
                return instance;
            }
        }

        public static float BusVolume(Bus bus) => Master * bus switch
        {
            Bus.Music => MusicVolume * Instance.duck * Instance.radioDuck,
            Bus.Radio => RadioVolume,
            Bus.Ambience => AmbienceVolume * Instance.duck,
            Bus.Ui => SfxVolume,
            _ => SfxVolume * Mathf.Lerp(1f, Instance.duck, 0.6f),
        };

        public AudioClip[] Clips(string id)
        {
            if (clips.TryGetValue(id, out var arr)) return arr;
            var list = new List<AudioClip>();
            var single = Resources.Load<AudioClip>("Audio/" + id);
            if (single != null) list.Add(single);
            for (int i = 1; i < 12; i++)
            {
                var c = Resources.Load<AudioClip>($"Audio/{id}_{i}");
                if (c == null) break;
                list.Add(c);
            }
            arr = list.ToArray();
            clips[id] = arr;
            return arr;
        }

        public static AudioClip Clip(string id)
        {
            var arr = Instance.Clips(id);
            return arr.Length == 0 ? null : arr[Random.Range(0, arr.Length)];
        }

        AudioSource Source()
        {
            foreach (var s in pool)
                if (!s.isPlaying) return s;
            if (pool.Count >= 48)
            {
                pool[0].Stop();
                return pool[0];
            }
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            pool.Add(src);
            return src;
        }

        /// <summary>Play a one-shot. Repeats of the same id within minGap seconds are dropped.</summary>
        public static AudioSource Play(string id, float volume = 1f, float pitch = 1f, float pan = 0f, Bus bus = Bus.Sfx, float minGap = 0.03f)
        {
            var self = Instance;
            float now = Unscaled.Time;
            if (self.lastPlayed.TryGetValue(id, out var last) && now - last < minGap) return null;
            var clip = Clip(id);
            if (clip == null) return null;
            self.lastPlayed[id] = now;
            var src = self.Source();
            src.clip = clip;
            src.loop = false;
            src.volume = Mathf.Clamp01(volume) * BusVolume(bus);
            src.pitch = pitch;
            src.panStereo = Mathf.Clamp(pan, -1f, 1f);
            src.Play();
            return src;
        }

        /// <summary>Play at a world position: pans with its screen position, quieter far away.</summary>
        public static AudioSource PlayAt(string id, Vector3 world, float volume = 1f, float pitch = 1f, float minGap = 0.03f)
        {
            var cam = Camera.main;
            float pan = 0f, att = 1f;
            if (cam != null)
            {
                var vp = cam.WorldToViewportPoint(world);
                pan = Mathf.Clamp((vp.x - 0.5f) * 1.6f, -0.85f, 0.85f);
                float dist = Vector3.Distance(cam.transform.position, world);
                att = Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(200f, 420f, dist));
            }
            return Play(id, volume * att, pitch, pan, Bus.Sfx, minGap);
        }

        /// <summary>Briefly lower everything (big horn, wrecks).</summary>
        public static void Duck(float amount, float release = 0.8f)
        {
            var self = Instance;
            self.duckTarget = Mathf.Min(self.duckTarget, 1f - amount);
            self.duckRelease = Mathf.Max(self.duckRelease, release);
        }

        public static void RadioDucking(bool on) => Instance.radioDuckTarget = on ? 0.55f : 1f;

        // ---------------------------------------------------------------- loops

        public sealed class Loop
        {
            public AudioSource Source;
            public Bus Bus;
            public float Volume, Target, Pitch = 1f, FadeSpeed = 1.5f;

            public void Set(float volume, float pitch = 1f) { Target = volume; Pitch = pitch; }
        }

        public static Loop StartLoop(string id, Bus bus, float volume = 0f)
        {
            var self = Instance;
            var clip = Clip(id);
            var src = self.gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.clip = clip;
            src.volume = 0f;
            var loop = new Loop { Source = src, Bus = bus, Target = volume };
            if (clip != null)
            {
                // A browser can't seek a compressed clip (Web Audio decodes it whole): it starts at the top.
                if (!Core.Platform.IsWeb) src.time = Random.Range(0f, clip.length * 0.9f);
                src.Play();
            }
            self.loops.Add(loop);
            return loop;
        }

        public static void StopLoop(Loop loop)
        {
            if (loop == null || instance == null) return;
            instance.loops.Remove(loop);
            if (loop.Source != null) Destroy(loop.Source);
        }

        void Update()
        {
            float dt = Unscaled.Delta;
            if (duckRelease > 0f) duckRelease -= dt;
            else duckTarget = 1f;
            duck = Mathf.MoveTowards(duck, duckTarget, dt * (duckTarget < duck ? 6f : 0.8f));
            radioDuck = Mathf.MoveTowards(radioDuck, radioDuckTarget, dt * 1.5f);
            foreach (var l in loops)
            {
                if (l.Source == null) continue;
                l.Volume = Mathf.MoveTowards(l.Volume, l.Target, dt * l.FadeSpeed);
                l.Source.volume = l.Volume * BusVolume(l.Bus);
                l.Source.pitch = Mathf.Lerp(l.Source.pitch, l.Pitch, dt * 8f);
            }
        }
    }
}
