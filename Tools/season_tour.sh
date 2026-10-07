#!/usr/bin/env bash
# The season tour exercises the real save ("Start a new season", an unreadable save), so it runs
# the built player against a throwaway config directory seeded with a save, never your own.
#
#   Tools/season_tour.sh reset   <absolute outdir>   a finished season, cleared from the logbook
#   Tools/season_tour.sh damaged <absolute outdir>   an unreadable save, kept aside
#   Tools/season_tour.sh migrate <absolute outdir>   an older build's save in PlayerPrefs, carried over
#   Tools/season_tour.sh readonly <absolute outdir>  the save folder can't be written: the keeper is told
#   Tools/season_tour.sh unopenable <absolute outdir> something at save.json that can't be opened: never written over
#
# The game's data folder (where save.json lives) is checksummed before and after.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN="${1:?reset, damaged, migrate, readonly or unopenable}"; OUT="${2:?an absolute output directory}"
case "$OUT" in /*) ;; *) echo "give the output directory as an absolute path" >&2; exit 2 ;; esac
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
LL_TOUR_CONFIG="$CFG" "$ROOT/Tools/tour.sh" season "$OUT/tour" -llSeasonRun "$RUN" -llSeasonHome "$CFG"
after="$(sums)"
if [ "$before" = "$after" ]; then echo "[season] PASS your own save is unchanged"; else echo "[season] FAIL your own save changed"; fi
