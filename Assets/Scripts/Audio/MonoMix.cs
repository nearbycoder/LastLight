using UnityEngine;

namespace LastLight.Audio
{
    /// <summary>
    /// Settings ▸ Sound ▸ Mono: on the listener, so it hears the whole mix, it folds every channel
    /// to the middle, so a keeper who hears with one ear (or plays through one speaker) misses
    /// nothing panned to the other side. Off, it passes the mix through untouched.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public sealed class MonoMix : MonoBehaviour
    {
        /// <summary>Fold the mix to mono (read on the audio thread).</summary>
        public static volatile bool On;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => On = false;

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!On || channels < 2) return;
            for (int i = 0; i + channels <= data.Length; i += channels)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += data[i + c];
                float mid = sum / channels;
                for (int c = 0; c < channels; c++) data[i + c] = mid;
            }
        }
    }
}
