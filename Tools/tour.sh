#!/usr/bin/env bash
# Screenshot tour in the built player:  Tools/tour.sh <script> <outdir> [extra player args]
# Prints the [Tour] log lines; screenshots land in <outdir>.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT="${1:-default}"; OUT="${2:-/tmp/ll-tour}"; shift 2 || true
rm -rf "$OUT"; mkdir -p "$OUT"
# Unity keeps the player's window size and the like in PlayerPrefs under ~/.config/unity3d (on
# Linux in a file every Unity player shares), so tours use a config directory of their own.
export XDG_CONFIG_HOME="${LL_TOUR_CONFIG:-$ROOT/Builds/tour-config}"
mkdir -p "$XDG_CONFIG_HOME"
timeout "${LL_TOUR_TIMEOUT:-600}" "$ROOT/Tools/play.sh" -logFile "$OUT/player.log" -llTour "$OUT" -llScript "$SCRIPT" "$@" > /dev/null 2>&1
grep -E "\[Tour\]|Exception" "$OUT/player.log" | head -60
ls "$OUT"
