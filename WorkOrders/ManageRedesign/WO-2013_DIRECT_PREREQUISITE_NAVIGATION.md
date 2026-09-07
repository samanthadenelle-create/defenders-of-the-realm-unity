# WO-2013 — Add Direct Navigation From Blockers to Their Prerequisites

**Status:** DONE - landed in a6bbc523d (verified 2026-09-06)

**Priority:** P0  
**Depends on:** WO-2001, WO-2003

## Objective

Turn every meaningful lock/refusal into a next step.

## Examples

Outrider:
`Requires Barracks Tier 4`
[ VIEW BARRACKS ]

Research:
`Requires Armorer Level 3`
[ VIEW ARMORER ]

Building upgrade:
`Requires Heart Level 5`
[ VIEW HEART ]

## Model-owned destination

The selected-item VM supplies a navigation action.

The UI may not decide:
- which tab contains the prerequisite
- which building to select
- whether the Heart is the destination
- how to resolve an ID

## Carry queue context

Where useful, the model may include a short destination status in the CTA/subtext:

`VIEW ARMORER · Builders 2/2 · +4 queued`

This is informational only and must come from model state.

## Navigation behavior

The command may:
- switch Manage tab
- set selected item
- focus the correct detail

No extra Back → category → search sequence.

## Acceptance criteria

- every Heart/building prerequisite can route directly to its source
- destination opens selected
- queue context can be shown without view service calls
- no dead-end lock message when a resolvable prerequisite exists


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
