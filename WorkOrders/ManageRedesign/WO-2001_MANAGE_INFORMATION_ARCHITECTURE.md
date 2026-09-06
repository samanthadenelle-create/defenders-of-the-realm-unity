# WO-2001 — Replace Manage Hub With Direct BUILD / ARMY / RESEARCH Tabs

**Priority:** P0  
**Depends on:** none  
**Blocks:** WO-2002 onward

## Objective

Remove the required four-tile Manage launcher and make Manage open directly into a persistent three-tab workspace:

- BUILD
- ARMY
- RESEARCH

QUEUE remains a global utility in the Manage header.

## Current problem

The current flow requires the player to choose a destination before seeing actionable content, then each destination uses a narrow rail. This adds navigation without improving comprehension.

## Required behavior

### Entry
When Manage opens:

1. if a valid previous Manage tab exists in session state, open it
2. otherwise open BUILD

Do not persist a stale tab that is no longer available because of feature gating.

### Header

Common header:

`BACK | MANAGE | BUILD | ARMY | RESEARCH | QUEUE[count]`

Exact geometry may vary, but the commands and hierarchy may not.

### Back / Close

- BACK returns to the immediately prior game surface.
- CLOSE, if the shell still requires it, exits Manage.
- Do not use BACK to return to a category launcher because the category launcher is removed.

## Model requirements

Root Manage model must expose:

- current tab
- available tabs
- command to select tab
- queue badge count
- command to open Queue
- command to close/back
- initial tab decision

The UI must not decide the default tab.

## UI restrictions

The view may only render the root state and invoke commands.

No tab gating logic in the view.

## Acceptance criteria

- Manage opens directly to BUILD/ARMY/RESEARCH.
- No four-tile chooser is required.
- BUILD, ARMY, RESEARCH are one tap from each other.
- Queue is reachable from all three.
- last-used tab behavior works.
- Back never routes through the retired launcher.
- headless capture exists for each initial tab state.


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
