using System.Runtime.InteropServices;
using UnityEngine;

namespace LastLight.Core
{
    /// <summary>
    /// What the game is running on, for the few things the browser build does differently: the
    /// page has no Quit and no window to size, fullscreen is the browser's to grant, Mono goes
    /// through the page's audio output (Web Audio has no OnAudioFilterRead), and the save is
    /// written where the page keeps it (IndexedDB, see the WebGL template's autoSyncPersistentDataPath).
    /// On a phone or tablet the page also draws the on-screen controls (Focus, Horn, Pause); the
    /// game reads them here and tells the page what they should show.
    /// The browser glue is Assets/Plugins/WebGL/LastLightWeb.jslib.
    /// </summary>
    public static class Platform
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        public static readonly bool IsWeb = true;

        [DllImport("__Internal")] static extern void LastLight_SetMono(int on);
        [DllImport("__Internal")] static extern void LastLight_AudioUnlock();
        [DllImport("__Internal")] static extern int LastLight_IsFullscreen();
        [DllImport("__Internal")] static extern void LastLight_RequestFullscreen(int on);
        [DllImport("__Internal")] static extern void LastLight_WhenHidden(string objectName, string methodName);
        [DllImport("__Internal")] static extern void LastLight_Ready();
        [DllImport("__Internal")] static extern int LastLight_TouchRead(int which);
        [DllImport("__Internal")] static extern float LastLight_TouchLayout(int which);
        [DllImport("__Internal")] static extern void LastLight_TouchState(int playing, int horn, float hornReady, int focused, int corner, float bearing);
        [DllImport("__Internal")] static extern void LastLight_PadUsed();
        [DllImport("__Internal")] static extern void LastLight_ChartState(float time, float duration, int playing);

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

        /// <summary>Lets the page start sound on any tap, click or key: music and the sea play
        /// through audio elements, which a strict browser lets start only inside such an event.</summary>
        public static void AudioUnlock() => LastLight_AudioUnlock();

        /// <summary>The page's on-screen controls are on: a touch-first device, or a finger used
        /// since the last mouse, key or pad.</summary>
        public static bool TouchMode => LastLight_TouchRead(0) != 0;

        /// <summary>A phone or tablet: a coarse pointer and no fine one (lighter defaults).</summary>
        public static bool TouchDevice => touchDevice ??= LastLight_TouchRead(5) != 0;
        static bool? touchDevice;

        /// <summary>The last visit ended while the game was showing, without the page being hidden
        /// or closed: most often a phone's browser stopping the tab for memory.</summary>
        public static bool LastVisitStopped => LastLight_TouchRead(6) != 0;

        /// <summary>The on-screen Focus button is held.</summary>
        public static bool TouchFocusHeld => LastLight_TouchRead(1) != 0;

        /// <summary>How many times each on-screen button has been pressed (compare with the last
        /// count read): Focus, Horn, and the corner's Pause or Back.</summary>
        public static int TouchFocusPresses => LastLight_TouchRead(2);
        public static int TouchHornPresses => LastLight_TouchRead(3);
        public static int TouchBackPresses => LastLight_TouchRead(4);

        /// <summary>The room the on-screen buttons take, as fractions of the canvas: the Pause
        /// button's at the top left (of the width), the Focus and Horn column's at the bottom
        /// right (of the width), and the home indicator's at the bottom (of the height).</summary>
        public static Vector3 TouchReserve => new Vector3(LastLight_TouchLayout(0), LastLight_TouchLayout(1), LastLight_TouchLayout(2));

        /// <summary>What the on-screen buttons show (the page changes them only when this does):
        /// the night is being played, tonight has the foghorn and how ready it is (0..1), the beam
        /// is focused, the corner button's job (0 none, 1 pause, 2 back), and the light's bearing
        /// in degrees (for the page's checks).</summary>
        public static void TouchState(bool playing, bool horn, float hornReady, bool focused, int corner, float bearing) =>
            LastLight_TouchState(playing ? 1 : 0, horn ? 1 : 0, hornReady, focused ? 1 : 0, corner, bearing);

        /// <summary>A gamepad was used: the on-screen controls step aside.</summary>
        public static void PadUsed() => LastLight_PadUsed();

        /// <summary>The dawn chart's replay as it stands, for the page's checks.</summary>
        public static void ChartState(float time, float duration, bool playing) => LastLight_ChartState(time, duration, playing ? 1 : 0);
#else
        public static readonly bool IsWeb = false;

        public static void SetMono(bool on) { }

        public static bool IsFullscreen => false;

        public static void RequestFullscreen(bool on) { }

        public static void WhenHidden(string objectName, string methodName) { }

        public static void Ready() { }

        public static void AudioUnlock() { }

        public static bool TouchMode => false;

        public static bool TouchDevice => false;

        public static bool LastVisitStopped => false;

        public static bool TouchFocusHeld => false;

        public static int TouchFocusPresses => 0;
        public static int TouchHornPresses => 0;
        public static int TouchBackPresses => 0;

        public static Vector3 TouchReserve => Vector3.zero;

        public static void TouchState(bool playing, bool horn, float hornReady, bool focused, int corner, float bearing) { }

        public static void PadUsed() { }

        public static void ChartState(float time, float duration, bool playing) { }
#endif
    }
}
