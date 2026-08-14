# WORK ORDER 965 — F8 inbox is a QUEUE: no owner capture is ever dropped again

**Status:** DONE — shipped `96100bc2` ("fix(harness): WO-965 - the F8 inbox is a QUEUE"). RESULT file still owed (not fabricated). *(Status corrected 2026-08-14: the old line still said "awaiting batch-gate + commit" after the commit landed.)*
**Silo:** Tooling / harness (no Unity, no game code — scripts + docs only)
**Lane:** isolated — `.claude/**` + `logs/f8-inbox/README.md` + `.cursor/rules/**` + CLAUDE.md §14
**Minted:** 2026-08-10 (number off the `CLI_LANES_WO_NUMBERS.md` banner; banner bumped 965 → 966 in the same edit)

---

## The defect (captured evidence, not a theory)

The owner's F8 flags reach a Claude Code seat through `logs/f8-inbox/` — `LATEST_CAPTURE.md` plus a
`PING.json` seq that `f8-check-inbox.ps1` compares against `ACK.json`.

**Today, on this machine:**

| Proof | Value |
|---|---|
| `logs/f8-inbox/ACK.json` (before fix) | `lastAckSeq: 2309` — the seat had last acked **2306**, then acked 2309 |
| `logs/f8-inbox/PING.json` | `seq: 2309`, fired `23:15:52Z` |
| `capture-20260810-181521.md` head | `# F8 Capture (auto-inbox seq=2307)` — owner flag: *"both NPC and echo but no movement"* |
| `capture-20260810-181546.md` head | `# F8 Capture (auto-inbox seq=2308)` — `[Flow:Tutorial] STEP-STUCK :: founding_walk` (an error capture) |
| `capture-20260810-181552.md` head | `# F8 Capture (auto-inbox seq=2309)` — the only one a seat ever saw |

Seq **2307 and 2308 fired, were written to disk with their harvest, and were surfaced to NOBODY.**
The owner is the PO and is explicitly never the bug detector (CLAUDE.md §14) — a harness that eats
her flags defeats the entire passive-listener design.

### Root cause, at source (three compounding mechanisms)

1. **Single-slot publish.** `f8-watch-daemon.ps1:113-114` (pre-fix) wrote
   `Set-Content $capPath` **and** `Set-Content $Latest`, then `Write-Ping` (`:49-59`) **overwrote**
   `PING.json` with the new seq. Both surfaces hold exactly ONE capture, so a burst between two seat
   looks collapses to its newest member. 2307 → 2308 → 2309 in 31 seconds is exactly that burst.
2. **The ack buried the rest.** `f8-ack.ps1:8-12` (pre-fix) wrote `lastAckSeq = PING.json.seq` — the
   **newest** seq — so acking 2309 marked 2307 and 2308 as triaged. `f8-check-inbox.ps1:22`
   (`if ($ping.seq -le $lastAck) { exit 1 }`) then reported a clean inbox. Silent, by construction.
3. **Same-second filename collision.** The per-seq file was named `capture-<yyyyMMdd-HHmmss>.md`
   (`f8-watch-daemon.ps1:82-83`), so two captures inside one second overwrote the FILE too — the
   only copy of the harvest.

Two further silent-loss paths found while reading:

4. **Daemon restart amnesia.** `f8-watch-daemon.ps1:129-132` baselined `$breakBase` to the *current*
   break-log line count on every start, so anything the owner flagged while the daemon was down
   (reboot, crash, seat restart) was skipped forever, with no trace.
5. **Cross-producer seq collision.** `websig-watch-daemon.ps1` and `bugreport-watch.ps1` share the
   same `PING.json` seq and each did read-then-write with no lock — two producers in the same second
   could mint the same seq, and one capture's ping would erase the other's.

Bonus defect proven from the live `PING.json`: `kind` read `"Main_Castle_Overworld"` (the *scene*),
because the regex `kind.*:\s*"(\w+)"` (`f8-watch-daemon.ps1:165`) is greedy and walked past the
`"kind"` field to the last quoted word on the line.

---

## The fix

**Design choice: an append-only queue (`logs/f8-inbox/QUEUE.jsonl`), one JSON line per capture.**
Chosen over "make `LATEST_CAPTURE.md` append" because (a) each capture keeps its own
`capture-*-seq<N>.md` file, so its auto-harvested `[Flow:*]` block stays intact and readable on its
own — a queued capture without its harvest is worth little; (b) a capture keeps a machine-readable
identity (its seq), which is what makes ordered, one-at-a-time acking possible; (c) an append-only
log cannot be corrupted by a concurrent producer the way a rewritten aggregate can. The per-seq
capture files already existed — they were simply unreachable.

| File | Change |
|---|---|
| `.claude/skills/run-defenders/f8-inbox-lib.ps1` | **NEW.** `Publish-F8Capture` (locked seq allocation → per-seq file → `LATEST` → `PING` → **append `QUEUE.jsonl`**), `Get-F8Pending` (every un-acked capture, oldest first, with recovery + loud warnings), `Get-F8AckState`/`Save-F8AckState` (contiguous watermark + out-of-order set), no-BOM writers, named-mutex lock |
| `f8-watch-daemon.ps1` | Publishes via the lib; per-seq filenames `capture-<stamp>-seq<N>.md`; break-log offset **persisted** to `daemon-state.json` (restart replays instead of skipping, and says so); `"kind"`-anchored regex |
| `websig-watch-daemon.ps1`, `bugreport-watch.ps1` | Publish via the same lib — web traces and tester bug reports enter the same queue, under the same lock |
| `f8-check-inbox.ps1` | Walks the queue: surfaces the **OLDEST** un-acked capture + `pending=N` + the full backlog list |
| `f8-ack.ps1` | Acks **ONE** capture (the oldest) — `-Seq n` for out of order, `-All` explicit; prints what REMAINS |
| `.claude/hooks/f8-prompt-check.ps1`, `f8-poll-rewake.ps1` | Inject / wake with the whole backlog, oldest first, pointing at that capture's own file |
| `logs/f8-inbox/README.md`, `SKILL.md`, `.cursor/rules/f8-auto-triage.mdc`, `CLAUDE.md` §14 | Canon: the inbox is a queue; drain `pending=` to 0; never ack "the latest" |

### Nothing fails silently (the whole point)

- Publishing over an un-acked capture logs `warn: seq=N SUPERSEDES un-acked seq=M` to
  `queue-events.log` + console, and stamps a **BACKLOG banner** into `LATEST_CAPTURE.md` itself.
- A pending seq with no queue entry → `WARN_UNQUEUED`, and the capture is **recovered** by scanning
  capture files for its `seq=N` header (this is the bridge for a pre-WO-965 daemon still running).
- A pending seq with no queue entry **and** no capture file → `ERROR_LOST_CAPTURE ... Tell the owner.`
- A daemon restart with a break-log backlog → `warn: daemon was DOWN for N break-log line(s) — replaying`.
- Every ack that leaves a backlog → `[f8-ack] STILL PENDING: N ... Do NOT stop here.`

### Contract preserved

- `f8-check-inbox.ps1`: exit **0** + `NEW_CAPTURE` when work waits, exit **1** + `NO_CAPTURE` when
  clean; `seq=` / `kind=` / `firedAt=` / `latest=` / `capture=` lines all still printed (`latest=`
  still points at `LATEST_CAPTURE.md`; `seq=`/`capture=` now name the oldest pending capture).
- `PING.json` keeps its shape and its monotonic `seq` (one field added: `source`).
- `ACK.json` keeps `lastAckSeq` as the **contiguous watermark**, so `f8-prompt-check.ps1` and
  `f8-poll-rewake.ps1`, and any other seat reading it, behave correctly with no change required.
- `f8-ack.ps1` still runs bare, exits 0, prints `[f8-ack] Acknowledged seq=N`.
- The repo-level single-instance lock in `f8-poll-rewake.ps1` is untouched.
- **The owner changes nothing.** She plays and presses F8.

### Verification (no Unity, no live-session disturbance)

Isolated fake-repo run in the scratchpad (the live inbox and the owner's running daemon were never
touched):
- Burst of 3 captures in the same second → 3 distinct per-seq files, 3 queue lines, supersede warnings
  fired, `check-inbox` reported `NEW_CAPTURE seq=1 pending=3` with the full backlog listed.
- `f8-ack.ps1` bare → acked seq 1 only, reported `STILL PENDING: 2`; `-Seq 3` out-of-order → watermark
  held at 1, seq 2 still pending; final ack → watermark rolled to 3, `NO_CAPTURE`, exit 1.
- Legacy-producer simulation (PING seq with no queue entry) → seq recovered from its capture file with
  `WARN_UNQUEUED`; a seq with no file at all → `ERROR_LOST_CAPTURE`.
- All 8 touched scripts parse clean under the PS 5.1 AST parser. All new writes are UTF-8 **no BOM**
  (`[System.IO.File]::WriteAllText`) — verified byte-wise.

### Follow-up for the operator (not code)

The daemon currently running on the owner's machine is **pre-WO-965 code**; its captures will surface
via the recovery path with `WARN_UNQUEUED` until it is restarted (`f8-watch-stop.ps1` then
`f8-watch-start.ps1`) at a moment that does not interrupt her session.

**PO note:** seq 2307 (*"both NPC and echo but no movement"*) and seq 2308 (`founding_walk`
STEP-STUCK) are still un-triaged — they were never seen. Their capture files are intact on disk:
`logs/f8-inbox/capture-20260810-181521.md` and `capture-20260810-181546.md`.
