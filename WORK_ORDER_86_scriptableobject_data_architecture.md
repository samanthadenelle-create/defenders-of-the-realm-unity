# WORK ORDER 86 — ScriptableObject Data Architecture

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Large — five new ScriptableObjects + refactor of Tower, Enemy, Wave, Ability, Pet scripts to read from them
**Depends on:** WO-69 (EnemyBrain), WO-70 (EnemyHealth), WO-82 (TowerVFXController)

---

## Goal

Move all game-balance stats out of MonoBehaviour fields and into
ScriptableObjects. Every tower, enemy, wave, ability, and pet will read its data
from an asset. Balancing becomes drag-and-drop in the Inspector — no code
changes required.

---

## 1. `TowerData.cs`

**Path:** `Assets/_Modules/Data/TowerData.cs`

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "NewTowerData", menuName = "Defenders/Data/Tower")]
public class TowerData : ScriptableObject
{
    [Header("Identity")]
    public string towerName        = "Basic Tower";
    public Sprite icon;

    [Header("Combat")]
    public int   baseDamage        = 18;
    public float attackRate        = 1.2f;      // Attacks per second
    public float attackRange       = 8f;
    public int   projectileSpeed   = 14;

    [Header("Upgrade Multipliers (per level 2 / 3)")]
    public float damageMultiplierL2   = 1.5f;
    public float damageMultiplierL3   = 2.4f;
    public float rangeMultiplierL2    = 1.15f;
    public float rangeMultiplierL3    = 1.3f;

    [Header("Cost")]
    public int   buildCost         = 80;
    public int   upgradeCostL2     = 120;
    public int   upgradeCostL3     = 200;

    [Header("VFX")]
    public VFXType muzzleFlashVFX  = VFXType.Projectile_ArcaneBolt;
    public VFXType impactVFX       = VFXType.Impact_Physical;
    public ShakeTier shotShakeTier = ShakeTier.Light;
    public bool  shakeOnEveryShot  = false;
}
```

---

## 2. `EnemyData.cs`

**Path:** `Assets/_Modules/Data/EnemyData.cs`

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Defenders/Data/Enemy")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName        = "Grunt";
    public bool   isElite          = false;
    public bool   isBoss           = false;

    [Header("Combat")]
    public int   maxHealth         = 45;
    public int   damage            = 12;
    public float attackCooldown    = 1.8f;
    public float attackRange       = 2.5f;
    public float detectionRange    = 15f;

    [Header("Movement")]
    public float chaseSpeed        = 4.2f;
    public float patrolSpeed       = 2.2f;

    [Header("Death Rewards")]
    public int   aetherReward      = 0;
    public int   woodReward        = 5;

    [Header("VFX")]
    public VFXType deathVFX        = VFXType.Death_EnemyExplosion;
    public int     heavyHitThreshold = 20;   // Damage amount that triggers heavy hit reaction
}
```

---

## 3. `AbilityData.cs`

**Path:** `Assets/_Modules/Data/AbilityData.cs`

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "NewAbilityData", menuName = "Defenders/Data/Ability")]
public class AbilityData : ScriptableObject
{
    [Header("Identity")]
    public string  abilityName     = "Fireball";
    public Sprite  icon;
    public string  description;

    [Header("Stats")]
    public int     damage          = 35;
    public float   cooldown        = 5f;
    public float   range           = 10f;
    public float   aoeRadius       = 0f;       // 0 = single target

    [Header("Timing")]
    public float   windupDuration  = 0.18f;
    public float   castDuration    = 0.35f;

    [Header("Cost")]
    public int     manaCost        = 0;        // Unused if no mana system

    [Header("VFX")]
    public VFXType projectileVFX   = VFXType.Projectile_ArcaneBolt;
    public VFXType impactVFX       = VFXType.Impact_ExplosionFire;
    public VFXType windupVFX       = VFXType.Projectile_ArcaneBolt;

    [Header("Feedback")]
    public ShakeTier impactShakeTier = ShakeTier.Medium;
    public float     hitStopDuration = 0.06f;
}
```

---

## 4. `WaveData.cs`

**Path:** `Assets/_Modules/Data/WaveData.cs`

```csharp
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct EnemySpawnEntry
{
    public EnemyData enemyType;
    public int       count;
    public float     spawnInterval;   // Seconds between each spawn of this type
}

[CreateAssetMenu(fileName = "NewWaveData", menuName = "Defenders/Data/Wave")]
public class WaveData : ScriptableObject
{
    [Header("Identity")]
    public int    waveNumber;
    public string waveTitle = "Wave 1";

    [Header("Enemies")]
    public List<EnemySpawnEntry> spawnEntries;

    [Header("Timing")]
    public float  prewaveDelay  = 2f;   // Calm seconds before first spawn
    public float  postWaveDelay = 5f;   // Celebration window before next wave starts

    [Header("Weather")]
    public bool   isBigWave     = false;   // Triggers rain/wind in WeatherManager
    public float  rainIntensity = 0.5f;

    [Header("Rewards")]
    public int    aetherReward  = 50;
    public int    woodReward    = 30;
    public bool   grantsBPXP   = true;
    public int    bpXPAmount   = 200;
}
```

---

## 5. `PetData.cs`

**Path:** `Assets/_Modules/Data/PetData.cs`

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "NewPetData", menuName = "Defenders/Data/Pet")]
public class PetData : ScriptableObject
{
    [Header("Identity")]
    public string petName       = "Flamepup";
    public Sprite icon;

    [Header("Combat")]
    public int   damage         = 9;
    public float attackRange    = 9f;
    public float attackCooldown = 2.2f;

    [Header("Leveling")]
    public int   xpPerLevel     = 300;
    public int   maxLevel       = 10;
    public float damagePerLevel = 1.5f;   // Flat damage added per level

    [Header("Aura (WO-58)")]
    public float level1EmissionRate = 4f;
    public float level3EmissionRate = 14f;
    public float level5EmissionRate = 28f;
    public bool  enableOrbitSparksAtL5 = true;
}
```

---

## 6. Refactor existing scripts to use ScriptableObjects

### `EnemyBrain.cs` — read from EnemyData

```csharp
[Header("Data")]
public EnemyData data;

private void Awake()
{
    _agent         = GetComponent<NavMeshAgent>();
    _animator      = GetComponent<Animator>();

    // Apply data — fall back to inspector fields if data is null for legacy prefabs
    if (data != null)
    {
        damage         = data.damage;
        attackCooldown = data.attackCooldown;
        attackRange    = data.attackRange;
        detectionRange = data.detectionRange;
        chaseSpeed     = data.chaseSpeed;
        patrolSpeed    = data.patrolSpeed;
    }

    _currentHealth = data != null ? data.maxHealth : maxHealth;
}
```

### `EnemyHealth.cs` — read from EnemyData

```csharp
[Header("Data")]
public EnemyData data;

private void Awake()
{
    CurrentHealth = data != null ? data.maxHealth : maxHealth;
    // ...
}

// In Die():
if (data != null && data.aetherReward > 0)
    MonetizationManager.Instance?.AddShards(data.aetherReward);
```

### `TowerCombat.cs` — read from TowerData

```csharp
[Header("Data")]
public TowerData data;

private void Start()
{
    if (data != null)
    {
        towerDamage    = data.baseDamage;
        attackRate     = data.attackRate;
        attackRange    = data.attackRange;
    }
}

public void UpgradeToLevel(int level)
{
    if (data == null) return;
    towerDamage = Mathf.RoundToInt(data.baseDamage *
        (level == 2 ? data.damageMultiplierL2 : data.damageMultiplierL3));
    attackRange *= (level == 2 ? data.rangeMultiplierL2 : data.rangeMultiplierL3);
    GetComponent<TowerVFXController>()?.OnUpgrade(level);
}
```

### `WaveManager.cs` — read from WaveData list

```csharp
[Header("Waves")]
public List<WaveData> waves;

private int _currentWaveIndex = 0;

public void StartNextWave()
{
    if (_currentWaveIndex >= waves.Count) return;

    WaveData wave = waves[_currentWaveIndex];
    _currentWaveIndex++;

    // Apply weather
    if (wave.isBigWave)
        WeatherManager.Instance?.SetRain(wave.rainIntensity);

    StartCoroutine(SpawnWave(wave));
}

private IEnumerator SpawnWave(WaveData wave)
{
    yield return new WaitForSeconds(wave.prewaveDelay);

    foreach (var entry in wave.spawnEntries)
    {
        for (int i = 0; i < entry.count; i++)
        {
            SpawnEnemy(entry.enemyType);
            yield return new WaitForSeconds(entry.spawnInterval);
        }
    }
}
```

---

## 7. Asset creation table (create these `.asset` files)

| Asset | Path | Notes |
|---|---|---|
| `TowerData_Ballista` | `Assets/_Data/Towers/` | Base tower |
| `TowerData_Fire` | `Assets/_Data/Towers/` | |
| `TowerData_Ice` | `Assets/_Data/Towers/` | |
| `TowerData_Lightning` | `Assets/_Data/Towers/` | |
| `EnemyData_Grunt` | `Assets/_Data/Enemies/` | |
| `EnemyData_ArmouredKnight` | `Assets/_Data/Enemies/` | |
| `EnemyData_Mage` | `Assets/_Data/Enemies/` | |
| `EnemyData_Elite` | `Assets/_Data/Enemies/` | isElite = true |
| `EnemyData_Boss` | `Assets/_Data/Enemies/` | isBoss = true |
| `WaveData_01` → `WaveData_10` | `Assets/_Data/Waves/` | 10 starting waves |
| `PetData_Flamepup` | `Assets/_Data/Pets/` | |
| `AbilityData_Fireball` | `Assets/_Data/Abilities/` | |
| `AbilityData_FrostArrow` | `Assets/_Data/Abilities/` | |
| `AbilityData_ShockwaveRing` | `Assets/_Data/Abilities/` | |

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Data/TowerData.cs` | **Create** |
| `Assets/_Modules/Data/EnemyData.cs` | **Create** |
| `Assets/_Modules/Data/AbilityData.cs` | **Create** |
| `Assets/_Modules/Data/WaveData.cs` | **Create** |
| `Assets/_Modules/Data/PetData.cs` | **Create** |
| `EnemyBrain.cs` | **Edit** — read from `EnemyData` |
| `EnemyHealth.cs` | **Edit** — read from `EnemyData`, grant Aether on death |
| `TowerCombat.cs` | **Edit** — read from `TowerData`, `UpgradeToLevel()` |
| `WaveManager.cs` | **Edit** — drive spawning from `WaveData` list |
| `PetCombatController.cs` | **Edit** — read from `PetData` |
| Ability scripts (Wizard/Ranger/Knight) | **Edit** — read from `AbilityData` |
| All tower/enemy/pet prefabs | **Edit** — assign correct `*Data` asset |

---

## Acceptance Criteria

- [ ] Changing `TowerData_Ballista.baseDamage` in the Inspector updates the tower's
      damage at runtime with no code change
- [ ] All 10 starting waves can be authored entirely in `WaveData` assets —
      no hard-coded spawn loops
- [ ] Killing an enemy grants `EnemyData.aetherReward` Aether via `MonetizationManager`
- [ ] Tower upgrade calls `UpgradeToLevel(2)` / `(3)` and stats match the multipliers
      in `TowerData`
- [ ] `AbilityData.cooldown` drives `AbilityCooldownUI.StartCooldown()`
- [ ] `PetData.damagePerLevel` increases pet combat damage on level-up
- [ ] All `.asset` files exist and are assigned to their respective prefabs
