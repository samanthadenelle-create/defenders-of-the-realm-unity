# RESULT - WORK ORDER 952 - EndState panel compresses its body below content size (REOPENED 2026-09-04)

**Filed:** 2026-09-04 (board agent, from `docs/reference/READY_RCA_LEDGER_2026-09-04.md` + the WO's appended `RCA re-verified 2026-09-04` block + a same-session read of `Builds/`)
**WO status:** FIXED - on the Seeker in build 2026.09.05.355872, awaiting owner felt-test. PO closes (CLAUDE.md s13).

## What shipped

- Commit `f6540db88` (2026-09-04 12:47), an ancestor of HEAD and of `32af7767c` (the base of build 2026.09.05.355872).
- `Assets/_Modules/Village/UI/EndState/EndStateView.cs` (+390): the 3-column spoils lever + the narrative strip.
  `EndStateView.cs:1035` `return RequiredBodyPxAt(vm, canvasH, cols, NarrativeStripAt(vm, canvasH, cols));`
  The 2026-08-10 originals (`MaxPanelHalf`, the owned compact solve) remain at `EndStateView.cs:340-446`, `:631`, `:701`.
- `Assets/Editor/Regression/EndStateBodyFitRegression.cs` (new, 295 lines): header cites this WO, F8 seq 4680, the
  578/540 case; RED recipe = `MaxSpoilColumns = 2`.

## Suites that pin it

- `[endstate-body-fit]` - registered `Assets/Editor/Regression/DataRegression.cs:679`.
- `Builds/regression.log` (mtime 2026-09-04 22:44, 11,091,578 bytes) line 113715: `REGRESSION_OK 377/377 suites -- 377 green, 0 red, 0 skipped`.

## Device build evidence

- Build 2026.09.05.355872 installed on the owner's Seeker 2026-09-04 22:22 (versionCode 355872); its base `32af7767c`
  has `f6540db88` as an ancestor (validated by the CLI tonight).
- No post-fix device capture of an arena EndState exists yet.

## Owner felt-test (3-5 taps)

1. Play an arena round to a win on build 355872+ where a GEAR DROP lands (5-row spoils).
2. On the victory panel, read crest / stars / time side by side in the narrative strip - one row, nothing wrapped.
3. Confirm every band (header, spoils, buttons) is at its own content size - nothing looks squashed (the pre-fix
   trace was `body rows COMPRESSED to fit: need=578px well=540px scale=0.933`).
4. Rotate through the surfaces you play on (2340x1080 phone; 1920x1080 if you use the exe).
5. F8 if any band is clipped or the strip wraps.

## Gaps the RCA block names

- `Builds/ui-capture/` holds only `EndStateWaveClear_{plain,repairAll}_{1920x1080,2340x1080,2670x1200}.png` -
  there is NO arena-with-gear EndState capture. The UI-capture case list still owes an arena 5-row gear-drop
  case at 2670x1200.
- The owner's felt call on the side-by-side narrative strip has not been recorded.
