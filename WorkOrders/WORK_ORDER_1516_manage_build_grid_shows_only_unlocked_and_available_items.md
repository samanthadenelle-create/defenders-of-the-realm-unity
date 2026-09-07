# WO-1516: the Manage BUILD grid shows only structures that are unlocked and available to the player

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:15:00, build 2026.09.07.359076) - "owner in chat 2026-09-07 09:1x, verbatim: 'the 15 verify are the new screen UI work correct? THose I verified' - the board panel listed Fixed rows only, so t...". PRIOR STATUS: AWAITING OWNER MATCH - device frame vs mockup panel 2 (BUILDINGS grid) not yet passed (2026-09-07); code landed uncommitted in the working tree. The owner walked all nine Manage screens on build 358872 beside docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate)*
**Silo:** Manage 2000-block - `ManageScreenVM` build inventory + `ManageWorkspacePanel` grid (WO-2006 / 2007).
**LANDS AFTER** the WO-1405 lane commits - `ManageScreenVM.cs` is being gated tonight.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1516 -> 1517 in the same edit).

## 1. EVIDENCE

Owner ruling, verbatim:

> "manage build scren should only show items that are unlocked and avaliable to them"

Today the grid shows everything:

```
reg-final2.log                                   build tile ids=24
ManageFlow_BUILD_gridtop_2670x1200.png (18:39)   includes LOCKED tiles with a "LEVEL 1 . T5"-style
                                                 disabled face
ManageFlow_BUILD_locked                          a PLANNED capture frame
```

Owner device frame `Logs/device/screens/owner-screen-20260906-200741.png` (build 358574, 20:07, Manage /
Build, DEFENSE tab) - **she took this frame immediately after the ruling**. Eight tiles:

```
Archer Tower (art)   Ballista (blank)   Sky Ballista / Anti-Air (blank)   Catapult (blank)
Wooden Palisade (blank)   Healing Caravan (blank)   Arcane Spire (art)   Barracks (art)
```

Every tile carries the SAME green up-arrow badge, and no locked/unlocked distinction is visible at all - so
the screen currently conveys neither state.

The ruling removes the locked state from this screen entirely.

## 2. FIX SHAPE

- The VM's build inventory FILTERS on the same unlock authority the Build palette already uses
  (`RequiresVillageTier` - the gate lives in `BuildingTierCatalog` / `BuildingUpgradeService.cs:54`; cite the
  exact palette predicate you find). **ONE authority, never a second predicate.**
- The `locked` frame leaves the capture plan (`BuildManageFlowPlan`).
- The "UNLOCKS AT VILLAGE LEVEL N" copy from WO-1418 section 8.10 item 3 moves to the Build palette's own
  locked presentation if it still has one; otherwise it is deleted.

## 3. WHAT NOT TO DO
- **Do not hide items that are unlocked but UNAFFORDABLE.** Affordability gets WORDS (WO-1411), never a filter
  - a player must be able to see what they are saving for.

## 4. ACCEPTANCE
- [ ] `ManageProgressiveDisclosureRegression` case: no tile in the BUILD grid carries a locked state, and the
      tile count equals the unlocked count from the authority.
- [ ] The green up-arrow badge on every tile means nothing to the player today: it either states a REAL
      affordance (upgrade available / can build) sourced from the VM, or it is removed.
- [ ] `BuildManageFlowPlan` no longer plans the `locked` frame; zero `CAPTURE_LEDGER_MISSING`.
- [ ] Fresh `ManageFlow_BUILD_gridtop` PNG opened in the RESULT.
- [ ] `REGRESSION_OK n/n` on a fresh log.

## 5. LANE HAND-BACK (edit-only lane, 2026-09-06) - what landed, and the ONE spec line owed

**Landed in `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs`:**
- `InventoryTiles()` now calls `BuildInventoryModel.Tiles(_activeFilter)` instead of
  `ManageTiles(...)`. `Tiles` is the accessor whose own doc comment records that it matches the
  BUILD palette (`BuildAvailability.Offered`), so the grid and the palette answer "is this unlocked"
  from ONE authority. `ManageTiles` is left standing and untouched - the ARMY grid's locked-troop
  treatment (mockup panel 4) is unaffected by this ruling.
- New `ProjectAffordanceTile(...)` wraps `ManageVmProjection.ProjectTile` and WITHHOLDS the status
  medallion whenever the tile's visual state is the `Available` CATCH-ALL and its primary action is
  not `Available`. The four distinct glyphs (locked / in-progress / queue / max) are untouched, so
  only the badge that was lying stops being painted. Used by BUILD, ARMY and RESEARCH-PERK tiles;
  deliberately NOT by the research SCHOOL tiles (they carry no `ManageAction` at all by design).

**Measured case:** `ManageProgressiveDisclosureRegression.CheckBuildGridIsUnlockedOnly`
(`[build-grid-is-unlocked-only]`) - stands up a GameState fixture, composes the BUILD grid, and
asserts (a) the tile count equals `BuildInventoryModel.Tiles(chip).Count`, (b) no tile renders
`ManageTileVisualState.Locked`, (c) a tile reading `SHORT`/`HEART GATED` carries no `StateIconKey`.

### ⛔ SPEC LINE OWED - `BuildManageFlowPlan` (NOT edited by this lane)

`Assets/Editor/UICaptureLaunch.cs` is **another lane's uncommitted work** (`git status` shows it
modified at the time of this hand-back), so it was deliberately not touched. It needs a THREE-LINE
change before the next `RunManageFlowMapCaptureHeadless`, or that run will `MANAGE_FLOW_MAP_FAIL`:
the BUILD grid can no longer produce a locked tile to photograph.

> In `BuildManageFlowPlan()` (`UICaptureLaunch.cs:7667`), skip the ONE combination
> `(ManageTabId.Build, ManageFlowFrame.LockedDetail)` when the plan is expanded - e.g. inside the
> `for (t) for (f)` loop, `if (tabs[t] == ManageTabId.Build && frames[f] == ManageFlowFrame.LockedDetail) continue;`
> with a comment citing WO-1516 and the owner's 20:07 ruling.
> **ARMY and RESEARCH keep their `LockedDetail` frame** - locked troops (mockup panel 4) and locked
> perks (WO-1518) both still exist and are still worth photographing. `Expected` is derived from
> `plan.Length`, so nothing else has to change and no count is hand-kept.
