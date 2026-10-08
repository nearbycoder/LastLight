#!/usr/bin/env bash
# The season tour exercises the real save ("Start a new season", an unreadable save), so it runs
# the built player against a throwaway config directory seeded with a save, never your own.
#
#   Tools/season_tour.sh reset   <absolute outdir>   a finished season, cleared from the logbook
#   Tools/season_tour.sh damaged <absolute outdir>   an unreadable save, kept aside
#   Tools/season_tour.sh migrate <absolute outdir>   an older build's save in PlayerPrefs, carried over
#   Tools/season_tour.sh readonly <absolute outdir>  the save folder can't be written: the keeper is told
#   Tools/season_tour.sh unopenable <absolute outdir> something at save.json that can't be opened: never written over
#   Tools/season_tour.sh quitwatch <absolute outdir>  a watch under way (paused) when the game is closed: it's kept
#   Tools/season_tour.sh termwatch <absolute outdir>  the same while playing, closed with SIGTERM as a logout does
#   Tools/season_tour.sh quitnight <absolute outdir>  night III under way when the game is closed: nothing is written
#   Tools/season_tour.sh killwatch <absolute outdir>  the player is killed (SIGKILL, as a crash) mid-watch; the next
#                                                     start keeps the watch as of its last checkpoint and says so
#   Tools/season_tour.sh closewatch <absolute outdir> the nested KWin closes the game's window mid-watch (the request
#                                                     the close button sends): it's kept. Only inside Tools/nested.sh
#
# The game's data folder (where save.json lives) is checksummed before and after.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN="${1:?reset, damaged, migrate, readonly, unopenable, quitwatch, termwatch, quitnight, killwatch or closewatch}"; OUT="${2:?an absolute output directory}"
case "$OUT" in /*) ;; *) echo "give the output directory as an absolute path" >&2; exit 2 ;; esac
if [ "$RUN" = closewatch ]; then
  # This asks the compositor to close a window, so it only runs in Tools/nested.sh's private KWin.
  case "${WAYLAND_DISPLAY:-}" in lastlight-nested-*) ;; *) echo "closewatch runs only inside Tools/nested.sh" >&2; exit 2 ;; esac
  command -v qdbus6 >/dev/null || { echo "closewatch needs qdbus6" >&2; exit 2; }
fi
CFG="$OUT/config"
DATA="$CFG/unity3d/Gannet Head/Last Light"
# The Linux player's PlayerPrefs land in unity3d/unknown/unknown (see SaveStore.cs).
PREFS="$CFG/unity3d/unknown/unknown"
rm -rf "$OUT"; mkdir -p "$DATA" "$PREFS"
python3 - "$RUN" "$DATA" "$PREFS" <<'PY'
import base64, json, os, sys
run, data, prefs = sys.argv[1], sys.argv[2], sys.argv[3]
# A finished season with a few settings changed (HUD text 115%, Hard, the horn also on H).
save = {
    "version": 1, "unlocked": 12,
    "lamps": [3, 3, 3, 3, 2, 3, 3, 2, 1, 3, 2, 2],
    "best": [610, 790, 1000, 1210, 1150, 1600, 1700, 1650, 1150, 1800, 1900, 2250],
    "shipsHome": 97, "homeNames": ["Little Auk", "Kittiwake"], "endingSeen": True,
    "watchBest": 9600, "watchShips": 57, "watchSeconds": 954,
    "watches": [{"score": 9600, "ships": 57, "seconds": 954, "hard": False, "speed": 0},
                {"score": 7450, "ships": 44, "seconds": 781, "hard": False, "speed": 0},
                {"score": 5100, "ships": 31, "seconds": 602, "hard": False, "speed": 0}],
    "hudScale": 1.15, "difficulty": 1, "fullscreen": False,
    "keys": {"turnLeft": [15, 61, 0], "turnRight": [18, 62, 0], "focus": [51, 37, 63], "horn": [22, 1, 0]},
    "hints": True, "hintsSeen": ["aim", "chart", "buoy"],
}
text = json.dumps(save, separators=(",", ":"))
if run == "damaged":
    text = text[:120]          # cut off mid-write
if run == "migrate":
    b64 = base64.b64encode(text.encode()).decode()
    open(os.path.join(prefs, "prefs"), "w").write('<unity_prefs version_major="1" version_minor="1">\n'
                          f'\t<pref name="lastlight.save" type="string">{b64}</pref>\n</unity_prefs>\n')
elif run == "unopenable":
    os.makedirs(os.path.join(data, "save.json"))
else:
    open(os.path.join(data, "save.json"), "w").write(text)
PY
if [ "$RUN" = readonly ]; then
  # The folder can't be written; give it back afterwards so it can be cleared away.
  chmod a-w "$DATA"
  trap 'chmod u+w "$DATA"' EXIT
fi
REAL="$HOME/.config/unity3d/Gannet Head/Last Light"
sums() { find "$REAL" -maxdepth 1 -type f -print0 2>/dev/null | sort -z | xargs -0 -r sha256sum; }
before="$(sums)"
seeded="$(sha256sum "$DATA/save.json" 2>/dev/null | cut -d' ' -f1 || true)"
if [ "$RUN" = termwatch ] || [ "$RUN" = killwatch ] || [ "$RUN" = closewatch ]; then
  # Close the player once the tour says the watch is under way: SIGTERM to its own process (found
  # by this run's unique output path) as a logout or shutdown does, SIGKILL as a crash leaves it,
  # or the compositor's close request for its window.
  LL_TOUR_CONFIG="$CFG" "$ROOT/Tools/tour.sh" season "$OUT/tour" -llSeasonRun "$RUN" -llSeasonHome "$CFG" &
  tour=$!
  for _ in $(seq 1 600); do grep -q "waiting to be closed" "$OUT/tour/player.log" 2>/dev/null && break; sleep 0.5; done
  game="$(pgrep -f -- "LastLight\.x86_64 .*-llTour $OUT/tour -llScript season" | head -1 || true)"
  if [ -z "$game" ]; then
    echo "[season] FAIL the player wasn't found to close"
  elif [ "$RUN" = termwatch ]; then
    echo "[season] sending SIGTERM to the player ($game)"; kill -TERM "$game"
  elif [ "$RUN" = killwatch ]; then
    echo "[season] sending SIGKILL to the player ($game)"; kill -KILL "$game"
  else
    # A KWin script closes the window that belongs to the player's process, as its close button would.
    printf 'for (const w of workspace.windowList()) if (w.pid === %d) { print("lastlight: closing " + w.caption); w.closeWindow(); }\n' "$game" > "$OUT/close.js"
    id="$(qdbus6 org.kde.KWin /Scripting org.kde.kwin.Scripting.loadScript "$OUT/close.js" "lastlight-close-$$")"
    echo "[season] asking KWin to close the player's window ($game, script $id)"
    qdbus6 org.kde.KWin "/Scripting/Script$id" org.kde.kwin.Script.run >/dev/null 2>&1 || qdbus6 org.kde.KWin /Scripting org.kde.kwin.Scripting.start >/dev/null
  fi
  wait "$tour" || true
else
  LL_TOUR_CONFIG="$CFG" "$ROOT/Tools/tour.sh" season "$OUT/tour" -llSeasonRun "$RUN" -llSeasonHome "$CFG"
fi
case "$RUN" in
  killwatch)
    # On disk: the last checkpoint the player logged, still under way. Then start the game again.
    python3 - "$OUT/tour/player.log" "$DATA/save.json" "$OUT/checkpoint.txt" <<'PY'
import json, re, sys
cps = re.findall(r"watch checkpoint: score=(\d+) ships=(\d+) seconds=(\d+)", open(sys.argv[1], errors="replace").read())
if not cps:
    print("[season] FAIL the player logged no checkpoint"); sys.exit()
score, ships, secs = map(int, cps[-1])
open(sys.argv[3], "w").write(f"{score} {ships} {secs}")
u = json.load(open(sys.argv[2])).get("watchUnderway", {})
ok = u.get("active") and (u["score"], u["ships"], u["seconds"]) == (score, ships, secs)
print(f"[season] {'PASS' if ok else 'FAIL'} after SIGKILL, save.json holds the last checkpoint ({score} points, {ships} ships, {secs} s) "
      f"under way: {u.get('active')}, {u.get('score')}, {u.get('ships')}, {u.get('seconds')} ({len(cps)} checkpoints logged)")
PY
    LL_TOUR_CONFIG="$CFG" "$ROOT/Tools/tour.sh" season "$OUT/tour2" -llSeasonRun recovered -llSeasonHome "$CFG"
    python3 - "$OUT/checkpoint.txt" "$DATA/save.json" "$OUT/tour2/player.log" <<'PY'
import json, sys
try:
    score, ships, secs = map(int, open(sys.argv[1]).read().split())
except Exception:
    print("[season] FAIL no checkpoint to compare with"); sys.exit()
save = json.load(open(sys.argv[2]))
watches = save.get("watches", [])
kept = [w for w in watches if (w["score"], w["ships"], w["seconds"]) == (score, ships, secs)]
print(f"[season] {'PASS' if kept else 'FAIL'} the next start kept the watch as of that checkpoint: {[w['score'] for w in watches]}")
print(f"[season] {'PASS' if not save.get('watchUnderway', {}).get('active') else 'FAIL'} and save.json no longer holds it under way")
print(f"[season] {'PASS' if 'a watch cut short was kept' in open(sys.argv[3], errors='replace').read() else 'FAIL'} the player said so in its log")
PY
    ;;
  quitwatch|termwatch|closewatch)
    python3 - "$OUT/tour/player.log" "$DATA/save.json" <<'PY'
import json, re, sys
log = open(sys.argv[1], errors="replace").read()
m = re.search(r"QUIT-AT score=(\d+) ships=(\d+) seconds=(\d+)", log)
if not m:
    print("[season] FAIL the tour never said what the watch stood at"); sys.exit()
score, ships, secs = map(int, m.groups())
watches = json.load(open(sys.argv[2])).get("watches", [])
kept = [w for w in watches if w["score"] == score and w["ships"] == ships and abs(w["seconds"] - secs) <= 1]
print(f"[season] {'PASS' if kept else 'FAIL'} the watch under way when the game closed ({score} points, {ships} ships, {secs} s) is in save.json: "
      f"{len(watches)} watches, {[w['score'] for w in watches]}")
print(f"[season] {'PASS' if 'the watch was kept as the game closed' in log else 'FAIL'} the player said so in its log")
under = json.load(open(sys.argv[2])).get("watchUnderway", {})
print(f"[season] {'PASS' if not under.get('active') else 'FAIL'} and nothing is left under way in save.json")
PY
    ;;
  quitnight)
    now="$(sha256sum "$DATA/save.json" | cut -d' ' -f1)"
    if [ "$now" = "$seeded" ]; then echo "[season] PASS closing the game mid-night leaves save.json as it was"; else echo "[season] FAIL closing the game mid-night changed save.json"; fi
    ;;
esac
after="$(sums)"
if [ "$before" = "$after" ]; then echo "[season] PASS your own save is unchanged"; else echo "[season] FAIL your own save changed"; fi
