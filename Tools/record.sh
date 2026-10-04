#!/usr/bin/env bash
# Records the gameplay reel from the built player:  Tools/record.sh [out.mp4] [extra player args]
# The "video" tour steps time at a fixed 30 fps, pipes frames into ffmpeg and captures the audio
# mix; this script muxes the two. Size with LL_W/LL_H (default 1920x1080).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${1:-$ROOT/Builds/LastLight_gameplay.mp4}"; shift || true
WORK="$(mktemp -d /tmp/ll-video.XXXX)"
export LL_W="${LL_W:-1920}" LL_H="${LL_H:-1080}" LL_TOUR_TIMEOUT="${LL_TOUR_TIMEOUT:-3600}"
"$ROOT/Tools/tour.sh" video "$WORK" -llFresh "$@"
[ -s "$WORK/video.mp4" ] || { echo "no video captured (see $WORK/player.log)" >&2; exit 1; }
read -r RATE CHANNELS < "$WORK/audio.txt"
ffmpeg -y -loglevel error -i "$WORK/video.mp4" -f f32le -ar "$RATE" -ac "$CHANNELS" -i "$WORK/audio.f32" \
  -map 0:v -map 1:a -c:v copy -c:a aac -b:a 192k -ac 2 -shortest -movflags +faststart "$OUT"
ls -la "$OUT"
rm -rf "$WORK"
