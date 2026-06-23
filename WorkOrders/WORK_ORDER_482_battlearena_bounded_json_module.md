# WORK_ORDER_482 (Arena refinement) — BattleArena as a BOUNDED JSON MODULE + SceneDirector lifecycle

**Status:** DESIGN LOCKED (owner-directed, 2026-06-23). Supersedes the far-offset arena hack
in `BattleArena.cs` (region at (5000,5000) inside the live scene). Extends WO-482 / memory
`overworld-encounter-isolated-battle`. Pairs with WO-483 roadmap.

## Why (owner's architecture call, 2026-06-23)
The far-offset-in-the-current-scene arena was an extraction shortcut. It rides whatever scene
you're standing in, inherits its lighting/navmesh, has no real backdrop, and gets more fragile
with every new battle area. For **battles in MANY areas** (castle, outerworld, dungeons…) the
right structure is a **dedicated, reusable arena loaded as its own scene**, with a **pure data
boundary** — JSON in, JSON out — and **disposed after use**.

This is the project's HP B2B law applied literally: the arena is a **bounded context**. Nothing
but serialized data crosses the seam — the arena never holds a reference to a world object and
the world never reaches into the arena.

## The three wins (why this is paramount, not just tidy)
1. **Headless-testable combat.** Feed an `ArenaRequest` JSON → get an `ArenaResult` JSON, with
   NO open world loaded. Drops straight into the `DataRegression` gate (fixture in → assert
   result out). The Knight's combat is provable without a playtest.
2. **World stays permanently cheap + framerate-constant.** The open world only ever holds single
   wandering "rep" mobs. ALL combat spend lives in the isolated arena, where nothing else
   simulates — so battle size never threatens world perf.
3. **Scale is a pure data dial.** Enemy count / composition / levels / AI aggression / quality
   tier are FIELDS in the request, driven by the progression **seed-budget** (memory
   `scene-chunk-dungeon-composer-northstar`). Throttle anything — 1 enemy or a 12-mob horde,
   high-fidelity desktop or throttled mobile — with zero world risk. Deterministic via `seed`
   (same seed → same fight → repro + regression).

Knight-paramount tie-in: the hero is **reconstructed from his data** (class/level/loadout), so we
perfect the data + the arena builder ONCE and it's identical in every battle area.

## The data contract (the ONLY thing crossing the seam)
**ArenaRequest (in):**
- `hero`: { class, level, loadout (4 slots, Q=basic), currentHp }
- `family`: enemy ids + per-enemy level/role (e.g. orc-warrior/tank/mage)
- `context`: backdrop theme — castle | outerworld | cavern (drives arena dressing)
- `return`: { scene, position, yaw } — where to port the hero back
- `scale`: { enemyCount, levelBand, aiBudget, qualityTier } — derived from the progression seed-budget
- `seed`: deterministic repro

**ArenaResult (out):**
- `outcome`: win | lose | flee
- `enemiesDowned`, `duration`
- `rewards`: { xp, skillPoints (Wisdom), gear[], resources{} }
- `heroEndState`: { hp }

The hero crosses as DATA, not a GameObject — the arena REBUILDS the Knight from `hero` (purest
isolation; "only JSON in, JSON out"). It does NOT warp the live hero object.

## SceneDirector (the scene-manager class — owns the lifecycle)
One owner for additive load → use → dispose + the JSON handoff. NOT arena-only — it is the
RegionGate crossing primitive too (memory `region-gate-crossing-primitive`: load-mode warp/stream),
so arena in/out today, region/dungeon streaming tomorrow. Fold the existing `WorldSceneLoader`
additive logic INTO it; do not grow a second loader.

Lifecycle (port in → use → port out → dispose):
1. `engage` (RepEngageWatcher) → build `ArenaRequest` JSON.
2. `SceneDirector.EnterArena(request)` → **additive-load** the dedicated BattleArena scene; world
   stays RESIDENT (keep-in-memory intent).
3. Arena deserializes the request → builds the Knight + family from data → bakes its OWN navmesh →
   dresses backdrop by `context` → runs the real-time fight (reuses PlayerAttackController /
   HeroAbilities / hero-aggro / HeroHealth — ZERO new combat code).
4. Resolve → serialize `ArenaResult` → `SceneDirector` ports hero home (`return`) + **UNLOADS /
   DISPOSES** the arena scene (frees memory).
5. World (resident) applies the `ArenaResult` (rewards, hero hp) — `OnBattleEnded`.

## Gate / verification
- **DataRegression** (`DeNelle.Editor.DataRegression.RunAll`): JSON fixtures (1v1, 1v6, mobile-tier,
  flee, loss) → assert `ArenaResult` invariants. Combat proven headless, no playtest. (§2c: holistic
  work ships with tests.)
- Deterministic seed makes each fixture reproducible.

## Migration (extract, don't rewrite — reuse the verified loop)
- Keep the real-time combat stack as-is (already verified: walk→engage→fight→resolve→warp home ran
  end-to-end 2026-06-23). Re-seat it behind the JSON contract + SceneDirector.
- Retire the far-offset region (ArenaCentre 5000,5000) once the dedicated scene loads.
- The overworld floor/navmesh re-center (WO-483, done 2026-06-23) is the ENGAGE side (reaching a
  battle) — independent of arena internals; it stays.

## Open / to confirm with owner
- Backdrop fidelity per `context` for V1 (simple themed dressing first; richer later).
- Where the seed-budget → `scale` mapping lives (progression service).
