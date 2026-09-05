# RESULT - PROD-021 - The R2 catalog for the shipped build was never pushed (occurrence FOUR)

**Filed:** 2026-09-04 (board agent, from `docs/reference/READY_RCA_LEDGER_2026-09-04.md` + the WO's appended `RCA re-verified 2026-09-04` block + a same-session read of `Builds/r2-parity.log`)
**WO status:** FIXED - on the Seeker in build 2026.09.05.355872, awaiting owner felt-test. PO closes (CLAUDE.md s13).

## What shipped

- Gate defect (THE WORK item 2) fixed at source in `486cd7b17` (2026-09-01, the last commit on `tools/r2-ship.ps1`),
  an ancestor of HEAD and of `32af7767c` (base of build 2026.09.05.355872): `:177 foreach ($t in $targets)`,
  `:182 --verify-catalog "ServerData/$name"` per target, `:161-162` `R2_PARITY_FAIL no target under ... nothing to
  verify is a FAILURE, not a pass`, `:223` `R2_PARITY_FAIL targets=... aggregate marker withheld`.
- The s16 freshness invariant holds: `git config core.hooksPath` = `.githooks`; the pre-push hook refuses a push
  whenever anything under `ServerData/` postdates `Builds/r2-parity.log`.

## Markers that pin it (read this session)

- `Builds/r2-parity.log` (mtime 2026-09-04 22:53:24): `R2_PARITY_TARGET_OK 92 object(s) verified`,
  `R2_PARITY_TARGET_OK 87 object(s) verified`, `R2_PARITY_TARGET_OK 92 object(s) verified`, then
  `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=271`.
- Files under `ServerData/` newer than that proof: **0** (PowerShell `LastWriteTime -gt` scan). The newest catalogs
  are `catalog_2026.09.05.355872.{bin,hash}`, written BEFORE the proof - so the build on the Seeker is covered.
- The 09-02 WebGL freshness gap the WO recorded is no longer live.

## Device build evidence

- Build 2026.09.05.355872 (Seeker, installed 22:22) is the build whose catalog the parity log verifies.
- This ticket was minted on the WINDOWS exe (the 93 F8 captures, seq 4081-4224); no post-fix exe log exists.

## Owner felt-test (3-5 taps)

1. Launch the Windows exe built from this tree (or the Seeker build 355872).
2. Title screen: real art, no placeholder capsules.
3. Enter the town: buildings render with their Synty art, enemies on the first wave are NOT tinted capsules.
4. Open the Player.log / logcat and confirm zero `VisualFactory model not found` and zero `StructureArtPending`
   lines, and no catalog `404`.
5. F8 if any structure or enemy is a placeholder.

## Gaps the RCA block names

- Falsification (acceptance line 3): commit `33ba9c966` claims "falsification test proves gate fails when any
  target missing", but NO artefact exists in the tree - `grep -l R2_PARITY_FAIL Builds/*.log` = none. Claimed in a
  commit message, not proven. The CLI still owes a captured run (rename one target's catalog in a scratch run,
  show `R2_PARITY_OK` withheld).
- Acceptance lines 4-5 (fresh exe run + owner felt-verify) are the open items above.
