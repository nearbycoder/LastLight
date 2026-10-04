#!/usr/bin/env bash
# Play-mode capture against the resident editor (Tools/unity.sh serve).
#   Tools/shot.sh <night> <seconds> <name> [auto] [timescale]
# Enters play mode on the given night, waits <seconds> of game time, captures the camera to
# Screenshots/<name>.png and prints the console errors.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NIGHT="${1:-1}"; SECS="${2:-5}"; NAME="${3:-shot}"; AUTO="${4:-}"; SCALE="${5:-1}"
mkdir -p "$ROOT/Temp" "$ROOT/Screenshots"
ARGS="-llNight $NIGHT"; [ -n "$AUTO" ] && ARGS="$ARGS -llAuto"
echo "$ARGS ${LL_EXTRA:-}" > "$ROOT/Temp/ll_boot.txt"
U() { local c="$1"; shift; unity command "$c" --project-path "$ROOT" "$@"; }
U editor_stop >/dev/null 2>&1 || true
U clear_console >/dev/null 2>&1 || true
U editor_play >/dev/null
for i in $(seq 1 60); do
  out=$(U eval 'return UnityEngine.Application.isPlaying && LastLight.Core.Game.Instance != null && LastLight.Core.Game.Instance.Runner != null;' 2>/dev/null || true)
  echo "$out" | grep -q '"result":true' && break
  sleep 1
done
U eval "UnityEngine.Time.timeScale = $SCALE; return 1;" >/dev/null
U wait_for -- --condition "{\"member\":\"UnityEngine.Time.time\",\"op\":\"greaterThan\",\"value\":$SECS}" --timeout_s $((SECS * 3 + 30)) >/dev/null || true
U eval "UnityEngine.Time.timeScale = 1; return 1;" >/dev/null
unity command capture_game_view --project-path "$ROOT" --format json -- --source camera --width "${LL_W:-1600}" --height "${LL_H:-900}" 2>/dev/null \
  | python3 -c "import json,sys,base64; d=json.load(sys.stdin); open(sys.argv[1],'wb').write(base64.b64decode(d['data']['result']['base64']))" "$ROOT/Screenshots/$NAME.png"
U console -- --tail 30 --level warn 2>/dev/null | grep -o '"message":"[^"]*"' | grep -v "GPUResidentDrawer\|UAC1001\|SourceAssetDB\|no scripts associated" | head -20 || true
ls -la "$ROOT/Screenshots/$NAME.png"
