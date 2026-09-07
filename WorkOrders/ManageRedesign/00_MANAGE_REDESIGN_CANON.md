# ECHOES OF ELARION — MANAGE REDESIGN CANON

**Status:** Implementation canon  
**Intent:** Replace the current rail-heavy Manage interaction model with a simple, game-first management system.  
**Architecture rule:** UI is dumb. State, labels, gating, affordability, progress, destination routing, queue state, and commands come from the model/VM.  
**Primary player-facing tabs:** BUILD · ARMY · RESEARCH  
**Global utility:** QUEUE  
**Progression spine:** HEART LEVEL

---

## 1. Why this exists

The current Manage flow exposes too little of the player's inventory at once. The measured flow contains 43 rows across four areas, while the rail exposes only about 2.2 entries at once. The redesign does **not** attempt to make the rail prettier. It replaces the rail interaction model.

The goal is not to reduce the amount of game content. The goal is to increase understandable exposure:

- current: ~2 visible choices at a time
- target: grid-first browsing with at least 12 visible build tiles at target aspect where the filter contains 12+ items
  > **STALE 2026-09-06 (WO-1534 B1):** superseded by `docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png` (implemented in `32659c0f6`). The shipped BUILD grid is 5 x 2 = **10** tiles - `Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs:3500-3507`.
- Army: all 9 troop types visible at once
- Research: research schools visible first, then a small local list of perks

---

## 2. Canon navigation

Opening Manage lands directly on the last-used tab, defaulting to BUILD.

Top-level Manage tabs:

1. BUILD
2. ARMY
3. RESEARCH

QUEUE is global and always available.

The current four-tile launcher is superseded.

---

## 3. BUILD canon

BUILD owns every physical village structure because all structure upgrades compete for the Builder queue.

Filters:

- ALL
- ECONOMY
- DEFENSE
- CRAFT
- STORAGE
- CIVIC
  > **STALE 2026-09-06 (WO-1534 B1): CIVIC is retired** - the mockup `docs/mockups/manage/MANAGE_MOCKUP_8_SCREENS.png` screen 2 draws FIVE chips, implemented in `32659c0f6`. Authority: `Assets/_Modules/Core/Catalog/BuildFilter.cs:57`, `:87-89`; the five CIVIC rows re-homed at `:59-73`.

Every structure must belong to at least one filter besides ALL.

**Important:** before implementation, reconcile the live structure inventory. The prior audit counted Buildings + Defense as 17 rows, while the redesign planning list contained more names. Do not lock acceptance tests to a guessed total. The model must expose the authoritative live list.

ALL may scroll. That is allowed.

Per-filter target:
- show at least 12 tiles simultaneously when the filter contains 12 or more
  > **STALE 2026-09-06 (WO-1534 B1):** same supersession as section 1 - the mockup's grid is 5 x 2 = **10** tiles (`ManageScreenVM.cs:3500-3507`, commit `32659c0f6`).
- all items in a filter reachable in one short vertical scroll
- no nested scroll region inside a selected-item detail card

---

## 4. ARMY canon

All 9 trainable troop types appear in one 3×3 grid at the target phone aspect.

No troop rail.
No troop pager.
No hidden troop list.

Locked troops remain visible.

---

## 5. RESEARCH canon

Research does not show 17 perks in one flat list.

First level:
- research schools/providers

Second level:
- perks belonging to the selected school

Examples of schools:
- Cathedral of Magic — Magic
- Armorer — Defense
- Forge / Weaponsmith — Weapons
- Barracks — Army

The model owns school membership. The UI does not infer it from IDs or names.

---

## 6. HEART LEVEL progression canon

The Heart becomes the visible realm-progression spine.

Player-facing:
- Heart Level = Realm progression level
- do not expose a separate "Village Tier" concept if it describes the same gate

Heart upgrades may unlock:
- higher building levels
- new buildings
- new troop types
- higher troop upgrade caps
- research schools/perks
- defenses
- buildable reach / influence radius
- other systems explicitly mapped by content data

Internal save compatibility may retain an existing VillageTier field if required, but the player-facing contract is Heart Level.

The Heart itself must be upgradeable through a real model/service path. No UI-only fake tier changes.

---

## 7. Manage item state canon

State must be explicit and model-owned.

Required concepts:

1. **Available**
2. **Locked**
3. **In progress**
4. **Queue blocked**
5. **Max upgrade track**

Important:
- MAX is a property of the upgrade track, not necessarily of the item.
- A max-level troop may still be trainable.
- A built building whose *next upgrade* is Heart-gated is not "Locked"; the building is owned and operating. Its **upgrade action** is gated.
- Queue-blocked means the action is valid in principle but cannot start because the relevant line has no capacity.

The UI must never infer these states from button interactability.

---

## 8. Tile state is mandatory

Every BUILD and ARMY tile must show one actionable state indicator supplied by the model.

Examples:
- upgrading now + timer
- upgrade affordable
- upgrade unaffordable
- queue blocked
- max
- locked
- trainable
- training now

Do not ship a grid where the player must tap every item to discover what can be acted on.

---

## 9. Dumb UI rule

Views/panels may:

- bind text already supplied by the VM/model
- bind sprites/asset keys already supplied by the VM/model
- show/hide regions according to explicit state fields
- invoke commands/callbacks supplied by the VM/model
- render progress values supplied by the VM/model
- apply common visual primitives

Views/panels may **not**:

- calculate costs
- inspect player resources to decide affordability
- decide locks
- determine Heart requirements
- determine whether an item is max level
- inspect queue service state directly
- calculate queue capacity
- derive labels from enum names
- parse IDs
- decide which destination a prerequisite CTA should open
- calculate upgrade deltas
- mutate save data
- call Barracks/BuildTimer/Research/Heart services directly

The view should be replaceable without changing game rules.

---

## 10. Common-class rule

Use one reusable Manage presentation path wherever possible.

Preferred shape:

- `ManageScreenVM` — root orchestration/model adapter
- `ManageTabVM` — BUILD / ARMY / RESEARCH tab state
- `ManageTileVM` — common tile contract
- `ManageSelectionVM` — common selected-item contract
- `ManageActionVM` — button/command contract
- `ManageActivityVM` — contextual current-job strip
- `ManageQueueVM` — global queue state
- `ManageWorkspacePanel` — common dumb renderer
- optional tiny tab-specific adapters only where layout truly differs

Do not create three independent UI systems with duplicated lock/cost/queue logic.

Exact class names may follow repository conventions, but the separation above is required.

---

## 11. Acceptance principle

For any selected thing, the player should immediately understand:

1. What is it?
2. What does it do?
3. What changes next?
4. What does it cost?
5. What can I do now?
6. If I cannot act, why?
7. Where do I go to resolve the blocker?

Anything beyond that belongs behind secondary detail or the Queue.


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
