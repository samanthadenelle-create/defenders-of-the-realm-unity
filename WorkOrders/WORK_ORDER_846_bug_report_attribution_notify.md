# WO-846 — Bug Report Attribution + Review Notify (tester program)

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated, APK 2026.08.27.343878).
**Lane:** 4 (UI/HUD, edit-only) + tooling (`.claude/skills/run-defenders/`, new scripts only)
**Origin:** owner ruling 2026-08-02 (tester program), verbatim:
> "when they submit a bug from settings it calls something to save stack trace to the db and lets us know to review it"
**Builds on:** WO-596 (player bug-report form + `api/bug-report.js` + Neon `bug_reports`) — the
"save stack trace to the db" half largely EXISTS; this WO closes the two gaps: **attribution**
(reports carried no account-bound player id outside Pi sign-in) and **notify** (nothing told the
CLI a report landed).

## Why (gap analysis against the ruling)
1. **Attribution gap.** `BugReportVM.BuildPayload` sent `piUid` (salted hash) ONLY when Pi
   sign-in was active — on the Android/Seeker tester builds (no Pi), `bug_reports.player_id`
   landed NULL. The endpoint already accepts the fallback: `api/bug-report.js:81-82` stores
   `piUid ?? body.playerId → player_id`, and `api/schema.sql:436` reserved the column
   ("NULL — HelpMenu does not send one (yet)"). The save pipe's identity key —
   `GameState.BoundWallet` (wallet address when bound, else the firebase/guest-local key;
   the exact `playerId` every `/api/game/save` sync posts, see `GameStateService.cs:1326`,
   and the same key `EventTracker.cs:168` stamps on analytics) — was never attached.
2. **Notify gap.** Reports landed silently in Neon. The §14 watcher fleet (f8 local +
   websig web) pings `logs/f8-inbox` on captures, but nothing watched `bug_reports` —
   the owner/CLI would only find tester reports by manually opening the db-viewer.
3. **Context** (stack/log): already solved by WO-596 — `BreakCaptureHarness`'s all-platform
   trace-tail ring (last 80 `[Flow:*]`/error/exception lines, 300 ch/line, oldest first)
   rides `traceTail[]`, within the endpoint caps (`api/bug-report.js:39-40`:
   MAX_TAIL_LINES=120, MAX_TAIL_CHARS=500). This WO adds client-side enforcement of those
   exact caps in the (now pure, now tested) payload builder so the bound can never drift.

## Implemented

### 1. Client — attribution (`Assets/_Modules/HUD/BugReportVM.cs`)
- New `PlayerIdKey()`: `Guard.Try`-wrapped READ-only read of
  `GameStateService.Instance?.State?.BoundWallet` — the bound identity SAVE KEY (wallet /
  firebase / `guest-local-<device hash>`). Null (no state / empty) omits the key and NEVER
  blocks the submit. No PII beyond the opaque id the save already ships on every sync.
- Payload now carries `"playerId": <BoundWallet>` per the endpoint contract; `piUid` (Pi
  hash) still rides when present and wins server-side (`piUid ?? playerId`, by design —
  Pi identity stays hashed).
- Payload assembly factored into **pure** `public static BuildPayloadJson(...)` (no
  Unity/service reads) with the endpoint caps enforced client-side:
  `MaxTailLines = 120` (newest kept, oldest truncated first, order preserved) and
  `MaxTailLineChars = 500` — cap mirrors of `api/bug-report.js` MAX_TAIL_LINES/MAX_TAIL_CHARS.
- Submit `FlowTrace.Step` line now logs `player=none|guest|bound` (classification only —
  the full key never rides a log line, since log lines feed future traceTails).

### 2. Client — honest UI (`Assets/_Modules/HUD/BugReportView.cs`)
- Disclosure line (one line, ASCII):
  `"Includes recent game logs and your player id to help us fix it."`
  (WO-596 rule kept: the submit button IS the consent; the line is the honesty.)

### 3. Notify — `.claude/skills/run-defenders/bugreport-watch.ps1` (+ `-start` / `-stop`)
- New daemon polling `api/admin/db.js` (auth mirrors `websig-watch-daemon.ps1`:
  `x-admin-key` header from gitignored `.admin-dash-key`, base URL re-read every poll from
  `Builds\admin-preview-url.txt`, Vercel protection-bypass query param).
- Cursor = `bug_reports.report_id` (BIGINT IDENTITY, `api/schema.sql:432`), persisted in
  `logs/f8-inbox/bugreport-watch.state.json`; first successful poll BASELINES (fires nothing).
- On each new row: writes `capture-bugreport-<stamp>-id<N>.md` + `LATEST_CAPTURE.md` into
  `logs/f8-inbox` (report's player_id, description, route, app_version, platform, session,
  context tail last 40 lines) and bumps the SAME `PING.json` seq contract the f8/websig
  daemons use — `f8-check-inbox.ps1` / `.cursor/rules/f8-auto-triage.mdc` surface bug
  reports with ZERO changes. One inbox, three sources.
- **DEGRADED MODE (works day one):** `api/admin/db.js` currently has NO `bug_reports` view
  (views: overview | players | metrics | traces). Until the one-view addition lands, the
  daemon detects new rows via `view=overview` (bug_reports count + latest are already
  exposed) and pings with a "content pending view=bugreports" capture.

### 4. Regression — `Assets/_Modules/HUD/Tests/BugReportPayloadTest.cs` (+ new `DeNelle.HUD.Tests.asmdef`)
EditMode fixture over the pure builder: contract keys present; `playerId` rides when bound /
key OMITTED when absent; piUid+playerId coexist; tail bounded to newest 120 with order
preserved; per-line 500 clamp; cap mirrors asserted against the endpoint's literals; JSON
escaping; all-null inputs still build (never throws).

## REQUIRED FOLLOW-UP (fenced from this lane — orchestrator)
`api/admin/db.js`: add the `view=bugreports` block (exact code specced in the TODO header of
`bugreport-watch.ps1` — static tagged-template SQL, `clampLimit`, `after_id` ascending cursor,
screenshot returned as a PRESENCE FLAG only, never the b64 blob) + extend the "Unknown view"
hint. Then the daemon's primary path lights up automatically (no script change needed).

## Acceptance criteria
- [x] Submit from Settings sends `playerId` = the exact save key (`BoundWallet`) — lands in
      `bug_reports.player_id` on non-Pi builds (endpoint: `api/bug-report.js:81-82`)
- [x] Context tail bounded to the endpoint caps client-side; failures to gather id/context
      never block the submit (Guard.Try; null => omit)
- [x] Form's disclosure line names logs + player id (one ASCII line)
- [x] New reports ping `logs/f8-inbox` via the existing PING.json contract (degraded until
      `view=bugreports` exists; full content after)
- [x] EditMode regression proves fields + bounds on the pure builder
- [ ] Gates: CompileGate green + `DeNelle.HUD.Tests` green (CLI batch-gate)
- [ ] PO felt-verify: submit a report on the Seeker build, confirm player_id in db-viewer,
      confirm the inbox ping fires

## What was NOT touched (fences honored)
`api/**` (read-only; contract cited), `f8-watch-daemon.ps1` / `websig-watch-daemon.ps1`
(patterns mirrored, files untouched), `CLI_LANES_WO_NUMBERS.md`, `DataRegression.cs`,
GameStateService/LoginPanel/Firebase/Wallet provider bodies (read-only `BoundWallet` access
via the existing public `Instance.State` surface — the same read `EventTracker` already does),
all live-lane files (Village/Harvest, HudKit, RaidDeploy, Shop, Buildings, Tutorial, etc.).
