> ## RECONCILED 2026-08-08 - true status is PARTIAL (stale index)
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: this index presents 873-883 as one uniform READY block; the truth is 6 shipped, 4 never started, 1 blocked on an owner ruling.
> The previous Status line read "READY (master/index). Audit-backed 2026-08-04 (read-only agent)." and was wrong.
> WARNING: do NOT plan off this index as written. Doing so will BOTH re-do finished work (878-883 shipped) AND skip unstarted work (875, 876, 877 never began; 874 is blocked on the owner). Check each child WO's own reconciled banner before scheduling anything here.

# WORK ORDER 872 — Combat VFX + Animation pass — MASTER

**Status:** PARTIAL (stale index) — reconciled 2026-08-08 (master/index). Audit-backed 2026-08-04 (read-only agent).
**Author:** UI/QA triage + audit (read-only, §13) — Claude UI
**Lane:** VFX + Animation (Combat/AI + Buildings + Hero). **WO#:** UI-seat block; **872**=this. Children below.
**Origin:** owner 2026-08-04 — *"I want the vfx to work well … from casting to projectiles from towers by type
(archer/arcane/ballista) and level (L1→L2→L3), on-hit with troops, cast-on-magic … look over ALL animations and add
rework everywhere."* Asset source: `docs/asset-inventory/04_vfx_spells_audio.md` + `01_kaykit.md`.

---

## 1. ⚠ Read first — the VFX architecture (name your LAYER)
It is NOT "38 of 1000 wired." There are **4 layers / 3 catalogs** — every child WO must state which it targets:
| Layer | Router | Catalog | Notes |
|---|---|---|---|
| **A. VFXType enum** | `VFXManager.Play(VFXType.X)` | `Resources/VFX/VFXCatalog.asset` (Lana + 5 Spells) | 45/78; built-in shaders → healed by `ProofUrpParticleShaders` |
| **B. Hovl string-key (ACTIVE path)** | `VFXManager.PlayKey("Key", …)` | `Resources/VFX/HovlVfxCatalog.asset` (~140 rows, Hovl + `PP_*`) | URP-clean; owner-tagged per-type/tier keys live HERE |
| **C. Projectile catalog** | `ProjectileVFXCatalog.SpawnFlying/Impact` | `Resources/VFX/Projectiles/` (9 Spells) | tower + hero projectile bodies |
| **D. Procedural fallback** | `AbilityVfxKit.*` | code-built | fires when a catalog row is null |
**Owned but 0% wired:** Mirza Beig (564), ~455 Spells Pack, most Hovl AAA families. **Reuse, author none;
owner-tags-the-key / CLI-maps-verbatim; route via `VFXManager`; WO-753 teardown.** One residual magenta hole:
`WeatherManager` Instantiates bypass the shader proofer (`WeatherManager.cs:363,553,561`).

## 2. The children (each a slice of the rework list; ordered by player-felt impact)
- **WO-870 — Tower cast→projectile→impact by TYPE × TIER** (rescoped from "tower fire"). Owner already tagged the
  per-type/per-tier Hovl keys; call sites wire only a subset (only ground-archer-None tiers; muzzle/impact never
  tier). Replace primitive projectile bodies; fix **ArcaneTower renders Fire while dealing Aether**
  (`ArcaneTower.cs:67`). Layer B+C. Files: `DefenseTower.cs`, `ArcaneTower.cs`, `ProjectileKeyFor/MuzzleVfxFor/ImpactKeyFor`.
- **WO-873 — Enemy death + melee-impact VFX** (highest player-felt). Regular enemies use ONE generic grey death
  burst (no per-species `Death_*`) and land melee hits with ZERO impact VFX. Wire `Enemy.Die()` per species
  (`Enemy.cs:2547-2565`) + on-landing impact (`Enemy.cs:1554`). Layer A/B.
- **WO-874 — Elite/Boss VFX: wire or kill `EliteVFXController`** (fully written, NEVER attached → all elite/boss
  spawn/aura/attack/death differentiation is dead). AddComponent it on the elite/boss spawn path OR delete + fold
  into DragonBoss; add DragonBoss `Boss_Spawn` entrance (`DragonBoss.cs`). Layer B/D.
- **WO-875 — Hero cast VFX (element + windup).** `RegistryOnlyMotionVfx=true` (`HeroAbilities.cs:1887`) + 14/17 empty
  registry `vfxKey` rows = hero casts are largely SILENT. Un-gate the built-but-disabled `SpellVfxFactory.PlayCast`
  (`HeroAbilities.cs:2350/2363`) for element-coded flashes; add `SpawnCastWindup` at cast-start (`:601`) for a
  telegraph. Layer B/D. (Feeds WO-861 Thrain/Sylas cast VFX.)
- **WO-876 — Troop combat VFX + ranged projectile.** Troops have NO impact/death VFX; Archer troops have NO
  projectile (instant damage). Reuse the tower `ProjectileVFXCatalog`/`Impact_*` stack at `TroopController.cs:501-543`.
  Layer B/C.
- **WO-877 — Animation placeholders.** Ranger's 4 ability casts all reuse ONE `Ranger_Aim_Idle`
  (`HeroAnimatorFactory.cs:216`); KayKit vendor/drillmaster NPCs are idle-only on a single-point-of-failure controller
  (T-pose risk, owner F8 2026-08-02); retire the stale `AnimatorSetup.Hero/Npc/Pet` parallel. Retarget from the owned
  401 Mixamo + KayKit libs. Silo: Art/animation. (Build-worker anim = WO-871.)

## 3. Owner decisions — RESOLVED (owner 2026-08-04)
1. ✅ **Tower System B is DEAD legacy — do NOT touch it.** System A (`DefenseTower`/`ArcaneTower`) is the only live
   tower path. (T9–T11 in WO-870 are out of scope.)
2. ✅ **Boss/elite VFX = WIRE it** — `AddComponent<EliteVFXController>` in the spawn path + map the `Boss_*`/`Elite_*`
   rows to real Mirza Beig prefabs + DragonBoss spawn entrance (WO-874, the "wire" path; NOT the kill path).
3. ✅ **ArcaneTower renders AETHER** — fix the visuals to match its Aether damage (WO-870 T7); it is NOT thematically fire.

## 4. Cross-cutting rules (all children)
Reuse owned prefabs (author none); `VFXManager` routing (never raw Instantiate); owner-tags-key/CLI-maps-verbatim
(memory `vfx-map-owner-tags-no-creative-pick`); WO-753 one-owner teardown; ASCII/colourblind unaffected (VFX/anim);
verify on the Seeker (headless can't judge VFX/anim). Ref: `docs/audits/AUDIT_vfx_2026-06-28.md` §2 has a ready map.
