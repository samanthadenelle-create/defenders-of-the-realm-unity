# F8 Inbox

Runtime folder for the persistent F8 watcher. The producers (`f8-watch-daemon.ps1`,
`websig-watch-daemon.ps1`, `bugreport-watch.ps1`) all publish here through
`.claude/skills/run-defenders/f8-inbox-lib.ps1`.

| File | Purpose |
|------|---------|
| `QUEUE.jsonl` | **The record (WO-965)** — append-only, one JSON line per capture. Never rewritten |
| `PING.json` | Monotonic capture counter — a VIEW of the newest capture |
| `LATEST_CAPTURE.md` | Newest capture + auto-harvested `[Flow:*]` context (VIEW; carries a backlog banner when others are queued) |
| `capture-*-seq<N>.md` | Per-capture harvest — one file per seq, what you actually triage |
| `ACK.json` | `lastAckSeq` (contiguous watermark) + `acked` (out-of-order acks) |
| `queue-events.log` | Loud ledger — supersede / unqueued / lost-capture / ack events |
| `daemon-state.json` | Persisted break-log offset so a daemon restart replays, never skips |
| `daemon.pid` | Running daemon process id |

**Consume it (never just read LATEST_CAPTURE.md):**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude\skills\run-defenders\f8-check-inbox.ps1
# NEW_CAPTURE + seq/kind/capture= (OLDEST pending) + pending=N ; exit 0 = work waiting, 1 = clean
powershell -NoProfile -ExecutionPolicy Bypass -File .claude\skills\run-defenders\f8-ack.ps1
# acks ONE capture (the oldest). Repeat until check-inbox reports NO_CAPTURE.
```

Why: before WO-965 both `LATEST_CAPTURE.md` and `PING.json` were single slots, so a burst collapsed
to its newest member and `f8-ack.ps1` (which acked PING's seq) buried the rest. On 2026-08-10 the
seat acked seq 2306, the next ping it saw was 2309, and the owner's seq 2307 + 2308 were never
surfaced to any seat.

**Start daemon (once per session / login):**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude\skills\run-defenders\f8-watch-start.ps1
```

Capture files are gitignored; this README stays in repo.
