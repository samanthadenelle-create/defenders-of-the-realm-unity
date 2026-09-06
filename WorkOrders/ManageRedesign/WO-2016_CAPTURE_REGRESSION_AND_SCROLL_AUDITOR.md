# WO-2016 — Rewrite Manage Regression Suite and Fix Scroll Auditor

**Priority:** P0  
**Depends on:** new layout implementation

## Objective

Retire regressions that enforce the old rail architecture and make automated geometry/touch auditing valid for scrolled content.

## Remove old architecture assumptions

Tests must no longer require:
- rail
- rail pager
- four-tile Manage launcher
- per-destination NOW band
- per-destination queue drawer

Preserve service-level behavior tests.

## Required captures

### Manage shell
- BUILD initial
- ARMY initial
- RESEARCH initial

### BUILD
- ALL top
- ALL scrolled to end
- each filter
- available upgrade
- unaffordable upgrade
- Heart-gated upgrade
- queue-blocked upgrade
- in-progress upgrade
- max building

### ARMY
- full 3×3 grid
- trainable
- max-upgrade-track but trainable
- queue blocked
- locked
- training in progress

### RESEARCH
- school grid
- selected school
- available perk
- researched perk
- locked perk
- queue blocked
- in progress

### Queue
- Builders
- Training
- Research
- empty
- full

### Heart
- upgradable
- prerequisite blocked
- queue blocked
- upgrading
- max
- unlock preview

## Scroll auditor fix

The current auditor must understand scroll content.

Do not count intentionally off-viewport child rows as geometry/touch failures.

Required logic:
- evaluate touch/geometry against the viewport intersection
- skip or specially classify fully clipped children
- verify content bounds/clamping independently
- verify last row can reach a valid final position
- verify scrolling cannot overshoot into excessive dead space

## BUILD/ALL bottom capture

Mandatory.

The grid must be captured scrolled to the end to detect inherited scroll-clamping bugs.

## Numeric checks

- ARMY: all 9 troop tiles visible simultaneously
- BUILD: ≥12 tiles visible simultaneously when dataset/filter has ≥12
- BUILD filter: all items reachable in one short scroll
- RESEARCH school grid: all schools visible without scrolling
- no primary action clipped
- all actionable touch targets meet project minimum

## Acceptance criteria

- no "judge by eye" disclaimer required for normal scroll captures
- geometry/touch audit passes legitimate scrolled states
- false positives from off-viewport rows eliminated


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
