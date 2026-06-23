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
BL="$HOME/AppData/LocalLow/DeNelle/Defenders of the Realm/break-log.jsonl"
WIN="${1:-60}"
ITERS=$(( WIN * 4 ))          # 15s poll cadence
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
      exit 0
    fi
    base=$cur                                          # only benign startup lines -> advance, keep watching
  fi
  sleep 15
done
echo "[f8-watch] ${WIN}m window elapsed, no captures."
