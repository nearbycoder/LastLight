using System.Collections;
using System.Diagnostics;
using System.IO;
using Unity.Collections;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// Offline gameplay capture for the tours. Time is stepped at a fixed rate
    /// (Time.captureFramerate), so frames are evenly spaced however slow the GPU is. Frames are
    /// piped raw into ffmpeg, and the mixed audio output is captured with AudioRenderer into a
    /// float32 file for Tools/record.sh to mux. Only frames while <see cref="Rolling"/> is set are
    /// kept, so a tour can fast-forward between scenes without recording them.
    /// </summary>
    public sealed class Recorder : MonoBehaviour
    {
        public bool Rolling;
        public int Frames { get; private set; }

        Process ffmpeg;
        Stream video;
        FileStream audio;
        byte[] frameBytes;
        float[] audioBuffer = new float[0];
        bool audioOn;

        public static Recorder Begin(string dir, int fps)
        {
            Time.captureFramerate = fps;
            var r = new GameObject("Recorder").AddComponent<Recorder>();
            DontDestroyOnLoad(r.gameObject);
            int w = Screen.width, h = Screen.height;
            var psi = new ProcessStartInfo("ffmpeg",
                $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {w}x{h} -r {fps} -i - " +
                $"-vf vflip -c:v libx264 -preset medium -crf 17 -pix_fmt yuv420p \"{Path.Combine(dir, "video.mp4")}\"")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            r.ffmpeg = Process.Start(psi);
            r.video = r.ffmpeg.StandardInput.BaseStream;
            r.audio = new FileStream(Path.Combine(dir, "audio.f32"), FileMode.Create);
            File.WriteAllText(Path.Combine(dir, "audio.txt"),
                $"{AudioSettings.outputSampleRate} {ChannelCount()}\n");
            r.audioOn = AudioRenderer.Start();
            r.StartCoroutine(r.Capture());
            return r;
        }

        static int ChannelCount() => AudioSettings.speakerMode switch
        {
            AudioSpeakerMode.Mono => 1,
            AudioSpeakerMode.Quad => 4,
            AudioSpeakerMode.Surround => 5,
            AudioSpeakerMode.Mode5point1 => 6,
            AudioSpeakerMode.Mode7point1 => 8,
            _ => 2,
        };

        IEnumerator Capture()
        {
            var eof = new WaitForEndOfFrame();
            int channels = ChannelCount();
            while (true)
            {
                yield return eof;
                if (video == null) yield break;

                if (audioOn)
                {
                    int count = AudioRenderer.GetSampleCountForCaptureFrame() * channels;
                    using var samples = new NativeArray<float>(count, Allocator.Temp);
                    AudioRenderer.Render(samples);
                    if (Rolling)
                    {
                        if (audioBuffer.Length != count) audioBuffer = new float[count];
                        samples.CopyTo(audioBuffer);
                        var bytes = new byte[count * 4];
                        System.Buffer.BlockCopy(audioBuffer, 0, bytes, 0, bytes.Length);
                        audio.Write(bytes, 0, bytes.Length);
                    }
                }

                if (!Rolling) continue;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                var raw = tex.GetRawTextureData<byte>();
                if (frameBytes == null || frameBytes.Length != raw.Length) frameBytes = new byte[raw.Length];
                raw.CopyTo(frameBytes);
                Destroy(tex);
                video.Write(frameBytes, 0, frameBytes.Length);
                Frames++;
            }
        }

        public void Finish()
        {
            if (video == null) return;
            if (audioOn) AudioRenderer.Stop();
            audio.Dispose();
            video.Flush();
            video.Dispose();
            video = null;
            ffmpeg.WaitForExit(120000);
            Time.captureFramerate = 0;
        }
    }
}
