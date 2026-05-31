# WORK ORDER 50 — VFXManager + Modern VFX Integration

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — new manager + catalog ScriptableObject + AbilityVfxKit migration
**Depends on:** None (can run alongside WO-49)

---

## Goal

Replace the outdated `AbilityVfxKit` procedural system with real prefabs from
the installed asset packs (Ultimate VFX by Mirza Beig, Spells Pack, Lana Studio
Casual RPG VFX). A new `VFXManager` singleton owns pooled instances; a
`VFXCatalog` ScriptableObject maps `VFXType` → prefab so nothing is hardcoded.
`AbilityVfxKit` becomes a thin wrapper that calls `VFXManager` instead of
spawning procedural particles itself.

---

## 1. Create `VFXManager.cs`

**Path:** `Assets/_Modules/VFX/VFXManager.cs`

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;          // Unity 2021+ built-in ObjectPool

public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [Header("Catalog")]
    [Tooltip("Assign the VFXCatalog ScriptableObject here.")]
    public VFXCatalog catalog;

    [Header("Pools")]
    public int defaultPoolSize    = 8;
    public int maxPoolSize        = 32;

    // type → pool of recycled GameObjects
    private readonly Dictionary<VFXType, ObjectPool<GameObject>> _pools
        = new Dictionary<VFXType, ObjectPool<GameObject>>();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (catalog != null)
            catalog.RegisterAll(this);
        else
            Debug.LogWarning("[VFXManager] No VFXCatalog assigned — call " +
                             "RegisterPool() manually or assign a catalog.");
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Spawn a VFX at world position. The instance is auto-returned to the
    /// pool once its ParticleSystem (or AutoReturn component) finishes.
    /// </summary>
    public GameObject Play(VFXType type, Vector3 position,
                           Quaternion rotation = default)
    {
        if (type == VFXType.None) return null;

        if (!_pools.TryGetValue(type, out var pool))
        {
            Debug.LogWarning($"[VFXManager] No pool registered for {type}. " +
                             "Add it to VFXCatalog.");
            return null;
        }

        var instance = pool.Get();
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);

        // Auto-return after the longest particle duration, if no AutoReturn
        // component is already present.
        var ps = instance.GetComponentInChildren<ParticleSystem>();
        if (ps != null && instance.GetComponent<VFXAutoReturn>() == null)
        {
            var ar = instance.AddComponent<VFXAutoReturn>();
            ar.Init(pool, instance, ps.main.duration + ps.main.startLifetime.constantMax);
        }

        return instance;
    }

    /// <summary>Register a prefab under a VFXType key (called by VFXCatalog).</summary>
    public void RegisterPool(VFXType type, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[VFXManager] RegisterPool: null prefab for {type}.");
            return;
        }
        if (_pools.ContainsKey(type)) return;

        _pools[type] = new ObjectPool<GameObject>(
            createFunc:      () => Instantiate(prefab),
            actionOnGet:     obj => obj.SetActive(true),
            actionOnRelease: obj => obj.SetActive(false),
            actionOnDestroy: obj => Destroy(obj),
            collectionCheck: true,
            defaultCapacity: defaultPoolSize,
            maxSize:         maxPoolSize);
    }
}
```

---

## 2. Create `VFXAutoReturn.cs`

**Path:** `Assets/_Modules/VFX/VFXAutoReturn.cs`

Lets pooled objects return themselves after their particle system finishes,
without any coroutine running on the manager.

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Added at runtime to pooled VFX GameObjects that don't already have a
/// return-to-pool mechanism. Returns the object after <c>lifetime</c> seconds.
/// </summary>
public class VFXAutoReturn : MonoBehaviour
{
    private ObjectPool<GameObject> _pool;
    private GameObject             _self;
    private float                  _lifetime;

    public void Init(ObjectPool<GameObject> pool, GameObject self, float lifetime)
    {
        _pool     = pool;
        _self     = self;
        _lifetime = Mathf.Max(0.1f, lifetime);
    }

    private void OnEnable()
    {
        StartCoroutine(ReturnAfterDelay());
    }

    private IEnumerator ReturnAfterDelay()
    {
        yield return new WaitForSeconds(_lifetime);
        _pool?.Release(_self);
    }
}
```

---

## 3. Create `VFXCatalog.cs`

**Path:** `Assets/_Modules/VFX/VFXCatalog.cs`

ScriptableObject that maps each `VFXType` to a prefab. All wiring lives here —
no magic strings, no code changes needed when swapping a prefab.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Defenders/VFX/VFX Catalog", fileName = "VFXCatalog")]
public class VFXCatalog : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public VFXType   type;
        public GameObject prefab;
    }

    [Tooltip("Map every VFXType to a real prefab from your installed packs.")]
    public List<Entry> entries = new List<Entry>();

    /// <summary>Called by VFXManager.Start() to register all entries.</summary>
    public void RegisterAll(VFXManager manager)
    {
        int registered = 0;
        foreach (var e in entries)
        {
            if (e.prefab == null)
            {
                Debug.LogWarning($"[VFXCatalog] Entry for {e.type} has a null prefab.");
                continue;
            }
            manager.RegisterPool(e.type, e.prefab);
            registered++;
        }
        Debug.Log($"[VFXCatalog] Registered {registered} VFX pools.");
    }
}
```

**After creating the script:**

1. In the Project window: right-click → Create → Defenders → VFX → VFX Catalog.
2. Name the asset `VFXCatalog` and place it at `Assets/Resources/VFX/VFXCatalog.asset`.
3. Fill in each entry by dragging prefabs from the installed packs (see §4).

---

## 4. Prefab mapping — installed packs

Use the following as a starting point. Adjust paths to match what Unity
imported under `Assets/Mirza Beig/`, `Assets/Spells Pack/`, and
`Assets/Lana Studio/`.

| VFXType | Suggested Prefab | Pack |
|---|---|---|
| `Projectile_ArcaneBolt` | `Mirza Beig/Particle Systems/.../Magic Orb` or similar | Ultimate VFX |
| `Projectile_FlameArrow` | `Spells Pack/Prefabs/FireArrow` or similar | Spells Pack |
| `Impact_ExplosionFire` | `Lana Studio/Casual RPG VFX/.../FireExplosion` | Lana Studio |
| `Impact_ExplosionAether` | `Mirza Beig/Particle Systems/.../Aether Burst` | Ultimate VFX |
| `Impact_ShockwaveRing` | `Mirza Beig/Particle Systems/.../Shockwave` | Ultimate VFX |
| `Impact_Heal` | `Lana Studio/Casual RPG VFX/.../Heal` | Lana Studio |
| `Casting_WizardCharge` | `Mirza Beig/Particle Systems/.../Charge Up` | Ultimate VFX |
| `Pet_Aura_Fire` | `Lana Studio/Casual RPG VFX/.../FireAura` | Lana Studio |
| `Pet_Aura_Ice` | `Lana Studio/Casual RPG VFX/.../IceAura` | Lana Studio |
| `Environment_TorchFire` | `Lana Studio/Casual RPG VFX/.../Torch` | Lana Studio |
| `Death_EnemyExplosion` | `Spells Pack/Prefabs/Explosion` or similar | Spells Pack |

Browse the packs in `Assets/Mirza Beig/Particle Systems/_Common/Demos` or the
Spells Pack demo scene to find the right prefab names before wiring them in the
catalog.

---

## 5. Update `AbilityVfxKit.cs`

`AbilityVfxKit` should become a **thin forwarding layer** — keeping the
existing call sites working while delegating to `VFXManager` under the hood.

Replace the body of each `Play*` method with a `VFXManager.Instance.Play()`
call. Keep the old procedural fallback behind a `#if UNITY_EDITOR` guard so
you can still iterate in Editor without needing the full manager wired up.

**Example pattern:**

```csharp
// Before (old procedural code):
public void PlayFireImpact(Vector3 pos)
{
    // ... spawn procedural particles ...
}

// After:
public void PlayFireImpact(Vector3 pos)
{
    if (VFXManager.Instance != null)
    {
        VFXManager.Instance.Play(VFXType.Impact_ExplosionFire, pos);
        return;
    }
#if UNITY_EDITOR
    // Fallback: procedural placeholder so Editor previews still work
    // without a VFXManager in scene.
    PlayProceduralFireImpactFallback(pos);
#else
    Debug.LogWarning("[AbilityVfxKit] VFXManager not present — VFX skipped.");
#endif
}
```

Apply the same pattern to every other `Play*` method in `AbilityVfxKit`.

---

## 6. Scene wiring

1. Create an empty GameObject in the Village scene named `VFXManager`.
2. Add the `VFXManager` component.
3. Assign the `VFXCatalog` asset to the **Catalog** field.
4. The manager is `DontDestroyOnLoad`, so it persists across scene transitions.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/VFX/VFXManager.cs` | **Create** — full source above |
| `Assets/_Modules/VFX/VFXAutoReturn.cs` | **Create** — auto-return helper |
| `Assets/_Modules/VFX/VFXCatalog.cs` | **Create** — ScriptableObject catalog |
| `Assets/Resources/VFX/VFXCatalog.asset` | **Create** — fill in prefab references |
| `Assets/_Modules/VFX/AbilityVfxKit.cs` | **Edit** — forward all Play* calls to VFXManager |

---

## Acceptance Criteria

- [ ] `VFXManager` boots on scene start, logs `"Registered N VFX pools"` (N > 0)
- [ ] `VFXManager.Instance.Play(VFXType.Impact_ExplosionFire, pos)` spawns the
      correct Lana Studio prefab at the given position
- [ ] Pooled instances are returned automatically; no orphaned GameObjects after
      repeated plays (verify with Scene Hierarchy)
- [ ] `AbilityVfxKit` call sites (hero abilities, tower effects, pet auras)
      continue to work without changes at the call site
- [ ] No procedural placeholder particles appear in non-Editor builds
- [ ] All `VFXType` enum values have a catalog entry (no `LogWarning` spam at runtime)
- [ ] No `NullReferenceException` if `VFXManager` is absent from scene in Editor
- [ ] `defaultPoolSize` and `maxPoolSize` are tunable from the Inspector
