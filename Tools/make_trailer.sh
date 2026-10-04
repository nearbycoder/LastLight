#!/usr/bin/env bash
# The README media, reproducibly, from the built player (Builds/Linux):
#   Tools/make_trailer.sh            shoot the trailer clips and the stills, then edit
#   Tools/make_trailer.sh --edit     re-edit an existing shoot (Captures/trailer)
# Outputs in docs/media: LastLight_trailer.mp4, trailer_poster.jpg, teaser.webp and the
# screenshots. The shoot plays scripted nights with the AutoKeeper (see
# Assets/Scripts/Automation/Trailer.cs); the edit is Tools/make_trailer.py.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CAP="$ROOT/Captures"
MEDIA="$ROOT/docs/media"
export LL_W=1920 LL_H=1080 LL_TOUR_TIMEOUT=3600

if [ "${1:-}" != "--edit" ]; then
  nice -n 5 "$ROOT/Tools/tour.sh" trailer "$CAP/trailer" -llFresh -llSeed 4242
  [ -s "$CAP/trailer/clips.json" ] || { echo "the trailer shoot failed (see $CAP/trailer/player.log)" >&2; exit 1; }
  nice -n 5 "$ROOT/Tools/tour.sh" stills "$CAP/stills" -llFresh -llSeed 4242
  mkdir -p "$MEDIA"
  for png in "$CAP/stills"/*.png; do
    magick "$png" -strip -quality 90 -sampling-factor 4:2:0 "$MEDIA/$(basename "${png%.png}").jpg"
  done
fi
nice -n 5 "$ROOT/Tools/.venv/bin/python" "$ROOT/Tools/make_trailer.py" --capture "$CAP/trailer" --out "$MEDIA"
ls -la "$MEDIA"
