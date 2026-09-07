# WO-1457 RESULT - schema_version cannot regress; downgrade and absent are named refusals

**Status:** FIXED. All four acceptance criteria met locally. Not yet deployed - the vercel --prod push is
held behind WO-1446.
**Commit:** `321b753c4` (2026-09-06 20:12).
**Files:**
- `api/game/save.js:411` - `schema_version = GREATEST(player_data.schema_version, EXCLUDED.schema_version)`
  on the upsert. Note the table is `player_data`, not the `game_saves` the ticket named.
- `:106-107` - the two new named codes, `SCHEMA_VERSION_MISSING` ("absent or unparseable - a malformed
  payload") and `SCHEMA_VERSION_DOWNGRADE` ("older than what is stored - a stale client"). The default-to-10
  is gone; the retired behaviour is recorded at `:91`.
- `:122-134` - `judgeSchemaVersion(incoming, stored)`, returning the missing code at `:130` and the downgrade
  code at `:134`.
- `:327-331` - the prior row's `schema_version` is read before the write so the judgement has something to
  compare against; `:350` calls the judge; `:353` logs `save_schema_version_refused` so a stale client is
  visible rather than silent.
- `test/game.save.schema-version.test.js` - the four cases.

**Gates:** `COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes).
`Builds/reg-quiet.log` (20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green,
0 skipped)`, NOT `REGRESSION_OK`. The two reds were a UI-MVVM conformance violation on
`Assets/_Modules/Village/BuildMode/BuildPreviewModal.cs:252-253` and a hollow-pass marker at
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs:761`; both were fixed at source and committed in
`eb161dc98` (20:10), i.e. AFTER both logs. Neither log postdates `eb161dc98` or the working tree - the
wave-two gate is owed. This ticket is api-only, so the node rail below is the one that bears on it.

## What landed

The server no longer trusts the client's version field in either direction: the stored value can only ever
move up, an explicit downgrade is refused with its own code and an audit row, and an absent version is a
malformed payload rather than a v10 payload. The fix is server-side only, as the ticket required.

## Acceptance

- [x] `GREATEST()` on the upsert; explicit downgrade refused with a named code - `save.js:411`, `:107`, `:134`.
- [x] Four cases: downgrade refused, equal accepted, upgrade accepted, absent refused -
      `node --test test/game.save.schema-version.test.js` run 2026-09-06 21:00: tests 7, pass 7, fail 0.
- [x] `node --test` green across `test/` - `npm test` run 2026-09-06 21:00: tests 424, pass 424, fail 0.

**Still owed:** no device capture. What is owed is the `vercel --prod` deploy, then one live save from the
v38 client returning 200 and one replayed old-version save returning the `SCHEMA_VERSION_DOWNGRADE` code.
