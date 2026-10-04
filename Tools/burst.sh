#!/usr/bin/env bash
# Burst capture against the resident editor (Tools/unity.sh serve): plays a night with the
# AutoKeeper until a C# condition holds, then grabs a run of frames.
#   Tools/burst.sh <night> '<C# bool expr over w (SimWorld)>' <frames> <gap_s> <name> [timeout_s]
# e.g. LL_EXTRA="-llNeglect 0" Tools/burst.sh 5 'w.Wrecks > 0' 6 0.4 wreck
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NIGHT="$1"; COND="$2"; N="${3:-6}"; GAP="${4:-0.4}"; NAME="${5:-burst}"; TIMEOUT="${6:-120}"
mkdir -p "$ROOT/Temp" "$ROOT/Screenshots"
echo "-llNight $NIGHT -llAuto ${LL_EXTRA:-}" > "$ROOT/Temp/ll_boot.txt"
U() { local c="$1"; shift; unity command "$c" --project-path "$ROOT" "$@"; }
U editor_stop >/dev/null 2>&1 || true
U editor_play >/dev/null
for i in $(seq 1 "$TIMEOUT"); do
  out=$(U eval "var g = LastLight.Core.Game.Instance; if (!UnityEngine.Application.isPlaying || g == null || g.Runner == null) return false; var w = g.Runner.World; return $COND;" 2>/dev/null || true)
  echo "$out" | grep -q '"result":true' && break
  sleep 1
done
for k in $(seq 1 "$N"); do
  unity command capture_game_view --project-path "$ROOT" --format json -- --source camera --width "${LL_W:-1600}" --height "${LL_H:-900}" 2>/dev/null \
    | python3 -c "import json,sys,base64; d=json.load(sys.stdin); open(sys.argv[1],'wb').write(base64.b64decode(d['data']['result']['base64']))" "$ROOT/Screenshots/${NAME}_$k.png"
  sleep "$GAP"
done
U eval 'var g = LastLight.Core.Game.Instance; return g != null && g.Runner != null ? $"t={g.Runner.World.Time:0.0}" : "-";' 2>/dev/null | grep -o '"result":"[^"]*"' || true
U editor_stop >/dev/null 2>&1 || true
ls "$ROOT/Screenshots/${NAME}_"*.png
