<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 454 — Faction Base Scene Generator (player base = enemy outpost = a scene)

**Status: READY TO IMPLEMENT** · Lane: Architect / World · P1 · Est: 1–2 days CLI
**Supersedes** the Grok draft titled "WO-452: Generic Scriptable NPC Base Builder" (that number was
arbitrary + collides with the real WO-452 AutoPilot-hardening). **WO# 454 is provisional** —
reconcile against `MASTER_PIPELINES_BACKLOG_2026-06-06.md` + `CLI_LANES_WO_NUMBERS.md` before minting.

## The insight (owner 2026-06-13)
**A base is a scene.** Player stronghold, enemy outpost, and garrison are all additively-loadable
scenes — the runtime already proves it (seam connectors load `Garrison_*` additively; castle/Village2
are scenes). So we do NOT build a second base-builder. We build **ONE scene generator + a config
layer**: player-base and enemy-outpost differ only by their **config + garrison**, never by the
geometry pipeline.

## Do NOT greenfield (reconcile with what exists)
The Grok draft proposes NPCBaseBuilder / TowerPlacer / EnemySpawnerHelper / PropPlacer from scratch —
that **duplicates ~5 shipped systems** which would then drift:
- Recipe→base geometry: `StructureFactory` → `BaseLayout` (the "sister-city / player-build-minus-the-
  player" factory) + `CastleHubBuilder` (reproducible, batchmode).
- Walls: `GridWallBuilder` + the Wood/Iron/ReinforcedSteel tier ladder.
- Towers/props: build-mode tower catalog + `StructureFactory` props.
- Enemy role mix + scaling: `WaveManager.SpawnComposedFamilyGroups` (role→Tank/Healer/Ranged/MiniBoss)
  + `EnemyFactory` (now with the codex variety) + `battleScaling` (level/count scaling).
- Outpost/raid: `EnemyOutpost.cs` + `RaidOutpostSystem` + the additive-`Garrison_*` crossing (WO this session).
**Lift only the ONE good idea from the draft: a ScriptableObject config per base variant.**

## Deliverables
### 1. `BaseConfig` ScriptableObject (`Assets/_Modules/.../World/BaseConfig.cs` + variant assets)
Drives the existing generator; the ONLY thing that differs player-base vs enemy-camp:
- Layout: baseRadius, wall tier, segments/side, central building, tower count/prefab, prop set/count.
- **Ownership:** `Player` | `Enemy` (player = buildable/owned, no garrison; enemy = garrison + boss).
- **Faction/theme:** Orc / Hollow / Troll-Ogre / mixed — picks the garrison roster + banner/theme.
- **Garrison (enemy only):** role composition (the triangle — tanks + kiting mages + DPS), count,
  `baseEnemyLevel`, `difficultyMultiplier`, and a **boss** capstone (overboss / orc-warlord).
- Variants to ship: Small Raider Camp, Fortified Garrison, Mage Enclave (kiting-heavy), Player Outpost.

### 2. Generator emits a SCENE from a config
- Reuse `StructureFactory`/`CastleHubBuilder` geometry + `GridWallBuilder` + navmesh bake — output an
  additively-loadable scene (the same shape the seam connectors already load).
- Editor MenuItem per variant + **batchmode** (`Defenders/Build/Base/<Variant>`). FlowTrace the build.

### 3. Garrison spawner (enemy scenes)
- Populate from `BaseConfig` garrison via the **existing** `WaveManager` composition + `EnemyFactory`
  (do NOT write a parallel spawner). The "regulars + boss" outpost design = role-triangle garrison +
  a boss-tier capstone. Level-scale via the existing `battleScaling`.

## Acceptance
- [ ] One `BaseConfig` asset → one reproducible scene (player or enemy) via Editor menu + batchmode.
- [ ] An enemy variant spawns a role-triangle garrison (tank + kiting mage + DPS) + a boss.
- [ ] Walls/towers/props all come from the EXISTING systems (no duplicated placement code).
- [ ] NavMesh bake-ready after generation; scene loads additively through the existing seam path.
- [ ] A player variant generates a buildable/owned base with no garrison.
- [ ] 3–4 example configs; AutoPilot can raid a generated outpost (ties to WO-452).

## Why it matters (depth, owner 2026-06-13)
The role-triangle garrison (tanks soak, kiting mages pressure from range, DPS swarm, boss anchors)
turns each camp from a shooting gallery into a **battle you read and prioritise**. "Player base or
enemy outpost is a scene" is the abstraction that makes one generator serve the whole loop —
raid an enemy scene, or build your own.
