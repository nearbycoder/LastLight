using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LastLight.Core;
using UnityEngine;

namespace LastLight.Automation
{
    /// <summary>
    /// Scripted screenshot tour for the built player, active only with <c>-llTour &lt;dir&gt;</c>.
    /// A script is a list of steps (see Steps) that drive the game through its public API and save
    /// full-frame screenshots (UI included) to the directory, then quit. Errors and exceptions
    /// logged meanwhile are counted and reported as [Tour] lines.
    /// </summary>
    public sealed class Tour : MonoBehaviour
    {
        string outDir;
        int errors;
        public static bool Active { get; private set; }
        public static readonly Dictionary<string, Func<Tour, IEnumerator>> Scripts = new Dictionary<string, Func<Tour, IEnumerator>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string dir = Game.ArgString("-llTour", null);
            if (string.IsNullOrEmpty(dir)) return;
            Active = true;
            var t = new GameObject("Tour").AddComponent<Tour>();
            t.outDir = dir;
        }

        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;

        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errors++;
                File.AppendAllText(Path.Combine(outDir, "errors.txt"), $"{type}: {msg}\n{stack}\n");
            }
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            yield return null;
            string script = Game.ArgString("-llScript", "default");
            Log($"started script={script} screen={Screen.width}x{Screen.height}");
            if (Scripts.TryGetValue(script, out var fn)) yield return fn(this);
            else yield return TourScripts.Default(this);
            Log($"done errors={errors}");
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        public void Log(string msg) => Debug.Log("[Tour] " + msg);

        public IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(outDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Log("shot " + name);
        }

        public static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }
    }
}
