#!/usr/bin/env bash
# Zips a build for a GitHub release:  Tools/package.sh [version] [linux|mac|windows]
# Output: Builds/release/LastLight-v<version>-<platform>.zip: the player, a short readme and the
# font licenses (Unity's debug-symbol folders are left out). Linux also gets a launcher.
#   linux    Builds/Linux    -> LastLight-v<version>-linux-x86_64.zip
#   mac      Builds/Mac      -> LastLight-v<version>-macos-universal.zip (unsigned, un-notarized)
#   windows  Builds/Windows  -> LastLight-v<version>-windows-x86_64.zip
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${1:-0.1.0}"
PLATFORM="${2:-linux}"
OUT="$ROOT/Builds/release"

CONTROLS='  Aim the beam      mouse, A/D or arrow keys; gamepad stick
  Focus             hold left mouse, Shift, W or Up; gamepad trigger
  Foghorn           Space or right mouse; gamepad A
  Pause / back      Esc or P; gamepad Start (B in menus)'
ABOUT='Keep the last lighthouse on a wrecking coast. Sweep its beam through the night to guide
ships home, chart the hidden reefs ahead of them, and expose the false lights trying to lure
them in.'

case "$PLATFORM" in
  linux)
    NAME="LastLight-v$VERSION-linux-x86_64"
    [ -x "$ROOT/Builds/Linux/LastLight.x86_64" ] || { echo "No build yet. Run Tools/unity.sh build-linux first." >&2; exit 1; }
    ;;
  mac)
    NAME="LastLight-v$VERSION-macos-universal"
    [ -d "$ROOT/Builds/Mac/LastLight.app" ] || { echo "No build yet. Run Tools/unity.sh build-mac first." >&2; exit 1; }
    ;;
  windows)
    NAME="LastLight-v$VERSION-windows-x86_64"
    [ -f "$ROOT/Builds/Windows/LastLight.exe" ] || { echo "No build yet. Run Tools/unity.sh build-windows first (it needs Unity's Windows Build Support module)." >&2; exit 1; }
    ;;
  *)
    echo "usage: $0 [version] [linux|mac|windows]" >&2
    exit 2
    ;;
esac

STAGE="$OUT/$NAME"
rm -rf "$STAGE" "$OUT/$NAME.zip"
mkdir -p "$STAGE/licenses"
cp "$ROOT/THIRD_PARTY_NOTICES.md" "$STAGE/"
cp "$ROOT"/Assets/Resources/Fonts/OFL-*.txt "$ROOT"/Assets/Resources/Fonts/LICENSE-*.txt "$STAGE/licenses/"

case "$PLATFORM" in
  linux)
    for f in LastLight.x86_64 LastLight_Data UnityPlayer.so libdecor-0.so.0 libdecor-cairo.so; do
      cp -a "$ROOT/Builds/Linux/$f" "$STAGE/"
    done
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

$ABOUT

Run ./LastLight.sh (or ./LastLight.x86_64). The game starts fullscreen; Settings > Display
switches to a window. You need 64-bit Linux and a GPU with OpenGL 4.5.

$CONTROLS

Progress and settings are saved under ~/.config/unity3d/Gannet Head/Last Light/.
Source, trailer and known issues: https://github.com/nearbycoder/LastLight
Fonts: see licenses/ and THIRD_PARTY_NOTICES.md.
TXT
    ;;
  mac)
    cp -a "$ROOT/Builds/Mac/LastLight.app" "$STAGE/Last Light.app"
    cat > "$STAGE/README.txt" <<TXT
LAST LIGHT  v$VERSION  (macOS 12 or later, Intel and Apple Silicon)

$ABOUT

THIS BUILD HAS NOT BEEN RUN ON A MAC. It was built on Linux, and it is not signed with an
Apple Developer ID or notarized, so macOS will refuse to open it the first time. To open it:

  1. Move "Last Light.app" to Applications (or anywhere you like).
  2. Control-click (or right-click) it and choose Open, then Open again in the dialog.
     On recent macOS, if there is no Open button: try to open it once, then go to
     System Settings > Privacy & Security and click "Open Anyway".
  Or, in Terminal:  xattr -dr com.apple.quarantine "/Applications/Last Light.app"

The game starts fullscreen; Settings > Display switches to a window.

$CONTROLS

Progress and settings are saved in ~/Library/Preferences/com.nearbycoder.lastlight.plist.
Source, trailer and known issues: https://github.com/nearbycoder/LastLight
Fonts: see licenses/ and THIRD_PARTY_NOTICES.md.
TXT
    ;;
  windows)
    for f in LastLight.exe LastLight_Data UnityPlayer.dll UnityCrashHandler64.exe MonoBleedingEdge D3D12; do
      [ -e "$ROOT/Builds/Windows/$f" ] && cp -a "$ROOT/Builds/Windows/$f" "$STAGE/"
    done
    cat > "$STAGE/README.txt" <<TXT
LAST LIGHT  v$VERSION  (Windows 10 or later, 64-bit)

$ABOUT

Run LastLight.exe. The game is not code-signed, so Windows SmartScreen may warn the first
time: choose "More info", then "Run anyway". The game starts fullscreen; Settings > Display
switches to a window.

$CONTROLS

Progress and settings are saved in the registry under HKCU\\Software\\Gannet Head\\Last Light.
Source, trailer and known issues: https://github.com/nearbycoder/LastLight
Fonts: see licenses/ and THIRD_PARTY_NOTICES.md.
TXT
    ;;
esac

# Zip with Python: keeps the executable bits and stores symlinks as links (no zip binary needed).
python3 - "$OUT" "$NAME" <<'PY'
import os, stat, sys, zipfile
out, name = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(os.path.join(out, name + ".zip"), "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for root, dirs, files in os.walk(os.path.join(out, name)):
        dirs.sort()
        for entry in sorted(files) + sorted(d for d in dirs if os.path.islink(os.path.join(root, d))):
            path = os.path.join(root, entry)
            arc = os.path.relpath(path, out)
            if os.path.islink(path):
                info = zipfile.ZipInfo(arc)
                info.create_system = 3
                info.external_attr = (stat.S_IFLNK | 0o777) << 16
                z.writestr(info, os.readlink(path))
                continue
            info = zipfile.ZipInfo.from_file(path, arc)
            info.compress_type = zipfile.ZIP_DEFLATED
            with open(path, "rb") as fh:
                z.writestr(info, fh.read(), compresslevel=9)
PY
ls -la "$OUT/$NAME.zip"
