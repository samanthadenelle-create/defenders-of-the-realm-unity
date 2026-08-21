> ## RECONCILED 2026-08-08 - true status is PARTIAL
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: this is an umbrella index. Its own precondition - the WO-884 facade - never landed, so children 886-893 wired straight to VFXManager instead, and this WO's "LOCKED contract" was silently voided by the very WOs it indexes.
> The previous Status line read "READY TO IMPLEMENT (after WO-884 Phase 0 platform + P1 land)" and was wrong.

# WORK ORDER 885 — VFX registry wiring: all remaining domains (phases 2–7)

**Status:** CLOSED — DEPRECATED, audit-verified obsolete (2026-08-21 backlog audit).

**Status:** PARTIAL (reconciled 2026-08-08) — umbrella index; the WO-884 Phase 0 platform precondition never landed
**Silo:** Village combat / VFX / economy / structures / dungeon
**PO:** Samantha (owner)
**Author:** UI seat · **For:** CLAUDE CLI (sole committer, build-verifier)
**Date:** 2026-08-05

**This WO is the UMBRELLA INDEX for every registry domain NOT covered by WO-884's P1 five. Each domain has
its own detailed WO with files + clear acceptance criteria below; this doc holds the shared context + sequencing.**

## Index — per-domain WOs (each self-contained, with acceptance criteria)
| Phase | WO | Domain |
|-------|----|--------|
| 2 | **WO-886** | Enemy death ladder |
| 3 | **WO-887** | On-hit surface + element impacts |
| 4 | **WO-888** | Heal + HP-state + item auras (colourblind fix) |
| 5 | **WO-889** | Persistent combat auras + loop-budget guard (nearest-N) |
| 6 | **WO-890** | Harvest resource auras + ready-to-collect beacon |
| 6 | **WO-891** | Healer structure + reusable structure pattern |
| 6b | **WO-892** | Building damage state (smoke→fire→critical beacon) |
| 7 | **WO-893** | Portals + spawn tiers + materialize/dissolve |

Plus already-clear: **WO-884** (facade platform + P1 five), **WO-909** (Mage/Ranger).

**Depends on / reads:**
- **WO-884 §0.2** — the LOCKED contract (facade → `VfxElementTables` → `VFXManager`; prefab builder policy; loop-budget numbers; enum LANDED). Non-negotiable; do not re-litigate.
- **`docs/vfx/VFX_PREFAB_HANDBOOK.md`** — the Step 1–8 pipeline for each prefab (measure Family → CopyAsset → Resources → catalog IsLoop → facade). **Follow it per recipe.**
- **`docs/vfx/VFX_CREATIVE_PICKS_REGISTRY.md`** — the ratified pick per moment (the "what it looks like").
- **Enum:** all needed `VFXType` values already LANDED (WO-884 §0.2). **Reference names only — never mint.**

**How to use this WO:** each phase below = a set of registry rows. For each, run the handbook Step 1–8
(builder-copy the pack recipe → catalog Map row → `VfxElementTables`/call-site). This WO gives the **scope,
files, and acceptance**; the registry gives the recipe; the handbook gives the mechanics.

**Global guardrails (from WO-884 §0.2 — apply to every phase):** builder-copy pack → committed `Resources/VFX`
(never gitignored path for shipped); keep multi-layer prefabs whole; append-only enum already landed; one bus;
FlowTrace new play paths; colourblind = shape/motion not colour; loop cap scene-tiered (village 24 / dungeon 48 /
boss 32) + nearest-N (6–8) on enemy/pet auras only, FlowTrace on cull.

---

## Phase 2 — DEATH LADDER  (registry §5 · burst-heavy, low loop pressure)

**Scope:** repoint each `Death_*` to its pack recipe with readable tier escalation (trash → boss set-piece).
**Recipes (registry §5):** Death_Generic→SmallExplosion · Death_Skeleton→SparksEffect+SmokeEffect wisp ·
Death_Wolf→SparksEffect+slow Steam · Death_Tiefling→SmallExplosion+WildFire lick · Death_Brute→DustExplosion ·
Death_EnemyExplosion_Dungeon→EnergyExplosion · Elite_Death→EnergyExplosion+SmokeEffect · Boss_Death/Death_Boss→**BigExplosion (8-layer, whole)**+WildFire/SmokeEffect linger.
**Builders:** SmallExplosion, BigExplosion, DustExplosion, EnergyExplosion, SparksEffect, SmokeEffect, WildFire → `Resources/VFX/Death/`.
**Files:** `VFXManager.cs` death `case` block, `VfxPool.SpawnDeathBurst`, `EliteVFXController.OnEliteDeath`, `Enemy.cs` death path.
**Accept:** tier reads escalate by scale/motion (trash ≠ boss); Boss_Death = 8-layer + lingering column + existing 0.7 shake; both `Death_Boss` legacy alias and `Boss_Death` point at BigExplosion; `*_BUILD_OK`+`VFX_CATALOG_OK`; headless death-burst screenshot.

## Phase 3 — ON-HIT SURFACES  (registry §4 · burst)

**Scope:** weapon/attack connect by surface + element.
**Recipes:** flesh→FleshImpacts · metal→MetalImpacts · stone→StoneImpacts · wood→WoodImpacts · dirt→SandImpacts · generic physical→SmallExplosion · fire→TinyExplosion+TinyFlames · ice→IceLance shards · arcane→EnergyExplosion · nature→GoopSpray+puddle · ranged release→MuzzleFlash.
**Builders:** the surface Impact set → `Resources/VFX/Impact/`. (FleshImpacts etc. are hybrid — force `IsLoop=false`, handbook §5.2 note.)
**Files:** melee/projectile hit resolution (surface/element detection), `TowerCombat.OnProjectileImpact`, `HeroAbilities` impact site.
**Accept:** correct surface plays per material; paired SfxId fires (VfxToSfx); no loop leak (all IsLoop=false); headless hit screenshot.

## Phase 4 — HEAL + HP-STATE + ITEM AURAS  (registry §6a/6b/6c · fixes the red-vignette accessibility bug — PROMOTE before Phase 2 if owner wants accessibility first)

**Scope:** heal moments, HP-state world auras (the colourblind fix), gear-granted auras.
**Recipes:** Cast_Heal→RisingSteam column · Impact_Heal→FireFlies upward · Regen→RisingSteam low loop · shell→HeatDistortion+DustMotes shell · manaweave→DustMotes inward · Aura_LowHealth→SmokeEffect gutter · Aura_NearDeath→TinyFlames fast gutter · Aura_HealingInProgress→RisingSteam · Aura_ItemHeal→RisingSteam + elemental weapon auras reuse Aura_Flame/Ice/EnemyCaster faint.
**Builders:** RisingSteam, FireFlies, SmokeEffect, TinyFlames, DustMotesEffect, HeatDistortion → `Resources/VFX/Aura|Heal/`.
**Files:** `HeroHealth.cs` (`UpdateInjuredState` L1166, `RegenTick` L1107 — drive emitter pulse off severity; **demote `HeroInjuredVignette` to secondary**); new **`GearAura`** held-loop component attached in `GearVisualApplier.Apply` L41 (mirror `ArcaneAura`/`Pets/AuraController`).
**Accept:** low-HP reads by pulse-rate/guttering shape WITHOUT the red vignette (owner colourblind); heal reads by rising shape; item heal-aura holds while equipped, Stop on unequip; HP auras mutually exclusive; headless low-HP + heal screenshots.

## Phase 5 — COMBAT AURAS + nearest-N  (registry §6d · ONLY after nearest-N exists)

**Scope:** persistent enemy/pet/tower/Heart/boss-phase auras + the budget guard.
**Recipes:** Aura_EnemyCaster→ElectricalSparks · Aura_Necromancer→PoisonGas · Aura_Healer→RisingSteam · Aura_Flame→TinyFlames · Aura_Ice→DustMotes **cold motion (slow drift/settle, NOT upward)** · Aura_Dust→GroundFog · Aura_SmokeReaper→SmokeEffect · Aura_HeartPulse→FireFlies (combat/raid Hearts only) · Aura_EmpowerTower→RisingSteam tinted · Pet L1/2/3→DustMotes→FireFlies→FireFlies+Sparks · Boss_Aura_Phase1/2/3→RisingSteam→MediumFlames→WildFire.
**FIRST — the guard:** scene-tier `_maxActiveLoops` (24/48/32) + **nearest-N (6–8) on enemy/pet auras**, reusing the `PoiCalloutSystem` nearest-N pattern; FlowTrace-throttle-log on cull. Build this BEFORE mass aura wiring.
**Files:** enemy aura attach (EnemyBrain/EliteVFXController), `PetAuraVFX`, `ArcaneAura` (tower), `HeartAuraController`, DragonBoss phase auras; the loop-cap/nearest-N gate.
**Accept:** dressed dungeon + wolf pack does NOT silently drop auras (cull logs instead); Ice reads cold (drift, not firefly); boss phases escalate calm→enraged→seething; headless dungeon aura screenshot.

## Phase 6 — HARVEST + STRUCTURES  (registry §6e/6f)

**Scope (harvest):** per-resource harvest aura + ready-to-collect beacon.
**Recipes:** Harvest_Iron→DustMotes+SparksEffect (settle+glint) · Harvest_Wood→DustMotes flat drift · Harvest_Food→FireFlies rising · Harvest_Crystal→FireFlies suspended · Harvest_Gold→SparksEffect falling · Collector_Ready→FireFlies rising bob (reuse SfxId.LevelUp).
**Files (harvest):** `NodeFillIndicator` (collecting/ready states host the aura), `CollectorStackView` (decorate the existing full tell with the ready beacon — do NOT rebuild it).
**Scope (structures):** the Healer + the general pattern.
**Recipes:** Healer = idle `Aura_Healer` RisingSteam field + per-tick `Impact_Heal` FireFlies cast pulse (**telegraphs-as-casting**) + heal contact FireFlies. General pattern: new structure = stats + `behaviorId` `case` + `VfxEmitter{Aura,element}` + `Vfx.On(this).AddImpact(element).At(...)` per tick — Healer=Holy, Slow-field=Ice, Damage-aura=Shadow, Buffer=Arcane.
**Files (structures):** `StructureFactory.AttachBehaviorImpl` (new `case "HealerTower"`, clone `HealingFountain` tick body retargeted Heart→units-in-radius).
**Accept:** each resource aura reads distinct by MOTION (iron settles, crystal hangs, gold falls); ready beacon rises + reuses the existing full tell; Healer pulses a visible cast each tick then heals in-radius allies; nearest-N gates the harvest auras; headless harvest + healer screenshots.

## Phase 6b — BUILDING DAMAGE STATE  (registry §6g · re-skin, don't rebuild)

**Scope:** richer smoke→fire escalation + the missing **critical-save beacon**.
**Recipes:** smolder(≤0.5)→SmokeEffect low · fire(≤0.25)→MediumFlames+SmokeEffect · **CRITICAL beacon (NEW at fire threshold)→SparksEffect fast-pulse + "!" tag** (alarm cadence) · broken→DustExplosion/BigExplosion + WildFire/SmokeEffect linger.
**Files:** `StructureDamageVisuals.cs` — **re-point its recipe keys only (data-driven, no observer rewrite)** + add the critical beacon at the fire threshold; `damage-states.json` if a threshold/key field is needed.
**Accept:** damaged buildings escalate smoke→fire readably; a critical building shows an unmistakable "repair me NOW" beacon (fast pulse, not colour); broken = explosion + smoking ruin; burn loops stay worst-first capped; headless damaged-building screenshot.

## Phase 7 — PORTALS + SPAWN + DISSOLVE  (registry §7)

**Scope:** portal open/enter/exit, spawn tiers, materialize/despawn.
**Recipes:** Env_DungeonPortal→keep procedural vortex + **SECONDARY** MediumFlames accent (don't blur with fireballs) · Portal_Enter→EnergyExplosion outward · Portal_Exit→EnergyExplosion inward · Enemy_Spawn→Respawn via SpawnEffect (**one-shot, no demo loop**) · Elite_Spawn→EnergyExplosion up · Boss_Spawn→BigExplosion+LightningStormCloud accent · summon→Respawn+ground swell · Despawn_Dissolve→Dissolve via SpawnEffect reversed (**one-shot**).
**Builders:** EnergyExplosion (Portal), Respawn/Dissolve (scripted — commit SpawnEffect shader + `_cutoff` material to Resources; missing→plain burst).
**Files:** `PortalVFXController`, spawn path (WaveManager/EliteVFXController), `SpawnEffect.cs` (one-shot wrap).
**Accept:** portal enter/exit mirror by motion (out vs in); scripted dissolve plays ONCE (no demo pause-loop); standard enemy spawn no longer pops from nothing; headless portal + spawn screenshots.

---

## Sequencing (WO-884 §0.2)
Phase 0 (WO-884 platform) → **P1 five (WO-884)** → **this WO: 2 → 3 → 4 → 5 → 6 → 6b → 7**.
Owner may promote Phase 4 before Phase 2 if the low-HP vignette accessibility fix is wanted first.

## Gates (every phase)
`COMPILE_GATE_OK` + builder `*_BUILD_OK` + `VFX_CATALOG_OK` + `REGRESSION_OK`; new play paths FlowTrace-instrumented;
no nullref on missing socket/bone; **headless screenshot-verify each phase (open the PNGs) before handing to owner.**

## RESULT
`WorkOrders/WORK_ORDER_885_vfx_registry_wiring_all_domains.RESULT.md` — per-phase: builders run, catalog rows,
call sites, markers, the loop-cap/nearest-N numbers used, and the phase screenshots. Owner felt-verifies + closes.

> **AUDIT 2026-08-21 (agent fleet, read-only):** DEPRECATED. Evidence: `VfxFacade.cs absent; children shipped direct` — contract voided. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
