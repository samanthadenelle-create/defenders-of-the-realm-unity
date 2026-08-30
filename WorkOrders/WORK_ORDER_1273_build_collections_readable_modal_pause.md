# WORK ORDER 1273 - Build collections: readable cards, category icons, and safe pause

**Status:** READY 2026-08-29 - placement committed on Seeker, but the real Done -> tutorial dialogue -> re-enter Build -> visible persistence/singleton-state path remains unproven.
**Owner missing-art follow-up (2026-08-29):** DEVICE-PRESENT in Seeker APK 2026.08.29.346849; owner test pending. Originated from
`Logs/device/stone-gate-missing-art.png`. `gate_stone` is now presentation-gated out of both
local and remote collection projections until finished art is approved; its catalog,
progression unlock, save, and placement state remain unchanged. Every category/card image in
`BuildCollectionBrowser` now resolves to its sprite or a neutral framed `Image coming soon`
fallback, never the former white rectangle. Progression-locked entries are filtered before page
counts and return only after `ProgressionUnlocks.IsUnlocked` reports authoritative unlock; Stone
Gate remains hidden independently until its finished art is approved. `BuildCollectionPlayerRegression`
pins these rules.
Required card copy now wraps and auto-sizes above a 20px floor with overflow left readable; action
buttons use short complete faces (`PLACE`, `BUILT`, `NEED RESOURCES`, `UNAVAILABLE`) while the
complete state remains in the status band. Ellipsis/truncate modes are regression-banned here.
Category cards are now projected only after item definition and authoritative unlock filtering. A
category with zero visible eligible definitions is absent from the bar/counts. Affordability is
deliberately not part of that filter: an unlocked but expensive category remains visible with the
existing exact cost/shortfall state, so it gives the player a truthful goal.
Finite placement is derived from the existing `StructureSingleton` authority: a singleton item is
removed from category capacity after its allowed placement; if that exhausts the category, the
category disappears. `SingletonReleased` restores it after sell/destruction/removal. Repeatable
definitions are not filtered by this rule, and surviving placed structures remain in Manage.

**First-visit Build tutorial implementation (2026-08-29; remains READY pending Seeker APK):** the
live collection and placement seams now advance one phone-readable guide through category -> item
-> move ghost (including the explicit `Pinch in or out to zoom` hint) -> rotate -> check mark. Each
step advances only from its real action in `BuildCollectionBrowser` / `BuildModeController`; skipped
or out-of-order confirmation cannot complete it. Only a successfully committed check-mark placement
writes `build.first_use.completed.v1`, so an abandoned session restarts without falsely suppressing
the guide. The Defense Manage shortcut is deferred during this first-use sequence so it cannot eject
a new player before item selection. `BuildFirstUseGuideRegression` pins the order, copy, action
emitters, no-ellipsis rule, and confirmation-only persistence.

**Defense upgrade doorway follow-up (2026-08-29; READY pending APK):** the Defenses category now
opens Manage directly on its existing authority-backed Defense projection, headed `UPGRADABLE
TOWERS`. Rows identify the placed tower and grid cell, current and next level, exact canonical
cost, affordability/shortfall, and route Upgrade through the existing composed placed-instance key
and `PlacedStructureUpgradeService`. Max-level rows remain absent and the honest empty state says
so. `Build new defense` is a secondary return to Build Mode. No second tower level/cost model was
introduced; locked collection items remain filtered before this doorway.
**Minted:** 2026-08-28 by Codex CLI under WO-1271.
**Lane:** Village Build presentation. Do not alter building simulation, costs, IDs, or placement rules.

## Goal

Replace the unreadable phone card strip with category-first navigation using WO-1272's shared card
collection and focused modal. Selecting a category opens its large building cards. Gameplay remains
paused while the player chooses; placement resumes only at the deliberate placement transition.

## Collection map

### Gathering

- `collector_lumbermill` - Lumber Mill
- `collector_farm` - Quarry
- `collector_forge` - Iron Mine
- `mine_crystal` - Crystal Mine

Collection icon: owner-provided **Resources** art (pickaxe, wood, stone, iron, crystals).

### Realm

- `barracks` - Barracks
- `pet-house` - Echo Hollow
- `arcane-tower` - Cathedral of Magic

Collection icon: owner-provided **Realm** castle/banner art.

### Defenses

- `tower_ground_archer` - Archer Tower
- `tower_ballista` - Ballista
- `tower_arcane_spire` - Arcane Spire
- `tower_catapult` - Catapult
- `tower_siege_tower` - Sky Ballista

Collection icon: owner-provided **Defense** tower/shield art. Show four full-size cards and swipe/page
to the fifth. Locked cards remain visible with plain-language requirements.

### Crafting

- `workshop` - Crafting Station
- `forge` - Weaponsmith
- `armorer` - Armorer
- `jeweler` - Jeweler

Collection icon: owner-provided **Crafting** hammer/anvil/crystal art.

### Storage

- `lumberyard` - Lumberyard
- `silo` - Stoneyard
- `foundry` - Foundry

Collection icon: owner-provided **Storage** resource-crate art.

### Protection

- `wall_wood` - Wooden Palisade
- `gate_stone` - Stone Gate
- `healing_caravan` - Healing Caravan

Stone Wall is not a directly buildable card: it remains the Wooden Palisade upgrade preview/path.
Collection icon: owner-provided **Protection** wall, banners, and shield art (redo; supersedes the
earlier resource-crate image, which is now correctly assigned to Storage).

### Trade

- `market` - Store

Collection icon: owner-provided **Trade** scales, bag, and coins art.

Legacy `lumbermill`, `mill`, `repair_default`, and `deco_torch` do not become normal category cards.

## Interaction

1. Open Build and acquire the shared pause lease.
2. Show large category cards.
3. Select a category and show its database-ordered building collection.
4. Select a building and show name, purpose, full cost, requirements, footprint, and build time.
5. `Place` closes the decision modal and transitions deliberately into placement behavior.
6. Back returns to categories; Close exits Build and restores the prior simulation state.

## Acceptance

- Modal uses about 80% of phone safe area and is readable on the attached Seeker.
- No more than four full building cards are visible at once; overflow swipes/pages.
- Every card visibly states what the building is and its complete cost without tiny abbreviations.
- Category icons are distinct collection-level assets; item art remains item-level.
- Current canonical IDs, costs, visibility, singleton rules, placement validation, and save replay remain unchanged.
- Locked cards cannot arm and state their requirement in words.
- Gameplay cannot damage the player/town or advance combat while browsing categories/details.
- Headed Seeker captures cover category, four-card collection, five-card paging, locked state, and pause.

## Must not

- Do not add another category layer beneath these seven collections.
- Do not directly expose Stone Wall as a separately placeable L1 structure.
- Do not shrink cards to make five fit.
- Do not hardcode role membership in UI code; author collection membership as data pointers.
