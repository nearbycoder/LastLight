using System;
using System.IO;
using UnityEngine;

namespace LastLight.Core
{
    /// <summary>
    /// Where the save lives: save.json in the game's own data folder (on Linux
    /// ~/.config/unity3d/Gannet Head/Last Light). The Linux player's PlayerPrefs go to a file at
    /// ~/.config/unity3d/unknown/unknown/prefs that every Unity player on the machine with the same
    /// fault shares, where another game could overwrite or clear them, so the save moved out of
    /// PlayerPrefs; a save found there from an older build is read once and carried over. Writes go
    /// to a temporary file first and replace the save in one step, so a crash mid-write can't
    /// leave half a save.
    /// </summary>
    public static class SaveStore
    {
        public const string FileName = "save.json";
        /// <summary>The season cleared by "Start a new season", kept so it can be put back by hand.</summary>
        public const string PreviousName = "save.previous.json";
        /// <summary>A save that couldn't be read, kept aside rather than overwritten.</summary>
        public const string UnreadableName = "save.unreadable.json";
        /// <summary>Where older builds kept the save.</summary>
        public const string PrefsKey = "lastlight.save";

        public static string Dir => Application.persistentDataPath;

        /// <summary>The save's text: the file if there is one, else an older build's PlayerPrefs
        /// entry (empty if neither). <paramref name="from"/> says which.</summary>
        public static string Read(string dir, out string from)
        {
            var path = Path.Combine(dir, FileName);
            // Something there that can't be opened is still the save: it throws, rather than
            // falling back to PlayerPrefs and later being written over.
            if (File.Exists(path) || Directory.Exists(path))
            {
                from = "file";
                return File.ReadAllText(path);
            }
            from = "prefs";
            return PlayerPrefs.GetString(PrefsKey, "");
        }

        /// <summary>Writes a file in the save folder by way of a temporary file, so the old copy
        /// stays whole until the new one is complete.</summary>
        public static void Write(string dir, string name, string text)
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, name);
            if (Platform.IsWeb)
            {
                // In a browser the folder is in memory and goes to IndexedDB as a whole once the
                // file is closed (the page's autoSyncPersistentDataPath), so that step is the
                // all-or-nothing one; a plain write is safest there.
                File.WriteAllText(path, text);
                return;
            }
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        /// <summary>Why a save couldn't be opened or written, in a few plain words.</summary>
        public static string Reason(Exception e)
        {
            if (e is UnauthorizedAccessException) return "permission was refused";
            if (e is DirectoryNotFoundException) return "its folder is missing";
            string m = e.Message ?? "";
            if (m.IndexOf("space", StringComparison.OrdinalIgnoreCase) >= 0) return "the disk is full";
            if (m.IndexOf("read-only", StringComparison.OrdinalIgnoreCase) >= 0) return "the folder is read-only";
            if (m.IndexOf("denied", StringComparison.OrdinalIgnoreCase) >= 0) return "permission was refused";
            return m.Length > 80 ? m.Substring(0, 80) + "..." : m;
        }

        public static string ReadFile(string dir, string name)
        {
            var path = Path.Combine(dir, name);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        /// <summary>Reads a save's text into <paramref name="into"/>. False if it can't be read:
        /// the caller starts afresh and keeps the text aside.</summary>
        public static bool TryParse(string json, SaveData into, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(json)) return true;
            try
            {
                JsonUtility.FromJsonOverwrite(json, into);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }
    }
}
