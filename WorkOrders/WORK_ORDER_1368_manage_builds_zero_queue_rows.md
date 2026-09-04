# WORK ORDER 1368 - Manage/Queues builds ZERO queue rows: no Finish Now, no Watch Ad, on the money path

**Status:** READY TO IMPLEMENT
**Silo / Lane:** Village/UI Manage - `ManageScreenPanel` / `ManageScreenVM` / `Core/Jobs`
**Type:** EXISTING system, REGRESSION (it worked earlier the same morning)
**Minted:** 2026-09-04 (CLI), live from her device while she played
**Severity:** ⛔ **P1, and it is the MONEY PATH.** The crystal sink and the rewarded-ad surface are
both unreachable from the queue.

## THE REPORT

Owner, playing the 2026.09.04.354315 production candidate on her Seeker, looking at
**MANAGE - DEFENSE** with two Builder jobs queued (Tower Ground Archer 5s, Lumberyard -> L2 7s):

> ***"i dont see the watch ad or pay crtystals to complete early stuff"***

Screenshot: `logs/f8-inbox/device/live-20260904-094558.png`.

## ⭐ THE PROVING LINE - and it proves a REGRESSION, not an absence

Same instrument, same device, same session (`ManageScreenPanel.cs:1270`):

```
09-04 07:51:20.028  [Flow:Manage] row bands: PRIMARY x0.760-0.980 ... queueRows=2 browseRows=6
09-04 09:34:26.006  [Flow:Manage] row bands: PRIMARY x0.760-0.980 ... queueRows=0 browseRows=6
09-04 09:35:11.669  [Flow:Manage] row bands: PRIMARY x0.760-0.980 ... queueRows=0 browseRows=6
09-04 09:35:12.388  [Flow:Manage] row bands: PRIMARY x0.760-0.980 ... queueRows=0 browseRows=4
```

**`queueRows=2` at 07:51 and `queueRows=0` at 09:34-09:35 with two jobs actually queued.** The screen
built no queue rows at all.

Meanwhile the RAIL built fine:

```
09-04 09:35:11.661  [Flow:QueueUi] QueueRailView built for Builder (h=200px)
09-04 09:35:11.643  [Flow:Manage] bands(px): canvas=965 panel=869 well=533 || strip=56[body]
                    rail=200[side-drawer] slot=0 tabs=0 notice=96[close-band] gaps=12
                    => fixed=68 LIST=465 (floor 240)
```

Source log: `logs/device/full-buffer-094110.log` (whole buffer, 1,111,915 lines).

## WHY THIS LOOKS LIKE "A MISSING FEATURE" AND IS NOT

⛔ **The cards she can see are NOT the rows that carry the actions.**

- **`QueueRailView`** (`Assets/_Modules/Core/UI/QueueRailView.cs`) is the side-drawer peek rail. It is
  **display-only BY DESIGN** - `CLAUDE.md` §7: *"The right-column Builders chip SURVIVES as a STATUS
  GLANCE ONLY (count/timer + the inline peek rail)."* Grepping it for `FinishNow` / `Rush` / `Ad` /
  `Button` returns nothing but labels and a plate. **It is behaving correctly.**
- **`ManageScreenPanel`'s queue ROWS** are where the verbs live
  (`Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs`):
  - `:1832` `if (r.FinishPrice > 0)` -> `:1846` `BuildTwoLineCta(row, "Finish Now", r.FinishCostText, ...)`
    -> `:1849` `_vm?.FinishNow(channel, jobId)`
  - `:1858` `bool wantAd = r.AdAvailable && FeatureFlags.RewardedAdSkip;` -> `:1872`
    `BuildObsidianButton(row, "Ad", ...)` -> `:1875` `_vm?.WatchAd(channel, jobId)`

**Those rows were never built, so neither verb could render.** The feature is present and wired; the
row list feeding it is empty.

## ⛔ WHAT IS ALREADY RULED OUT - do not re-derive these

1. **It is NOT the ad feature flag.** `FeatureFlags.RewardedAdSkip` is **`defaultOn: true`**
   (`Assets/_Modules/Core/FeatureFlags.cs:829`; a second definition at `:800` returns `true` flat).
   ⚠ **AND THE CODE COMMENT SAYS THE OPPOSITE.** `ManageScreenPanel.cs:1863-1866` reads *"the 'Ad'
   control is NEVER CONSTRUCTED while FeatureFlags.RewardedAdSkip is OFF (the shipping state - no ad
   SDK is wired anywhere in the project)"*. **Both halves of that parenthesis are now false** - the
   flag defaults ON, and LevelPlay/ironSource IS integrated (canon records $0.04 of real ad revenue
   over 14 days). Fix the comment in the same change (§15); a seat trusting it would chase a flag
   that is already on.
2. **It is NOT "Finish Now was never built"** - see the file:line above.
3. **It is NOT the rail misbehaving** - the rail is display-only on purpose.

## THE INVESTIGATION - start here, and instrument before editing (§12)

The question is narrow: **why does `ManageScreenVM` yield zero queue rows while
`ObsidianQueueState` holds two Builder jobs?**

- ⭐ **The `queueRows=2` -> `0` transition inside one morning is the highest-value lead.** Something
  changed between 07:51 and 09:34 - a tab change, a channel filter, a drawer expand
  (`[Flow:Manage] queue drawer expanded` appears at 09:34:29 and 09:35:17), or a state reload.
  **Bisect on that window; do not start from a cold read of the VM.**
- ⚠ **Suspect the AGGREGATE path.** `ManageScreenVM.cs:489` sets `FinishPrice = 0` with the comment
  *"⚠ Q11/Q12: no paid verb on an aggregate either"*, while `:508`/`:530` set the real price
  (`svc.InstantFinishPrice(channel, job.StructureId)`) for per-job rows. **If the screen is emitting
  aggregate rows - or emitting none because it expects an aggregate - that reconciles every symptom.**
  NOT PROVEN; it is the first hypothesis to test, with a capture.
- **Check the tab/channel coupling.** `09:35:12.368 [Flow:Manage] tab -> Defense (line Builder)` -
  the Defense tab maps to the Builder line. Does the row builder filter queue rows by the browse
  tab's structure set and legitimately find none?

## ACCEPTANCE

- [ ] ⛔ **A capture shows `queueRows` matching the real job count** on the same screen state that
      produced `queueRows=0`. Quote before and after.
- [ ] `Finish Now` renders on a queued job with its crystal price on the face, and charges correctly.
- [ ] The `Ad` control renders (the flag is already on) and `WatchAd` completes a job.
- [ ] The root cause is **proven from captured data, not inferred** - §12. No fix before that line
      exists.
- [ ] `ManageScreenPanel.cs:1863-1866`'s stale comment about the ad flag and the ad SDK is corrected
      in the same change.
- [ ] An oracle asserts `queueRows > 0` whenever the queue is non-empty. ⚠ **This regressed silently
      in one morning with every marker green** - the same class as WO-952's never-written
      `COMPRESSED`-absence oracle. Without this, it regresses again and she finds it again.

## WHAT NOT TO TOUCH

- ⛔ Do not add Finish Now / Ad controls to `QueueRailView`. It is a status glance by ruling (§7), and
      a second action surface is the duplicated-state defect this repo keeps paying for.
- ⛔ Do not flip `RewardedAdSkip` - it is already on.
- ⛔ Do not re-tune the crystal price curve (WO-1129's convex Finish-Now curve). The price is not the
      defect; the row is missing entirely.
