# Defenders of the Realm — Overnight Batch Assignment

**Date:** 2026-05-28
**For:** Claude Code CLI
**Standard:** Follow the format and best practices established throughout all WO files.
**Rule:** Read each WO file completely before writing any code.

---

## How to Run This Batch

These WOs are grouped so each **Parallel Group** can be started simultaneously
because no WO within the same group touches the same file as another.
Run them in batch order (Group A first, then B, etc.) since later groups depend
on files created in earlier ones.

After each group: `git add` new `.cs` files and their `.meta` files only,
then commit with `feat: batch-[letter] WO-XX WO-XX ...`.

Do NOT stage Unity auto-generated files, `.asset` files, or scene files.
Do NOT modify `CLAUDE.md`.

---

## GROUP A0 — Data Foundation (Run FIRST — everything depends on this)

These two WOs create the base data layer. No other WO can run until both are done.

| # | WO File | Action | New Files |
|---|---|---|---|
| A0-1 | `WORK_ORDER_86_scriptableobject_data_architecture.md` | CREATE | `TowerData.cs`, `EnemyData.cs`, `AbilityData.cs`, `WaveData.cs`, `PetData.cs` |
| A0-2 | `WORK_ORDER_88_itemdata_object_spawner.md` | CREATE + EDIT | `ItemData.cs`, `ProjectileData.cs`, `ObjectSpawner.cs`; refactor Data SOs to inherit `ItemData` |

**A0-1 and A0-2 touch no shared files — run simultaneously.**

**Commit message:** `feat: batch-A0 ScriptableObjects ItemData ObjectSpawner`

---

## GROUP A — Foundation (Run after A0, can be parallel within group)

These create entirely new files with no dependency on each other.

| # | WO File | Action | New Files |
|---|---|---|---|
| A1 | `WORK_ORDER_69_enemy_pet_combat_overhaul.md` | REPLACE `EnemyBrain.cs` | `EnemyBrain.cs`, `PetCombatController.cs` |
| A2 | `WORK_ORDER_50_vfx_manager.md` | CREATE | `VFXManager.cs`, `VFXAutoReturn.cs`, `VFXCatalog.cs` |
| A3 | `WORK_ORDER_68_atb_system_fix.md` | CREATE | `ATBCombatManager.cs` |

**A1, A2, A3 touch no shared files — run simultaneously.**

**Commit message:** `feat: batch-A enemy-brain VFXManager ATBCombatManager`

---

## GROUP B — Core Health + Damage Pipeline (depends on Group A)

| # | WO File | Action | New Files | Edited Files |
|---|---|---|---|---|
| B1 | `WORK_ORDER_70_final_combat_system.md` | CREATE + EDIT | `HeroHealth.cs`, `EnemyHealth.cs` | `PetCombatController.cs` (use EnemyHealth) |
| B2 | `WORK_ORDER_61_ground_decals_hit_polish.md` | CREATE | `CameraShakeManager.cs`, `HitStopManager.cs`, `DecalSpawner.cs`, `FootstepDustController.cs` | — |
| B3 | `WORK_ORDER_55_torch_fire_polish.md` | CREATE | `TorchFireController.cs` | — |

**B1, B2, B3 touch no shared files — run simultaneously.**

**Commit message:** `feat: batch-B HeroHealth EnemyHealth CameraShake Torches`

---

## GROUP C — Combat Feel (depends on Group B)

| # | WO File | Action | New Files | Edited Files |
|---|---|---|---|---|
| C1 | `WORK_ORDER_81_hero_combat_overhaul.md` | CREATE + EDIT | `HeroCombatController.cs`, `HeroHitReaction.cs`, `AbilityCooldownUI.cs` | `HeroLocomotion.cs` (acceleration model) |
| C2 | `WORK_ORDER_84_enemy_death_hit_reactions.md` | CREATE + EDIT | `EnemyHitReaction.cs` | `EnemyHealth.cs` (add `_hitReaction?.React()`, enhance `Die()`) |
| C3 | `WORK_ORDER_65_portal_vfx.md` | CREATE | `PortalVFXController.cs` | — |
| C4 | `WORK_ORDER_66_boss_vfx.md` | CREATE | `EliteVFXController.cs` | — |

**C1 edits `HeroLocomotion.cs`. C2 edits `EnemyHealth.cs`. C3, C4 create new files.**
**No shared file conflicts — run all four simultaneously.**

**Commit message:** `feat: batch-C hero-combat EnemyHitReaction EliteVFX PortalVFX`

---

## GROUP D — VFX Integration + Towers (depends on Groups A + C)

| # | WO File | Action | New Files | Edited Files |
|---|---|---|---|---|
| D1 | `WORK_ORDER_56_vfxmanager_integration.md` | EDIT | — | Wizard, Ranger, Knight ability scripts; tower scripts |
| D2 | `WORK_ORDER_82_tower_power_pass.md` | CREATE + EDIT | `TowerVFXController.cs` | `TowerCombat.cs`, `TowerProjectile.cs` |
| D3 | `WORK_ORDER_52_weather_manager.md` | CREATE | `WeatherManager.cs` | — |
| D4 | `WORK_ORDER_58_pet_aura_system.md` | CREATE | `AuraController.cs` | — |
| D5 | `WORK_ORDER_63_levelup_celebration.md` | CREATE | `LevelUpVFXController.cs`, `LevelUpEvents.cs` | — |

**D1 edits ability/tower scripts. D2 edits `TowerCombat.cs` and `TowerProjectile.cs`.**
**D3, D4, D5 create new files — run alongside D1/D2 safely.**
**D1 and D2 edit different files — run simultaneously.**

**Commit message:** `feat: batch-D VFX-integration TowerPower WeatherManager AuraController LevelUp`

---

## GROUP E — Wave Systems + Kill Combo (depends on Group D)

| # | WO File | Action | New Files | Edited Files |
|---|---|---|---|---|
| E1 | `WORK_ORDER_60_wave_clear_celebration.md` | CREATE + EDIT | `KillComboTracker.cs` | `WaveManager.cs` (add combo registration) |
| E2 | `WORK_ORDER_83_wave_clear_kill_combo.md` | CREATE + EDIT | `WaveCelebrationManager.cs` | `KillComboTracker.cs` (add Tier3), `WaveManager.cs` (call celebration) |
| E3 | `WORK_ORDER_59_dungeon_vfx.md` | CREATE + EDIT | `DungeonVFXSettings.cs` | `VFXManager.cs` (add `ApplyDungeonMode`) |

**E1 and E2 both edit `WaveManager.cs` and `KillComboTracker.cs` — implement E1 first, then E2.**
**E3 is independent — run simultaneously with E1.**

**Commit message:** `feat: batch-E KillCombo WaveCelebration DungeonVFX`

---

## GROUP F — Performance + Animation (depends on Group C)

| # | WO File | Action | New Files | Edited Files |
|---|---|---|---|---|
| F1 | `WORK_ORDER_53_animator_culling.md` | CREATE + PREFAB | `AnimatorCullingController.cs` | `EnemyBrain.cs` (add RequireComponent) |
| F2 | `WORK_ORDER_51_mobile_performance.md` | CREATE | `MobilePerformanceSettings.cs`, `PerformanceManager.cs` | — |
| F3 | `WORK_ORDER_57_mobile_quality_settings.md` | CREATE | `MobileQualitySettings.cs`, `QualityToggleUI.cs` | — |
| F4 | `WORK_ORDER_64_master_quality_controller.md` | CREATE | `GameQualityController.cs`, `QualityDebugMenu.cs` | — |

**F1 edits `EnemyBrain.cs` (add `[RequireComponent]` only — 1 line). F2, F3, F4 create new files.**
**Run F1, F2, F3, F4 simultaneously.**

**Commit message:** `feat: batch-F AnimatorCulling MobilePerf QualitySettings`

---

## GROUP G — Data Architecture + Camera (depends on Groups D + F)

| # | WO File | Action | New Files | Edited Files |
|---|---|---|---|---|
| G1 | `WORK_ORDER_86_scriptableobject_data_architecture.md` | CREATE + EDIT | `TowerData.cs`, `EnemyData.cs`, `AbilityData.cs`, `WaveData.cs`, `PetData.cs` | `EnemyBrain.cs`, `EnemyHealth.cs`, `TowerCombat.cs`, `WaveManager.cs`, `PetCombatController.cs` |
| G2 | `WORK_ORDER_87_cinemachine_camera.md` | CREATE + EDIT | `CinemachineCameraController.cs` | `CameraShakeManager.cs` (wrapper delegation) |
| G3 | `WORK_ORDER_62_audio_integration.md` | EDIT | — | `VFXManager.cs` (playSound param), ability/audio bridge scripts |

**G1 edits many files. G2 edits only `CameraShakeManager.cs`. G3 edits `VFXManager.cs`.**
**G1, G2, G3 edit different files — run simultaneously.**

**Commit message:** `feat: batch-G ScriptableObjects Cinemachine AudioIntegration`

---

## GROUP H — Final Scene + Integration (scene work, no code conflicts)

| # | WO File | Action |
|---|---|---|
| H1 | `WORK_ORDER_71_world_implementation_polish.md` | SCENE EDIT — terrain, lighting, NavMesh, foliage, occlusion bake |
| H2 | `WORK_ORDER_85_world_atmosphere_immersion.md` | SCENE EDIT — extends H1, wave-reactive weather, AnimatorCullingAuditor |
| H3 | `WORK_ORDER_67_master_integration_checklist.md` | TARGETED EDIT — cleanup, legacy deletion, docs |

**H1 must complete before H2 (H2 extends the same scene).**
**H3 can run alongside H1 — it's code cleanup only, not scene work.**

**Commit message:** `feat: batch-H world-polish integration-cleanup`

---

## GROUP I — Monetisation (Phase 5 — run only after Groups A–H are green)

Run these sequentially in WO order:

```
WO-72 → WO-73 → WO-74 → WO-75 → WO-76 → WO-77 → WO-78 → WO-79 → WO-80
```

**Commit after each WO individually:** `feat: WO-7X — <title>`

---

## Quality Gates (check before moving to next group)

After each group commit, verify:
- [ ] Unity Editor opens the scene without compile errors
- [ ] Play mode enters without exceptions in the Console
- [ ] The acceptance criteria listed in each WO file are met
- [ ] `git status` shows only `.cs` and `.meta` files staged — no scene files, no `.asset` files, no `Library/`

---

## File Conflict Reference

If two WOs in the same group edit the same file, that's a conflict and they
cannot be parallel. Current known shared-file edits that require sequencing:

| File | Sequential pair |
|---|---|
| `WaveManager.cs` | E1 before E2 |
| `KillComboTracker.cs` | E1 before E2 |
| `EnemyBrain.cs` | G1 after F1 (F1 adds 1 line; G1 adds `data` field reading — apply in that order) |

All other groups have been verified to have zero shared file conflicts.
