using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LastLight.Core
{
    /// <summary>
    /// F11 and Alt+Enter switch between a window and fullscreen, and the choice is kept, so
    /// Settings ▸ Display shows it and the next start keeps it. The game does this itself (the
    /// player's own Alt+Enter is off) and runs before the menus each frame, so the Enter doesn't
    /// also press the menu item that's chosen. In a browser Alt+Enter asks the page for
    /// fullscreen (F11 is the browser's own), and the choice follows the page, which the browser's
    /// Esc can end at any time; a page can't start fullscreen, so it isn't kept.
    /// </summary>
    [DefaultExecutionOrder(-2000)]   // before the EventSystem (-1000)
    public sealed class ScreenMode : MonoBehaviour
    {
        int restoreNavFrame = -1;

        /// <summary>Switch between a window and fullscreen (from what the window is now, which a
        /// size given on the command line can make differ from the save), and keep the choice.</summary>
        public static void Toggle()
        {
            var save = SaveData.Current;
            if (Platform.IsWeb)
            {
                save.fullscreen = !Platform.IsFullscreen;
                Platform.RequestFullscreen(save.fullscreen);
                Debug.Log($"[Screen] asked the browser for {(save.fullscreen ? "fullscreen" : "the page")}");
                return;
            }
            save.fullscreen = Screen.fullScreenMode == FullScreenMode.Windowed;
            save.Apply();
            save.Save();
            Debug.Log($"[Screen] switched to {(save.fullscreen ? "fullscreen" : "a window")}");
        }

        void Update()
        {
            var es = EventSystem.current;
            if (restoreNavFrame >= 0 && Time.frameCount >= restoreNavFrame)
            {
                restoreNavFrame = -1;
                if (es != null) es.sendNavigationEvents = true;
            }
            if (Application.isEditor) return;   // the editor's game view has no window mode to change
            if (Platform.IsWeb) SaveData.Current.fullscreen = Platform.IsFullscreen;
            var kb = Keyboard.current;
            // Not while the keys panel waits for a key.
            if (kb == null || (Game.Instance != null && Game.Instance.SettingsListening)) return;
            bool alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
            bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            bool f11 = kb.f11Key.wasPressedThisFrame && !Platform.IsWeb;
            if (!f11 && !(alt && enter)) return;
            // The menus mustn't take this Enter as a choice: they read it later this frame.
            if (enter && es != null && es.sendNavigationEvents)
            {
                es.sendNavigationEvents = false;
                restoreNavFrame = Time.frameCount + 1;
            }
            Toggle();
        }
    }
}
