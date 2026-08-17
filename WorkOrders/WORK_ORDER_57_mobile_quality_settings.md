<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 57 — Mobile Quality Settings System

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-28
**Priority:** High
**Scope:** Small-Medium — ScriptableObject + options UI toggle
**Depends on:** WO-51 (MobilePerformanceSettings), WO-53 (AnimatorCullingController)
**Note:** WO-51 is the fuller implementation. This WO adds the `MobileQualitySettings`
companion ScriptableObject and the in-game UI toggle that calls `PerformanceManager.SetTier()`.

---

## Goal

Give players a visible Low / Medium / High quality toggle in the Options menu
that applies instantly to VFX, animator culling, and post-processing.

---

## 1. Create `MobileQualitySettings.cs`

**Path:** `Assets/_Modules/Settings/MobileQualitySettings.cs`

```csharp
using UnityEngine;

/// <summary>
/// Lightweight per-tier ScriptableObject. Create three assets
/// (Low / Medium / High) and assign them in QualityToggleUI.
/// For the full parameter set see MobilePerformanceSettings (WO-51).
/// </summary>
[CreateAssetMenu(menuName = "Defenders/Mobile Quality Settings")]
public class MobileQualitySettings : ScriptableObject
{
    [Header("General")]
    [Range(0.1f, 1f)] public float particleMultiplier    = 1f;
    [Range(0.1f, 1f)] public float vfxScaleMultiplier    = 1f;
    public bool enablePostProcessing = true;
    public bool enableScreenShake    = true;
    public bool enableHitStop        = true;

    [Header("Animator Culling Distances")]
    public float heroCullDistance  = 60f;
    public float enemyCullDistance = 35f;
    public float petCullDistance   = 45f;

    // The tier this asset represents — must match the PerformanceTier in WO-51.
    public QualityTier tier = QualityTier.Medium;

    public static MobileQualitySettings Current { get; private set; }

    /// <summary>Apply this asset and forward to PerformanceManager.</summary>
    public void Apply()
    {
        Current = this;

        // Forward to PerformanceManager (WO-51) so the single authoritative
        // quality applier does the heavy lifting.
        if (PerformanceManager.Instance != null)
        {
            PerformanceManager.Instance.SetTier(tier);
            return;
        }

        // Fallback: apply culling manually if PerformanceManager not present.
        foreach (var c in Object.FindObjectsOfType<AnimatorCullingController>())
        {
            if      (c.CompareTag("Hero"))  c.updateTransformsDistance = heroCullDistance;
            else if (c.CompareTag("Enemy")) c.updateTransformsDistance = enemyCullDistance;
            else if (c.CompareTag("Pet"))   c.updateTransformsDistance = petCullDistance;
        }
    }
}
```

---

## 2. Create three ScriptableObject assets

In the Project window: right-click → Create → Defenders → Mobile Quality Settings.
Create three assets:

| Asset name | `tier` | `particleMultiplier` | `enablePostProcessing` |
|---|---|---|---|
| `QualityLow.asset` | Low | 0.3 | false |
| `QualityMedium.asset` | Medium | 0.6 | true |
| `QualityHigh.asset` | High | 1.0 | true |

Save to `Assets/Resources/Settings/`.

---

## 3. Create `QualityToggleUI.cs`

**Path:** `Assets/_Modules/Settings/QualityToggleUI.cs`

Simple three-button toggle for the Options panel:

```csharp
using UnityEngine;
using UnityEngine.UI;

public class QualityToggleUI : MonoBehaviour
{
    [Header("Quality Assets")]
    public MobileQualitySettings low;
    public MobileQualitySettings medium;
    public MobileQualitySettings high;

    [Header("Buttons")]
    public Button btnLow;
    public Button btnMedium;
    public Button btnHigh;

    private void Start()
    {
        btnLow   .onClick.AddListener(() => Select(low));
        btnMedium.onClick.AddListener(() => Select(medium));
        btnHigh  .onClick.AddListener(() => Select(high));

        // Restore saved choice.
        var saved = (QualityTier)PlayerPrefs.GetInt("QualityTier", (int)QualityTier.Medium);
        var initial = saved == QualityTier.Low  ? low
                    : saved == QualityTier.High ? high
                    : medium;
        Select(initial, save: false);
    }

    private void Select(MobileQualitySettings settings, bool save = true)
    {
        settings.Apply();
        if (save)
            PlayerPrefs.SetInt("QualityTier", (int)settings.tier);

        // Visual feedback — highlight the active button.
        btnLow   .interactable = settings != low;
        btnMedium.interactable = settings != medium;
        btnHigh  .interactable = settings != high;
    }
}
```

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Settings/MobileQualitySettings.cs` | **Create** |
| `Assets/_Modules/Settings/QualityToggleUI.cs` | **Create** |
| `Assets/Resources/Settings/QualityLow.asset` | **Create** (in Editor) |
| `Assets/Resources/Settings/QualityMedium.asset` | **Create** (in Editor) |
| `Assets/Resources/Settings/QualityHigh.asset` | **Create** (in Editor) |
| Options menu scene/prefab | **Edit** — add `QualityToggleUI` + wire 3 buttons |

---

## Acceptance Criteria

- [ ] Tapping Low in Options immediately drops particle counts and disables post-process
- [ ] Tapping High restores full quality with no scene reload
- [ ] Selected tier persists across app restarts
- [ ] `AnimatorCullingController` distances update when tier changes
- [ ] UI correctly highlights the currently active button
