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
# Step/Warn land in each instance's PER-RUN player.log next to its break-log
# (autopilot-runs/<i>/player.log, WO-1102 2026-08-16 — the fleet passes -logFile
# per instance; before that the shared root Player.log lost the trace on fleets).

set -u
# persistentDataPath = LocalLow/<companyName>/<productName>; productName became "Echoes of
# Elarion" on 2026-08-08 (store-listing match), which MOVES this folder. Try the new name, then
# the legacy name, then the known absolute path (Windows Git-Bash HOME may not map to LocalLow).
RUNDIR="${1:-$HOME/AppData/LocalLow/DeNelle/Echoes of Elarion/autopilot-runs}"
[ -d "$RUNDIR" ] || RUNDIR="$HOME/AppData/LocalLow/DeNelle/Defenders of the Realm/autopilot-runs"
[ -d "$RUNDIR" ] || RUNDIR="/c/Users/Kayden-Laptop/AppData/LocalLow/DeNelle/Echoes of Elarion/autopilot-runs"
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
     'no graphics device','gfxdevice',
     # -nographics MSAA/render-target artifacts (Camera:Render, no real GfxDevice) —
     # mirrors AutoPilotTickets.RenderArtifactNeedles (2026-07-06 audit)
     'samples but','endrenderpass','not inside a renderpass','rendertexture.create failed',
     'drawopaqueobjects','drawtransparentobjects','attachment 0 was created']
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
echo "=== per-instance player.log (Step-level FlowTrace, WO-1102) ==="
found_pl=0
for f in "$RUNDIR"/*/player.log; do
  [ -f "$f" ] || continue
  found_pl=1
  bytes=$(wc -c < "$f" | tr -d ' ')
  flow=$(grep -c '\[Flow:' "$f" 2>/dev/null || true)
  echo "  $f  (${bytes} bytes, ${flow} [Flow:*] lines)"
done
[ "$found_pl" -eq 1 ] || echo "  (none - pre-WO-1102 fleet, or instances never wrote a log)"
echo "=== ranked ticket file (emitter output, written by the fleet) ==="
# Repo root is machine-dependent (C:\eoa / D:\eoa) — resolve it from this script's own
# location: .claude/skills/run-defenders/harvest.sh -> three dirs up is the root.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
ls -la "$REPO_ROOT/Builds/autopilot-tickets.md" 2>/dev/null || echo "  (see <repo>/Builds/autopilot-tickets.md)"
