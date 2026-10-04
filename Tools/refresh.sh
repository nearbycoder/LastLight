#!/usr/bin/env bash
# Refresh assets in the resident editor, wait for compilation, report compile errors.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity command editor_stop --project-path "$ROOT" >/dev/null 2>&1 || true
unity command eval --project-path "$ROOT" 'UnityEditor.AssetDatabase.Refresh(); return "ok";' >/dev/null 2>&1
sleep 2
for i in $(seq 1 90); do
  out=$(unity command console_status --project-path "$ROOT" 2>/dev/null) && echo "$out" | grep -q '"compiling":false' && break
  sleep 2
done
echo "$out" | grep -o '"compilationFailed":[a-z]*'
grep -E "error CS[0-9]+" "$ROOT/Logs/serve.log" | tail -n 200 | sort -u | tail -15 || true
