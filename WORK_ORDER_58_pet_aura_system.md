# WORK ORDER 58 — Pet Aura System with Level Scaling

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** Medium-High
**Scope:** Medium — new component, prefab wiring, level-up hook
**Depends on:** WO-50 (VFXManager), WO-56 (pet VFX wiring)

---

## Goal

Every pet has a persistent ambient aura that visibly grows stronger as it
levels up, with a distinct colour theme per pet type. Levelling up triggers a
short celebration burst so players feel the progression.

---

## 1. Create `AuraController.cs`

**Path:** `Assets/_Modules/Pets/AuraController.cs`

```csharp
using System.Collections;
using UnityEngine;

/// <summary>
/// Drives a persistent aura ParticleSystem whose intensity scales with pet level.
/// Attach to any pet prefab alongside its Animator and PetBrain.
/// </summary>
public class AuraController : MonoBehaviour
{
    [Header("Aura Prefabs (assign per pet type)")]
    [Tooltip("Looping aura ParticleSystem — already a child of this prefab, or drag one in.")]
    public ParticleSystem auraPrefab;

    [Header("Level Scaling")]
    [Tooltip("Emission rate multiplier per level tier.")]
    public float level1EmissionRate = 4f;
    public float level3EmissionRate = 14f;
    public float level5EmissionRate = 28f;

    [Header("Orbiting Sparks (Level 5+)")]
    [Tooltip("Secondary orbiting sparks ParticleSystem (optional).")]
    public ParticleSystem orbitSparksPrefab;

    [Header("Level-Up Burst")]
    public float burstIntensityMultiplier = 2.5f;
    public float burstDuration            = 2f;
    [Tooltip("VFXType to play on level-up. Defaults to LevelUp_Celebration.")]
    public VFXType levelUpVfxType         = VFXType.LevelUp_Celebration;

    // ── State ─────────────────────────────────────────────────────────────────
    private int _currentLevel = 1;
    private ParticleSystem _auraInstance;
    private ParticleSystem _orbitInstance;
    private Coroutine _burstRoutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _auraInstance  = auraPrefab != null
            ? Instantiate(auraPrefab,  transform.position, Quaternion.identity, transform)
            : GetComponentInChildren<ParticleSystem>();

        if (orbitSparksPrefab != null)
            _orbitInstance = Instantiate(orbitSparksPrefab,
                transform.position, Quaternion.identity, transform);
    }

    private void OnEnable()
    {
        _auraInstance?.Play();
        ApplyLevel(_currentLevel, animate: false);
    }

    private void OnDisable()
    {
        _auraInstance?.Stop();
        _orbitInstance?.Stop();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this whenever the pet's level changes.
    /// </summary>
    public void OnLevelUp(int newLevel)
    {
        _currentLevel = newLevel;
        ApplyLevel(newLevel, animate: true);

        // Level-up celebration VFX.
        VFXManager.Instance?.Play(levelUpVfxType, transform.position);

        if (_burstRoutine != null) StopCoroutine(_burstRoutine);
        _burstRoutine = StartCoroutine(BurstRoutine());
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private void ApplyLevel(int level, bool animate)
    {
        if (_auraInstance == null) return;

        float targetRate = level >= 5 ? level5EmissionRate
                         : level >= 3 ? level3EmissionRate
                         :              level1EmissionRate;

        var em = _auraInstance.emission;
        em.rateOverTime = targetRate;

        // Orbit sparks only appear at level 5+.
        if (_orbitInstance != null)
        {
            if (level >= 5 && !_orbitInstance.isPlaying) _orbitInstance.Play();
            else if (level < 5 &&  _orbitInstance.isPlaying) _orbitInstance.Stop();
        }
    }

    private IEnumerator BurstRoutine()
    {
        if (_auraInstance == null) yield break;

        var em = _auraInstance.emission;
        float normalRate = em.rateOverTime.constant;
        em.rateOverTime  = normalRate * burstIntensityMultiplier;

        yield return new WaitForSeconds(burstDuration);

        em.rateOverTime = normalRate;
        _burstRoutine   = null;
    }
}
```

---

## 2. Per-pet-type colour / prefab assignment

| Pet Type | Aura prefab | Orbit sparks prefab | Colour |
|---|---|---|---|
| Fire (Flame Pup) | Lana Studio `FireAura` loop | Lana Studio `EmberSparks` | Orange / red |
| Ice / Aether Sprite | Lana Studio `IceAura` loop | Mirza Beig `FrostOrbit` | Blue / cyan |
| Future types | — | — | Match element |

1. Open each pet prefab.
2. Add `AuraController` component.
3. Drag the appropriate Lana Studio loop particle system into **Aura Prefab**.
4. Optionally drag the orbit sparks PS into **Orbit Sparks Prefab**.

---

## 3. Hook into pet level-up

Find where pet XP is awarded (likely `PetBrain.cs` or `PetManager.cs`) and add:

```csharp
private void OnLevelGained(int newLevel)
{
    GetComponent<AuraController>()?.OnLevelUp(newLevel);
    // ... existing level-up logic ...
}
```

---

## 4. Add `LevelUp_Celebration` to `VFXType`

**Edit** `Assets/_Modules/VFX/VFXManager.cs` — append to the `VFXType` enum:

```csharp
LevelUp_Celebration,
```

Register a matching prefab in `VFXCatalog`:
- Vertical light beam + golden sparks rising upward.
- Short lifetime (~2 s).
- Source: Mirza Beig or custom procedural.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Pets/AuraController.cs` | **Create** |
| All pet prefabs | **Edit** — add `AuraController`, wire prefabs |
| `Assets/_Modules/Pets/PetBrain.cs` (or PetManager) | **Edit** — call `OnLevelUp` |
| `Assets/_Modules/VFX/VFXManager.cs` | **Edit** — add `LevelUp_Celebration` to enum |
| `Assets/Resources/VFX/VFXCatalog.asset` | **Edit** — add LevelUp entry |

---

## Acceptance Criteria

- [ ] Level 1 pet has a subtle, soft glow (emission rate ~4/s)
- [ ] Level 3 pet has clearly more particles and brighter aura
- [ ] Level 5 pet has orbiting sparks in addition to base aura
- [ ] Fire pets are orange/red; ice pets are blue/cyan — no type bleed
- [ ] Levelling up triggers a visible burst for 2 seconds, then returns to normal
- [ ] `LevelUp_Celebration` VFX plays at pet position on level-up
- [ ] Aura is parented to pet and moves with it
- [ ] Aura stops when pet is disabled (pool-safe)
