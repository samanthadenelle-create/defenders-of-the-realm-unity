# WO-2002 — Create the Shared Dumb Manage UI Contract and Common Renderer

**Priority:** P0  
**Depends on:** WO-2001  
**Blocks:** BUILD, ARMY, RESEARCH implementation work

## Objective

Create one common presentation contract so BUILD, ARMY, and RESEARCH reuse the same dumb UI infrastructure instead of implementing separate business logic in panels.

## Non-negotiable architecture

The Manage view is a renderer, not a rule engine.

### Model/VM owns

- item order
- grouping/filter membership
- labels
- descriptions
- level text
- lock text
- state text
- cost text
- affordability
- timers
- progress fraction
- queue counts
- queue capacity state
- upgrade deltas
- prerequisite destination
- button visibility
- button enabled state
- action labels
- action commands
- asset keys
- selected item
- current activity

### UI owns

- layout
- sizing
- styling
- image assignment from supplied asset key
- text binding
- visible/hidden binding
- enabled/disabled binding
- invoking supplied callbacks

## Preferred contracts

Use repository naming conventions, but preserve these responsibilities.

### `ManageTileVM`

Required fields conceptually:

- `Id`
- `Title`
- `Subtitle`
- `PortraitKey`
- `IsSelected`
- `VisualState`
- `StateText`
- `StateIconKey`
- `Progress01` optional
- `TimerText` optional
- `Activate`

### `ManageSelectionVM`

- `Title`
- `LevelText`
- `Description`
- `State`
- `StateText`
- `PortraitKey`
- `Stats[]`
- `Costs[]`
- `PrimaryAction`
- `SecondaryAction`
- `RequirementAction`
- `Progress`
- `AuxiliaryText`

### `ManageActionVM`

- `Label`
- `Enabled`
- `Visible`
- `StyleRole`
- `Activate`
- `DisabledReasonText` optional

### `ManageActivityVM`

- `Visible`
- `IconKey`
- `Title`
- `TimerText`
- `QueuedCountText`
- `OpenQueue`

## Common renderer

Create/refactor one `ManageWorkspacePanel`-equivalent that can render:

- common header
- tab navigation
- tile grid region
- selected-item region
- contextual activity strip
- queue overlay door

Tab-specific code should provide state, not rewrite action logic.

## Explicit prohibitions

The panel must not contain code equivalent to:

- `if (gold >= cost)`
- `if (heartLevel >= requirement)`
- `if (queue.Count < max)`
- `if (level == maxLevel)`
- `switch(itemId)`
- string parsing of IDs to determine category
- direct calls to training/build/research services

## Regression requirement

Add source-level regression checks preventing the shared Manage view from referencing core economy/progression services directly.

Whitelist should be explicit if a harmless UI-only service is required.

## Acceptance criteria

- BUILD/ARMY/RESEARCH all bind through the common contract.
- removing/replacing the UI does not change gameplay rules.
- no duplicated affordability logic in view code.
- no duplicated lock logic in view code.
- no direct service mutation calls from Manage panel.
- commands exposed by VM are the only action path.


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
