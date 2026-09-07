# WO-2008 — Replace Troop Rail With 3×3 ARMY Grid

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:15:00, build 2026.09.07.359076) - "owner in chat 2026-09-07 09:1x, verbatim: 'the 15 verify are the new screen UI work correct? THose I verified' - the board panel listed Fixed rows only, so t...". PRIOR STATUS: AWAITING OWNER MATCH - device frame vs mockup panel 4 (TROOPS 3x3 grid) not yet passed (2026-09-07); code landed bb51b8b9c. The owner walked all nine Manage screens on build 358872 beside docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png and none matched; headless capture is evidence, never the verdict. *(was: DONE - landed in bb51b8b9c (verified 2026-09-06))*

**Priority:** P0  
**Depends on:** WO-2002

## Objective

Show all nine trainable troop types at once.

## Required grid

3 columns × 3 rows at target 2670×1200 landscape.

Every troop tile shows:

- portrait
- troop name
- level
- mandatory state indicator

Examples:
- trainable
- training
- queue blocked
- locked by Barracks/Heart
- max upgrade track

## Locked troops

Remain visible and selectable.

Requirement copy comes from model.

Examples:
- Requires Barracks Tier 4
- Requires Heart Level 5

## No rail

Delete:
- vertical troop rail
- troop page index
- previous/next troop pager

## Acceptance criteria

- all 9 troop types visible simultaneously
- no scroll needed to discover troop roster
- every tile exposes actionability state
- selection highlight is shape + color


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
