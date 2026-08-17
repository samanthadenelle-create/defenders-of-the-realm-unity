<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 64 — Master Performance & Quality System + Final Polish Pass

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** Critical (final step before testing)
**Scope:** Large — ties WO-51/53/57 together + polish sweep
**Depends on:** WO-51 (PerformanceManager), WO-53 (AnimatorCullingController),
WO-57 (MobileQualitySettings), WO-55 (TorchFireController)

---

## Goal

One `GameQualityController` singleton is the single source of truth for quality.
It drives VFXManager, all `AnimatorCullingController` instances, post-processing,
and `WeatherManager` intensity from one place. A simple Options toggle applies
changes instantly and persists across sessions.

---

## 1. Create `GameQualityController.cs`

**Path:** `Assets/_Modules/Settings/GameQualityController.cs`

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GameQualityController : MonoBehaviour
{
    public static GameQualityController Instance { get; private set; }

    [Header("Quality Assets")]
    public MobileQualitySettings low;
    public MobileQualitySettings medium;
    public MobileQualitySettings high;

    [Header("References")]
    public Volume postProcessVolume;

    private MobileQualitySettings _active;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        QualityTier saved = (QualityTier)PlayerPrefs.GetInt(
            "QualityTier", (int)QualityTier.Medium);
        ApplyTier(saved);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ApplyTier(QualityTier tier)
    {
        _active = tier == QualityTier.Low  ? low
                : tier == QualityTier.High ? high
                : medium;

        _active?.Apply();                          // calls PerformanceManager
        ApplyToVFXManager();
        ApplyToWeather();
        ApplyToPostProcess();

        PlayerPrefs.SetInt("QualityTier", (int)tier);
        Debug.Log($"[GameQualityController] Quality set to {tier}");
    }

    public MobileQualitySettings Current => _active;

    // ── Appliers ──────────────────────────────────────────────────────────────

    private void ApplyToVFXManager()
    {
        if (VFXManager.Instance == null || _active == null) return;
        VFXManager.Instance.defaultPoolSize = _active.tier == QualityTier.Low  ?  5
                                            : _active.tier == QualityTier.High ? 16
                                            : 8;
    }

    private void ApplyToWeather()
    {
        if (WeatherManager.Instance == null || _active == null) return;
        WeatherManager.Instance.enableOnMobile = _active.tier != QualityTier.Low;
        if (_active.tier == QualityTier.Low)
            WeatherManager.Instance.SetRain(0f);
    }

    private void ApplyToPostProcess()
    {
        if (postProcessVolume == null || _active == null) return;
        postProcessVolume.weight = _active.enablePostProcessing ? 1f : 0f;
    }
}
```

---

## 2. Options menu toggle

Wire three buttons (Low / Medium / High) to:

```csharp
// Low button onClick:
GameQualityController.Instance.ApplyTier(QualityTier.Low);

// Medium button onClick:
GameQualityController.Instance.ApplyTier(QualityTier.Medium);

// High button onClick:
GameQualityController.Instance.ApplyTier(QualityTier.High);
```

---

## 3. Editor debug menu

**Path:** `Assets/Editor/QualityDebugMenu.cs`

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class QualityDebugMenu
{
    [MenuItem("Defenders/Debug/Quality — Low")]
    static void SetLow()   => GameQualityController.Instance?.ApplyTier(QualityTier.Low);

    [MenuItem("Defenders/Debug/Quality — Medium")]
    static void SetMedium() => GameQualityController.Instance?.ApplyTier(QualityTier.Medium);

    [MenuItem("Defenders/Debug/Quality — High")]
    static void SetHigh()  => GameQualityController.Instance?.ApplyTier(QualityTier.High);

    [MenuItem("Defenders/Debug/Spawn Test Enemy")]
    static void SpawnEnemy()
    {
        var prefab = Resources.Load<GameObject>("Enemies/TestEnemy");
        if (prefab != null)
            Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
    }

    [MenuItem("Defenders/Debug/Play VFX — Fire Explosion")]
    static void PlayFireVfx()
        => VFXManager.Instance?.Play(VFXType.Impact_ExplosionFire,
               SceneView.lastActiveSceneView.pivot);

    [MenuItem("Defenders/Debug/Toggle Rain")]
    static void ToggleRain()
        => WeatherManager.Instance?.ToggleRain(
               WeatherManager.Instance.rainIntensity <= 0f);
}
#endif
```

---

## 4. Final polish sweep

Claude must perform these in the same pass:

- [ ] Confirm every torch/brazier in the Village scene has `TorchFireController` (WO-55)
- [ ] Confirm `FootstepDustController` is on all hero and enemy prefabs (WO-61)
- [ ] Add 1–2 `ParticleSystem` "floating dust mote" objects to the village scene
      (very low emission, slow upward drift, semi-transparent white, lifetime 8 s)
- [ ] All decal, hit-reaction, and screen-shake calls use
      `if (!MobileQualitySettings.Current.enableScreenShake) return` guards
- [ ] Run a final mobile test: full wave, 10+ enemies, Low quality → target 60 FPS

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Settings/GameQualityController.cs` | **Create** |
| `Assets/Editor/QualityDebugMenu.cs` | **Create** |
| Options menu UI | **Edit** — wire 3 quality buttons |
| Village scene | **Edit** — add dust motes, verify torch/footstep coverage |

---

## Acceptance Criteria

- [ ] `GameQualityController.ApplyTier(QualityTier.Low)` visibly reduces particles,
      disables post-process, and stops weather in one call
- [ ] Quality persists across app restarts
- [ ] Editor debug menu items all work from the Unity menu bar
- [ ] All torches flicker, all characters have footstep dust
- [ ] 60 FPS on mid-range Android during 10-enemy wave at Low quality
