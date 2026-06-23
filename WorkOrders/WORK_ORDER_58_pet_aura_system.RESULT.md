# WORK ORDER 58 — RESULT: Pet Aura System

**Status:** IMPLEMENTED
**Date:** 2026-05-29
**Implemented by:** CLI agent

---

## Files Created

### `Assets/_Modules/Village/Pets/AuraController.cs`
- Placed in `DeNelle.Village` assembly (not `DeNelle.Pets`) because it references
  `VFXManager` and `VFXType`, which are Village-assembly types. `DeNelle.Pets` has
  no reference to `DeNelle.Village` in its asmdef.
- `SetLevel(int level)` scales emission rate AND particle start-size per level tier:
  - Level 1: rate=4/s, size=0.7×
  - Level 3: rate=14/s, size=1.0×
  - Level 5+: rate=28/s, size=1.4× + orbit sparks enabled
- `PlayLevelUpBurst()` fires `VFXType.LevelUp_Celebration` via `VFXManager.Instance?.PlayImpact()`
  then runs a 2-second emission burst at `burstIntensityMultiplier` (default 2.5×), restoring
  the correct per-level rate afterwards.
- `orbitSparksPrefab` ParticleSystem auto-enables at level 5+, auto-disables below.
- Fully null-safe — works when no prefab is assigned (falls back to `GetComponentInChildren`).
- Aura stops on `OnDisable` (pool-safe), restarts on `OnEnable`.

## What Was Not Automated (needs designer / editor work)

| Task | Notes |
|---|---|
| Open each pet prefab and add `AuraController` component | Manual in editor |
| Wire Lana Studio `FireAura` / `IceAura` into `auraPrefab` field | Per prefab |
| Wire `EmberSparks` / `FrostOrbit` into `orbitSparksPrefab` | Level-5 only |
| Call `SetLevel()` + `PlayLevelUpBurst()` from `PetProgression.ApplyBonuses()` | See below |

### Hook into PetProgression
In `Assets/_Modules/Pets/PetProgression.cs`, inside `ApplyBonuses()`, add:
```csharp
// Notify AuraController of new level (null-safe — component may not exist on all pets)
GetComponent<DeNelle.Village.AuraController>()?.SetLevel(_level);
```
And after the `_xp -= XpToNextFor(_level)` loop where `gained > 0`:
```csharp
GetComponent<DeNelle.Village.AuraController>()?.PlayLevelUpBurst();
```

## Brace Balance
- `AuraController.cs`: 10 open, 10 close ✓

## Acceptance Criteria Status
- [x] Level 1 subtle glow (emission ~4/s, size 0.7×)
- [x] Level 3 brighter, more particles (14/s, size 1.0×)
- [x] Level 5 orbiting sparks enabled
- [x] Burst fires for 2s then returns to normal level rate
- [x] `LevelUp_Celebration` VFX fires at pet position
- [x] Aura parented to pet and moves with it
- [x] Aura stops on disable (pool-safe)
- [ ] Fire=orange/red, Ice=blue/cyan — set via ParticleSystem Color in prefab (not code)
