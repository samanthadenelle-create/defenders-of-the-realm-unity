#!/usr/bin/env bash
# =============================================================================
# seat-send.sh - WO-1200 : the UI seat SENDS a message to the CLI seat.
# -----------------------------------------------------------------------------
# This is the outbound half the UI seat lacked. It rides case (b) from WO-1200
# sec.3: the UI seat cannot share the CLI's tree but CAN push, so the message
# lands on a dedicated `seat-mail/ui-to-cli` git ref the CLI fetches. The ref is
# SEPARATE from any code branch, so this channel structurally cannot touch
# WorkOrders/*.md or BOARD.html (acceptance 6).
#
# Usage:
#   seat-mail/seat-send.sh <kind> "<subject>" "<body>"
#     <kind> = question | blocked | delivered | fyi
#              (blocked and question are the two that must never sit unread)
#
# It fetches the ref, appends via seatmail.py (append-only QUEUE.jsonl + one file
# per message, monotonic seq), commits to the ref, and pushes. ASCII-only bodies
# (seatmail.py rejects non-ASCII). Do NOT put secrets/tokens here - the ref is
# pushed = published (WO-1200 sec.5).
# =============================================================================
set -euo pipefail

REF="seat-mail/ui-to-cli"
FROM="${SEATMAIL_FROM:-ui-seat}"
HERE="$(cd "$(dirname "$0")" && pwd)"          # .../seat-mail (on the code tree)
PY="$HERE/seatmail.py"
ROOT="$(git -C "$HERE" rev-parse --show-toplevel)"

if [ "$#" -ne 3 ]; then
  echo "usage: seat-send.sh <question|blocked|delivered|fyi> \"<subject>\" \"<body>\"" >&2
  exit 2
fi
KIND="$1"; SUBJECT="$2"; BODY="$3"
UTC="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

# --- materialize the ref in a throwaway worktree (mailbox files ONLY) ---------
git -C "$ROOT" fetch -q origin "$REF" 2>/dev/null || true
WT="$(mktemp -d)"
cleanup() { git -C "$ROOT" worktree remove --force "$WT" >/dev/null 2>&1 || rm -rf "$WT"; }
trap cleanup EXIT

if git -C "$ROOT" rev-parse --verify -q "origin/$REF" >/dev/null 2>&1; then
  git -C "$ROOT" worktree add -q -f -B "$REF" "$WT" "origin/$REF"
else
  # first ever message: create an orphan history holding only the mailbox
  git -C "$ROOT" worktree add -q -f --detach "$WT"
  git -C "$WT" checkout -q --orphan "$REF"
  git -C "$WT" reset -q --hard 2>/dev/null || true
  find "$WT" -mindepth 1 -maxdepth 1 ! -name '.git' -exec rm -rf {} + 2>/dev/null || true
fi

# --- append the message (logic lives in the tested single-source core) --------
python3 "$PY" enqueue \
  --queue "$WT/QUEUE.jsonl" --msgdir "$WT/msg" \
  --from "$FROM" --utc "$UTC" --kind "$KIND" \
  --subject "$SUBJECT" --body "$BODY"

SEQ="$(python3 "$PY" pending --queue "$WT/QUEUE.jsonl" --cursor /dev/null | sed 's/pending=//')"

# --- commit to the ref and push ----------------------------------------------
git -C "$WT" add QUEUE.jsonl msg
git -C "$WT" -c user.name="seat-mail" -c user.email="seat-mail@local" \
  commit -q -m "seat-mail(${FROM}): ${KIND} - ${SUBJECT}"
git -C "$WT" push -q origin "HEAD:refs/heads/${REF}"
echo "[seat-send] pushed to ${REF} (queue now holds ${SEQ} message(s)). CLI fetches ${REF}."
