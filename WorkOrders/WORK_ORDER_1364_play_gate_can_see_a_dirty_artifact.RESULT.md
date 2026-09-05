# RESULT - WORK ORDER 1364 - Make the Play artifact gate able to SEE a dirty artifact

**Filed:** 2026-09-04 (board agent, from `docs/reference/READY_RCA_LEDGER_2026-09-04.md` + the WO's appended `RCA re-verified 2026-09-04` block)
**WO status:** FIXED - on the Seeker in build 2026.09.05.355872, awaiting owner felt-test. PO closes (CLAUDE.md s13).

## What shipped

- Commits `6979fb961` (gate + regression) and `61d19a23b` "WO-1364: the PS1 artifact scanner kept in lockstep with
  the C# gate" (233-line rewrite of `tools/android/assert-google-play-aab-clean.ps1`), both 2026-09-04, both
  ancestors of HEAD and of `32af7767c` (base of build 2026.09.05.355872).
- `Assets/Editor/Regression/GooglePlayPackagingGate.cs:50-62`: ONE `ForbiddenTokens` array carrying `solana`,
  `jupiter`, `usdc`, `blockchain`, `crypto`, `web3`, bare `skr` (`:54`) and the USDC mint `EPjFWdd5...` (`:62`);
  `:77 ShortTokensRequiringTextContext = {"skr","$skr","usdc","web3"}`; `FalsePositiveAllowlist` with per-entry
  reasons (`:100-115`). The two-tier `UserFacingContentTokens` / `OpaqueExecutableTokens` design the WO cites no
  longer exists; the header `:17-44` records why.
- `tools/android/assert-google-play-aab-clean.ps1:71,73` parse `ForbiddenTokens` / `FalsePositiveAllowlist` out of
  the C# file at run time - the PS1 is no longer a second copy.

## Suites that pin it

- `[play-packaging]` (`GooglePlayPackagingRegression.cs:123-150`) - `mustPolice` includes `crypto, web3, solana,
  jupiter, usdc, blockchain, skr` and both mints, failing with "this is the WO-1364 blind spot returning" if the
  opaque path drops any; `:106-108` REJECTS `$userFacingTokens = @(` / `$opaqueTokens = @(` in the ps1.
  Registered `Assets/Editor/Regression/DataRegression.cs:575`.
- `Builds/regression.log` (2026-09-04 22:44) line 113715: `REGRESSION_OK 377/377 suites`.
- RED proven on a real AAB: `Builds/wo1367-aab.log:37493 PLAY_ARTIFACT_DIRTY` / `:37507 PLAY_ARTIFACT_REJECTED`
  (AAB 472,637,397 bytes, Sep 4 09:17). That RED is the point of the ticket.

## Device build evidence

- This is a gate, not a player-facing change; build 2026.09.05.355872 carries the commits (its base `32af7767c`
  has both as ancestors) but there is nothing on the device to tap.

## Owner felt-test (3-5 taps)

1. None on the device. The proof is the RED above and the green regression run.
2. Optional desk check: open `Builds/wo1367-aab.log` at line 37493 and read the `PLAY_ARTIFACT_DIRTY` line naming
   `canon-strings.json` / `solana`.
3. When WO-1366 + WO-1377 land, the next AAB scan must print `PLAY_ARTIFACT_CLEAN_OK` - that is the box still open.

## Gaps the RCA block names

- The "CLEAN_OK on a purged artifact" box is not met - blocked by WO-1366 and the WO-1377 ruling.
