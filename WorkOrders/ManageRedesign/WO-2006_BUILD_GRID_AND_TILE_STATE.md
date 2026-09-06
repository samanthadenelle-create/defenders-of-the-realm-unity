# WO-2006 — Replace BUILD Rail With Stateful Grid Tiles

**Priority:** P0  
**Depends on:** WO-2002, WO-2005

## Objective

Delete the BUILD left rail/pager interaction and render structures as a dense, readable grid.

## Tile content

Each tile must show:

- building portrait
- building name
- current level
- exactly one primary state indicator
- selection state

## Mandatory tile state

State is not optional.

The model supplies one of the appropriate visual states, such as:

- upgrade affordable
- upgrade unaffordable
- upgrading now
- queue blocked
- upgrade gated by Heart
- max upgrade track
- locked/not yet built where applicable

The exact enum can differ, but the UI must never make the player tap every tile to discover actionability.

## Timer behavior

If the selected building or tile is currently upgrading:
- model supplies timer text/progress
- tile may show compact timer
- UI does not calculate remaining time

## Selection

Selection must use shape/border treatment in addition to color.

## Scrolling

One vertical grid scroll only.

Selected-item details must not create an independent vertical scroll at normal content length.

## Prohibited

- left rail
- previous/next buttons
- per-item modal before selection
- action state calculated by view

## Acceptance criteria

- ≥12 tiles visible when inventory/filter size allows
- all visible tile states match VM state
- locked/gated/max/queue-blocked are distinguishable without opening detail
- tile state remains readable for colorblind players


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
