#!/usr/bin/env bash
# Content validation in the running editor (Tools/unity.sh serve): data integrity, beam
# reachability, route safety for every hull, and an AutoKeeper playthrough of all twelve nights.
# Without a running editor, use `Tools/unity.sh test` (the same checks as NUnit EditMode tests).
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
echo 'return LastLight.Sim.Validation.Report();' > /tmp/ll_validate.cs
"$ROOT/Tools/eval.sh" /tmp/ll_validate.cs 1800000
