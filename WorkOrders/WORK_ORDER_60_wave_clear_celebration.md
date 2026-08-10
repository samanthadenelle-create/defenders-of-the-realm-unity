# WORK ORDER 60 — Wave Clear Celebration + Kill Combo System

**Status:** CLOSED — SUPERSEDED by WO-83 (owner-approved sweep 2026-08-09: identical scope, WO-83 RESULT exists)
**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — WaveManager hook + new KillComboTracker component
**Depends on:** WO-50 (VFXManager), WO-56 (VFXType catalog must include celebration types)

---

## Goal

Clearing a wave and landing kill combos feel rewarding and exciting, not silent.
Vertical light beams, golden sparks, and a combo burst give players a clear
sense of momentum.

---

## 1. Wave clear — hook into WaveManager

**Edit** `Assets/_Modules/Village/Waves/WaveManager.cs` — in `CompleteWave()`:

```csharp
private void CompleteWave()
{
    // ... existing logic ...

    // Celebration VFX at village centre.
    var centre = GetVillageCentre();
    VFXManager.Instance?.Play(VFXType.WaveClear_Celebration, centre);

    // Sound sting (WO-62):
    // AudioService.Instance?.PlaySfx(SfxId.WaveClear);
}

private Vector3 GetVillageCentre()
{
    // Return the transform position of a "VillageCentre" tagged object,
    // or fall back to Vector3.zero.
    var go = GameObject.FindWithTag("VillageCentre");
    return go != null ? go.transform.position : Vector3.zero;
}
```

---

## 2. Create the `WaveClear_Celebration` prefab

1. Create a new empty GameObject: `WaveClearCelebration`.
2. Add these child particle systems (Lana Studio / Mirza Beig packs):

| Child | Description | Settings |
|---|---|---|
| `LightBeams` | 4–6 vertical beams of golden light | Direction: up, lifetime 1.5 s, size large |
| `GoldenSparks` | Rising / floating golden particles | Emission burst 80, gravity -0.3, lifetime 2 s |
| `ScreenFlash` | One-shot bright quad/sprite | Scale up → fade, duration 0.25 s |

3. Auto-return after 3 s (uses `VFXAutoReturn` from WO-50).
4. Save to `Assets/Resources/VFX/WaveClearCelebration.prefab`.
5. Add `VFXType.WaveClear_Celebration` to the enum and catalog.

---

## 3. Create `KillComboTracker.cs`

**Path:** `Assets/_Modules/Village/KillComboTracker.cs`

```csharp
using System.Collections;
using UnityEngine;

/// <summary>
/// Tracks consecutive kills within a time window and triggers combo VFX + sound.
/// Attach to the hero or a persistent manager object in the Village scene.
/// </summary>
public class KillComboTracker : MonoBehaviour
{
    public static KillComboTracker Instance { get; private set; }

    [Header("Combo Settings")]
    public float comboWindow  = 4f;   // seconds between kills to extend combo
    public int   tier1Kills   = 3;    // first burst threshold
    public int   tier2Kills   = 5;    // big burst threshold

    [Header("VFX")]
    [Tooltip("Hero transform — combo burst spawns here.")]
    public Transform heroTransform;

    private int   _comboCount  = 0;
    private float _lastKillTime = -999f;
    private Coroutine _resetRoutine;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Call from EnemyBrain.Die() or the damage system on each kill.</summary>
    public void RegisterKill()
    {
        float now = Time.time;

        if (now - _lastKillTime > comboWindow)
            _comboCount = 0;   // gap too large — reset

        _comboCount++;
        _lastKillTime = now;

        if (_resetRoutine != null) StopCoroutine(_resetRoutine);
        _resetRoutine = StartCoroutine(ResetAfterWindow());

        EvaluateCombo();
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private void EvaluateCombo()
    {
        Vector3 pos = heroTransform != null
            ? heroTransform.position
            : Vector3.zero;

        if (_comboCount == tier2Kills)
        {
            // Big combo — screen shake + large burst.
            VFXManager.Instance?.Play(VFXType.Combo_Tier2, pos);
            CameraShakeManager.Instance?.Shake(ShakeTier.Medium, 0.35f);
            // AudioService.Instance?.PlaySfx(SfxId.ComboBig);
        }
        else if (_comboCount == tier1Kills)
        {
            // Small combo burst.
            VFXManager.Instance?.Play(VFXType.Combo_Tier1, pos);
            // AudioService.Instance?.PlaySfx(SfxId.ComboSmall);
        }
    }

    private IEnumerator ResetAfterWindow()
    {
        yield return new WaitForSeconds(comboWindow);
        _comboCount   = 0;
        _resetRoutine = null;
    }
}
```

---

## 4. Hook kill registration

In the enemy damage / death handler (wherever HP reaches 0):

```csharp
KillComboTracker.Instance?.RegisterKill();
```

---

## 5. Add new VFXType entries

**Edit** `VFXManager.cs` enum:

```csharp
WaveClear_Celebration,
Combo_Tier1,
Combo_Tier2,
```

Add matching prefabs in `VFXCatalog`:
- `Combo_Tier1`: small violet/golden burst, radius ~1 m, lifetime 0.8 s.
- `Combo_Tier2`: large golden explosion, radius ~2.5 m, screen shake companion.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/Waves/WaveManager.cs` | **Edit** — call celebration VFX in `CompleteWave` |
| `Assets/_Modules/Village/KillComboTracker.cs` | **Create** |
| `Assets/Resources/VFX/WaveClearCelebration.prefab` | **Create** |
| `Assets/_Modules/VFX/VFXManager.cs` | **Edit** — add 3 new VFXType entries |
| `Assets/Resources/VFX/VFXCatalog.asset` | **Edit** — wire new prefabs |

---

## Acceptance Criteria

- [ ] Wave clear plays vertical light beams + golden sparks at village centre
- [ ] 3 kills within 4 s triggers Tier 1 combo burst around the hero
- [ ] 5 kills within 4 s triggers Tier 2 combo + medium camera shake
- [ ] Combo resets if no kill for 4 s
- [ ] All celebration effects are pooled and auto-returned
- [ ] Kill combo does not trigger in menus or while paused
