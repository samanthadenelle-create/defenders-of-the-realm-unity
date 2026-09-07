# WORK ORDER 1368 - Manage/Queues builds ZERO queue rows: no Finish Now, no Watch Ad, on the money path

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:06:00, build 2026.09.07.359076). PRIOR STATUS: FIXED - implemented in f6540db88 (2026-09-04 12:47), on the Seeker in build 2026.09.05.355872; RCA re-verified 2026-09-04 (see the appended block). Awaiting owner felt-test: open Manage/Queues with a job running on the device and confirm the queue rows render with Finish Now (charges crystals) and Watch Ad. PRIOR STATUS: READY TO IMPLEMENT - ⛔ **THE ORIGINAL DIAGNOSIS BELOW IS REFUTED. READ §0 FIRST.**
**Silo / Lane:** Village/UI Manage - `ManageScreenPanel` / `ManageScreenVM` / `Core/Jobs`
**Type:** EXISTING system, REGRESSION (it worked earlier the same morning)
**Minted:** 2026-09-04 (CLI), live from her device while she played
**Severity:** ⛔ **P1, and it is the MONEY PATH.** The crystal sink and the rewarded-ad surface are
both unreachable from the queue.

## §0. ⛔ THE LEAD'S "REGRESSION" NARRATIVE WAS WRONG. THE REAL CAUSE IS ONE MISSING CALL.

**I minted this ticket claiming `queueRows` went 2 -> 0 within one session and told the RCA to bisect
that window. That was a false trail and it would have cost a session.** Two facts kill it:

1. **07:51 and 09:34 are DIFFERENT PROCESSES** - `I/Unity (22805)` vs `I/Unity (28972)`, with
   `[Flow:Manage] ManageScreenPanel installed` at 09:30:53 under the new pid. The app had restarted.
2. **Nothing was queued at 09:34/09:35.** No `notice: Upgrade queued.` between 09:30:53 and 09:35:17.
   **`queueRows=0` was CORRECT.**

And when she DID queue the two jobs at **09:45:05 / 09:45:07**, `queueRows` read **2**:

```
09:45:05.636 queueRows=1 browseRows=4   <- "notice: Upgrade queued." tower_ground_archer
09:45:07.715 queueRows=2 browseRows=4   <- "notice: Upgrade queued." lumberyard
09:45:33.220 queueRows=2 browseRows=4   <- the state in her screenshot
09:46:05.021 queueRows=0                <- both jobs completed
```

⭐ **`queueRows` tracked the real job count perfectly all morning. It is a WORKING INSTRUMENT, not the
defect** - which is exactly why the acceptance criterion I first wrote ("an oracle asserts
`queueRows > 0` whenever the queue is non-empty") **would have passed all morning and proven nothing.**

⚠ **The transferable lesson:** I compared two log lines without checking they came from the same
PROCESS. A pid changed between them. *Two numbers from one file are not two numbers from one run.*

### ⭐ THE ACTUAL ROOT CAUSE - `AddQueueRow` HAS ZERO CALLERS

`ManageScreenPanel.cs:1737` defines `private void AddQueueRow(QueueRowVM r)` - the method that builds
`Finish Now` (`:1832`), `Ad` (`:1872`), `Cancel` and `Move up`. A repo-wide search finds exactly two
hits: **the definition**, and a **string literal in a regression that FAILS if the call is restored**.

`_vm.QueueRows` is read at exactly ONE place in the panel - `:1274`, inside a FlowTrace format string.
**The VM computes the rows; the View logs their count and renders none of them.** The verbs have no
build site in any tab, any channel, at any queue depth.

The removal is documented in-file (`:1250-1255`): queue actions were moved to *"the explicit header
Queue drawer"*. But `BuildQueueDrawer` (`:911-943`) contains only the display-only rail
(`Drawer_QueueRail` -> `QueueRailView.Build`) and the Buy-Builder offer. **`MountRail`'s own comment
states the contradiction** (`:1185-1186`): *"The rail is DECORATION here: its cards are raycast-off
... Every action lives on the rows."* **The rows it defers to were deleted in the same change.**

⛔ **Queue actions were moved to a surface that never rendered them.**

`ToggleQueueDrawer` (`:950`) completes the picture: opening QUEUE also **hides the entire list band**
(`_operationalListBand.SetActive(!_queueDrawerOpen)`), which is why her screenshot shows only two
raycast-off cards and no browse list.

### ⛔ AND AN ORACLE ENFORCES THE ABSENCE - reconcile it IN THE SAME CHANGE

`Assets/Editor/Regression/ManageQueueDrawerRegression.cs:27-29` fails the build if
`AddQueueRow(_vm.QueueRows` is restored as written:

```csharp
if (panel.Contains("AddSectionHeader(\"IN QUEUE - \"") ||
    panel.Contains("AddQueueRow(_vm.QueueRows"))
    failures.Add("queue jobs are duplicated inline beneath the primary upgrade catalogue");
```

Suite header: *"F8 2026-08-31: tower browsing leads; queue administration is opt-in."* **That is a real
owner-felt ruling** - inline queue rows made the browse list overflow at landscape height. ⛔ **Do not
simply delete the suite.** The verbs must return somewhere that does not re-create the overflow -
most likely INSIDE the drawer, next to the rail, which is where `MountRail` already says they live.
⚠ **That is a UI design question and it is the owner's.**

### Hypotheses from the original ticket, all settled

| # | Hypothesis | Verdict |
|---|---|---|
| 1 | Bisect 07:51 -> 09:34 | **REFUTED** - different process, queue genuinely empty |
| 3 | `ManageScreenVM:489` aggregate `FinishPrice = 0` | **REFUTED** - `StackKeyOf` (`:534-540`) keys on `job.StructureId`, so those two never stack; `run <= 1` -> real price at `:508`. Moot anyway: no row renders |
| 4 | Coupled to the browse tab | Coupled, but not causal - Defense->Builder is correct and produced 2 rows |

⚠ **A SECOND, INDEPENDENT GAP that will still bite after the rows return:**
`BuildTimerService.CanWatchAdToSkip` (`:1068-1100`) also requires
`AdGateService.IsOffered(BuildSkipPlacementId)` **and** `RewardedAdManager.Instance != null &&
mgr.IsAdReady`. **NOT PROVEN** whether either held on her device. So restoring the rows may bring back
`Finish Now` without `Ad`.

⚠ The stale `:1863-1866` comment about the ad flag is still worth fixing - but note it sits **inside
dead code**.

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

---
## RCA re-verified 2026-09-04 (QA read-only pass)
**Verdict:** SUPERSEDED
**Evidence:**
- Commit `f6540db88 2026-09-04 "P0 wave: freeze fixed, queue verbs restored..."` is an ancestor of HEAD; its body names WO-1368 ("verbs now build inside the drawer").
- `Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs:1236 private void RenderQueueDrawer()`, `:1259-1262` `for (...) AddQueueRow(_vm.QueueRows[i]);` - `AddQueueRow` now HAS a caller (its definition moved `:1737` -> `:1878`). Verbs: `:1992 "Finish Now"` -> `:1996 _vm?.FinishNow(channel, jobId)`; `:2005 wantAd`; `:2027 BuildObsidianButton(row, "Ad", ...)` -> `_vm?.WatchAd`.
- Stale ad comment corrected in-file at `:2015-2023` ("CORRECTED 2026-09-04 (WO-1368 s15)... BOTH HALVES ARE FALSE").
- New FlowTrace `:1286-1294` "queue drawer BUILT {0} row(s)... FinishNow={1} Ad={2} Cancel={3}" plus a Warn when no verb renders.
- Oracle re-pointed: `Assets/Editor/Regression/ManageQueueDrawerRegression.cs:6` "RE-POINTED 2026-09-04 (WO-1368). THIS SUITE ENFORCED THE DEFECT"; `:96` ban now scoped to RenderList; `:106-121 [rows-have-a-home]` requires `AddQueueRow(_vm.QueueRows[i])` inside RenderQueueDrawer. Registered `DataRegression.cs:1011`.
- `FeatureFlags.cs:800` / `:829` `RewardedAdSkip => Get("rewardedadskip", defaultOn: true)` - as cited.
- Path correction: `BuildTimerService` lives at `Assets/_Modules/Village/Buildings/BuildTimerService.cs:1075`, not `Core/Jobs` as the WO cites.
- ManageScreenPanel was touched again by `1ef5f6ad4` after the fix; the caller is still present at `:1262`.
**What changed since the RCA:** the fix landed in `f6540db88`; line numbers shifted ~+140. This WO's `**Status:**` line was never flipped (`git log -1 -- <WO>` = `d3409a15e`, before the fix), so the derived board still shows it READY.
**Ready for a lane?** no - implemented and oracle-pinned; open acceptance is device felt-verify ("Finish Now renders and charges", "Ad renders"). No capture under `logs/device/` post-dates the fix - every log there is the 09-04 morning pre-fix pull. Files a lane would touch: this WO (Status line only).
**Pins/rulings needed:** owner felt-verify on a build newer than `2026.09.04.354315` (the build that lacked the rows).
