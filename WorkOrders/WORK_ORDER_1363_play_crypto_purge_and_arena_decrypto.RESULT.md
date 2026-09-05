# RESULT - WORK ORDER 1363 - Nothing crypto in the Play AAB: compile the literals out, de-crypto Arena

**Filed:** 2026-09-04 (board agent, from `docs/reference/READY_RCA_LEDGER_2026-09-04.md` + the WO's appended `RCA re-verified 2026-09-04` block)
**WO status:** FIXED - on the Seeker in build 2026.09.05.355872, awaiting owner felt-test. PO closes (CLAUDE.md s13).
**Caveat, said loudly:** the "clean AAB" acceptance box is NOT green. See Gaps.

## What shipped

- Commit `6979fb961` (2026-09-04) "WO-1363/1364: the Play crypto purge, the gate that can finally see it, and the
  ceiling neither can cross" - ancestor of HEAD and of `32af7767c` (base of build 2026.09.05.355872). Commit body:
  COMPILE_GATE_OK both variants, REGRESSION_OK 363/363; 24 token-bearing literals down to 2.
- `Assets/_Modules/Core/UI/SkrShowcasePanel.cs:46` `#if !GOOGLE_PLAY` wraps the whole panel; `:277` `#else // GOOGLE_PLAY`
  stub (the WO's `:68` early-return mechanism was recorded as wrong in the commit body).
- `StakeRewardsPanel.cs:57,67` `#if GOOGLE_PLAY` per-channel consts.
- `TitleController.cs:337` `#if !GOOGLE_PLAY` around the badge at `:356` (guards now at `:207` and `:337`).
- Part 2 replaced by a rule: `Assets/Editor/GooglePlayContentExclusion.cs:406 PLAY_NEUTRAL_UNMAPPED_TOKEN`; mirror
  pairs include `siege-stakes.json:161`, `ad-placements.json:162`; `storeBalance*` mapped `:198-204`,
  `heroSelect.subtitle` `:210`, `_storePiSkinNote` `:170`.
- Part 3 (Arena wager denomination) split out to WO-1366 (READY); the identifier ceiling is WO-1377 (BLOCKED on ruling).

## Suites that pin it

- `[play-packaging]` (`GooglePlayPackagingRegression`) - registered `Assets/Editor/Regression/DataRegression.cs:575`.
- `Builds/regression.log` (2026-09-04 22:44) line 113715: `REGRESSION_OK 377/377 suites`.
- Real-artifact proof the gate now SEES contamination: `Builds/wo1367-aab.log:37493 PLAY_ARTIFACT_DIRTY:
  content:base/assets/Data/Canonical/canon-strings.json token:solana`, `:37507 PLAY_ARTIFACT_REJECTED` (Sep 4 09:20).

## Device build evidence

- Build 2026.09.05.355872 (Seeker, installed 22:22) is the Seeker/dApp variant - it carries the guarded code, not
  the `GOOGLE_PLAY` define. The Play-variant artifact is the AAB at `Builds/Android/EchoesOfElarion-GooglePlay.aab`
  (`Builds/aab-status.txt`: `AAB_ON_DISK 472637397 bytes`), which the gate currently REJECTS (above).

## Owner felt-test (3-5 taps)

1. On the Seeker build, open Title - the SKR badge and store copy should read as they did (this variant keeps them).
2. Open the Store and the Stake/Rewards panel - nothing should have regressed on the Seeker variant.
3. No Play-variant device exists yet to tap; the Play check is the artifact scan, not a felt-test.

## Gaps the RCA block names

- `canon-strings.json:184,231` still carry "Solana" in `_nightMarketNote` / `_storePiSkinNote` - the artifact scan
  goes RED on it, so the "clean AAB" box stays open (that file is also modified-uncommitted in the working tree).
- The artifact-clean proof is blocked by WO-1366 (Arena SKR literals -> Crystals on Play, already ruled) and the
  WO-1377 owner ruling (rename vs save serialisation of the enum identifiers).
