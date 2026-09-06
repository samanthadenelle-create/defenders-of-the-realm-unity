# WO-2011 — Add a Unified Item / Upgrade / Action State Model

**Status:** FIXED (commit a6bbc523d; COMPILE_GATE_OK, REGRESSION_OK 400/400 suites, CATALOG_FALLBACK_GEN_OK) *(was: READY)*

**Priority:** P0  
**Depends on:** none  
**Blocks:** BUILD, ARMY, RESEARCH state correctness

## Objective

Represent ownership, upgrade-track state, and action availability separately enough that the UI never lies.

## Required distinctions

### Item ownership/availability
Examples:
- owned
- not yet unlocked
- unavailable/feature gated

### Upgrade-track state
Examples:
- upgradable
- max

### Action state
Examples:
- available
- unaffordable
- prerequisite blocked
- queue blocked
- in progress

Do not collapse these into one enum if doing so creates contradictions.

## Canon examples

### Built Lumber Mill, next upgrade Heart-gated
- item: owned
- upgrade track: upgradable
- upgrade action: prerequisite blocked
- CTA: VIEW HEART

### Max-level Footman, train queue open
- item: owned
- upgrade track: max
- train action: available
- CTA: TRAIN

### Max-level Footman, train queue full
- item: owned
- upgrade track: max
- train action: queue blocked
- CTA: VIEW QUEUE

### Locked Outrider
- item: not unlocked
- train action: prerequisite blocked
- CTA: VIEW BARRACKS or VIEW HEART

## Model output

Expose explicit state and player-facing text.

UI must not reverse-engineer state from:
- null callbacks
- disabled buttons
- label text
- color
- level values

## Acceptance criteria

- all existing captured edge cases map cleanly
- no contradictory combination is rendered
- MAX does not suppress valid non-upgrade actions
- queue-blocked has a first-class representation


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
