using System.Runtime.InteropServices;

namespace LastLight.Core
{
    /// <summary>
    /// What the game is running on, for the few things the browser build does differently: the
    /// page has no Quit and no window to size, fullscreen is the browser's to grant, Mono goes
    /// through the page's audio output (Web Audio has no OnAudioFilterRead), and the save is
    /// written where the page keeps it (IndexedDB, see the WebGL template's autoSyncPersistentDataPath).
    /// The browser glue is Assets/Plugins/WebGL/LastLightWeb.jslib.
    /// </summary>
    public static class Platform
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        public static readonly bool IsWeb = true;

        [DllImport("__Internal")] static extern void LastLight_SetMono(int on);
        [DllImport("__Internal")] static extern int LastLight_IsFullscreen();
        [DllImport("__Internal")] static extern void LastLight_RequestFullscreen(int on);
        [DllImport("__Internal")] static extern void LastLight_WhenHidden(string objectName, string methodName);
        [DllImport("__Internal")] static extern void LastLight_Ready();

        /// <summary>Folds the page's sound output to one channel, or back to stereo.</summary>
        public static void SetMono(bool on) => LastLight_SetMono(on ? 1 : 0);

        /// <summary>The page is fullscreen now (the browser's Esc can end it at any time).</summary>
        public static bool IsFullscreen => LastLight_IsFullscreen() != 0;

        /// <summary>Asks for fullscreen at once, while the browser still counts the click or key
        /// that asked as recent; Unity's Screen.fullScreen would wait for the next input event.</summary>
        public static void RequestFullscreen(bool on) => LastLight_RequestFullscreen(on ? 1 : 0);

        /// <summary>Calls <paramref name="methodName"/> on the named object when the tab is hidden
        /// or the page closed: browsers stop running hidden tabs and never call OnApplicationQuit.</summary>
        public static void WhenHidden(string objectName, string methodName) => LastLight_WhenHidden(objectName, methodName);

        /// <summary>Tells the page the title is up (its checks wait for this).</summary>
        public static void Ready() => LastLight_Ready();
#else
        public static readonly bool IsWeb = false;

        public static void SetMono(bool on) { }

        public static bool IsFullscreen => false;

        public static void RequestFullscreen(bool on) { }

        public static void WhenHidden(string objectName, string methodName) { }

        public static void Ready() { }
#endif
    }
}
