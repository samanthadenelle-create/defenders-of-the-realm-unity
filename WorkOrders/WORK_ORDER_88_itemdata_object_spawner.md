# WORK ORDER 88 — ItemData ScriptableObject + ObjectSpawner (Unified Pool Factory)

**Status:** CLOSED — SUPERSEDED by WO-86 (owner-approved sweep 2026-08-09: SO data-architecture RESULT exists; pooling residue unverified)
**Date:** 2026-05-28
**Priority:** Critical
**Scope:** Medium — two new scripts + targeted edits to existing Data SOs + all spawn callsites
**Depends on:** WO-86 (TowerData, EnemyData, PetData ScriptableObjects)
**Supersedes:** Manual `Instantiate` / `Destroy` calls in TowerCombat, WaveManager, PetCombatController

---

## Goal

One spawner handles towers, enemies, VFX, projectiles, loot, and pets — with
automatic per-type object pooling. No `Instantiate` / `Destroy` spam anywhere
in the codebase. All data-driven via ScriptableObjects. Balancing stays in the
Editor with zero code changes.

---

## 1. `ItemData.cs` — base ScriptableObject

**Path:** `Assets/_Modules/Data/ItemData.cs`

All game-object data SOs inherit from this.

```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "Defenders/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    public string     itemName;

    [Header("Prefab")]
    public GameObject prefab;           // The prefab this data spawns
    public float      spawnOffsetY = 0f;

    [Header("Pool Settings")]
    public int        initialPoolSize = 8;
    public int        maxPoolSize     = 32;

    [Header("Base Stats (override in subclasses)")]
    public int   cost     = 0;
    public float cooldown = 0f;
}
```

---

## 2. Update existing Data SOs to inherit `ItemData`

**Edit** each file in `Assets/_Modules/Data/`:

### `TowerData.cs`

```csharp
// Change:
public class TowerData : ScriptableObject

// To:
public class TowerData : ItemData
// Remove duplicate: public string towerName  → use base itemName
// Remove duplicate: public int buildCost      → use base cost
// Remove duplicate: public float attackRate   → keep (not in base)
// CreateAssetMenu stays on TowerData specifically:
[CreateAssetMenu(fileName = "NewTowerData", menuName = "Defenders/Data/Tower")]
```

### `EnemyData.cs`

```csharp
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Defenders/Data/Enemy")]
public class EnemyData : ItemData
// Remove duplicate: public string enemyName → use base itemName
```

### `PetData.cs`

```csharp
[CreateAssetMenu(fileName = "NewPetData", menuName = "Defenders/Data/Pet")]
public class PetData : ItemData
// Remove duplicate: public string petName → use base itemName
```

### `AbilityData.cs`

```csharp
[CreateAssetMenu(fileName = "NewAbilityData", menuName = "Defenders/Data/Ability")]
public class AbilityData : ItemData
// Remove duplicate: public float cooldown → use base cooldown
// Remove duplicate: public string abilityName → use base itemName
```

### New: `ProjectileData.cs`

**Path:** `Assets/_Modules/Data/ProjectileData.cs`

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "NewProjectileData", menuName = "Defenders/Data/Projectile")]
public class ProjectileData : ItemData
{
    [Header("Projectile")]
    public float speed          = 14f;
    public int   damage         = 18;
    public float lifetime       = 5f;     // Auto-return to pool after this many seconds
    public bool  isAoE          = false;
    public float aoeRadius      = 0f;

    [Header("VFX")]
    public VFXType trailVFX     = VFXType.Projectile_ArcaneBolt;
    public VFXType impactVFX    = VFXType.Impact_ExplosionFire;
}
```

---

## 3. `ObjectSpawner.cs` — unified pool factory

**Path:** `Assets/_Modules/Spawning/ObjectSpawner.cs`

```csharp
using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;

public class ObjectSpawner : MonoBehaviour
{
    public static ObjectSpawner Instance { get; private set; }

    private readonly Dictionary<ItemData, ObjectPool<GameObject>> _pools
        = new Dictionary<ItemData, ObjectPool<GameObject>>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieve a pooled instance of data.prefab, positioned at world position + spawnOffsetY.
    /// Creates the pool on first use.
    /// </summary>
    public GameObject Spawn(ItemData data, Vector3 position,
                            Quaternion rotation = default)
    {
        if (data == null || data.prefab == null)
        {
            Debug.LogError($"[ObjectSpawner] Null data or prefab on {data?.name}");
            return null;
        }

        var pool = GetOrCreatePool(data);
        var instance = pool.Get();

        instance.transform.SetPositionAndRotation(
            position + Vector3.up * data.spawnOffsetY,
            rotation == default ? Quaternion.identity : rotation);

        return instance;
    }

    /// <summary>
    /// Return a pooled instance. Must have been spawned via ObjectSpawner.Spawn().
    /// </summary>
    public void ReturnToPool(ItemData data, GameObject instance)
    {
        if (data == null || instance == null) return;

        if (_pools.TryGetValue(data, out var pool))
            pool.Release(instance);
        else
            Destroy(instance);   // Fallback if pool was never created
    }

    // ── Convenience wrappers ──────────────────────────────────────────────────

    /// <summary>Spawn and auto-return after <paramref name="lifetime"/> seconds.</summary>
    public GameObject SpawnTemporary(ItemData data, Vector3 position,
                                     float lifetime,
                                     Quaternion rotation = default)
    {
        var instance = Spawn(data, position, rotation);
        if (instance != null)
            StartCoroutine(ReturnAfterDelay(data, instance, lifetime));
        return instance;
    }

    private System.Collections.IEnumerator ReturnAfterDelay(
        ItemData data, GameObject instance, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (instance != null && instance.activeSelf)
            ReturnToPool(data, instance);
    }

    // ── Pool management ───────────────────────────────────────────────────────

    private ObjectPool<GameObject> GetOrCreatePool(ItemData data)
    {
        if (_pools.TryGetValue(data, out var existing))
            return existing;

        var pool = new ObjectPool<GameObject>(
            createFunc:      () => Instantiate(data.prefab),
            actionOnGet:     obj => obj.SetActive(true),
            actionOnRelease: obj => obj.SetActive(false),
            actionOnDestroy: obj => Destroy(obj),
            collectionCheck: true,
            defaultCapacity: data.initialPoolSize,
            maxSize:         data.maxPoolSize);

        _pools[data] = pool;
        return pool;
    }

    /// <summary>
    /// Pre-warm a pool before gameplay starts (call from loading screen).
    /// </summary>
    public void PreWarm(ItemData data)
    {
        var pool = GetOrCreatePool(data);
        var temp = new GameObject[data.initialPoolSize];
        for (int i = 0; i < data.initialPoolSize; i++)
            temp[i] = pool.Get();
        foreach (var obj in temp)
            pool.Release(obj);
    }
}
```

---

## 4. Updated callsites

### Tower placement

```csharp
// TowerBuildSystem.cs or BuildManager.cs
public void BuildTower(TowerData towerData, Vector3 buildPosition)
{
    var tower = ObjectSpawner.Instance.Spawn(towerData, buildPosition);
    tower.GetComponent<TowerCombat>()?.Initialize(towerData);
    VFXManager.Instance?.Play(VFXType.LevelUp_Celebration, buildPosition);
    // AudioService.Instance?.PlaySfx(SfxId.TowerPlace);
}
```

### Enemy spawning (WaveManager)

```csharp
// WaveManager.cs — replace Instantiate call
private void SpawnEnemy(EnemyData enemyData)
{
    Vector3 spawnPos = GetRandomSpawnPoint();
    var enemy = ObjectSpawner.Instance.Spawn(enemyData, spawnPos);
    // Apply data to EnemyBrain / EnemyHealth
    if (enemy.TryGetComponent<EnemyBrain>(out var brain))
        brain.Initialize(enemyData);
    if (enemy.TryGetComponent<EnemyHealth>(out var health))
        health.Initialize(enemyData);
}
```

### Projectile firing (TowerCombat)

```csharp
// TowerCombat.cs
[Header("Projectile")]
public ProjectileData projectileData;

private void Shoot(Transform target)
{
    Vector3 dir = (target.position - muzzlePoint.position).normalized;
    var proj = ObjectSpawner.Instance.Spawn(
        projectileData, muzzlePoint.position,
        Quaternion.LookRotation(dir));

    if (proj.TryGetComponent<TowerProjectile>(out var tp))
        tp.Initialize(projectileData, target, this);

    GetComponent<TowerVFXController>()?.OnShoot(target.position, currentLevel);
}
```

### Enemy death — return to pool instead of `Destroy`

```csharp
// EnemyHealth.cs — replace Invoke(nameof(DisableSelf), 2.8f) with:
private void DisableSelf()
{
    // Try to return to pool; fall back to SetActive(false)
    if (_enemyData != null)
        ObjectSpawner.Instance?.ReturnToPool(_enemyData, gameObject);
    else
        gameObject.SetActive(false);
}
```

Add `[Header("Data")] public EnemyData _enemyData;` field to `EnemyHealth`
and wire it in the Inspector (same asset as EnemyBrain.data).

### Pet spawning

```csharp
// PetManager.cs or wherever pets are placed
public void SpawnPet(PetData petData, Vector3 position)
{
    var pet = ObjectSpawner.Instance.Spawn(petData, position);
    pet.GetComponent<PetCombatController>()?.Initialize(petData);
    pet.GetComponent<AuraController>()?.Initialise(1);
}
```

---

## 5. Pre-warm on scene load

In your loading screen or `GameManager.Start()`:

```csharp
// Pre-warm the most frequently spawned items
void PreWarmPools()
{
    foreach (var enemyData in waveManager.GetAllEnemyTypes())
        ObjectSpawner.Instance.PreWarm(enemyData);

    ObjectSpawner.Instance.PreWarm(basicProjectileData);
}
```

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Data/ItemData.cs` | **Create** — base SO |
| `Assets/_Modules/Data/ProjectileData.cs` | **Create** — inherits ItemData |
| `Assets/_Modules/Data/TowerData.cs` | **Edit** — inherit ItemData, remove duplicate fields |
| `Assets/_Modules/Data/EnemyData.cs` | **Edit** — inherit ItemData, remove duplicate fields |
| `Assets/_Modules/Data/PetData.cs` | **Edit** — inherit ItemData, remove duplicate fields |
| `Assets/_Modules/Data/AbilityData.cs` | **Edit** — inherit ItemData, remove duplicate fields |
| `Assets/_Modules/Spawning/ObjectSpawner.cs` | **Create** |
| `WaveManager.cs` | **Edit** — use `ObjectSpawner.Spawn(enemyData, ...)` |
| `TowerCombat.cs` | **Edit** — use `ObjectSpawner.Spawn(projectileData, ...)` |
| `EnemyHealth.cs` | **Edit** — `DisableSelf()` returns to pool |
| Persistent manager GO in scene | **Edit** — add `ObjectSpawner` component |
| All enemy/pet/tower prefabs | **Edit** — assign `EnemyData`/`PetData`/`TowerData` asset |

---

## OVERNIGHT_BATCH.md update

Insert **Group A0** (run before Group A) in `OVERNIGHT_BATCH.md`:

```
GROUP A0 — Data Foundation (run before everything else)
A0-1: WORK_ORDER_86 (ScriptableObjects)
A0-2: WORK_ORDER_88 (ItemData + ObjectSpawner)
These two have no external dependencies. Run simultaneously.
Commit: feat: batch-A0 ScriptableObjects ObjectSpawner
```

---

## Acceptance Criteria

- [ ] `ObjectSpawner.Spawn(enemyData, spawnPos)` returns a pooled enemy and activates it
- [ ] `ReturnToPool(enemyData, instance)` deactivates the instance and returns it to the pool
- [ ] Spawning the same `EnemyData` 20 times: no new `Instantiate` calls after the pool is full
- [ ] `SpawnTemporary(data, pos, 2.5f)` auto-returns the object after 2.5 s
- [ ] `PreWarm(enemyData)` pre-populates the pool with `initialPoolSize` instances before wave 1
- [ ] `TowerData`, `EnemyData`, `PetData`, `AbilityData` all inherit `ItemData` — existing `.asset` files still load correctly (Unity serialises inherited fields)
- [ ] All `Instantiate` / `Destroy` calls for towers, enemies, and projectiles are removed from `WaveManager` and `TowerCombat`
- [ ] No null-ref if `ObjectSpawner` is absent (callers use `?.`)
