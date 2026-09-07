# WO-1564 RESULT - the picker's capacity is derived, and the queue drawer speaks words

**Status:** AWAITING OWNER MATCH - device frame vs mockup panel 7 (RESEARCH picker), 9 (QUEUE overlay) not yet passed (2026-09-07); code landed uncommitted in the working tree. The owner walked all nine Manage screens on build 358872 beside docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate. Edit-only lane: no Unity run, gate)*
or commit. Built ON TOP of the uncommitted `ManageScreenVM.cs` edits, none reverted. Every line read
at source this session (CLAUDE.md section 11B).

## PART 1 - the research picker
`ManageScreenVM.ApplyPickerCapacity` (`:4509-4530`), called `:3624` right after `FillActiveTab` so it
reads the schools actually composed; the authored `4 x 1` is left as a documented **seed**.
**The shape, and why - stated so the next seat does not undo it (the WO's own instruction):**
`columns = ceil(sqrt(n))`, `rows = ceil(n / columns)`. **No literal column or row count survives.**
4 -> 2x2, **5 -> 3x2** (3+2), 9 -> 3x3. At most `columns - 1` cells stay empty, so no school is
orphaned ALONE on a ragged row the way the Lumber Mill was. **Two rows rather than one wide row,
deliberately:** the renderer caps a cell at `MaxTileHeightPx = 190`
(`ManageWorkspacePanel.cs:161`, applied `:491`) - **that cap, not the column count, is why ~60% of
the well was black** at `rows = 1`. Two rows double the fill without touching the renderer, which is
**WO-1563's file** this wave. Zero schools keeps the seed and warns (a derived 0x0 makes the renderer
refuse the band, `:494-499`). **Picker only** - the perk TREE keeps its authored 1 column, because
one column is what makes the renderer lay ROWS instead of cards (`:598`). BUILD and ARMY untouched.

## PART 2 - the queue drawer (`ManageScreenVM.MakeJobRow`, `:843-940`)
1. `name + " - Level " + job.TargetTier` (`:881`), matching the detail card's `LEVEL n`.
2. **TWO catalogs, and the second is not optional** (`:883-917`). `BuildingTierCatalog` holds the
   TIER-LADDER buildings only; every TOWER and WALL lives in `CatalogRegistry` (structures-catalog).
   Order: tier catalog -> `CatalogRegistry` (`:899`, asked with the RAW id minus its `@`/`:` suffix -
   `NormalizeBuildingJobId` rewrites `_`->`-`, which no structures-catalog id carries) -> the miss.
   ⛔ Without the middle step every tower upgrade - the commonest Builder job - would have renamed to
   a placeholder and fired a `Fail` on healthy data.
3. **A genuine miss is a TRACED FAILURE** (`:909-916`): `FlowTrace.Fail` naming both catalogs, plus
   an honest `"Unknown structure - Level n"`. The row still paints; nothing is prettified.
4. **TRAIN / RESEARCH rows** never reach that branch, and `FormatJobTarget` still speaks `-> L` for
   troop / barracks / tower upgrades (`ObsidianQueueHud.cs:487`, `:507`, `:520`), so the drawer
   **normalises the notation it receives** (`:929`) - presentation, on this side.
5. **A raw id reaching the player is loud** (`:936`): any label carrying `_` or `:` trips a `Fail`.
⚠ **THE TICKET'S EVIDENCE IS ONE HOP OFF.** WO-1564 attributes the drawer's fallback to
`BuildTimerService.PrettyJobLabel` (`:2328`); at source it falls through
`ObsidianQueueHud.FormatJobTarget` -> `ShortStructureId` -> `SpacedName` (`ObsidianQueueHud.cs:575`,
`:584`), and **`PrettyJobLabel`'s only caller is the HUD chip** (`BuildTimerService.cs:2130`). Its
behaviour is UNCHANGED as instructed, and `ObsidianQueueHud` is untouched -
`ObsidianQueueRegression.cs:247` pins `"Barracks -> L2"` verbatim, and `ManageRowBenefitRegression` /
`ManageDefenseCardRegression` pin `-> L` on **browse row** labels, a different composer.

## ORACLES - RED recipes written in-file
- **`[research-picker-capacity]`** - `ManageProgressiveDisclosureRegression:372-418`, called `:350`,
  inside the existing WO-1516 fixture. Asserts `capacity >= schools`, `capacity - schools < columns`
  (no whole dead row) and `columns <= schools`. **RED:** restore `4 x 1`, drop the call.
- **`[case 13]`** - `ManageTroopsTrainDoorRegression:817`, called `:304`, on the suite's live
  service+state fixture. Enqueues a real `barracks` -> L4 and a bogus `no_such_structure_zz` -> L2 on
  `ChannelId.Builder`; asserts no row carries `->`, `_`, `:` or a title-cased id, that Barracks reads
  `Level 4`, and **that the bogus row is PRESENT and reads the placeholder** - without which the case
  green-passes on the barracks half alone. ⛔ The bogus id is deliberately NOT `tower_ground_archer`
  (a real structures-catalog id). **RED:** restore `" -> L"`, or delete the miss branch.
## REGISTRATION - none. `DataRegression.cs:438` / `:439` already carry both suites.
## OWED
- `COMPILE_GATE_OK` + `REGRESSION_OK n/n` on **fresh** logs, judged by the marker, never the exit code.
- Both oracles **proven RED before green**, both runs recorded (this lane cannot run Unity).
- **Fresh** `ManageFlow_RESEARCH_gridtop` + `ManageFlow_BUILD_queue`, opened (the 18:39 frames
  predate `949e848a0`); then owner felt-verify + close (section 13: the PO closes, not the CLI).
## NOT TOUCHED
`ManageWorkspacePanel.cs` (WO-1563); the drawer's overlap / clipped timer / `X` (WO-1488); the BUILD
grid's 10 tiles and five chips; RUSH / SPEED-UP; the `FillActiveTab` activity-strip conflict (WO-2012).

## 2026-09-06 - SCOPE EXTENSION: the TROOP-UPGRADE arm leaked the id the Part 2 instrument caught
The Part 2 id-grammar instrument fired on its own capture: `Builds/cap-manage-wave3.log:3739` -
*queue row label 'Troop Upgrade:militia - Level 2' carries id grammar for job 'troop-upgrade:militia'
(channel Research)*, raised at `ManageScreenVM.MakeJobRow` (`Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:937`).
The string was MADE upstream, in the View helper that VM calls:
`ObsidianQueueHud.JobTargetLabel`'s TroopUpgrade arm
(`Assets/_Modules/Village/BuildMode/ObsidianQueueHud.cs:480-488`, pre-edit).
`TroopIdFromUpgrade` (`:562`, pre-edit) returned the WHOLE job id whenever it did not start with
`BarracksService.TroopUpgradePrefix` ("barracks-troop-upgrade:"), and `TroopDisplayName` then fell
through to `SpacedName`, which rewrites `-` and `_` but **not** `:` - so `troop-upgrade:militia`
title-cased into `Troop Upgrade:militia`. Same class as the Part 2 defect, one producer over.

**Fixed (edit-only; no Unity run, no git):**
- `Assets/_Modules/Village/BuildMode/ObsidianQueueHud.cs:480-505` - the TroopUpgrade arm now names the
  troop from **`TroopCatalog.Find`** (the authority `ArmyMusterVM.DisplayNameOf`,
  `Assets/_Modules/Village/Troops/ArmyMusterVM.cs:149-152`, and `ManageScreenVM.cs:2984` both use) and
  spells the level in words: `"Archer - Level 3"`. A catalog MISS is a `FlowTrace.Fail("HUD", ...)`
  naming both the troop id and the job id, and paints `"Unknown troop - Level N"` - mirroring
  `ManageScreenVM`'s `"Unknown structure - Level N"`, never a title-cased id.
- `ObsidianQueueHud.cs:~575` - `TroopIdFromUpgrade` is now **prefix-agnostic**: known prefix stripped,
  otherwise the segment after the last `':'` (troop ids carry hyphens, never colons - the same
  invariant `TroopIdFromTrain` already relies on).
- `ObsidianQueueHud.cs:~589` - new `TroopCatalogDisplayNameOrNull`. Deliberately SEPARATE from
  `TroopDisplayName`: the **TRAIN** arm shares that helper and still wants its silent spaced-id
  fallback, so making it loud would have started logging errors under the train pin.
- `Assets/Editor/Regression/ObsidianQueueRegression.cs:249-282` - the pin now asserts the exact words
  `"Archer - Level 3"` (fixture id corrected to the REAL catalog id `troop-archer`; the old `"archer"`
  matched no troops.json row and passed only through the fallback being removed), plus a SECOND case on
  the capture's own grammar `"troop-upgrade:troop-archer"` asserting `"Archer - Level 2"` - which pins
  the prefix-agnostic extraction. New helper `AssertNoIdGrammar` (`:~304`) fails any label containing
  `':'`, `'_'`, `"->"` or a surviving `"troop-"` id prefix.
- `Assets/Editor/UICaptureLaunch.cs:7314-7318` - the capture fixture id `troop-upgrade:militia` was
  **not a real troop** (troops.json runs `troop-footman` .. `troop-echo-legionnaire`; there is no
  `militia`), the identical "the id was not real" defect already annotated one line above for the perk
  id. Now `troop-upgrade:troop-footman`, so the next capture's row reads `"Footman - Level 2"` instead
  of tripping the honest new miss trace.

**Sibling recorded, NOT fixed here:** the TRAIN arm has the same silent `SpacedName` fallback, and both
its fixtures use non-catalog ids (`barracks-train:footman:abc12345` in the regression,
`ObsidianQueueHud.cs:472-478`). It is left alone deliberately - it is a different arm with other
callers, and changing it under the train pin is a separate ticket. Likewise the **barracks / tower**
upgrade rows still speak `" -> L"` in the HUD string (normalised downstream by
`ManageScreenVM.cs:~930`'s `Replace(" -> L", " - Level ")`); out of this extension's scope.

**No miss-path regression case was added.** `FlowTrace.Fail` routes to `Debug.LogError`
(`FlowTrace.cs:171` -> `UnityLogSink:Error`, `:600`); no `logMessageReceived`/`LogType.Error` hook was
found in `DataRegression.cs` or `ObsidianQueueRegression.cs`, but a deliberate error line in a batchmode
gate log is a hazard this lane cannot prove safe without running Unity. Flagged rather than guessed.

**OWED (this lane runs no Unity, no git):** `COMPILE_GATE_OK` + `REGRESSION_OK n/n` on fresh logs, the
new pin proven RED before green (RED recipe: restore `" -> L"` in the TroopUpgrade arm, or revert
`TroopIdFromUpgrade` to the prefix-only form), and a fresh `ManageFlow_BUILD_queue` capture showing the
row as `Footman - Level 2` with no `[Flow:Manage] queue row label ... id grammar` line.
Brace/NUL check: `ObsidianQueueHud.cs` 55/55, `ObsidianQueueRegression.cs` 137/137,
`UICaptureLaunch.cs` 894/894, zero NUL bytes in all three.
