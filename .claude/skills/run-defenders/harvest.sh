#!/usr/bin/env bash
# harvest.sh — summarize the latest AutoPilot fleet run (the OBSERVE step of the
# headless drive loop). Reads every per-run break-log.jsonl + autopilot-summary.json,
# filters known -nographics render artifacts, and prints a triage-ready verdict.
#
# Usage:  bash .claude/skills/run-defenders/harvest.sh
# (Run AFTER a fleet completes — see SKILL.md. No args; auto-finds the runs dir.)
#
# This is the packaged form of the harvest one-liner used across the 2026-06-20
# overnight loop (OVERNIGHT_AUTOPILOT_LOG.md). break-log.jsonl captures ONLY
# error-level lines (FlowTrace.Fail / exceptions / softlocks / owner F8 flags);
# Step/Warn go to Player.log (overwritten per fleet — not reliable for fleets).

set -u
RUNDIR="${1:-$HOME/AppData/LocalLow/DeNelle/Defenders of the Realm/autopilot-runs}"
# Windows Git-Bash HOME may not map to LocalLow; fall back to the known absolute path.
[ -d "$RUNDIR" ] || RUNDIR="/c/Users/Kayden-Laptop/AppData/LocalLow/DeNelle/Defenders of the Realm/autopilot-runs"

if [ ! -d "$RUNDIR" ]; then echo "no autopilot-runs dir at: $RUNDIR"; exit 2; fi

echo "=== fleet runs ==="; ls "$RUNDIR" 2>/dev/null | tr '\n' ' '; echo
echo "=== per-run AssertVendorTalkRoute verdict ==="
grep -h "talk-route violation" "$RUNDIR"/*/autopilot-summary.json 2>/dev/null | sort | uniq -c
echo "=== high-signal counts ==="
echo -n "  TALK-ROUTE violations: "; grep -h "TALK-ROUTE VIOLATION" "$RUNDIR"/*/break-log.jsonl 2>/dev/null | wc -l
echo -n "  dialogue No-node:      "; grep -h "No node has been selected" "$RUNDIR"/*/break-log.jsonl 2>/dev/null | wc -l
echo -n "  possible softlocks:    "; grep -h '"kind":"possible_softlock"' "$RUNDIR"/*/break-log.jsonl 2>/dev/null | wc -l
echo "=== NEW real errors (render artifacts + guard-handled magenta excluded) ==="
grep -h '"kind":"exception"\|"kind":"error"\|"kind":"possible_softlock"' "$RUNDIR"/*/break-log.jsonl 2>/dev/null | python -c "
import sys,json
from collections import Counter
art=['videodecode','video decode shader','videocomposite','custom render path shader',
     'could not find video','could not find material hidden/video','d3d11','direct3d',
     'no graphics device','gfxdevice']
c=Counter()
for l in sys.stdin:
  try:
    m=json.loads(l)['message']; ml=m.lower()
    if any(a in ml for a in art): continue        # -nographics render noise (NOT a bug)
    if 'MagentaGuard' in m: m='[MagentaGuard hid a stray placeholder - guard handles it]'
    c[m[:100]]+=1
  except Exception: pass
hits=c.most_common(15)
print('\n'.join(f'  x{n}: {m}' for m,n in hits) if hits else '  (none — clean)')
"
echo "=== ranked ticket file (emitter output, written by the fleet) ==="
ls -la /c/eoa/Builds/autopilot-tickets.md 2>/dev/null || echo "  (see <repo>/Builds/autopilot-tickets.md)"
