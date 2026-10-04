using UnityEditor;
using UnityEngine;

namespace LastLight.EditorTools
{
    /// <summary>
    /// Synthesized audio: music streams from disk, ambience loops stay compressed in memory, short
    /// effects decompress on load for zero-latency playback.
    /// </summary>
    public class AudioImportSettings : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var s = importer.defaultSampleSettings;
            importer.loadInBackground = true;
            if (file.StartsWith("music_"))
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
                s.preloadAudioData = false;
                importer.forceToMono = false;
            }
            else if (file.StartsWith("amb_") || file.StartsWith("voice_") || file.StartsWith("thunder") || file.Length > 0 && file.StartsWith("foghorn"))
            {
                s.loadType = AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.55f;
                s.preloadAudioData = true;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                s.preloadAudioData = true;
            }
            importer.defaultSampleSettings = s;
        }
    }
}
