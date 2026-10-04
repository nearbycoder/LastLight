#!/usr/bin/env bash
# Runs the built Linux player. The X11/XWayland path hangs at startup on this machine, so use
# Unity's native Wayland backend when a Wayland session is available.
set -euo pipefail
GAME="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Builds/Linux/LastLight.x86_64"
[ -x "$GAME" ] || { echo "No build yet. Run Tools/unity.sh build-linux first." >&2; exit 1; }
args=(-screen-fullscreen 0 -screen-width "${LL_W:-1600}" -screen-height "${LL_H:-900}")
[ -n "${WAYLAND_DISPLAY:-}" ] && args+=(-force-wayland)
exec "$GAME" "${args[@]}" "$@"
