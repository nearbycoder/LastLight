#!/usr/bin/env bash
# Build the Linux player through the resident editor (Tools/unity.sh serve) if it is running,
# otherwise in a batch-mode editor.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if unity command console_status --project-path "$ROOT" >/dev/null 2>&1; then
  "$ROOT/Tools/refresh.sh"
  unity command editor_stop --project-path "$ROOT" >/dev/null 2>&1 || true
  start=$(wc -l < "$ROOT/Logs/serve.log")
  unity command eval --project-path "$ROOT" --timeout 1800 -- --code 'return LastLight.EditorTools.BuildScript.BuildLinuxFromEditor();' --timeout 1800000 >/dev/null 2>&1 &
  for i in $(seq 1 360); do
    tail -n +"$start" "$ROOT/Logs/serve.log" | grep -q "\[LastLight\] StandaloneLinux64 build" && break
    sleep 5
  done
  tail -n +"$start" "$ROOT/Logs/serve.log" | grep "\[LastLight\] StandaloneLinux64 build"
  wait
else
  "$ROOT/Tools/unity.sh" build-linux | grep -E "\[LastLight\]|error" 
fi
