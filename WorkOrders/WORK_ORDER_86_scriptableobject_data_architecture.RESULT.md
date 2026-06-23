# WO-86 RESULT — ScriptableObject Data Architecture

**Status:** COMPLETE  
**Date:** 2026-05-29  
**Implemented by:** CLI agent

---

## Files Created

### ScriptableObjects (Assets/Data/)

| File | Namespace | Menu Path |
|---|---|---|
| `Assets/Data/EnemyData.cs` | `DeNelle.Data` | Defenders/Data/Enemy |
| `Assets/Data/WaveData.cs` | `DeNelle.Data` | Defenders/Data/Wave |
| `Assets/Data/AbilityData.cs` | `DeNelle.Data` | Defenders/Data/Ability |
| `Assets/Data/PetData.cs` | `DeNelle.Data` | Defenders/Data/Pet |

**Note on TowerData:** `Assets/_Modules/Core/Data/TowerData.cs` already exists in `DeNelle.Core.Data` with `upgrades[].range`, `upgrades[].damage`, `upgradeCost`, `empowerment`, etc. `TowerCombat.cs` already reads `Tower.CurrentRange`/`Tower.CurrentDamage` live from it. A new `DeNelle.Data.TowerData` was blocked (both assemblies auto-referenced — naming conflict). `Assets/Data/TowerData.cs` was created as a comment-only redirect file. The existing Core TowerData fully satisfies WO-86 intent.

---

## Files Edited

### EnemyBrain.cs (`Assets/_Modules/Village/Enemies/EnemyBrain.cs`)
- Added `using DeNelle.Data;`
- Added `[SerializeField] private EnemyData _enemyData;` field
- In `Awake()`: overlays `damage` and `attackCooldown` from `_enemyData` if assigned
- Also restored complete file from git HEAD (working copy was truncated before this session; all prior WO-49/90/92 uncommitted changes re-applied)
- Brace check: 42/42 BALANCED

### WaveManager.cs (`Assets/_Modules/Village/Waves/WaveManager.cs`)
- Added `using DeNelle.Data;`
- Added `[SerializeField] private List<WaveData> _soWaves` field in new "Wave SO Authoring" header
- Existing JSON-driven loop (`_schedule`/`WaveDef`) is **unchanged** — SO list is additive
- Also restored from git HEAD (file was truncated); preserved prior `WaveCountdownUI` call and null-check additions
- Brace check: 110/110 BALANCED

### Pet.cs (`Assets/_Modules/Pets/Pet.cs`)
- Added `using DeNelle.Data;`
- Added `[SerializeField] private PetData _petData;` field
- In `Configure()`: PetData overlay applied after JSON values — SO stats win over JSON
- Brace check: 29/29 BALANCED

### PetProgression.cs (`Assets/_Modules/Pets/PetProgression.cs`)
- Added `using DeNelle.Data;`
- Replaced hardcoded `DamagePerLevel`/`HpPerLevel` constants with property accessors reading from `_petData` when assigned, falling back to original constants
- Added `[SerializeField] private PetData _petData;` field
- Brace check: 6/6 BALANCED

---

## Not Implemented (intentional)

- **Ability scripts** (Wizard/Ranger/Knight): no ability script files exist in the codebase yet — `AbilityData` SO is ready to wire when they are created
- **`_Data` .asset files**: Unity `.asset` files require the Editor running — they cannot be created in batchmode without a custom AssetDatabase script. Recommend creating them in the Unity Editor via the CreateAssetMenu paths
- **`EnemyHealth.cs`**: does not exist; enemy HP lives in `Enemy.cs` driven by `EnemyDef` JSON. EnemyBrain now reads `damage`/`attackCooldown` from EnemyData

---

## Brace Balance — ALL PASSED

```
EnemyData.cs           2/2   BALANCED
WaveData.cs            3/3   BALANCED
AbilityData.cs         2/2   BALANCED
PetData.cs             2/2   BALANCED
EnemyBrain.cs         42/42  BALANCED
WaveManager.cs       110/110  BALANCED
Pet.cs                29/29  BALANCED
PetProgression.cs      6/6   BALANCED
```
