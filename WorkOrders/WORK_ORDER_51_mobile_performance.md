<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 51 — Mobile Performance Settings

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Medium — ScriptableObject quality tiers + runtime applier
**Depends on:** WO-53 (AnimatorCullingController — apply culling tier from here)

---

## Goal

Centralise all mobile performance knobs in a single `MobilePerformanceSettings`
ScriptableObject with three tiers (Low / Medium / High). A `PerformanceManager`
MonoBehaviour reads the active tier at startup and applies it — particle limits,
post-processing intensity, shadow quality, and animator culling distances — so
nothing is hardcoded in individual systems.

---

## 1. Create `MobilePerformanceSettings.cs`

**Path:** `Assets/_Modules/Performance/MobilePerformanceSettings.cs`

```csharp
using System;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Defenders/Performance/Mobile Performance Settings",
                 fileName = "MobilePerformanceSettings")]
public class MobilePerformanceSettings : ScriptableObject
{
    public QualityTier activeTier = QualityTier.Medium;

    [Header("Low")]
    public PerformanceTier low    = PerformanceTier.DefaultLow();

    [Header("Medium")]
    public PerformanceTier medium = PerformanceTier.DefaultMedium();

    [Header("High")]
    public PerformanceTier high   = PerformanceTier.DefaultHigh();

    public PerformanceTier Active => activeTier switch
    {
        QualityTier.Low    => low,
        QualityTier.High   => high,
        _                  => medium,
    };
}

public enum QualityTier { Low, Medium, High }

[Serializable]
public struct PerformanceTier
{
    [Header("Particles")]
    [Tooltip("Global multiplier on ParticleSystem maxParticles.")]
    [Range(0.1f, 1f)] public float particleCountMultiplier;

    [Header("Post-Processing")]
    [Tooltip("Master intensity multiplier for all post-process volumes.")]
    [Range(0f, 1f)] public float postProcessIntensity;
    public bool enableBloom;
    public bool enableDepthOfField;
    public bool enableMotionBlur;

    [Header("Shadows")]
    public ShadowQuality shadowQuality;
    public ShadowResolution shadowResolution;
    [Range(10f, 150f)] public float shadowDistance;

    [Header("Animator Culling Distances")]
    [Tooltip("Distance at which heroes switch to CullUpdateTransforms.")]
    public float heroCullUpdateDistance;
    [Tooltip("Distance at which enemies switch to CullUpdateTransforms.")]
    public float enemyCullUpdateDistance;
    [Tooltip("Distance at which background NPCs switch to CullUpdateTransforms.")]
    public float npcCullUpdateDistance;

    // ── Defaults ─────────────────────────────────────────────────────────────

    public static PerformanceTier DefaultLow() => new PerformanceTier
    {
        particleCountMultiplier  = 0.3f,
        postProcessIntensity     = 0f,
        enableBloom              = false,
        enableDepthOfField       = false,
        enableMotionBlur         = false,
        shadowQuality            = ShadowQuality.Disable,
        shadowResolution         = ShadowResolution.Low,
        shadowDistance           = 15f,
        heroCullUpdateDistance   = 20f,
        enemyCullUpdateDistance  = 15f,
        npcCullUpdateDistance    = 10f,
    };

    public static PerformanceTier DefaultMedium() => new PerformanceTier
    {
        particleCountMultiplier  = 0.6f,
        postProcessIntensity     = 0.5f,
        enableBloom              = true,
        enableDepthOfField       = false,
        enableMotionBlur         = false,
        shadowQuality            = ShadowQuality.HardOnly,
        shadowResolution         = ShadowResolution.Medium,
        shadowDistance           = 40f,
        heroCullUpdateDistance   = 30f,
        enemyCullUpdateDistance  = 25f,
        npcCullUpdateDistance    = 15f,
    };

    public static PerformanceTier DefaultHigh() => new PerformanceTier
    {
        particleCountMultiplier  = 1f,
        postProcessIntensity     = 1f,
        enableBloom              = true,
        enableDepthOfField       = true,
        enableMotionBlur         = true,
        shadowQuality            = ShadowQuality.All,
        shadowResolution         = ShadowResolution.High,
        shadowDistance           = 80f,
        heroCullUpdateDistance   = 60f,
        enemyCullUpdateDistance  = 45f,
        npcCullUpdateDistance    = 35f,
    };
}
```

---

## 2. Create `PerformanceManager.cs`

**Path:** `Assets/_Modules/Performance/PerformanceManager.cs`

Reads the active tier on `Awake` and applies it. Also exposes
`SetTier(QualityTier)` so a settings screen can switch at runtime.

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PerformanceManager : MonoBehaviour
{
    public static PerformanceManager Instance { get; private set; }

    [Header("Settings Asset")]
    public MobilePerformanceSettings settings;

    [Header("Post Processing")]
    [Tooltip("Assign the global Volume that owns Bloom / DoF / MotionBlur.")]
    public Volume globalPostProcessVolume;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (settings == null)
        {
            Debug.LogWarning("[PerformanceManager] No settings asset assigned.");
            return;
        }

        // Auto-select tier based on device RAM on first run.
        if (!PlayerPrefs.HasKey("QualityTier"))
            AutoSelectTier();

        settings.activeTier = (QualityTier)PlayerPrefs.GetInt(
            "QualityTier", (int)settings.activeTier);

        Apply(settings.Active);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void SetTier(QualityTier tier)
    {
        settings.activeTier = tier;
        PlayerPrefs.SetInt("QualityTier", (int)tier);
        Apply(settings.Active);
    }

    // ── Apply ────────────────────────────────────────────────────────────────

    private void Apply(PerformanceTier tier)
    {
        ApplyShadows(tier);
        ApplyPostProcess(tier);
        ApplyParticles(tier);
        ApplyAnimatorCulling(tier);
        Debug.Log($"[PerformanceManager] Applied tier: {settings.activeTier}");
    }

    private void ApplyShadows(PerformanceTier tier)
    {
        QualitySettings.shadows           = tier.shadowQuality;
        QualitySettings.shadowResolution  = tier.shadowResolution;
        QualitySettings.shadowDistance    = tier.shadowDistance;
    }

    private void ApplyPostProcess(PerformanceTier tier)
    {
        if (globalPostProcessVolume == null) return;
        globalPostProcessVolume.weight = tier.postProcessIntensity;

        if (globalPostProcessVolume.profile.TryGet<Bloom>(out var bloom))
            bloom.active = tier.enableBloom;
        if (globalPostProcessVolume.profile.TryGet<DepthOfField>(out var dof))
            dof.active = tier.enableDepthOfField;
        if (globalPostProcessVolume.profile.TryGet<MotionBlur>(out var mb))
            mb.active = tier.enableMotionBlur;
    }

    private void ApplyParticles(PerformanceTier tier)
    {
        foreach (var ps in FindObjectsOfType<ParticleSystem>())
        {
            var main = ps.main;
            // Scale down max particles — clamp so we don't set it to 0.
            main.maxParticles = Mathf.Max(1,
                Mathf.RoundToInt(main.maxParticles * tier.particleCountMultiplier));
        }
    }

    private void ApplyAnimatorCulling(PerformanceTier tier)
    {
        foreach (var cc in FindObjectsOfType<AnimatorCullingController>())
            cc.ApplyTier(tier);
    }

    // ── Auto-select ──────────────────────────────────────────────────────────

    private void AutoSelectTier()
    {
        int ram = SystemInfo.systemMemorySize;
        QualityTier tier = ram >= 4096 ? QualityTier.High
                         : ram >= 2048 ? QualityTier.Medium
                         :               QualityTier.Low;
        PlayerPrefs.SetInt("QualityTier", (int)tier);
        Debug.Log($"[PerformanceManager] Auto-selected tier: {tier} ({ram} MB RAM)");
    }
}
```

> **Note:** `AnimatorCullingController.ApplyTier(PerformanceTier)` is defined in
> WO-53. Make sure WO-53 is implemented before wiring this up.

---

## 3. Scene wiring

1. Create an empty GameObject in the Village (persistent) scene: `PerformanceManager`.
2. Add `PerformanceManager` component.
3. Create the settings asset: right-click in Project → Create → Defenders →
   Performance → Mobile Performance Settings. Save to
   `Assets/Resources/Settings/MobilePerformanceSettings.asset`.
4. Assign the asset to the **Settings** field.
5. Assign the global URP post-process Volume to the **Global Post Process Volume**
   field.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Performance/MobilePerformanceSettings.cs` | **Create** |
| `Assets/_Modules/Performance/PerformanceManager.cs` | **Create** |
| `Assets/Resources/Settings/MobilePerformanceSettings.asset` | **Create** (in Editor) |
| `Assets/_Modules/Animation/AnimatorCullingController.cs` | **Edit** — add `ApplyTier(PerformanceTier)` (see WO-53) |

---

## Acceptance Criteria

- [ ] Switching to **Low** disables shadows, halves particle counts, zeroes post-process weight
- [ ] Switching to **High** restores full quality; bloom / DoF / motion blur re-enable
- [ ] Tier selection persists across app restarts via `PlayerPrefs`
- [ ] On first launch, tier is auto-selected based on device RAM
- [ ] `PerformanceManager.SetTier()` can be called from a settings UI button at runtime
- [ ] No `NullReferenceException` if the post-process Volume is not assigned
