# RESULT - WORK ORDER 1365 - The AAB has no ship chain: no wrapper, no R2 push, no size guard

**Filed:** 2026-09-04 (board agent, from `docs/reference/READY_RCA_LEDGER_2026-09-04.md` + the WO's appended `RCA re-verified 2026-09-04` block + a same-session read of `Builds/aab-status.txt` and `Builds/r2-parity.log`)
**WO status:** FIXED - on the Seeker in build 2026.09.05.355872, awaiting owner felt-test. PO closes (CLAUDE.md s13).

## What shipped

- Commit `da9694c86` (2026-09-04) "WO-1365: the AAB finally has a ship chain..." - ancestor of HEAD and of
  `32af7767c` (base of build 2026.09.05.355872).
- `google-play-aab-build.ps1` (+350, NEW, at the REPO ROOT - not under `tools/`, which is why a `tools/` search
  misses it): `:270` `-ExpectMarker '[AndroidBuild] SUCCEEDED'`; `:316` `& powershell -NoProfile -File (Join-Path
  $root 'tools\r2-ship.ps1')` (the ONE-file call, not re-inlined - CLAUDE.md s16); `:223-233` `AAB_SIZE_FAIL` /
  `AAB_SIZE_OK`; `:243` `exit 6`; `:62` `SizeCeilingBytes = 500000000`; signing preflight `:106-132`.
- `Assets/Editor/AndroidBuild.cs` (+4/-4): `:10` and `:21` now read `6000.4.8f1`. Markers at `:196`
  PLAY_ARTIFACT_REJECTED, `:201` SUCCEEDED, `:206` FAILED, `:272` ANDROID_CATALOG_MISSING.
- `docs/CLI_OPERATIONS_RUNBOOK.md:189-190` (build-table rows), `:196-209` (marker list).
- Canon fix filed with this RESULT: `KEY_FACTS.md:158` said "THE AAB LANE HAS NO SHIP CHAIN" - corrected 2026-09-04
  with a dated line pointing at `google-play-aab-build.ps1` / `da9694c86` (the stale wording kept beneath it, s15).

## Suites / markers that pin it

- No regression suite pins the size guard (`grep -i size GooglePlayPackagingRegression.cs` = 0 hits); it is a PS1
  judged by MARKER on a fresh log (CLAUDE.md s16).
- `Builds/aab-status.txt` (read this session): `AAB_SIZE_TOOLS editor=6000.4.8f1 jar=bundletool-all-1.17.2.jar`,
  `AAB_SIZE_OK 469202267 (30797733 under 500000000)`, `AAB_DONE 2026-09-04T13:12:57`.
- `Builds/r2-parity.log` (mtime 2026-09-04 22:53:24, read this session): `R2_PARITY_TARGET_OK` x3 then
  `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=271`; 0 files under `ServerData/` are newer
  than the proof.

## Device build evidence

- This is a build chain; build 2026.09.05.355872 on the Seeker carries the commit but there is nothing on the
  device that exercises it. The AAB at `Builds/Android/EchoesOfElarion-GooglePlay.aab` is the artefact.

## Owner felt-test (3-5 taps)

1. None on the device. Machine evidence is the two marker files above.
2. Optional desk check: open `Builds/aab-status.txt` and read `AAB_SIZE_OK`.
3. Optional desk check: open the tail of `Builds/r2-parity.log` and read `R2_PARITY_OK targets=Android,...`.

## Gaps the RCA block names

- "AAB with no fresh R2 push FAILS on the marker" is wired at `:316` but NO captured RED run exists.
- "31 MB accounted for" is WO-1367's lane (IN PROGRESS, acceptance unchecked).
- "Was the 09-01 catalog pushed?" is not answered anywhere in the tree - a recorded unknown.
