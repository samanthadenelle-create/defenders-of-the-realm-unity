# WORK ORDER 87 — Cinemachine Camera System

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — replace manual camera with Cinemachine virtual cameras + impulse shake
**Depends on:** WO-61 (CameraShakeManager — Cinemachine replaces the manual version), WO-83 (WaveCelebrationManager)

> **Supersedes the manual camera shake in WO-61.** After this WO, all shake
> goes through `CinemachineImpulseSource` instead of
> `CameraShakeManager.Perlin`. Keep `CameraShakeManager` as a thin wrapper so
> all existing callers still compile.

---

## Goal

Replace raw Transform camera follow with Cinemachine virtual cameras.
Get screen shake, combat zoom, wave-clear cinematics, and smooth hero tracking
for free — no custom camera code.

---

## 1. Install Cinemachine

**Window → Package Manager → Unity Registry → Cinemachine → Install**

Required version: 2.9+ (ships with Unity 2022+). Does not require URP changes.

---

## 2. Scene Setup

### Cinemachine Brain

Select the Main Camera. Add Component: **CinemachineBrain**.

Settings:
```
Default Blend:      Cut → change to EaseInOut, 0.35 s
World Up Override:  Y
Update Method:      Late Update
```

### Virtual Camera hierarchy

Create these as separate GameObjects:

```
CinemachineVirtualCameras (empty parent)
├── VC_Village          ← default follow cam
├── VC_Combat           ← tighter zoom when enemies near
└── VC_WaveClear        ← cinematic pull-back on wave end
```

---

## 3. `VC_Village` — default hero-follow camera

Add Component: **CinemachineVirtualCamera**.

```
Priority:         10
Follow:           Hero transform
Look At:          Hero transform

Body (Framing Transposer):
  Camera Distance:    14
  Tracked Object Offset Y: 1.5
  Damping X/Y/Z:      0.3 / 0.3 / 0.3
  Softzone W/H:       0.3 / 0.3

Aim (Composer):
  Tracked Object Offset Y: 0.5
  Soft Zone W/H:       0.4 / 0.4
  Damping:            0.1
```

Add **CinemachineImpulseListener** to this VC (receives shake impulses).

---

## 4. `VC_Combat` — tighter zoom when enemies in range

Add Component: **CinemachineVirtualCamera**.

```
Priority:         11   (higher = takes control when active)
Follow:           Hero transform
Look At:          Hero transform

Body (Framing Transposer):
  Camera Distance:    10
  Damping X/Y/Z:      0.15 / 0.15 / 0.15   (snappier response)

Aim (Composer):
  (same as VC_Village)
```

Start **disabled**. A script enables it when enemies are within detection range.

---

## 5. `VC_WaveClear` — pull-back cinematic

```
Priority:         12
Follow:           Village centre (empty GO)
Look At:          Village centre

Body (Framing Transposer):
  Camera Distance:    28     (wide establishing shot)
  Damping:           0.5

Noise (Basic Multi Channel Perlin):
  Amplitude Gain:     0.3
  Frequency Gain:     0.4
```

Start **disabled**. Enable for `WaveCelebrationManager.slowMoDuration` seconds.

---

## 6. `CinemachineCameraController.cs`

**Path:** `Assets/_Modules/Village/Camera/CinemachineCameraController.cs`

```csharp
using UnityEngine;
using Cinemachine;

public class CinemachineCameraController : MonoBehaviour
{
    public static CinemachineCameraController Instance { get; private set; }

    [Header("Virtual Cameras")]
    public CinemachineVirtualCamera vcVillage;
    public CinemachineVirtualCamera vcCombat;
    public CinemachineVirtualCamera vcWaveClear;

    [Header("Combat Zoom")]
    public float combatZoomRadius   = 12f;   // If any enemy is within this radius → combat cam
    public float checkInterval      = 0.4f;  // Seconds between enemy proximity checks

    [Header("Impulse Source — attach to this GO")]
    private CinemachineImpulseSource _impulseSource;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void Start()
    {
        InvokeRepeating(nameof(CheckCombatProximity), 0f, checkInterval);
    }

    // ── Camera switching ──────────────────────────────────────────────────────

    private void CheckCombatProximity()
    {
        var heroes = FindObjectsOfType<HeroLocomotion>();
        if (heroes.Length == 0) return;

        bool enemyNear = false;
        foreach (var h in heroes)
        {
            var cols = Physics.OverlapSphere(h.transform.position, combatZoomRadius,
                LayerMask.GetMask("Enemy"));
            if (cols.Length > 0) { enemyNear = true; break; }
        }

        vcCombat.enabled  = enemyNear;
        vcVillage.enabled = !enemyNear;
    }

    public void PlayWaveClearCinematic(float duration)
    {
        vcWaveClear.enabled = true;
        Invoke(nameof(EndWaveClearCinematic), duration);
    }

    private void EndWaveClearCinematic() => vcWaveClear.enabled = false;

    // ── Shake ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this instead of CameraShakeManager.Shake() once Cinemachine is active.
    /// Tier maps to impulse force.
    /// </summary>
    public void Shake(ShakeTier tier)
    {
        if (_impulseSource == null) return;

        float force = tier switch
        {
            ShakeTier.Light  => 0.15f,
            ShakeTier.Medium => 0.4f,
            ShakeTier.Heavy  => 0.9f,
            _                => 0.15f
        };

#if UNITY_ANDROID || UNITY_IOS
        force *= 0.55f;
#endif

        _impulseSource.GenerateImpulse(force);
    }
}
```

---

## 7. Update `CameraShakeManager.cs` — thin wrapper

Replace the Perlin noise body with a delegation call so all existing callers
compile unchanged:

```csharp
public void Shake(ShakeTier tier)
{
    // Delegate to Cinemachine if available (WO-87)
    if (CinemachineCameraController.Instance != null)
    {
        CinemachineCameraController.Instance.Shake(tier);
        return;
    }

    // Legacy Perlin fallback (for scenes without Cinemachine)
    StartCoroutine(LegacyShake(tier));
}
```

---

## 8. Wire wave-clear cinematic

In `WaveCelebrationManager.PlayWaveClear()`, add at the start of the coroutine:

```csharp
CinemachineCameraController.Instance?.PlayWaveClearCinematic(slowMoDuration + 0.5f);
```

---

## Files to Create / Edit

| File | Action |
|---|---|
| Package Manager | **Install** Cinemachine |
| Main Camera | **Edit** — add `CinemachineBrain` |
| `VC_Village` GO | **Create** — `CinemachineVirtualCamera` + `CinemachineImpulseListener` |
| `VC_Combat` GO | **Create** — `CinemachineVirtualCamera` (disabled by default) |
| `VC_WaveClear` GO | **Create** — `CinemachineVirtualCamera` (disabled by default) |
| `Assets/_Modules/Village/Camera/CinemachineCameraController.cs` | **Create** |
| `Assets/_Modules/Village/Camera/CameraShakeManager.cs` | **Edit** — delegate to Cinemachine |
| `WaveCelebrationManager.cs` | **Edit** — call `PlayWaveClearCinematic()` |

---

## Acceptance Criteria

- [ ] Hero is smoothly followed by `VC_Village` with damped movement
- [ ] When enemies enter `combatZoomRadius`, camera blends to `VC_Combat`
      (closer, snappier) within 0.35 s
- [ ] Camera blends back to `VC_Village` when no enemies are near
- [ ] Wave clear triggers `VC_WaveClear` pull-back for the slow-mo duration
- [ ] `CinemachineCameraController.Shake(ShakeTier.Medium)` produces visible impulse shake
- [ ] All existing `CameraShakeManager.Instance?.Shake(...)` calls still work —
      they delegate to Cinemachine automatically
- [ ] Mobile shake is scaled by 0.55× automatically
- [ ] No camera clipping or NaN errors at any camera position
