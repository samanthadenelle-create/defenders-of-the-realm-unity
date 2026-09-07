# WO-1460: the F8 daemon stopped capturing at 13:42 while the device played until 14:38

**Status:** DONE (2026-09-06 CLI tooling lane - heartbeat shipped; see section 7 residual: the dedupe
defect that caused the silence is surfaced, not fixed, and needs its own ticket)
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

**THE CAUSE WAS FOUND BY THIS LANE, AND IT IS NOT A DEAD DAEMON.** Both producers ran all day
(`daemon.pid` 13472, `device-bridge.pid` 2196, `device-state.json updatedUtc 2026-09-07T01:38:59Z`). The
bridge's dedupe key suppressed 319 eligible captures - including the owner's FLAG press at 01:18:13Z.
**That half is WO-1531.** This ticket keeps the heartbeat work, which stands on its own: a bridge that
publishes nothing and a bridge that has died look identical from the inbox, and only a heartbeat separates
them.

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
- [x] The stop cause is NAMED with the log line that proves it.
- [x] `HEARTBEAT.json` (sibling of `PING.json`) carries a heartbeat; `f8-check-inbox.ps1` reports STALE when it ages past 90 s.
- [x] Proven by backdating the heartbeat and showing the stale report.

## 5. ROOT CAUSE (measured 2026-09-06, CLI tooling lane)

**Nothing stopped. The bridge deduped the whole session into silence.**

Both producers were alive the entire time (`daemon.pid` 13472, `device-bridge.pid` 2196; `device-state.json`
`updatedUtc 2026-09-07T01:38:59Z`). The last line the queue's own log wrote:

```
logs/f8-inbox/queue-events.log
2026-09-06T13:42:52.5433572Z [info] queued seq=4685 kind=error source=device file=...capture-device-20260906-084252-seq4685.md
2026-09-06T13:43:43.7214454Z [info] acked seq=4685 (watermark now 4685)
```

...and nothing after it, while `logs/f8-inbox/device/SM02G4061955851/break-log.jsonl` grew to 11359 lines
whose LAST entry is the owner's own flag:

```
{"kind":"flagged","message":"[Main_Castle_Overworld] on-screen FLAG button (mobile)", ... "utc":"2026-09-07T01:18:13.1285390Z"}
```

Replaying the bridge's own filter over the staged log against the live `device-state.json`:

```
eligible entries newer than lastUtc (2026-09-06T13:42:43Z): 319  {error: 316, possible_softlock: 2, flagged: 1}
suppressed by seen-hash: 319   would-publish: 0
```

`f8-device-bridge.ps1` keys its rolling dedupe on `SHA1(kind + '|' + message[0:200])` with **no time
component and no session boundary** (`Get-EntryKey`, and the `$seen.ContainsKey($key)` test). Every kind of
message the owner had already produced once - **including the FLAG button, whose text is byte-identical on
every press** - is suppressed forever after its first capture. Two `possible_softlock` events and her flag
were dropped this way. There is no `adb logcat` pipe in this chain to die: the bridge is a 30 s
`adb pull` poll, and it polled correctly all day.

## 6. WHAT SHIPPED

- `f8-inbox-lib.ps1`: `Write-F8Heartbeat` / `Get-F8Heartbeat` (mutex-serialised, one section per producer).
- `f8-watch-daemon.ps1`: beats every 30 s; the poll loop is now wrapped in try/catch so a throwing pass
  logs and continues instead of terminating the daemon with no trace.
- `f8-device-bridge.ps1`: beats on EVERY pass including the quiet ones, and NAMES the reason -
  `no-adb` / `no-device` / `no-break-log` / `no-new-signal` / **`all-deduped`** / `pass-failed` / `published`,
  with `lastDeviceUtc` = the newest device entry consumed.
- `f8-check-inbox.ps1`: prints `F8_DAEMON_OK` or `F8_DAEMON_STALE <ageSeconds> producer=... pid=...` past 90 s,
  and `F8_DAEMON_STALE -1 ... reason=no-heartbeat-file` when no producer has ever beaten. Exit-code contract
  (0 = NEW_CAPTURE, 1 = NO_CAPTURE) is unchanged.

## 7. RESIDUAL - STILL OPEN

**The dedupe defect itself is NOT fixed** - it is only made visible (`reason=all-deduped`). Un-suppressing it
here would have dumped 319 captures into the live queue. It needs its own ticket: key the dedupe per play
session, or exempt `flagged` (an owner press is always intentional and always new). **Until then the owner's
FLAG button still does not reach a seat twice.**

**The running producers predate this change** and therefore do not beat yet - `f8-check-inbox.ps1` currently
prints `F8_DAEMON_STALE -1` against the live inbox, correctly. The lane was barred from starting/stopping the
daemon; the CLI must run `f8-watch-stop.ps1` then `f8-watch-start.ps1` for the heartbeat to go live.
