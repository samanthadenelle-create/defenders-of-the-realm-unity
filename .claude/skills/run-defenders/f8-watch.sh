#!/usr/bin/env bash
# =============================================================================
# f8-watch.sh — F8 / break-log LIVE-TRIAGE watcher (CLAUDE.md binding CLI step).
# -----------------------------------------------------------------------------
# The owner is NEVER the bug detector (memory: never-dragdrop-or-manual-playtest).
# While the owner felt-tests, the CLI arms this so every F8 flag / error / softlock
# the harness records lands on the CLI the moment it appears — the CLI triages LIVE
# (RCA from the captured line + screenshot) instead of waiting to be told.
#
# Launch it with the Bash tool's run_in_background so the agent is NOTIFIED on a
# capture (a detached/hook process can't route findings back to the agent). Re-arm
# after each fire to cover the whole test session.
#
# Fires ONLY on real captures (F8 "flagged" / error / exception / softlock); ignores
# session_start + scene_loaded startup noise. Re-baselines on a fresh Play session
# (break-log shrinks) so the next capture is still caught.
#
# Usage: bash .claude/skills/run-defenders/f8-watch.sh [windowMinutes]   (default 60)
# =============================================================================
# LocalLow/<companyName>/<productName>; productName became "Echoes of Elarion" 2026-08-08.
# Prefer the new folder, fall back to the legacy one so older captures still triage.
DIR="$HOME/AppData/LocalLow/DeNelle/Echoes of Elarion"
[ -d "$DIR" ] || DIR="$HOME/AppData/LocalLow/DeNelle/Defenders of the Realm"
BL="$DIR/break-log.jsonl"
PLAYER_LOG="$DIR/Player.log"
EDITOR_LOG="$HOME/AppData/Local/Unity/Editor/Editor.log"
WIN="${1:-60}"
ITERS=$(( WIN * 4 ))          # 15s poll cadence

# On a fire, AUTO-HARVEST the already-captured data so the agent reads the trace
# FIRST, not guesses (memory: never-inference-fix — "the answer is already written;
# LOOK before you reason"). The owner should NEVER have to say "did you check the data".
# Pulls the recent [Flow:*] + FeatureFlags lines from whichever log this run wrote to
# (Editor.log when felt-testing in-editor, Player.log for a build) — the instant context
# that names the dead step / the flag state behind the flagged symptom.
harvest_context() {
  echo ""
  echo "=== AUTO-HARVESTED CAPTURE CONTEXT (read THIS before any code-read / agent) ==="
  for L in "$EDITOR_LOG" "$PLAYER_LOG"; do
    [ -f "$L" ] || continue
    hits=$(grep -nE '\[Flow:|\[FeatureFlags\]|ff\.[a-z]+ =|Guard\.|EXCEPTION|NullReference' "$L" 2>/dev/null | tail -n 60)
    if [ -n "$hits" ]; then
      echo "--- $L (last 60 [Flow:*]/FeatureFlags/Guard/exception lines) ---"
      echo "$hits"
    fi
  done
  echo "=== END CONTEXT — triage from the lines above, not from a theory ==="
}

base=$(wc -l < "$BL" 2>/dev/null || echo 0)
echo "[f8-watch] armed; baseline=$base lines; window=${WIN}m; file=$BL"
for ((i=0; i<ITERS; i++)); do
  cur=$(wc -l < "$BL" 2>/dev/null || echo 0)
  if [ "$cur" -lt "$base" ]; then base=$cur; fi        # new Play session wiped it -> re-baseline
  if [ "$cur" -gt "$base" ]; then
    new=$(tail -n +$((base+1)) "$BL")
    meaningful=$(echo "$new" | grep -vE '"kind":"(session_start|scene_loaded)"')
    if [ -n "$meaningful" ]; then
      echo "=== NEW break-log capture(s) — TRIAGE NOW ==="
      echo "$meaningful"
      harvest_context
      exit 0
    fi
    base=$cur                                          # only benign startup lines -> advance, keep watching
  fi
  sleep 15
done
echo "[f8-watch] ${WIN}m window elapsed, no captures."
