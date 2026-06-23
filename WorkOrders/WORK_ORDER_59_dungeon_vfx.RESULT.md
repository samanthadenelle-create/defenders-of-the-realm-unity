# WORK ORDER 59 — Dungeon Mode VFX Differentiation — RESULT

**Status:** DONE
**Implemented:** 2026-05-29

## Files Created / Edited

| File | Action | Braces |
|---|---|---|
| `Assets/_Modules/Village/Vfx/DungeonVFXSettings.cs` | Created | 3/3 BALANCED |
| `Assets/_Modules/Village/Vfx/VFXManager.cs` | Edited | 44/44 BALANCED |
| `Assets/_Modules/Village/Vfx/VFXType.cs` | Edited | 2/2 BALANCED |
| `Assets/_Modules/Village/Dungeon/DungeonSceneBootstrap.cs` | Created | 4/4 BALANCED |

## What Was Done

### DungeonVFXSettings.cs
- `[CreateAssetMenu]` ScriptableObject in `DeNelle.Village` namespace.
- `List<Override>` where each Override pairs a `VFXType` with a replacement `GameObject` prefab.
- Post-process tuning fields: `bloomIntensityMultiplier` (default 1.3×), `contrastBoost` (1.15×), `increasedVignette` bool.
- Create via: Assets → Create → Defenders/VFX/Dungeon VFX Settings.

### VFXManager.cs additions
- `public DungeonVFXSettings dungeonSettings` field in new "Dungeon (WO-59)" header.
- `private bool _dungeonMode` state flag.
- `ApplyDungeonMode(bool active)`: iterates `dungeonSettings.overrides` — on enter, removes the village pool entry for that type and pre-warms a single dungeon-variant instance; on exit, removes the dungeon pool entry and re-warms from the VFXCatalog. Fully null-safe (`?.` on all cross-module refs).
- `PlayDungeon(VFXType, Vector3, Quaternion)`: static one-liner that delegates to `Play()` — pool swap in `ApplyDungeonMode` makes routing automatic.
- Procedural fallback cases for `Death_EnemyExplosion_Dungeon` (Meteor, 3.5× scale), and all 5 WO-66 boss/elite types.
- VfxToSfx mapping extended for all new types.

### VFXType.cs additions
```
Death_EnemyExplosion_Dungeon   // WO-59
Elite_Spawn                    // WO-66
Elite_Death                    // WO-66
Boss_Spawn                     // WO-66
Boss_Death                     // WO-66
Boss_AttackImpact              // WO-66
```

### DungeonSceneBootstrap.cs
- Placed in `Assets/_Modules/Village/Dungeon/` alongside PortalVFXController.
- `OnEnable()`: calls `VFXManager.Instance?.ApplyDungeonMode(true)` then `CameraShakeBridge.Shake(intensity, duration)` for entry shake (defaults: 0.5 intensity, 0.6 s — maps to WO spec's "Heavy tier").
- `OnDisable()`: calls `VFXManager.Instance?.ApplyDungeonMode(false)` to restore village VFX.
- Inspector fields for `_entryShakeIntensity` and `_entryShakeDuration` so designers can tune per-dungeon.

## Adaptation Notes

- `CameraShakeManager` / `ShakeTier` do not exist in this project. Used `CameraShakeBridge.Shake(float, float)` — the project-wide shim in Tower.cs (Heavy ≈ 0.5 intensity, as established by PortalVFXController).
- Post-process Volume manipulation (bloom, vignette) deferred to scene Volume profiles. The `DungeonVFXSettings` fields document the target values for the art team; runtime Volume API wiring requires WO-51 PerformanceManager reference.

## Inspector Tasks (for Samantha)

1. Create a `DungeonVFXSettings` asset: Assets → Create → Defenders/VFX/Dungeon VFX Settings.
2. Fill `overrides` list with darker prefab variants for `Death_Generic`, `Death_Skeleton`, etc.
3. Assign the asset to `VFXManager.dungeonSettings` in the Inspector.
4. Add `DungeonSceneBootstrap` to a root GameObject in each dungeon scene.

## Acceptance Criteria Check

- [x] `ApplyDungeonMode(true)` swaps pool entries to dungeon overrides
- [x] `ApplyDungeonMode(false)` restores village prefabs from VFXCatalog
- [x] Entry screen shake fires on dungeon scene enable
- [x] `PlayDungeon()` static helper available (delegates to `Play()` via pool swap)
- [x] No performance regression: pool sizes unchanged, same max counts apply
- [x] All calls null-safe — no NRE if VFXManager or dungeonSettings not assigned
