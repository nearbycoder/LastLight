#!/usr/bin/env bash
# Runs the Unity 6000.6.2f1 editor against this project.
#
# The editor links against libxml2.so.2, but CachyOS/Arch ship libxml2.so.16 only, so the
# editor would exit with "libxml2.so.2: cannot open shared object file". We point the loader
# at a local copy of the legacy library (the proper fix is `sudo pacman -S libxml2-legacy`).
#
#   Tools/unity.sh                 open the project in the editor (GUI)
#   Tools/unity.sh serve           resident batch-mode editor (no GUI) for `unity command`
#   Tools/unity.sh setup           apply rendering/project setup (batch, quits)
#   Tools/unity.sh build-linux     batch-build Builds/Linux/LastLight.x86_64
#   Tools/unity.sh build-mac       batch-build Builds/Mac/LastLight.app (universal, unsigned)
#   Tools/unity.sh build-windows   batch-build Builds/Windows/LastLight.exe (needs the Windows module)
#   Tools/unity.sh test            run EditMode tests (mission solvability etc.)
set -euo pipefail

UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.6.2f1/Editor/Unity}"
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LIBS="${LASTLIGHT_UNITY_LIBS:-$HOME/.local/share/ptt-unity-libs}"
export LD_LIBRARY_PATH="$LIBS${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
mkdir -p "$PROJECT/Logs"

case "${1:-open}" in
  open)
    exec "$UNITY" -projectPath "$PROJECT"
    ;;
  serve)
    exec "$UNITY" -batchmode -projectPath "$PROJECT" -logFile "$PROJECT/Logs/serve.log"
    ;;
  setup)
    exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
      -executeMethod LastLight.EditorTools.ProjectSetup.ApplyBatch -logFile -
    ;;
  build-linux)
    exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
      -executeMethod LastLight.EditorTools.BuildScript.BuildLinux -logFile -
    ;;
  build-mac)
    exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -buildTarget OSXUniversal \
      -executeMethod LastLight.EditorTools.BuildScript.BuildMac -logFile -
    ;;
  build-windows)
    exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
      -executeMethod LastLight.EditorTools.BuildScript.BuildWindows -logFile -
    ;;
  test)
    exec "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests -testPlatform EditMode \
      -testResults "$PROJECT/Logs/test-results.xml" -logFile -
    ;;
  *)
    echo "usage: $0 [open|serve|setup|build-linux|build-mac|build-windows|test]" >&2
    exit 2
    ;;
esac
