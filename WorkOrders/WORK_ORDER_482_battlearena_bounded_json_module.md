<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-23
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-23) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

> ⚠ **NUMBER COLLISION — this document does not own WO-482; `WORK_ORDER_482_overworld_encounter_realtime_battle.md` does.**
> Referred to hereafter as **WO-482-B (BattleArena bounded JSON module)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.
> ⚠ **Work HAS shipped under this number** — commit messages and/or a `.RESULT.md` cite WO-482 for THIS document. It is deliberately **not renumbered**; a renumber would orphan those references. Use the alias above when you need to name it unambiguously.

# WORK_ORDER_482 (Arena refinement) — BattleArena as a BOUNDED JSON MODULE + SceneDirector lifecycle

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

> **PARTIAL - re-scoped 2026-08-14 (phantom sweep).** Most of this WO is present in HEAD; a named
> remainder is outstanding. No per-WO path:line was recorded here: see the 2026-08-14 phantom sweep for the
> implementation site and the remainder. Do not re-implement the shipped part.
> (Any prior dated reconciliation note on this file stands - see the preserved line below.)
> _Prior status line, preserved: Status: READY TO IMPLEMENT - partial (reconciled 2026-08-09 from the tree - the data boundary LANDED: `Assets/_Modules/Core/Arena/ArenaContracts.cs` names this WO as its spec and carries the ArenaRequest / ArenaResult seam. The dedicated arena SCENE and the SceneDirector lifecycle do NOT exist: no arena scene under `Assets/Scenes` and no `SceneDirector*.cs` anywhere. DUPLICATE NUMBER: two files claim 482)_

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

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `ArenaContracts.cs only; no SceneDirector` — arena scene + lifecycle unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
