using System.Collections;
using LastLight.Core;
using LastLight.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LastLight.Automation
{
    /// <summary>
    /// The season tour (-llScript season). Unlike the other tours it reads and writes a real save,
    /// so it must run against a throwaway config directory (Tools/season_tour.sh seeds one): it
    /// refuses to do anything unless the save lives under the directory given with -llSeasonHome.
    /// -llSeasonRun reset: a finished season; the logbook's "Start a new season" asks, Stay and Esc
    /// change nothing, going ahead clears the season and keeps settings, and the old season is kept
    /// as save.previous.json. -llSeasonRun damaged: save.json is unreadable; the game starts fresh
    /// and keeps the damaged text aside. -llSeasonRun migrate: an older build's save in PlayerPrefs
    /// is read and carried over to save.json.
    /// </summary>
    public static class SeasonTour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Tour.Scripts["season"] = Run;

        static int failures;

        static void Check(Tour t, bool ok, string what)
        {
            if (!ok) failures++;
            t.Log($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        static IEnumerator Press(Key key)
        {
            TourScripts.Key(key, true);
            yield return null;
            yield return null;
            TourScripts.Key(key, false);
            yield return null;
            yield return null;
        }

        static IEnumerator Run(Tour t)
        {
            failures = 0;
            string home = Game.ArgString("-llSeasonHome", "");
            string data = Application.persistentDataPath;
            bool safe = home.Length > 8 && data.StartsWith(home) && !Game.HasArg("-llFresh");
            t.Log($"save directory {data} (throwaway {home})");
            if (!safe)
            {
                Check(t, false, "the save is under the throwaway directory (nothing was touched)");
                yield break;
            }
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var g = Game.Instance;
            var save = SaveData.Current;
            yield return Tour.Wait(4f);
            var title = (TitleScreen)g.TourScreen("title");
            var logbook = (LogbookScreen)g.TourScreen("logbook");

            string run = Game.ArgString("-llSeasonRun", "reset");
            if (run == "damaged")
            {
                string kept = SaveStore.ReadFile(data, SaveStore.UnreadableName) ?? "";
                Check(t, !save.HasProgress && save.unlocked == 1, $"an unreadable save starts a fresh season (unlocked {save.unlocked}, progress {save.HasProgress})");
                Check(t, kept.StartsWith("{\"version\":1,\"unlocked\":12") && kept.Length == 120, $"the damaged text is kept as {SaveStore.UnreadableName} ({kept.Length} characters: \"{(kept.Length > 40 ? kept.Substring(0, 40) : kept)}...\")");
                Check(t, title.BeginLabel == "Begin the watch", $"the title offers \"{title.BeginLabel}\"");
                string notice = title.NoticeShown;
                Check(t, notice.Contains("couldn't be read") && notice.Contains(SaveStore.UnreadableName) && notice.Contains(data), $"the title says so: \"{notice}\"");
                yield return t.Shot("save_notice_damaged");
                save.Save();
                var written = new SaveData();
                bool readable = SaveStore.TryParse(SaveStore.ReadFile(data, SaveStore.FileName), written, out _);
                Check(t, (SaveStore.ReadFile(data, SaveStore.UnreadableName) ?? "") == kept && readable, $"a later save writes a whole {SaveStore.FileName} ({readable}) and leaves the damaged text where it is");
                t.Log($"season {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
                yield break;
            }
            if (run == "readonly" || run == "unopenable")
            {
                string savePath = System.IO.Path.Combine(data, SaveStore.FileName);
                bool isDir = System.IO.Directory.Exists(savePath);
                string seeded = isDir ? null : SaveStore.ReadFile(data, SaveStore.FileName);
                string notice = title.NoticeShown;
                if (run == "readonly")
                {
                    Check(t, save.unlocked == 12 && notice == "", $"a save in a read-only folder still loads ({save.unlocked} nights open), and the title says nothing yet (\"{notice}\")");
                }
                else
                {
                    Check(t, isDir && !save.HasProgress, $"a save that can't be opened leaves a blank season (progress {save.HasProgress})");
                    Check(t, notice.Contains("couldn't be opened") && notice.Contains("won't be kept") && notice.Contains(data), $"the title says so: \"{notice}\"");
                    yield return t.Shot("save_notice_unopenable");
                }
                // Keep night I with the AutoKeeper, quickly, and see what dawn says.
                g.AutoPlay = true;
                g.TourBriefing(1);
                yield return Tour.Wait(3.5f);
                g.TourBegin();
                g.Runner.AutoPlay = true;
                g.Runner.TimeScale = 4f;
                float waited = 0f;
                while (!g.ShowingResults && waited < 120f) { waited += Time.unscaledDeltaTime; yield return null; }
                yield return Tour.Wait(2.5f);
                var results = g.TourResults;
                string note = results.SaveNoteShown;
                Check(t, g.ShowingResults && note.Contains(run == "readonly" ? "couldn't be saved" : "wasn't saved"), $"the dawn card says the night wasn't saved: \"{note}\" (after {waited:0} s)");
                yield return t.Shot("save_dawn_" + run);
                g.TourHideAll();   // as the logbook would on the way back to the title
                g.TourTitle();
                yield return Tour.Wait(3f);
                notice = title.NoticeShown;
                if (run == "readonly")
                    Check(t, notice.Contains("isn't being saved") && notice.Contains(data), $"and so does the title: \"{notice}\"");
                else
                    Check(t, notice.Contains("couldn't be opened"), $"and the title still says why: \"{notice}\"");
                yield return t.Shot("save_title_" + run);
                bool untouched = run == "readonly" ? SaveStore.ReadFile(data, SaveStore.FileName) == seeded : System.IO.Directory.Exists(savePath);
                Check(t, untouched && !System.IO.File.Exists(savePath + ".tmp"), $"the save is as it was ({(run == "readonly" ? "the seeded text" : "still there, unopened")}), with nothing half-written beside it");
                t.Log($"season {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
                yield break;
            }
            if (run == "recovered")
            {
                // The second start after killwatch: the watch the player was killed in is kept.
                string notice = title.NoticeShown;
                var onDisk = new SaveData();
                SaveStore.TryParse(SaveStore.ReadFile(data, SaveStore.FileName), onDisk, out _);
                Check(t, save.watches.Count == 4 && onDisk.watches.Count == 4, $"the watch cut short is in the table ({save.watches.Count} watches; {onDisk.watches.Count} in {SaveStore.FileName})");
                Check(t, !onDisk.watchUnderway.active, "and no longer under way in the save");
                Check(t, notice.Contains("cut short") && notice.Contains("kept"), $"the title says so: \"{notice}\"");
                foreach (var r in save.watches) t.Log($"table: {r.score} points, {r.ships} ships, {r.seconds} s");
                yield return t.Shot("watch_kept_notice");
                t.Log($"season {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures); Tools/season_tour.sh compares the table with the last checkpoint");
                yield break;
            }
            if (run == "quitwatch" || run == "termwatch" || run == "quitnight" || run == "killwatch" || run == "closewatch")
            {
                // A watch (or night III) under way when the game is closed. The tour logs what the
                // watch stood at and closes the game; Tools/season_tour.sh then reads save.json.
                bool watch = run != "quitnight";
                g.AutoPlay = true;
                if (watch) g.TourWatch(); else g.TourBriefing(3);
                yield return Tour.Wait(3.5f);
                g.TourBegin();
                g.Runner.TimeScale = 6f;
                float waited = 0f;
                while (waited < 60f && g.Runner.World.Time < 120f && g.Runner.World.Outcome == Sim.MissionOutcome.Running) { waited += Time.unscaledDeltaTime; yield return null; }
                if (run == "quitwatch") g.TourPause();          // closed from the pause menu's night
                else g.Runner.TimeScale = 0f;                     // still playing, held so the numbers stay put
                yield return Tour.Wait(0.5f);
                var w = g.Runner.World;
                Check(t, w.Outcome == Sim.MissionOutcome.Running && w.Time > 60f, $"{(watch ? "a watch" : "night III")} is under way ({UiKit.Clock(w.Time)}, {(run == "quitwatch" ? "paused" : "playing")})");
                // (A hint seen tonight saves, so the file itself may have been rewritten.)
                var onDisk = new SaveData();
                SaveStore.TryParse(SaveStore.ReadFile(data, SaveStore.FileName), onDisk, out _);
                Check(t, onDisk.watches.Count == 3, $"no watch has been recorded yet ({onDisk.watches.Count} in {SaveStore.FileName}, as seeded)");
                var u = onDisk.watchUnderway;
                if (watch)
                    Check(t, u.active && u.seconds >= 100 && u.seconds <= Mathf.RoundToInt(w.Time) && u.score <= w.Score,
                        $"the save holds the watch under way as of its last checkpoint ({u.score} points, {u.ships} ships, {u.seconds} s; the watch is at {w.Score}, {w.Arrivals}, {w.Time:0} s)");
                else
                    Check(t, !u.active, "a night of the twelve writes no checkpoint");
                t.Log($"QUIT-AT score={w.Score} ships={w.Arrivals} seconds={Mathf.RoundToInt(w.Time)}");
                t.Log($"season {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures); the save is checked by Tools/season_tour.sh after the game closes");
                if (run == "termwatch" || run == "killwatch" || run == "closewatch")
                {
                    // Tools/season_tour.sh closes this player: SIGTERM, as a logout or shutdown does;
                    // SIGKILL, as a crash would leave it; or the compositor's close request.
                    t.Log("waiting to be closed");
                    for (float held = 0f; held < 60f; held += Time.unscaledDeltaTime) yield return null;
                    Check(t, false, "the player was closed within a minute");
                }
                yield break;   // the tour then quits the game, as the window's close button does
            }
            if (run == "migrate")
            {
                var carried = new SaveData();
                string file = SaveStore.ReadFile(data, SaveStore.FileName);
                bool parsed = file != null && SaveStore.TryParse(file, carried, out _);
                Check(t, save.unlocked == 12 && save.watches.Count == 3 && save.hudScale > 1.1f, $"an older build's save in PlayerPrefs is read: {save.unlocked} nights open, {save.watches.Count} watches, HUD text {save.hudScale * 100:0}%");
                Check(t, parsed && carried.unlocked == 12 && carried.watches.Count == 3, $"and carried over to {SaveStore.FileName} ({(file == null ? "missing" : file.Length + " characters")})");
                Check(t, title.BeginLabel.StartsWith("Continue") && title.WatchShown, $"the title offers \"{title.BeginLabel}\" and the Night Watch ({title.WatchShown})");
                t.Log($"season {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
                yield break;
            }

            // ---- A finished season, as seeded.
            string before = JsonUtility.ToJson(save);
            Check(t, save.unlocked == 12 && save.endingSeen && save.watches.Count == 3 && save.hudScale > 1.1f,
                $"the seeded season loaded: {save.unlocked} nights open, {save.TotalLamps} lamps, {save.watches.Count} watches, HUD text {save.hudScale * 100:0}%");
            Check(t, title.BeginLabel.StartsWith("Continue") && title.WatchShown, $"the title offers \"{title.BeginLabel}\" and the Night Watch ({title.WatchShown})");
            g.TourShowLogbook();
            yield return Tour.Wait(1.5f);
            Check(t, logbook.NewSeasonShown, "the logbook offers \"Start a new season\"");

            // Asking: Stay is chosen, Enter stays, and nothing changed.
            logbook.TourAsk();
            yield return Tour.Wait(0.5f);
            Check(t, logbook.Confirming && logbook.Selected.Contains("Stay"), $"it asks first, with {logbook.Selected} chosen");
            yield return t.Shot("season_ask");
            yield return Press(Key.Enter);
            yield return Tour.Wait(0.4f);
            Check(t, !logbook.Confirming && JsonUtility.ToJson(save) == before && logbook.Selected.Contains("new season"),
                $"Enter on Stay goes back to the book, on {logbook.Selected}, with the season as it was");
            // Esc backs out of the question, not the book.
            logbook.TourAsk();
            yield return Tour.Wait(0.4f);
            yield return Press(Key.Escape);
            yield return Tour.Wait(0.4f);
            Check(t, !logbook.Confirming && logbook.Visible && g.Current == Game.State.Logbook && JsonUtility.ToJson(save) == before,
                $"Esc backs out of the question to the book (logbook {logbook.Visible}), with the season as it was");

            // The pad: A asks, right to "Start afresh", A starts the new season.
            var pad = InputSystem.AddDevice<Gamepad>("SeasonPad");
            yield return null;
            logbook.TourAsk();
            yield return Tour.Wait(0.4f);
            yield return TourScripts.PadPress(pad, GamepadButton.DpadRight);
            string onPad = logbook.Selected;
            yield return TourScripts.PadPress(pad, GamepadButton.South);
            yield return Tour.Wait(1f);
            InputSystem.RemoveDevice(pad);
            Check(t, onPad.Contains("Start afresh") && !logbook.Confirming, $"the pad reaches {onPad} and A starts afresh");

            bool cleared = save.unlocked == 1 && save.TotalLamps == 0 && save.shipsHome == 0 && !save.endingSeen && save.watches.Count == 0 && save.watchBest == 0 && save.hintsSeen.Count == 0;
            Check(t, cleared, $"the season is cleared: {save.unlocked} night open, {save.TotalLamps} lamps, {save.shipsHome} ships, ending {save.endingSeen}, {save.watches.Count} watches, {save.hintsSeen.Count} hints seen");
            Check(t, save.hudScale > 1.1f && save.difficulty == 1 && save.keys.First(KeeperAction.Horn) == "H", $"settings and keys stay: HUD text {save.hudScale * 100:0}%, difficulty {save.Difficulty}, horn on {save.keys.First(KeeperAction.Horn)}");
            var stored = new SaveData();
            SaveStore.TryParse(SaveStore.ReadFile(data, SaveStore.FileName), stored, out _);
            Check(t, stored.unlocked == 1 && stored.watches.Count == 0 && stored.hudScale > 1.1f, $"and saved (stored: {stored.unlocked} night open, {stored.watches.Count} watches)");
            var previous = new SaveData();
            SaveStore.TryParse(SaveStore.ReadFile(data, SaveStore.PreviousName) ?? "{}", previous, out _);
            Check(t, previous.unlocked == 12 && previous.watches.Count == 3, $"the old season is kept as {SaveStore.PreviousName} ({previous.unlocked} nights, {previous.watches.Count} watches)");
            Check(t, logbook.Summary.StartsWith("0 of 36") && !logbook.NewSeasonShown, $"the logbook reads \"{logbook.Summary}\", with nothing to clear");
            yield return t.Shot("season_fresh_logbook");
            yield return Press(Key.Escape);
            yield return Tour.Wait(1.5f);
            Check(t, title.BeginLabel == "Begin the watch" && !title.WatchShown, $"the title offers \"{title.BeginLabel}\" and no Night Watch ({title.WatchShown})");
            t.Log($"season {(failures == 0 ? "PASS" : "FAIL")} ({failures} failures)");
        }
    }
}
