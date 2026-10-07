#!/usr/bin/env bash
# Runs a command inside a private, invisible KWin (a virtual output, its own D-Bus session and its
# own config folders), so test windows never appear on, or change, the desktop you're using.
#
#   Tools/nested.sh Tools/tour.sh chart "$PWD/Builds/out" -llFresh
#
# LL_W and LL_H set the virtual screen's size (and the game window's, through Tools/play.sh).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
[ $# -gt 0 ] || { echo "usage: $0 <command> [args...]" >&2; exit 2; }
command -v kwin_wayland >/dev/null || { echo "kwin_wayland isn't installed" >&2; exit 1; }
W="${LL_W:-1600}"; H="${LL_H:-900}"
NEST="$ROOT/Builds/nested/$$"
mkdir -p "$NEST/config" "$NEST/data" "$NEST/cache" "$NEST/state"
SOCK="lastlight-nested-$$"
# The command, quoted word by word, run with the nested compositor's socket.
{
  echo '#!/usr/bin/env bash'
  printf 'export WAYLAND_DISPLAY=%q\n' "$SOCK"
  printf 'cd %q\n' "$PWD"
  printf '%q ' "$@"; echo '> "'"$NEST"'/out" 2>&1; echo $? > "'"$NEST"'/status"'
} > "$NEST/session.sh"
chmod +x "$NEST/session.sh"
# KWin's own settings (outputs, effects) go to the private folders, not ~/.config.
XDG_CONFIG_HOME="$NEST/config" XDG_DATA_HOME="$NEST/data" XDG_CACHE_HOME="$NEST/cache" XDG_STATE_HOME="$NEST/state" \
  dbus-run-session -- kwin_wayland --virtual --no-lockscreen --socket "$SOCK" --width "$W" --height "$H" \
  --exit-with-session "$NEST/session.sh" > "$NEST/kwin.log" 2>&1 || true
status="$(cat "$NEST/status" 2>/dev/null || echo 1)"
cat "$NEST/out" 2>/dev/null || true
[ "$status" = 0 ] && rm -rf "$NEST" || echo "nested: exit $status, KWin log in $NEST/kwin.log" >&2
exit "$status"
