#!/usr/bin/env bash
# Zips the Linux build for a GitHub release:  Tools/package.sh [version]
# Output: Builds/release/LastLight-v<version>-linux-x86_64.zip: the player, a launcher, a short
# readme and the font licenses (Unity's debug-symbol folder is left out).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:-0.1.0}"
NAME="LastLight-v$VERSION-linux-x86_64"
OUT="$ROOT/Builds/release"
STAGE="$OUT/$NAME"
[ -x "$ROOT/Builds/Linux/LastLight.x86_64" ] || { echo "No build yet. Run Tools/unity.sh build-linux first." >&2; exit 1; }
rm -rf "$STAGE" "$OUT/$NAME.zip"
mkdir -p "$STAGE/licenses"
for f in LastLight.x86_64 LastLight_Data UnityPlayer.so libdecor-0.so.0 libdecor-cairo.so; do
  cp -a "$ROOT/Builds/Linux/$f" "$STAGE/"
done
cp "$ROOT/THIRD_PARTY_NOTICES.md" "$STAGE/"
cp "$ROOT"/Assets/Resources/Fonts/OFL-*.txt "$ROOT"/Assets/Resources/Fonts/LICENSE-*.txt "$STAGE/licenses/"

cat > "$STAGE/LastLight.sh" <<'SH'
#!/bin/sh
# Starts Last Light. Under Wayland it uses Unity's native Wayland backend, because the
# X11/XWayland path can hang at startup. Arguments are passed on to the game.
cd "$(dirname "$0")" || exit 1
if [ -n "${WAYLAND_DISPLAY:-}" ]; then
  exec ./LastLight.x86_64 -force-wayland "$@"
fi
exec ./LastLight.x86_64 "$@"
SH
chmod +x "$STAGE/LastLight.sh" "$STAGE/LastLight.x86_64"

cat > "$STAGE/README.txt" <<TXT
LAST LIGHT  v$VERSION  (Linux x86_64)

Keep the last lighthouse on a wrecking coast. Sweep its beam through the night to guide
ships home, chart the hidden reefs ahead of them, and expose the false lights trying to lure
them in.

Run ./LastLight.sh (or ./LastLight.x86_64). The game starts fullscreen; Settings > Display
switches to a window. You need 64-bit Linux and a GPU with OpenGL 4.5.

  Aim the beam      mouse, A/D or arrow keys; gamepad stick
  Focus             hold left mouse, Shift, W or Up; gamepad trigger
  Foghorn           Space or right mouse; gamepad A
  Pause / back      Esc or P; gamepad Start (B in menus)

Progress and settings are saved under ~/.config/unity3d/Gannet Head/Last Light/.
Source, trailer and known issues: https://github.com/nearbycoder/LastLight
Fonts: see licenses/ and THIRD_PARTY_NOTICES.md.
TXT

# Zip with Python (keeps the executable bits; no zip binary needed).
python3 - "$OUT" "$NAME" <<'PY'
import os, sys, zipfile
out, name = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(os.path.join(out, name + ".zip"), "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for root, dirs, files in os.walk(os.path.join(out, name)):
        dirs.sort()
        for f in sorted(files):
            path = os.path.join(root, f)
            info = zipfile.ZipInfo.from_file(path, os.path.relpath(path, out))
            info.compress_type = zipfile.ZIP_DEFLATED
            with open(path, "rb") as fh:
                z.writestr(info, fh.read(), compresslevel=9)
PY
ls -la "$OUT/$NAME.zip"
