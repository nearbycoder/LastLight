#!/usr/bin/env bash
# Run a C# file in the resident editor and print its return value.  Tools/eval.sh file.cs [timeout_ms]
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity command eval_file --project-path "$ROOT" --format json -- --file "$1" --timeout "${2:-120000}" 2>&1 | python3 -c "
import json,sys
raw=sys.stdin.read()
try:
    d=json.loads(raw)
    r=d.get('data',{}).get('result',{})
    if isinstance(r,dict) and 'result' in r:
        print(r['result'] if r.get('success',True) else r)
        if r.get('diagnostics'): print(r['diagnostics'])
    else: print(json.dumps(d)[:3000])
except Exception as e:
    print(raw[:3000])
"
