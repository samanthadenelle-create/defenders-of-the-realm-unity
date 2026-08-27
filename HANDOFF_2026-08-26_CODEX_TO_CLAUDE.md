# Codex -> Claude handoff — 2026-08-26 afternoon

## Read this first

The current game APK is installed on the Seeker as package
`com.denellestudios.echoesofelarion`.

- APK/catalog: `2026.08.26.342478`
- APK source commit: `bcef3be7`
- APK SHA-256: `B7833811509C7CFD3FA3387BDAAA52F7ED2A04115EFF46FD303001AC460DCD38`
- Install result: streamed install `Success`
- Ship-chain proof: `SCHEMA_PARITY_OK`, `APK_OK`, `R2_PARITY_OK 43 object(s)`, `APK_DONE`

Do not claim commits after `bcef3be7` are in that APK. The board validation ledger is keyed to
that exact APK/source pair.

## What Codex completed

Commits, oldest first:

- `bee577cf` — Army Muster/HUD follow-up; fresh `COMPILE_GATE_OK` and `REGRESSION_OK 291/291`.
- `4efbbfde` — backend dungeon-status manifest coverage; focused 4/4 plus deliberate RED.
- `2be9b184` — board reconciliation.
- `bcef3be7` — softlock idle-vs-stuck classifier; fresh `COMPILE_GATE_OK` and
  `REGRESSION_OK 292/292`. This is the APK source.
- `d25e9f7c` — APK-scoped Owner Validation UI in `BOARD.html`/`tools/board_build.py`.
- `b1203138` — closed exactly 20 owner-reported PASS tickets against APK
  `2026.08.26.342478`.
- `3cd28c86` — storage-container Slice A plus reconciliation of WO-1152 and PROD-016.
- `110c968b` — dungeon FLAG acknowledgement correction plus reconciliation of WO-1100,
  WO-1205 and WO-1227.

Two later commits were made by another active seat and are already at HEAD:

- `6dfd07292` — WO-1238 tutorial-stall ticket.
- `130203dae` — WO-1239 barracks footprint regression ticket.

## Owner validation contract

The owner supplied 20 explicit Pass results. All 20 canonical work orders were changed from FIXED
to CLOSED and committed in `b1203138`. The board UI stores Pass/Fail/Needs Work, notes and a separate
Validated flag in browser localStorage. This state never rewrites work-order status automatically.

## Current gate truth

- Fresh compile after the latest code changes: `COMPILE_GATE_OK` in
  `Builds/batch-ready2-compile.log`.
- Full regression is **RED**, not license-blocked. The wrapper mislabeled it `LICENSE_ERROR`
  because benign entitlement messages occur in the log, but Unity licensing initialized and the
  suite completed.
- Actual verdict in `Builds/batch-ready2-regression-retry.log`:
  `REGRESSION_FAIL: 1 failure(s) (291/292 registered suites green, 0 skipped)`.
- Sole failure: `STRUCTURE_CADENCE_FAIL` — Barracks width 7.64 m exceeds the current 2x family
  band of 7.56 m.

RCA already proven: WO-1224 changed the three GenericContainer rows from effective heightMul 1.0
to 0.5. That changed the 27-row family median from 4.32 m to 3.78 m; Barracks itself stayed 7.64 m.
Do not blame Unity Hub and do not loosen the band or lower Barracks heightMul. Continue WO-1239's
ordered investigation: verify fit-time orientation, then decide whether a principled
`repo.maxFootprint` is warranted. A value chosen merely to slip under 7.56 m is threshold gaming.

## Partial work that must remain honest

- WO-1224 remains READY/PARTIAL: Slice A is implemented, RED 3/3 banked, focused JSON green,
  fallback generated, compile green; regression is red on WO-1239 and Slice B needs owner art.
- WO-1236 remains READY/PARTIAL: duplicate FLAG acknowledgement removed and shared toast zone used;
  RED 2/2 plus compile green. The one-face dungeon `calm(explore)` mask is intentionally `0x04`
  (Bag only). Adding Quests/Manage/exit requires the owner product ruling named in the WO.
- WO-1238 and WO-1239 were minted by the other seat after the last board regeneration; inspect them
  before assignment.

## Recommended next sequence

1. Finish WO-1239 RCA and restore `REGRESSION_OK 292/292` without weakening the oracle.
2. Regenerate `CatalogFallbackData.g.cs` if either canonical structures catalog changes.
3. Run fresh compile and full regression with marker assertions. Read the actual
   `REGRESSION_FAIL` block if the wrapper reports a license error.
4. Have a separate reconciliation pass promote only fully proven tickets to FIXED, regenerate the
   board, run `python tools/board_build.py --check`, and commit the batch.
5. Continue READY work in dependency order: live auth/signature retention (WO-1211), dual Stone
   authority (WO-1212), offline reconciliation (WO-1128), capacity-trim honesty (WO-1207), passive
   repair disclosure (WO-1231), then device/UI and gear batches.

Several READY entries are not ordinary implementation tickets: PROD-008 is verification-only;
WO-1199 is a production/rollback exercise; WO-1175 and WO-1195 have owner/art blockers; WO-1129 and
WO-935 are programs that need numbered slices. Do not mark these FIXED merely because one slice ran.

## Late WO-1211 implementation (after initial handoff)

WO-1211 is implemented and intentionally remains READY until Claude completes the full gate and the
owner performs device proof.

- Boot load is cached-only: guest header or an already-usable in-memory wallet session; otherwise
  it keeps the local save and never signs.
- Connect/auto-resume no longer mints a backend session. The first authenticated action mints.
- Save writes use shared `BackendRequestSigner.TryAttachAsync` and refuse/requeue without proof.
- The duplicate live nonce/sign authority in `GameStateService` is retired.
- RED: `Builds/wo1211-red.log` contains `BACKEND_SAVE_AUTH_FAIL` on the pre-fix tree.
- Focused green: `Builds/wo1211-green2.log` contains `BACKEND_SAVE_AUTH_OK`.
- Compile: `Builds/wo1211-compile.log` contains `COMPILE_GATE_OK`.

Claude must run full `DataRegression.RunAll` with `-ExpectMarker REGRESSION_OK`. The known WO-1239
structure-cadence red is unrelated but currently prevents global green. Device acceptance is two
cold launches with zero wallet sheets, town continuity visible, and no boot-window `sign_messages`.

## Working tree ownership

The following tracked modifications predate this handoff work and were deliberately not staged:

- `ProjectSettings/ProjectSettings.asset`
- `docs/WEAPON_ARMOR_ORIENT_LOGIC.md`
- `docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`

There are also many pre-existing untracked Logs/tmp/dev artifacts. Preserve them. The intended
handoff commit contains only this document and the regenerated board.
