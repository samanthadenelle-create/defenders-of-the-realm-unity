# RESULT - WORK ORDER 1368 - Manage/Queues builds ZERO queue rows: no Finish Now, no Watch Ad, on the money path

**Filed:** 2026-09-04 (board agent, from `docs/reference/READY_RCA_LEDGER_2026-09-04.md` + the WO's appended `RCA re-verified 2026-09-04` block)
**WO status:** FIXED - on the Seeker in build 2026.09.05.355872, awaiting owner felt-test. PO closes (CLAUDE.md s13).

## What shipped

- Commit `f6540db88` (2026-09-04 12:47) "P0 wave: freeze fixed, queue verbs restored..." - ancestor of HEAD and of
  `32af7767c` (base of build 2026.09.05.355872). Body names WO-1368 ("verbs now build inside the drawer").
- `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs:1236 private void RenderQueueDrawer()`; `:1259-1262`
  `for (...) AddQueueRow(_vm.QueueRows[i]);` - `AddQueueRow` (now defined at `:1878`) finally HAS a caller.
  Verbs: `:1992 "Finish Now"` -> `:1996 _vm?.FinishNow(channel, jobId)`; `:2005 wantAd`; `:2027
  BuildObsidianButton(row, "Ad", ...)` -> `_vm?.WatchAd`.
- Stale ad comment corrected in-file at `:2015-2023` ("CORRECTED 2026-09-04 (WO-1368 s15)... BOTH HALVES ARE FALSE").
- New FlowTrace `:1286-1294` "queue drawer BUILT {0} row(s)... FinishNow={1} Ad={2} Cancel={3}" plus a Warn when
  no verb renders.
- `FeatureFlags.cs:800` / `:829` `RewardedAdSkip => Get("rewardedadskip", defaultOn: true)` - as cited.
- Path correction for the record: `BuildTimerService` lives at
  `Assets/_Modules/Village/Buildings/BuildTimerService.cs:1075`, not `Core/Jobs` as the WO cites.

## Suites that pin it

- `[manage-queue-drawer]` (`Assets/Editor/Regression/ManageQueueDrawerRegression.cs`) - RE-POINTED 2026-09-04: `:6`
  "THIS SUITE ENFORCED THE DEFECT"; `:96` ban scoped to RenderList; `:106-121 [rows-have-a-home]` requires
  `AddQueueRow(_vm.QueueRows[i])` inside `RenderQueueDrawer`. Registered `DataRegression.cs:1011`.
- `Builds/regression.log` (2026-09-04 22:44) line 113715: `REGRESSION_OK 377/377 suites`.

## Device build evidence

- Build 2026.09.05.355872 installed on the Seeker 22:22; its base `32af7767c` has `f6540db88` as an ancestor.
- Build `2026.09.04.354315` was the build that LACKED the rows. No device log under `logs/device/` post-dates the
  fix (all are the 09-04 morning pre-fix pull).

## Owner felt-test (3-5 taps)

1. On build 355872+, start any build or train job so the Obsidian queue has at least one row.
2. Tap the bar's Manage/Queues face (the re-pointed `Upgrade` face) and open the queue drawer.
3. Confirm the row renders with a **Finish Now** button - tap it and confirm crystals are charged and the job completes.
4. Start another job and confirm a **Watch Ad** button renders on the row (rewarded-ad skip, flag defaults on).
5. F8 if the drawer is empty; logcat should carry `queue drawer BUILT N row(s) ... FinishNow=... Ad=...`.

## Gaps the RCA block names

- Open acceptance is the device felt-verify ("Finish Now renders and charges", "Ad renders") on a build newer than
  `2026.09.04.354315` - no post-fix device capture exists yet.
