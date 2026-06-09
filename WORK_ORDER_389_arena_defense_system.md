# WORK_ORDER_389 — Arena Defense System (CoC-style pre-placed troops)

**Status:** SPEC — Phase 1 (data/core) ready to build; Phase 2 (UI/placement) pending reuse-map.
**Lane:** 2 Combat/AI + 6 Economy + 4 UI/HUD.
**Source:** Owner spec, 2026-06-09. Pairs with WO-388 (Use My Castle — the raid loads the player's base; this adds the *defenders* on it).

## Concept
The defender spends a **limited point pool (start 50 pts)** to pre-place troops in their castle for **Arena defense** — SEPARATE from normal base buildings. When someone raids the castle in the Arena (WO-388), these pre-placed troops **spawn and auto-fight**. CoC defense layer.

## Time Limit (win condition)
- **Default 180s (3 minutes).** If the attacker fails to destroy the objective within the limit → **the defender wins automatically.**
- **REUSE — mostly already built:** `ArenaMode` already has `RaidTimeoutSeconds = 180f` + `WatchForLoss()` (0.5s poll) with a timeout branch. Wire the timeout outcome to "attacker failed → **defender wins**" (confirm it credits the defender / counts as an attacker loss, not a silent win). Don't add a new timer system.
- Make the value **easy to change** + comment `// TODO data-driven: move to arena-defense.json/config`.

## Troops (Phase 1) — HARDCODE values now, comment "→ data-driven (JSON)"
| Troop | Cost | Kind |
|---|---|---|
| Ranger | 5 | unit (ranged) |
| Knight | 10 | unit (melee) |
| Wizard | 15 | unit (caster) |
| Healer | 12 | unit (support) |
| Healing Shrine | 18 | static structure |
| Ballista | 20 | static structure (ranged) |
Stats (damage/health/range/etc.) hardcoded for now, **every value commented `// TODO data-driven: move to arena-defense.json`**.

## Phase 1 — Data & Core Logic (build first, gate-able, no UI)
- **`ArenaDefenseCatalog`** (mirror the project's existing catalog pattern — see reuse map): the 6 troop definitions (id, displayName, cost, kind, stats), hardcoded, with the JSON-TODO comments. Eventually loaded via the project's CanonicalJson/CatalogRegistry path.
- **`DefenseSetup`** data structure: the player's placed defense layout — a list of `{ troopId, position(/cell), rotation }` + the spent points. Serializable (mirror `PlacedStructureData`/`BaseLayout`).
- **Save/load**: persist `DefenseSetup` through the existing save path (mirror how `GameState.BaseLayout` is saved in `SaveSchema`/`GameStateService`) — a new `arenaDefense` field. Point-pool validation (can't exceed 50; sum of placed costs ≤ pool).
- Acceptance P1: catalog returns the 6 troops with costs; a `DefenseSetup` can be built, validated against the 50-pt pool, saved, and loaded back identically. Compiles green.

## Phase 2 — UI & Placement (after the reuse-map)
- **Placement UI: REUSE the existing FF-style placement system** (BuildMode/PlacementGrid/the FF-style placement — see reuse map) — do NOT build a new placement system. A dedicated **"Arena Defense Setup"** screen lets the player place the troops on their castle layout, spending the point pool.
- **Spawning + AI: REUSE the friendly/hostile flag** — the AI brain already supports friendly vs hostile via a flag; spawn the placed troops as **friendly** combat units (via the existing unit/enemy factory) that auto-fight raiders. Do NOT write new AI/targeting logic.
- On a raid (WO-388 path): after the defender base is realized, also spawn the `DefenseSetup` troops as friendly defenders at their placed positions.

## Reuse requirements (non-negotiable — the spec's own rule)
- Placement UI → existing FF-style placement system.
- Troop AI → existing friendly/hostile flag, NOT new AI.
- Troops (Ranger/Knight/Wizard/Healer) → existing hero/companion classes where possible.
- Save → mirror `BaseLayout`/`SaveSchema`.
- Catalog → mirror existing catalog (hardcoded now, JSON later).

## What NOT to do
- No new placement system, no new AI/targeting, no bespoke save layer. Reuse.
- Don't conflate with normal base buildings — Arena defense is a SEPARATE layout/pool.
- Hardcode stats but comment every one as data-driven.
