<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 65 — Scene Transition & Portal VFX Polish

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Date:** 2026-05-28
**Priority:** High
**Scope:** Small-Medium — PortalVFXController + DungeonPortal edits + loading screen
**Depends on:** WO-50 (VFXManager), WO-61 (CameraShakeManager)

---

## Goal

Entering and exiting a dungeon feels magical and dramatic. A swirling vortex
activates when the portal is approached, a burst + flash plays when the player
steps through, and a matching exit effect plays on arrival. Loading screens
have subtle floating particles so transitions never feel empty.

---

## 1. Create `PortalVFXController.cs`

**Path:** `Assets/_Modules/Village/Dungeon/PortalVFXController.cs`

```csharp
using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to any dungeon portal GameObject. Drives idle vortex VFX,
/// activation VFX on approach, and entry/exit burst.
/// </summary>
public class PortalVFXController : MonoBehaviour
{
    [Header("Particle Systems")]
    [Tooltip("Looping swirling vortex — plays when portal is active.")]
    public ParticleSystem vortexParticles;
    [Tooltip("One-shot burst played when hero steps through.")]
    public ParticleSystem entryBurstParticles;

    [Header("Light")]
    public Light portalLight;
    [Range(0.5f, 5f)] public float idleLightIntensity   = 1.8f;
    [Range(1f, 8f)]   public float activeLightIntensity = 4.5f;

    [Header("Transition")]
    public float activationRadius = 4f;
    public float flashDuration    = 0.22f;

    private bool _active = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (vortexParticles != null) vortexParticles.Play();
        if (portalLight    != null) portalLight.intensity = idleLightIntensity;
    }

    private void Update()
    {
        if (Camera.main == null) return;
        // Could also check hero distance from DungeonPortal — handled there.
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Call from DungeonPortal when hero enters activation radius.</summary>
    public void OnHeroApproach()
    {
        if (_active) return;
        _active = true;
        StartCoroutine(ActivateRoutine());
    }

    /// <summary>Call from DungeonPortal when hero steps through.</summary>
    public void OnHeroEnter()
    {
        entryBurstParticles?.Play();
        VFXManager.Instance?.Play(VFXType.Portal_Enter, transform.position);
        CameraShakeManager.Instance?.Shake(ShakeTier.Medium, 0.3f);
        // AudioService.Instance?.PlaySfx(SfxId.PortalEnter);
        StartCoroutine(ScreenFlashRoutine());
    }

    /// <summary>Call in the destination scene to play the exit effect.</summary>
    public void OnHeroExit()
    {
        entryBurstParticles?.Play();
        VFXManager.Instance?.Play(VFXType.Portal_Exit, transform.position);
        // AudioService.Instance?.PlaySfx(SfxId.PortalExit);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private IEnumerator ActivateRoutine()
    {
        float elapsed = 0f;
        float rampTime = 0.5f;
        while (elapsed < rampTime)
        {
            if (portalLight != null)
                portalLight.intensity = Mathf.Lerp(
                    idleLightIntensity, activeLightIntensity, elapsed / rampTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator ScreenFlashRoutine()
    {
        var flash = GameObject.FindWithTag("ScreenFlash");
        if (flash == null) yield break;
        var img = flash.GetComponent<UnityEngine.UI.Image>();
        if (img == null) yield break;

        img.color = Color.white;
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            img.color = Color.Lerp(Color.white, Color.clear, elapsed / flashDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        img.color = Color.clear;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, activationRadius);
    }
}
```

---

## 2. Edit `DungeonPortal.cs`

Wire the `PortalVFXController` calls into the existing proximity trigger logic:

```csharp
// Add field:
private PortalVFXController _portalVfx;

// In Awake/Start:
_portalVfx = GetComponent<PortalVFXController>();

// When hero enters activation radius:
_portalVfx?.OnHeroApproach();

// When hero confirms entry (F key / tap):
_portalVfx?.OnHeroEnter();
// ... then load scene ...
```

In the destination scene's bootstrap:
```csharp
// Find the exit portal and play arrival effect:
FindObjectOfType<PortalVFXController>()?.OnHeroExit();
```

---

## 3. Add `VFXType` entries

```csharp
Portal_Enter,
Portal_Exit,
```

Register prefabs in VFXCatalog:
- `Portal_Enter`: swirling violet/blue vortex + bright flash, lifetime 1.2 s.
- `Portal_Exit`: same reversed / softer, lifetime 0.8 s.

---

## 4. Loading screen floating particles

In the loading screen scene/canvas, add a looping ParticleSystem:
- Emission rate: 4–6/s.
- Particles: tiny wisps or stars, semi-transparent.
- Drift slowly upward.
- Colour: matches the dungeon/village tone being loaded into.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Village/Dungeon/PortalVFXController.cs` | **Create** |
| `Assets/_Modules/Village/Dungeon/DungeonPortal.cs` | **Edit** — wire `PortalVFXController` |
| `Assets/_Modules/VFX/VFXManager.cs` | **Edit** — add `Portal_Enter`, `Portal_Exit` |
| Loading screen scene/prefab | **Edit** — add floating particle system |

---

## Acceptance Criteria

- [ ] Portal light ramps up when hero comes within 4 m
- [ ] Stepping through plays vortex burst + white screen flash + medium shake
- [ ] Arrival in destination scene plays the exit VFX
- [ ] Loading screen has subtle floating particles (never fully blank)
- [ ] All portal effects respect mobile quality settings

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `PortalVFXController.cs:26` — portal vfx shipped. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
