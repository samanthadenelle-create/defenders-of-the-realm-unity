# WORK ORDER 59 — Dungeon Mode VFX Differentiation

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at VFXManager.cs:394,441 + DungeonSceneBootstrap.cs:35-50.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — ScriptableObject + VFXManager overload + post-process swap
**Depends on:** WO-50 (VFXManager), WO-51 (PerformanceManager)

---

## Goal

Dungeon runs feel darker, more intense, and visually distinct from the Village —
without any extra runtime cost. The mechanism is a `DungeonVFXSettings`
ScriptableObject that swaps the active prefab set in `VFXManager` when the
dungeon scene loads.

---

## 1. Create `DungeonVFXSettings.cs`

**Path:** `Assets/_Modules/VFX/DungeonVFXSettings.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds darker, higher-contrast prefab overrides for dungeon scenes.
/// Assign to VFXManager.dungeonSettings and call VFXManager.ApplyDungeonMode().
/// </summary>
[CreateAssetMenu(menuName = "Defenders/VFX/Dungeon VFX Settings",
                 fileName = "DungeonVFXSettings")]
public class DungeonVFXSettings : ScriptableObject
{
    [Serializable]
    public struct Override
    {
        public VFXType  type;
        [Tooltip("Replacement prefab — darker colours, stronger lights, more sparks.")]
        public GameObject prefab;
    }

    [Tooltip("Overrides applied in dungeon mode. Village prefabs are used for any " +
             "type not listed here.")]
    public List<Override> overrides = new List<Override>();

    [Header("Post-Processing Overrides")]
    [Range(0f, 2f)] public float bloomIntensityMultiplier    = 1.3f;
    [Range(0f, 2f)] public float contrastBoost               = 1.15f;
    public bool increasedVignette = true;
}
```

---

## 2. Extend `VFXManager` — dungeon mode

**Edit** `Assets/_Modules/VFX/VFXManager.cs`:

```csharp
// Add field:
[Header("Dungeon")]
public DungeonVFXSettings dungeonSettings;
private bool _dungeonMode = false;

// Add method:
/// <summary>
/// Swap to dungeon prefab overrides. Call on dungeon scene load.
/// </summary>
public void ApplyDungeonMode(bool active)
{
    _dungeonMode = active;
    if (dungeonSettings == null) return;

    foreach (var ov in dungeonSettings.overrides)
    {
        if (ov.prefab == null) continue;

        if (active)
        {
            // Replace pool with dungeon variant.
            if (_pools.ContainsKey(ov.type)) _pools.Remove(ov.type);
            RegisterPool(ov.type, ov.prefab);
        }
        else
        {
            // Restore village variant from catalog.
            if (_pools.ContainsKey(ov.type)) _pools.Remove(ov.type);
            if (catalog != null)
            {
                var entry = catalog.entries.Find(e => e.type == ov.type);
                if (entry.prefab != null) RegisterPool(ov.type, entry.prefab);
            }
        }
    }
}

/// <summary>Play VFX — uses dungeon overrides when in dungeon mode.</summary>
public GameObject PlayDungeon(VFXType type, Vector3 position,
                              Quaternion rotation = default)
    => Play(type, position, rotation); // routing is automatic via pool swap above
```

---

## 3. Scene hooks — DungeonSceneBootstrap

**Edit** or create `Assets/_Modules/Village/Dungeon/DungeonSceneBootstrap.cs`:

```csharp
private void OnEnable()
{
    // Swap VFX to dungeon variants.
    VFXManager.Instance?.ApplyDungeonMode(true);

    // Darken post-process.
    if (PerformanceManager.Instance != null &&
        VFXManager.Instance?.dungeonSettings != null)
    {
        var ds = VFXManager.Instance.dungeonSettings;
        // Multiply bloom on the active URP Volume.
        // (Access PerformanceManager.globalPostProcessVolume directly or via a ref.)
    }
}

private void OnDisable()
{
    VFXManager.Instance?.ApplyDungeonMode(false);
}
```

---

## 4. Specific dungeon enhancements

| Moment | Effect |
|---|---|
| Enemy death | Bigger explosion with lingering smoke — use darker `Death_EnemyExplosion_Dungeon` prefab |
| Hero spells | Extra glowing runes on impact — add rune ring child PS to arcane impact prefab |
| Portal entry | `VFXType.Portal_Enter` swirling vortex + bright flash (defined in WO-65) |
| Screen effect on enter | Bloom spike + brief vignette flash via post-process |

---

## 5. Screen shake on dungeon enter

```csharp
// In DungeonSceneBootstrap.OnEnable() after VFX swap:
CameraShakeManager.Instance?.Shake(ShakeTier.Heavy, duration: 0.6f);
```

(`CameraShakeManager` defined in WO-61.)

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/VFX/DungeonVFXSettings.cs` | **Create** |
| `Assets/_Modules/VFX/VFXManager.cs` | **Edit** — add `dungeonSettings`, `ApplyDungeonMode`, `PlayDungeon` |
| `Assets/Resources/VFX/DungeonVFXSettings.asset` | **Create** (in Editor) — fill overrides |
| `Assets/_Modules/Village/Dungeon/DungeonSceneBootstrap.cs` | **Edit** — call `ApplyDungeonMode` |

---

## Acceptance Criteria

- [ ] Enemy death explosion in dungeon is visibly larger and darker than in Village
- [ ] Hero spell impacts show glowing rune ring overlay in dungeon
- [ ] Bloom is 1.3× brighter in dungeon vs Village
- [ ] Switching back to Village scene restores all original VFX prefabs
- [ ] No performance regression vs Village (same pool sizes, same max counts)
- [ ] All effects respect mobile quality settings (WO-51 tier applied on top)
