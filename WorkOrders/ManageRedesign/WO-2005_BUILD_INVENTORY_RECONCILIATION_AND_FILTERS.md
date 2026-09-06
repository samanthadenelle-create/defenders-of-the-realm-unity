# WO-2005 — Reconcile Live BUILD Inventory and Add Complete Filters

**Status:** FIXED (commit a6bbc523d; COMPILE_GATE_OK, REGRESSION_OK 400/400 suites, CATALOG_FALLBACK_GEN_OK) *(was: READY)*

**Priority:** P0  
**Depends on:** WO-2002

## Objective

Merge the current Buildings + Defense management surfaces into BUILD based on their shared Builder queue, then classify the authoritative live structure inventory.

## First task: reconcile inventory

Do not assume the planning list count is correct.

Generate the authoritative list from live model/content definitions and classify each item.

Record:
- ID
- player-facing name
- current source destination
- buildable/upgradeable
- current max level
- filter
- asset key
- current unlock gate
- whether it is planned/unreachable/not yet implemented

Resolve naming collisions such as Forge vs Weaponsmith before acceptance tests are written.

## Required filters

- ALL
- ECONOMY
- DEFENSE
- CRAFT
- STORAGE
- CIVIC

Every live structure must belong to at least one non-ALL filter.

Suggested conceptual mapping only; live definitions win.

### ECONOMY
resource producers

### DEFENSE
towers, walls, gates, defensive emplacements

### CRAFT
crafting/forge/armorer/jeweler style structures

### STORAGE
resource storage structures

### CIVIC
Barracks, Cathedral of Magic, Echo Hollow, Store, Healing Caravan and other town-service structures that do not fit the other filters

## Model responsibility

Filter membership must be supplied by data/model.

UI must not infer category from:
- ID prefix
- class name
- asset name
- source tab

## Default behavior

ALL may be the default and may scroll.

Do not claim ALL fits without scrolling.

## Numeric acceptance criteria

At target 2670×1200 landscape:

- if a filter contains 12+ items, at least 12 tiles are visible simultaneously
- every item in a filter is reachable in at most one short vertical scroll
- no horizontal pagination
- no hidden "Other" items reachable only through ALL
- ALL includes every live structure exactly once

## Deliverable

Include an inventory report alongside the implementation so future work orders use the real count.


---

## Provenance and reconciliation (added by the CLI seat, 2026-09-06)

Authored outside this repo and delivered by the owner as `Elarion_Manage_Redesign_Detailed_WorkOrders.zip`
on 2026-09-06. Filed verbatim; **the body above is the author's and has not been edited.** Everything the CLI
seat adds appears under a heading like this one.

**Numbering:** this set uses a **2000-block**, a THIRD namespace alongside the CLI main line and the UI seat's
reserved block. It is declared on `CLI_LANES_WO_NUMBERS.md` so it cannot collide (CLAUDE.md section 2 - the banner is the
sole authority). Do not renumber these into the main line.

**Supersedes:** `WORK_ORDER_1427` (why-can't-I) and `WORK_ORDER_1428` (the Manage card grows to the mockup).
Both were minted earlier the same day from the owner's playtest and her mockup; this program subsumes them and
goes further by replacing the rail model rather than enriching the card.

**Measured facts this set is consistent with** (from `docs/manage-flow-map/MAP.md`, run `Builds/flowmap1`):
43 rail rows across four areas, about two visible at a time; Buildings 6 + Defense 11 = 17, which is the number
the canon cites; the scroll auditor reporting `geometry=5 touch=5` on deliberately scrolled frames, which WO-2016
is right to call a fix rather than a waiver.
