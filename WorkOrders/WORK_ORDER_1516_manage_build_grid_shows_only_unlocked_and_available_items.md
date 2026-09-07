# WO-1516: the Manage BUILD grid shows only structures that are unlocked and available to the player

**Status:** READY TO IMPLEMENT - owner ruling, 2026-09-06 20:07
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
