# WO-2004 — Define Data-Driven Heart Unlock Bundles and Upgrade Requirements

**Priority:** P0  
**Depends on:** WO-2003

## Objective

Make Heart upgrades actually unlock the broader game instead of functioning as a cosmetic level number.

## Data model

For each Heart level transition, author one progression record containing:

- target Heart level
- costs
- duration
- prerequisite conditions
- newly unlocked building IDs
- newly unlocked building upgrade caps
- newly unlocked troop IDs
- newly unlocked troop upgrade caps
- newly unlocked research schools/perks
- newly unlocked defenses
- newly unlocked systems
- reach/radius value if applicable
- optional reward/message metadata

## Do not hard-code unlocks in UI

The model computes the unlock result from progression data.

UI may show:
- "Unlocks at Heart Level 4"
- a preview list supplied by model

UI may not know which Heart level unlocks Outrider, Stone Gate, etc.

## Unlock preview

Heart selection/detail model should support a short preview:

`NEXT LEVEL UNLOCKS`
- Stone Gate
- Outrider
- Armorer Level 3
- +X build reach

Preview contents come from the progression model.

## Gate normalization

Every system currently using a village/town tier gate must be audited.

For each gate:
- map to Heart Level if it represents realm progression
- leave unchanged only if it is truly a different mechanic
- document exceptions

## Acceptance criteria

- no duplicated Heart-level unlock tables across services
- one authoritative progression table
- every player-facing tier lock resolves to a real Heart upgrade path
- unlocking a Heart level invalidates/rebuilds dependent Manage state
- no restart required to see newly unlocked tiles/actions


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
