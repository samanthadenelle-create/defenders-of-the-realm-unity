# WO-2017 — Add a Heart Management Surface

**Priority:** P0  
**Depends on:** WO-2003, WO-2004, WO-2013

## Objective

Give the player a real destination when content says "Requires Heart Level X."

## Entry points

At minimum:
- direct prerequisite CTA: VIEW HEART
- BUILD/CIVIC Heart tile if the Heart is represented as a town structure in Manage
- existing in-world Heart interaction may also open the same model-backed detail

All entry points must bind to the same Heart model.

## Heart detail

Show:

- HEART
- current level
- short realm-progression description
- current reach if applicable
- next reach if applicable
- next-level unlock preview
- costs
- duration
- prerequisite state
- primary action or blocker action

## Available example

HEART — LEVEL 3

Strengthens Elarion and expands what the realm can support.

Reach: 28m → 34m

Next level unlocks:
- Stone Gate
- Outrider
- Armorer Level 3

Costs...
Time...

[ UPGRADE HEART ]

## Prerequisite blocked

HEART — LEVEL 3

Before Level 4:
- Barracks Level 3
- Cathedral Level 2

[ VIEW BARRACKS ]

If multiple requirements exist, model supplies the ordered list and primary recommended next action.

## Queue blocked

Builder queue full.

[ VIEW QUEUE ]

## In progress

HEART — LEVEL 3 → 4
2h 18m remaining

[ VIEW QUEUE ]

## Max

HEART — LEVEL 6 · MAX

No upgrade CTA.

## UI restrictions

The Heart panel must not:
- calculate unlock previews
- calculate reach
- inspect structure levels
- inspect queue capacity
- inspect resources
- mutate Heart level

## Acceptance criteria

- every Heart-gated Manage item has a valid destination
- Heart surface uses same common Manage contracts where practical
- upgrade executes through model/service command
- unlocks refresh immediately on completion


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
