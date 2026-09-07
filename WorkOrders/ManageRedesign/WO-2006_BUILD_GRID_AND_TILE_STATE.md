# WO-2006 — Replace BUILD Rail With Stateful Grid Tiles

> **PARTLY SUPERSEDED 2026-09-06 (WO-1534 Part B1). Body frozen per CLAUDE.md section 15 - do not rewrite it.**
> The density criterion at line 64 (">=12 tiles visible when inventory/filter size allows") is no longer
> what the shipped screen owes. `docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png` is the spec and commit
> `32659c0f6` implemented it: `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:3500-3507` authors the
> BUILD grid as `GridColumns = 5` and `GridRows = 2` - **10 tiles**, deliberately, with the tab row's
> reclaimed 120px band going to tile height (`ManageWorkspacePanel.cs:412-418`). **The grid itself, the
> deleted rail and the tile state model all stand**; only the number is superseded. The colourblind
> criterion on the same list is separately unmet and is ticketed as WO-1534 section B2.

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:15:00, build 2026.09.07.359076) - "owner in chat 2026-09-07 09:1x, verbatim: 'the 15 verify are the new screen UI work correct? THose I verified' - the board panel listed Fixed rows only, so t...". PRIOR STATUS: AWAITING OWNER MATCH - device frame vs mockup panel 2 (BUILDINGS grid) not yet passed (2026-09-07); code landed bb51b8b9c. The owner walked all nine Manage screens on build 358872 beside docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: DONE - landed in bb51b8b9c; the >=12-tile density acceptance at :64 is SUPERSEDED 2026-09-06 by the owner's hub mockup (implemented in 32659c0f6, 5x2=10 tiles); the colourblind-legibility acceptance on the same list is OPEN (WO-1534 section B2), not superseded. See banner. *(was: DONE - landed in bb51b8b9c (verified 2026-09-06))*)*

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
