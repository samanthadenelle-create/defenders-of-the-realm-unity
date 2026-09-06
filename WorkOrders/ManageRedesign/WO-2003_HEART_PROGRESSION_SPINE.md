# WO-2003 — Make the Heart the Realm Progression Spine

**Priority:** P0  
**Depends on:** model/save architecture review  
**Blocks:** Heart-gated content, prerequisite navigation

## Objective

Create a real upgrade mechanic for the Heart and make its level the player-facing realm progression tier.

## Product rule

**Heart Level = Realm progression level.**

Do not teach both Heart Level and Village Tier as separate player concepts if they gate the same content.

An existing internal `VillageTier` save field may remain for compatibility, but it must map deterministically to Heart Level.

## Required model/service behavior

Create or extend the authoritative Heart progression model/service to provide:

- current Heart level
- maximum Heart level
- next Heart level
- current Heart upgrade state
- upgrade requirements
- upgrade cost
- upgrade duration
- affordability
- unmet prerequisites
- active/in-queue state
- upgrade command
- post-upgrade unlock result set
- reach/radius result if buildable reach is supported

Do not make the Manage view calculate any of these.

## Save compatibility

Before changing persisted fields:

1. locate current save field(s) used by any `VillageTier`, Heart state, or progression gate
2. identify all readers/writers
3. preserve existing saves
4. if introducing `HeartLevel`, provide migration from the existing canonical value
5. ensure a save cannot disagree between two equivalent tier fields

Preferred rule:
- one authoritative stored value
- compatibility aliases/read adapters if old code still expects VillageTier

## Heart upgrade gating

Heart upgrades should be data-driven.

Example structure:

- required current Heart level
- required building levels or progression milestones
- resource costs
- build time
- optional story/quest gate
- resulting unlock bundle

The exact content table is an owner/content decision. The system must support it without UI changes.

## Reach / influence

If Heart upgrades expand buildable territory:

- radius/reach must come from progression data
- map/build placement systems read the authoritative reach
- UI only displays supplied next/current reach values
- no hard-coded radii in panels

## Player-facing copy

Use:
- HEART LEVEL 3
- Requires Heart Level 4
- Upgrade Heart to Level 4

Do not use:
- Village Tier 4

unless a separate mechanic truly exists.

## Failure cases

The model must represent:

- insufficient resources
- prerequisite building unmet
- story gate unmet
- Builder queue full
- Heart already upgrading
- max Heart level

## Acceptance criteria

- Heart can be upgraded through real gameplay logic.
- Heart level persists.
- old saves remain valid.
- Heart level gates content.
- higher Heart level can expand reach if configured.
- Manage receives Heart state from model only.
- no UI code writes progression tier.


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
