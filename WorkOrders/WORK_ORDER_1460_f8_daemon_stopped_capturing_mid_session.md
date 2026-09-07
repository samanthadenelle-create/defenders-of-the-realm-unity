# WO-1460: the F8 daemon stopped capturing at 13:42 while the device played until 14:38

**Status:** READY TO IMPLEMENT
**Silo:** `.claude/skills/run-defenders/f8-watch-daemon.ps1` + `logs/f8-inbox/`. Tooling only.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1460 -> 1461 in the same edit).

## 1. EVIDENCE

```
logs/f8-inbox/QUEUE.jsonl   tail: seq 4685, utc 2026-09-06T13:42:52Z
device log                  last line: 09-06 14:37:56.597
```

Fifty-six minutes of play produced zero queue entries. The entire troop-AI-blind session - the one that
carried the raid loot loss, the hostile-admit storm and the 11 fps floor - is absent from the inbox.

This is the exact failure CLAUDE.md sec.14 exists to prevent: the owner became the bug detector again,
because the harness that was meant to surface captures had silently stopped and nothing said so.

## 2. FIX SHAPE

- Find why it stopped - device disconnect, the repo-level single-instance poller lock, or an unhandled
  exception in the watch loop. Read the daemon's own output before theorising.
- Add a HEARTBEAT field to `PING.json`, bumped on a timer whether or not a capture lands, so silence is
  distinguishable from health. Surface a stale heartbeat in `f8-check-inbox.ps1` output.
- Make the daemon log its own exit reason to `logs/f8-inbox/`.

## 3. WHAT NOT TO DO
- Do not "fix" it by restarting the daemon and closing the ticket. Silence without a heartbeat will recur and
  be invisible again.
- Do not touch the ack semantics - the queue is a queue, not a slot (WO-965).

## 4. ACCEPTANCE
- [ ] The stop cause is NAMED with the log line that proves it.
- [ ] `PING.json` carries a heartbeat; `f8-check-inbox.ps1` reports STALE when it ages past a threshold.
- [ ] Proven by killing the daemon and showing the stale report.
