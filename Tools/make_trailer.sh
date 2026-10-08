#!/usr/bin/env bash
# The README media, reproducibly, from the built player (Builds/Linux):
#   Tools/make_trailer.sh            shoot the trailer clips and the stills, then edit
#   Tools/make_trailer.sh --edit     re-edit an existing shoot ($LL_CAPTURES/trailer)
# Outputs in docs/media: LastLight_trailer.mp4, trailer_poster.jpg, teaser.webp and the
# screenshots. The shoot plays scripted nights with the AutoKeeper (see
# Assets/Scripts/Automation/Trailer.cs); the edit is Tools/make_trailer.py.
#
# LL_CAPTURES sets where the raw shoot goes (default Captures/, gitignored) and LL_FIDELITY the
# Graphics fidelity step it's shot at (default 3, Ultra; 2 is High, the look as released). Frames
# are captured at a fixed 30 fps however long each takes to render, so Ultra costs only time.
# With kwin_wayland installed the shoot runs in a private nested KWin (Tools/nested.sh), so no
# window reaches the desktop.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CAP="${LL_CAPTURES:-$ROOT/Captures}"
MEDIA="$ROOT/docs/media"
FIDELITY="${LL_FIDELITY:-3}"
export LL_W=1920 LL_H=1080 LL_TOUR_TIMEOUT=3600
# The screenshots the README shows (the stills shoot takes a few more).
STILLS="title play chart foghorn storm false_light wreck night_watch results logbook dawn_chart settings"

if [ "${1:-}" != "--edit" ]; then
  run=()
  command -v kwin_wayland >/dev/null && run=("$ROOT/Tools/nested.sh")
  nice -n 10 "${run[@]}" "$ROOT/Tools/tour.sh" trailer "$CAP/trailer" -llFresh -llSeed 4242 -llFidelity "$FIDELITY"
  [ -s "$CAP/trailer/clips.json" ] || { echo "the trailer shoot failed (see $CAP/trailer/player.log)" >&2; exit 1; }
  nice -n 10 "${run[@]}" "$ROOT/Tools/tour.sh" stills "$CAP/stills" -llFresh -llSeed 4242 -llFidelity "$FIDELITY"
  mkdir -p "$MEDIA"
  for name in $STILLS; do
    png="$CAP/stills/$name.png"
    [ -s "$png" ] || { echo "the stills shoot made no $name.png (see $CAP/stills/player.log)" >&2; exit 1; }
    magick "$png" -strip -quality 90 -sampling-factor 4:2:0 "$MEDIA/$name.jpg"
  done
fi
nice -n 10 "$ROOT/Tools/.venv/bin/python" "$ROOT/Tools/make_trailer.py" --capture "$CAP/trailer" --out "$MEDIA"
ls -la "$MEDIA"
