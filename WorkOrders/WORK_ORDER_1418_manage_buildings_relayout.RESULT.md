# WO-1418 RESULT - Manage - Buildings re-layout: portrait rail + selected card + BUILDING NOW

**Status:** FIXED 2026-09-05 (Codex dev lane, four lanes + reviewer rework; gated by the CLI; device build after the owner's reboot; her felt-test closes)
**Owner rulings:** the two mockups are the target; "reuse the building cards from build" (material, promoted into the kit); "less text".

## What landed (Codex worktrees A-D, integration overlay applied three-way onto `11dfea3c1`)
- **Lane A (Core kit):** `ElarionUiKit.CostRow` gains optional `fontPx` (default 13, existing callers byte-identical);
  new `ElarionUiKitGoldPerimeter.cs` - `ElarionUiKit.GoldPerimeter(Transform)` promoted from the WO-1417 card.
- **Lane B (VM):** `BuildingChoiceVM` (the section-5 field list verbatim) + `BuildBuildingChoices()`; maxed buildings
  included (`Max`); `StateWord` in {Building, Locked, Max, Upgradable}; description via `StructureCardVM.DescriptionFor`
  else the tier Effect; benefit line = next tier Effect (WO-1405); upgrade time from `BuildTimerConfig` when reachable,
  never a literal; `"-> T"` labels gone; `ChannelSummary.Describe()` idle wording (WO-1406); army summary; builder
  upsell priced from `PackDef.UsdReference` (Commerce) only when every slot is busy (WO-1412); store return door via
  `PanelManager.SetReturnDoor` (ruling 8.5 #3).
- **Lane C (View):** `RenderBuildingsDestination` after `FindSummary` (outside the `[rows-not-inline]` scan); rail +
  card at `TroopWorkspacePx`; full-width BUILDING NOW band (deviation from the mockup, drawer fold proven at 533 px);
  `DrawerInBandMode` covers Buildings; `ApplyDrawerPlacement` collapses `BuildingNowPrefix`; the "Need another town
  structure? / Open build" footer moved, dead `else if` deleted; every chip is a tab door through the guarded
  `ActivateLauncherCard(dest, commitLauncherNavigation:false)` (Barracks lock honoured, chip 1's drawer tap retired);
  locked Troops card = `BUILD A BARRACKS` door; Max is the only early return before cost + benefit; lines 590-645
  untouched; capture fixture adds a Max and a Locked building, frame count stays 12; new
  `ManageBuildingsCardRegression` (ten RED-first cases).
- **Lane D (store):** WO-1409 walletless Night Market copy + rail; store CLOSE traces the return door and reaches
  `PanelManager.NotifyClosed` (no `CloseAll`), so the sending Manage tab reopens.
- **Lead edits:** `ManageApprovedLauncherRegression.cs:32/35` re-pointed WITH the WO-1406 ruling (toast literal must
  NOT return; `BUILD A BARRACKS` + `BarracksUnlock.IsUnlocked` required; latch guard now conditional); suite
  registered as `DeNelle.Editor.ManageBuildingsCardRegression`; one namespace qualifier fixed in the VM
  (`BuildMode.StructureCardVM` -> `StructureCardVM`, the first compile red).

## Evidence
- Two read-only lead reviews against the pin list (batch_results_state.md:607 and :691): every section-4 pin, the
  drawer geometry, the untouched launcher block, the capture count, hygiene - PASS; five rework items fixed and
  rechecked at source.
- Gates: see the commit message for the fresh `COMPILE_GATE_OK` / `REGRESSION_OK` / `MANAGE_OPERATIONAL_CAPTURE_OK`
  markers and the opened `ManageBuildings_*.png` frames.

## Polish (BATCH_STATE s8.10, second Codex hand-back, landed on top)
All six measured defects fixed and re-reviewed: chips `<Name> B/S . D queued` + FitSingleLine (no overflow at 1920);
BUILDING NOW paints `<DisplayName> -> L<n>` and `+N more` INSIDE the plate; locked CTA `UNLOCKS AT VILLAGE LEVEL <n>`;
rail seats the selected row flush; description/benefit one line (first clause); NPC faces can no longer land in a
building medallion (tall 2:3 sheets rejected -> concept icon / hammer).
**Art finding, corrected by the lead review:** every file in `Assets/Resources/Portraits/` is 784x1168 on disk; the
"square tier sheets" in the hand-back are square only because their import profile scales to 1024x1024. None of the
six Buildings ladders (arcane-tower, armorer, barracks, forge, lumbermill, farm/Quarry) has an authored STRUCTURE
portrait - the medallions are honest blanks until the art drop. The lane also removed the `Portraits/<slug>-<level>`
tier-sheet route; accepted for now because those sheets belong to Defense ladders that never appear on this tab; the
pin `[building-art-palette-first]` is reworded when the route returns.
Gates: `COMPILE_GATE_OK` (`Builds/c13` 23:51), `REGRESSION_OK 390/390` (`Builds/r14` 23:54),
`MANAGE_OPERATIONAL_CAPTURE_OK 12/12 touch=clean` (`Builds/capman2` 23:49); `ManageBuildings_2670x1200.png` +
`_1920x1080.png` opened. Minor, not blocking: the "+N more" count includes stack children (may overcount); the
Open-build footer row is clipped by CLOSE at 1920 inside its scroll zone.

## Owed / open
- `StoreReturnToManageRegression` + `NightMarketNoWalletRegression` (RED-first) - in-house follow-up.
- The footer hint text ("Recommended next upgrades" in the mockup) ships as the moved "Need another town structure?"
  row - owner may rename.
- Phase 2: unify `Troop*` / `Building*` builders (own WO, after both tabs pass her felt-test).
- WO-1405's Defense half (`grid x,y` -> name) stays on 1405.
