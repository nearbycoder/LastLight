using UnityEngine;

namespace LastLight.Audio
{
    /// <summary>
    /// The score: a calm bed per screen (title, night, dawn, ending) crossfaded, plus a tension layer
    /// over the night bed that rises when ships are lost, lured or about to strike.
    /// </summary>
    public sealed class Music : MonoBehaviour
    {
        static Music instance;
        AudioSource a, b, tension;
        AudioSource current;
        string currentId;
        float tensionTarget;
        float fadeSpeed = 0.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        static Music Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("Music");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Music>();
                instance.a = instance.Make();
                instance.b = instance.Make();
                instance.tension = instance.Make();
                return instance;
            }
        }

        AudioSource Make()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.volume = 0f;
            return s;
        }

        /// <summary>Crossfade to a track (no-op if it is already playing).</summary>
        public static void PlayTrack(string id, float fade = 2.5f, bool loop = true)
        {
            var m = Instance;
            if (m.currentId == id) return;
            var clip = Sfx.Clip(id);
            m.currentId = id;
            var next = m.current == m.a ? m.b : m.a;
            next.clip = clip;
            next.loop = loop;
            next.volume = 0f;
            if (clip != null) next.Play();
            m.current = next;
            m.fadeSpeed = 1f / Mathf.Max(0.1f, fade);
            if (id == "music_night")
            {
                var t = Sfx.Clip("music_tension");
                if (t != null && m.tension.clip != t)
                {
                    m.tension.clip = t;
                    m.tension.Play();
                    m.tension.timeSamples = 0;
                }
            }
        }

        public static void Stop(float fade = 2f)
        {
            var m = Instance;
            m.currentId = null;
            m.current = null;
            m.fadeSpeed = 1f / Mathf.Max(0.1f, fade);
        }

        /// <summary>0 calm .. 1 everything is going wrong.</summary>
        public static void SetTension(float t) => Instance.tensionTarget = Mathf.Clamp01(t);

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float bus = Sfx.BusVolume(Bus.Music);
            foreach (var s in new[] { a, b })
            {
                float target = s == current ? 1f : 0f;
                float v = Mathf.MoveTowards(s.volume / Mathf.Max(bus, 0.0001f), target, dt * fadeSpeed);
                s.volume = v * bus;
                if (target == 0f && v <= 0.001f && s.isPlaying) s.Stop();
            }
            float tt = currentId == "music_night" ? tensionTarget : 0f;
            float tv = Mathf.MoveTowards(tension.volume / Mathf.Max(bus, 0.0001f), tt, dt * (tt > 0.01f ? 0.6f : 0.25f));
            tension.volume = tv * bus * 0.9f;
            // Keep the tension layer in step with the night bed.
            if (current != null && current.isPlaying && tension.isPlaying && current.clip != null && tension.clip != null && current.clip.length > 1f)
            {
                float want = current.time % tension.clip.length;
                if (Mathf.Abs(tension.time - want) > 0.25f) tension.time = want;
            }
        }
    }
}
