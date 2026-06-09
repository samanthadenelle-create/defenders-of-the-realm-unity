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

## Reuse map (verified 2026-06-09 — includes a premise correction)
**Net new code = ~6 small pieces; everything else reused:**
1. `ArenaDefenseDef` + `ArenaDefenseCatalog` — mirror `ArenaCatalog` (static class + plain def class). Troops are UNITS, not `CatalogEntry` structures.
2. `PlacedDefenderData` struct in **`DeNelle.Core.State`** — mirror `PlacedStructureData` (`itemId`, `cellX/cellZ`, `yawSteps`); grid-cell-relative (server re-verifiable).
3. Save: add `[JsonProperty("arenaDefense")] List<PlacedDefenderData> ArenaDefense` to `SaveSchema.PersistedState` (append at END), bump `CurrentVersion` **18→19**; additive-default-on-read (null→empty, no migration step needed).
4. Placement: a **slim `ArenaDefenseSetupController`** reusing `PlacementGrid` + `GhostPreview` + `StructureFactory` (do NOT thread a "mode" flag through the 1581-line `BuildModeController`). Swap the `EconomyService` resource cost for a simple **point-pool** check (sum of `PointCost` ≤ 50) at the single affordability call (`IsValidPlacement` step 5).
5. Friendly spawn: reuse `StoryCompanionInjector.BuildPlaceholder(HeroClass, pos)` for the 4 troops; `StructureFactory.Create` for Shrine/Ballista. Tether troops to a **guard post** (mirror `OutpostDefender.GuardPost`/`EnemyOutpost.MakeAnchor`), NOT StoryCompanion's hero-leash.
6. `SpawnDefenders()` hook in `EnemyOutpost.Start()` next to `SpawnGarrison()` (after `BuildFortification`), reading `GameState.ArenaDefense`, spawning friendly defenders at their cells.

**Friend/foe = the `CombatFaction` flag (owner was right; the reuse-agent missed it).** `IDamageable.Faction` (`Assets/_Modules/Core/Combat/IDamageable.cs:28`) = `{ Friendly=0 (hero/pets/buildings/walls/Heart), Hostile=1 (enemies) }`. EVERY targeting check filters it — `DefenseTower`/`TowerCombat`/`ArcaneTower`/`HeroAbilities`/`HeroTargetIndicator`/`PlayerAttackController` all do `if (mb is IDamageable d && d.Faction == CombatFaction.Hostile)`. Raiders already report `Hostile` (`EnemyDamageable.Faction => Hostile`).
- So an Arena **DEFENDER** = a unit with **`Faction = Friendly`** that **targets `Hostile`** (reuse the existing Hostile target filter — `TowerCombat`/`StoryCompanion`/`OutpostDefender` already do it) + implements `IDamageableStructure` so raiders attack it back. **Set the flag, reuse the filter → zero new combat logic.**
- `StoryCompanion` is still the richest body to reuse — it already IS the 4 troops (HeroClass Knight/Ranger/Mage=Wizard/Cleric=Healer) with abilities (Taunt+Bulwark / Multishot / Burst / Mend). Spawn it `Friendly`, tether to a guard post (not the hero-leash).
- Healing Shrine = a small heal-aura behavior (verify/add in `StructureFactory.AttachBehavior`); Ballista = reuse `behaviorId="DefenseTower"`.
- Time limit = reuse `ArenaMode.RaidTimeoutSeconds`/`WatchForLoss` (above).

## What NOT to do
- No new placement system, no new AI/targeting, no bespoke save layer. Reuse.
- Don't conflate with normal base buildings — Arena defense is a SEPARATE layout/pool.
- Hardcode stats but comment every one as data-driven.
