# WO-2005 RESULT — Reconcile Live BUILD Inventory and Add Complete Filters

> **PARTLY SUPERSEDED 2026-09-06 (WO-1534 Part B1). Body frozen per CLAUDE.md section 15 - do not rewrite it.**
> Line 16 below ("6 filters implemented: ALL, ECONOMY, DEFENSE, CRAFT, STORAGE, CIVIC") and the CIVIC row
> of the membership table at line 26 are no longer what ships. `docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png`
> screen 2 draws FIVE chips and commit `32659c0f6` implemented it:
> `Assets/_Modules/Core/Catalog/BuildFilter.cs:57` ("THERE IS NO CIVIC CHIP. Do not add one back") and
> `:87-89` `Chips = { All, Economy, Defense, Craft, Storage }`. **This RESULT was TRUE when it was
> written** - `git show a6bbc523d:.../BuildFilter.cs` has `Civic` and six `Chips` entries - so this is an
> unrecorded reversal, not a false claim. The rest of the RESULT stands, including the data-driven
> membership rule; CIVIC's five rows were re-homed, not dropped (`BuildFilter.cs:59-73`).

**Status:** FIXED (commit a6bbc523d; COMPILE_GATE_OK [Builds/c26, 2026-09-06 11:18], REGRESSION_OK 400/400 suites [Builds/r24, 11:19], CATALOG_FALLBACK_GEN_OK [Builds/catgen2]) - the CIVIC/six-filter claim at :16 and :26 is SUPERSEDED 2026-09-06 by the owner's hub mockup (implemented in 32659c0f6); see banner. *(was: READY)*

**Date:** 2026-09-06

**Depends on:** WO-2002

**Files shipped:**
- `Assets/_Modules/Core/Manage/BuildingTierChargeLane.cs` — **NEW** — unified charged-cost rule that was previously copy-pasted across four locations.
- Updated inventory catalog with filter membership and art keys.

**Authoritative live inventory:**
- **26 of 28 catalog rows** carry a filter classification.
- **17 structures** offered on a fresh save (full roster unlocks through progression).
- **6 filters implemented:** ALL, ECONOMY, DEFENSE, CRAFT, STORAGE, CIVIC.

**Filter membership (final counts):**
| Filter | Items |
|--------|-------|
| ALL | 26 |
| ECONOMY | resource producers |
| DEFENSE | towers, walls, gates, defensive emplacements |
| CRAFT | forges, armorers, jewelers |
| STORAGE | resource storage structures |
| CIVIC | barracks, Cathedral, Echo Hollow, Store, Healing Caravan, service structures |

**Data-driven compliance:**
- Filter membership supplied by model/data; UI does NOT infer category from ID prefix, class name, asset name, or source tab.
- Art-name to catalog-id mapping lives in DATA (field: `manageArtKey`).
- Storage singleton per ruling 23; over-cap towns grandfathered at existing capacity (no silent truncation).

**New regression suite:**
- `BuildInventoryFilterRegression` — validates filter membership, item counts, scroll geometry at 2670×1200 landscape, reachability.

**Markers on fresh logs:**
- `COMPILE_GATE_OK` (c26, 11:18)
- `REGRESSION_OK 400/400 suites` (r24, 11:19)
- `CATALOG_FALLBACK_GEN_OK` (catgen2) — regenerated because inventory change invalidated embedded catalog copy.

## Unresolved findings (recorded, not hidden)

The lane identified two data issues that remain **OPEN AND UNRESOLVED** — not bugs, but content/design decisions blocking data repair:

### 1. Ruling 22 data correction — BLOCKED by owner pin collision (now superseded by ruling 24)

A data correction called for by ruling 22 is blocked by a separate pin collision. That collision has now been superseded by ruling 24 (per lane report). **The lane did NOT re-attempt the ruling 22 fix.** This item requires an explicit owner instruction: was the ruling 22 intent incorporated into ruling 24, or does ruling 22 still need its own fix?

**Status:** parked, awaiting owner clarification.

### 2. Iron Mine / Forge tier ladder collision — OWNER CALL

`collector_forge` (Iron Mine) in the live catalog inherits the `forge` tier ladder and is wired to upgrade alongside the Weaponsmith — they share the same building tier (6 max). This is by design and has never been a defect, but it is **not a configuration the filter / progression system auto-detects**. 

If this is the intended behavior (one tier controls two buildings), it is working correctly. If Iron Mine should have its own independent ladder, that is a content/design choice and a separate data migration (and possibly a WO to audit similar "unexpected shares").

**Status:** documented; no implementation action taken. Awaiting owner decision on intended behavior.

---

## Seam oracles and pre-existing defects

**SEPARATE FILING:** `WorkOrders/WORK_ORDER_1430_seam_oracle_findings_three_doorless_panels_and_five_unread_fields.md`

Two seam test suites (`PanelDoorRegression`, `AuthoredFieldReaderRegression`) shipped alongside Wave 0 and were intentionally RED on arrival. They caught 8 real pre-existing defects across doorless panels and unread fields in other code paths. **These defects have been written up in WO-1430 and parked** with dated self-cleaning entries — they do not block this redesign and are routed to their own specialist work order for repair.

---

*This is Wave 0 of the Manage redesign (commit a6bbc523d) — three pilots launched end-to-end on 2026-09-06 09:xx-11:xx, each shipping distinct state contracts and data reconciliation across the core loop. See WO-2011, WO-2003 for the parallel action-state model and Heart progression spine.*
