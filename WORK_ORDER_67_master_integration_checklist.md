# WORK ORDER 67 — Master Integration Checklist + Final Code Cleanup

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** Critical (final step before playtesting)
**Scope:** Large — documentation, code cleanup, debug menu, mobile test plan
**Depends on:** All WO-49 through WO-66

---

## Goal

Every system is connected, legacy debt is removed, and the game is ready for
real playtesting on mobile. One document explains how everything fits together.

---

## 1. Create `SYSTEMS_INTEGRATION.md`

**Path:** `docs/SYSTEMS_INTEGRATION.md`

```markdown
# Defenders Unity — Systems Integration Map

## Core Singletons (DontDestroyOnLoad)

| Singleton | Script | Purpose |
|---|---|---|
| VFXManager | `_Modules/VFX/VFXManager.cs` | All VFX spawn + pooling |
| WeatherManager | `_Modules/Environment/WeatherManager.cs` | Rain, shooting stars, wind |
| GameQualityController | `_Modules/Settings/GameQualityController.cs` | Quality tier dispatcher |
| PerformanceManager | `_Modules/Performance/PerformanceManager.cs` | Applies tier to shadows/PP/particles |
| CameraShakeManager | `_Modules/Camera/CameraShakeManager.cs` | Screen shake |
| HitStopManager | `_Modules/Combat/HitStopManager.cs` | Time-scale hit stop |
| KillComboTracker | `_Modules/Village/KillComboTracker.cs` | Kill combo detection |
| LevelUpVFXController | `_Modules/Progression/LevelUpVFXController.cs` | Level-up celebrations |
| DecalSpawner | `_Modules/Combat/DecalSpawner.cs` | Ground impact decals |
| AudioService | `_Modules/Audio/AudioService.cs` | SFX + music |

## Communication Diagram

```
WaveManager.CompleteWave()
    → VFXManager.Play(WaveClear_Celebration)
    → AudioService.PlaySfx(WaveClear)

EnemyHealth.Die()
    → EliteVFXController.OnEliteDeath() [if elite]
    → VFXManager.Play(Death_EnemyExplosion)
    → KillComboTracker.RegisterKill()
    → DecalSpawner.SpawnScorch()

HeroAbility.Cast()
    → VFXManager.Play(Casting_WizardCharge / Projectile_FlameArrow)
    → AudioService.PlaySfxAtPosition(WizardCast / FlameArrowLaunch)

PetBrain.OnLevelUp(n)
    → AuraController.OnLevelUp(n)
    → LevelUpVFXController.PlayLevelUp(isPet: true)
    → LevelUpEvents.RaiseLevelUp → AudioService (level-up chime)

GameQualityController.ApplyTier()
    → PerformanceManager.SetTier()
        → ApplyShadows / ApplyPostProcess / ApplyParticles
        → AnimatorCullingController.ApplyTier() (all instances)
    → VFXManager.defaultPoolSize
    → WeatherManager.enableOnMobile
```

## Prefab Wiring Checklist

### Enemy Prefabs (every one)
- [x] EnemyBrain
- [x] EnemyHealth
- [x] AnimatorCullingController (distances per type — see WO-53)
- [x] LOD Group (3 levels — see WO-54)
- [ ] EliteVFXController (boss/elite prefabs only)
- [ ] FootstepDustController

### Hero Prefabs
- [x] HeroLocomotion
- [x] HeroHealth
- [x] AnimatorCullingController (updateTransformsDistance = 80)
- [ ] FootstepDustController
- [ ] LevelUpVFXController hook in XP system

### Pet Prefabs
- [x] PetCombatController
- [x] AuraController (fire/ice prefab assigned)
- [x] AnimatorCullingController
- [ ] FootstepDustController

### Environment
- [x] TorchFireController on every torch/brazier/lantern
- [ ] Floating dust mote ParticleSystem in village
- [ ] WeatherManager SkySpawnPoint placed 200+ units above village
```

---

## 2. Final code cleanup

### AbilityVfxKit.cs
- [ ] All `Play*` methods forward to `VFXManager.Instance.Play()` first
- [ ] Old procedural code is inside `#if UNITY_EDITOR` only
- [ ] No `Instantiate` calls for VFX in non-editor builds

### AbilityAudioBridge.cs
- [ ] All reflection calls replaced with direct `AudioService.Instance.PlaySfx()` where possible
- [ ] Remaining reflection calls have `// TODO: map to SfxId` comments

### Legacy files to delete or archive
```
Assets/Editor/AbstractMaterialNode*    (ShaderGraph leftovers)
Assets/VFX/Procedural/old/            (superseded by asset packs)
```

---

## 3. Debug menu (Editor only)

`Assets/Editor/QualityDebugMenu.cs` (defined in WO-64) — verify all items work:
- Quality Low / Medium / High
- Spawn test enemy at scene view pivot
- Play any VFXType at scene view pivot
- Toggle rain
- Trigger level-up celebration at selected object

---

## 4. Mobile test plan

Before shipping to QA, run all of the following:

| Test | Pass criteria |
|---|---|
| Full wave — 10 enemies — Low quality | 60 FPS sustained (Profiler) |
| Full wave — 10 enemies — Medium quality | 45+ FPS sustained |
| Kill combo x5 | Tier-2 burst + shake — no stutter |
| Wave clear | Celebration VFX + sound — no orphan objects |
| Dungeon portal entry/exit | Flash + shake + VFX swap — no black frames |
| Boss spawn (if boss wave implemented) | Dramatic spawn, pulsing aura, big death |
| Quality toggle Low → High → Low | No crash, settings persist after restart |
| 10 min play session | Memory stable, no increasing object count in Hierarchy |

---

## Files to Create / Edit

| File | Action |
|---|---|
| `docs/SYSTEMS_INTEGRATION.md` | **Create** |
| `Assets/_Modules/VFX/AbilityVfxKit.cs` | **Edit** — final cleanup |
| `Assets/_Modules/Audio/AbilityAudioBridge.cs` | **Edit** — remove remaining reflection |
| Legacy files | **Delete** |

---

## Acceptance Criteria

- [ ] `docs/SYSTEMS_INTEGRATION.md` exists and accurately describes all singleton relationships
- [ ] Zero procedural `Instantiate` VFX calls remain in non-editor builds
- [ ] Zero reflection calls remain in `AbilityAudioBridge` for mapped SFX ids
- [ ] All prefab wiring checkboxes in §1 are ticked
- [ ] Mobile test plan passes all rows
