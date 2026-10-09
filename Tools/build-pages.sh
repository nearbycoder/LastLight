#!/usr/bin/env bash
# Rebuilds the browser build that GitHub Pages serves at https://nearbycoder.github.io/LastLight/.
#
#   Tools/build-pages.sh            build into Builds/Pages/LastLight (gitignored), log in Logs/build-pages.log
#
# The folder is the whole site: index.html at its root, Build/ (Brotli files the page's loader
# decompresses itself, so no Content-Encoding headers are needed), TemplateData/ and .nojekyll.
# Publish its contents as the root of the gh-pages branch. To try it as Pages will serve it, serve
# Builds/Pages and open /LastLight/ (Tools/check-pages.mjs does that with --serve).
#
# LL_WEB_OPTIMIZATION picks Unity's code optimization (default DiskSizeLTO, the smallest; BuildTimes
# is the quickest to build). Only one Unity editor may have the project open: close it first.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SITE="$ROOT/Builds/Pages/LastLight"
LOG="$ROOT/Logs/build-pages.log"
mkdir -p "$ROOT/Logs"

echo "[pages] building the web player (log: $LOG)"
start=$(date +%s)
if ! nice -n 10 "$ROOT/Tools/unity.sh" build-web >"$LOG" 2>&1; then
  echo "[pages] the build failed; the log's errors:" >&2
  grep -E "error|Error|\[LastLight\]" "$LOG" | grep -v "^\s*$" | tail -n 30 >&2 || true
  exit 1
fi
grep -E "\[LastLight\] WebGL" "$LOG" || true
[ -f "$SITE/index.html" ] || { echo "[pages] no index.html in $SITE" >&2; exit 1; }
touch "$SITE/.nojekyll"   # Pages serves the files as they are (no Jekyll)

# GitHub refuses files of 100 MB or more; keep each well under, and say what the first visit downloads.
status=0
while IFS= read -r -d '' f; do
  size=$(stat -c %s "$f")
  if [ "$size" -ge 100000000 ]; then echo "[pages] TOO BIG for GitHub (100 MB): ${f#$SITE/} $size bytes" >&2; status=1
  elif [ "$size" -ge 50000000 ]; then echo "[pages] warning: over 50 MB: ${f#$SITE/} $size bytes" >&2; fi
done < <(find "$SITE" -type f -print0)
total=$(du -sb "$SITE" | cut -f1)
largest=$(find "$SITE" -type f -printf '%s %P\n' | sort -n | tail -n 1)
echo "[pages] site: $SITE"
echo "[pages] total $(awk -v b="$total" 'BEGIN{printf "%.1f MB", b/1e6}'), largest file: $(awk '{printf "%s (%.1f MB)", $2, $1/1e6}' <<<"$largest")"
find "$SITE" -type f -printf '%s %P\n' | sort -rn | awk '{printf "  %8.2f MB  %s\n", $1/1e6, $2}'
echo "[pages] built in $(( $(date +%s) - start )) s"
exit $status
