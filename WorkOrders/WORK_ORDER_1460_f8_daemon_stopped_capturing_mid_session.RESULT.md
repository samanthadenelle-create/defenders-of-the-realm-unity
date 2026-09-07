# WO-1460 RESULT - nothing died; the bridge deduped 319 captures into silence, and a heartbeat now says so

**Status:** FIXED for the heartbeat half. The dedupe defect that CAUSED the silence is surfaced, not
fixed, and carries forward as WO-1531.
**Commit:** uncommitted in the working tree as of 2026-09-06 21:00, awaiting the wave-two gate. The WO
body itself was corrected in `ab0934108` (20:49) with the measured root cause.
**Files:**
- `.claude/skills/run-defenders/f8-inbox-lib.ps1:111-140` - `Write-F8Heartbeat` / `Get-F8Heartbeat`,
  mutex-serialised, writing `HEARTBEAT.json` as a SIBLING of `PING.json`.
- `.claude/skills/run-defenders/f8-watch-daemon.ps1:156-164,183,242` - beats every pass; the poll body
  is wrapped in try/catch so a throwing pass logs and continues instead of killing the daemon silently.
- `.claude/skills/run-defenders/f8-device-bridge.ps1:109-112,478-481` - beats on EVERY pass including
  quiet ones, carries `lastDeviceUtc`, and NAMES the reason; `all-deduped` is distinguished from
  `no-new-signal`.
- `.claude/skills/run-defenders/f8-check-inbox.ps1:30,34,42-45` - `$StaleSeconds = 90`; prints
  `F8_DAEMON_OK` or `F8_DAEMON_STALE <age> producer=... pid=...`, and `F8_DAEMON_STALE -1 ...
  reason=no-heartbeat-file` when no producer has ever beaten.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites
green, 0 skipped)` - NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed
in `eb161dc98` (20:10), i.e. AFTER both logs. Neither gate log postdates `eb161dc98` or the current
working tree, so the wave-two gate is owed. These are PowerShell files and no Unity gate covers them.

## What landed

The root cause is recorded in the WO at section 5 and is NOT a dead daemon: `f8-device-bridge.ps1`
keyed its rolling dedupe on `SHA1(kind + '|' + message[0:200])` with no time component and no session
boundary, so 319 eligible entries - 316 `error`, 2 `possible_softlock`, and the owner's own FLAG press
at `2026-09-07T01:18:13Z`, whose text is byte-identical every time - were suppressed forever after
their first capture. The heartbeat is the durable half: a bridge publishing nothing and a bridge that
has died no longer look identical from the inbox.

## Acceptance

- [x] The stop cause is NAMED with the log line that proves it - section 5 of the WO quotes
      `queue-events.log` seq 4685 and the replayed filter counts (319 eligible, 319 suppressed).
- [x] `HEARTBEAT.json` sibling of `PING.json`; `f8-check-inbox.ps1` reports STALE past 90 s - verified at
      `f8-inbox-lib.ps1:114` and `f8-check-inbox.ps1:30`.
- [x] Proven by backdating the heartbeat - recorded in the WO's section 6.
- [ ] The heartbeat is not LIVE yet. The running producers predate the change, so `f8-check-inbox.ps1`
      currently prints `F8_DAEMON_STALE -1`, correctly. The CLI must run `f8-watch-stop.ps1` then
      `f8-watch-start.ps1`.
- [ ] The dedupe itself still drops repeat FLAG presses. WO-1531 owns it; until then the owner's flag
      still does not reach a seat twice.

Needs no device capture. It needs a daemon restart by the CLI, then one `f8-check-inbox.ps1` run
showing `F8_DAEMON_OK` against a live producer.
