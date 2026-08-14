# WORK ORDER 55 — Torch & Environmental Fire Polish (Lana Studio)

**Status:** PARTIAL - remaining: editor/scene wiring (code is dead, zero scene refs)

> **PARTIAL - re-scoped 2026-08-14 (phantom sweep).** Remaining work: EDITOR/SCENE WIRING. TorchFireController is code-complete but a GUID search finds ZERO prefab/scene references, so it is dead code. Remaining: wire it in the editor.
> Everything else in this WO is present in HEAD. The named remainder IS the ticket now - do not
> re-implement the shipped part.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Date:** 2026-05-28
**Priority:** High
**Scope:** Small — one script, applied to all torch/brazier prefabs
**Depends on:** WO-50 (VFXManager) recommended but optional

---

## Goal

Every torch, brazier, and lantern in the village gets dynamic flickering fire,
rising embers, and a warm point light. Optionally the fire intensifies when
combat is happening nearby, rewarding the player with atmospheric feedback.

---

## 1. Create `TorchFireController.cs`

**Path:** `Assets/_Modules/Environment/TorchFireController.cs`

```csharp
using System.Collections;
using UnityEngine;

/// <summary>
/// Drives dynamic fire VFX + warm point light on a torch, brazier, or lantern.
/// Attach to any GameObject that has a child ParticleSystem (fire) and
/// optionally a child Light.
/// </summary>
public class TorchFireController : MonoBehaviour
{
    [Header("Particle Systems")]
    [Tooltip("Main fire particle system (from Lana Studio).")]
    public ParticleSystem fireParticles;
    [Tooltip("Rising embers / sparks particle system.")]
    public ParticleSystem emberParticles;

    [Header("Light")]
    public Light pointLight;
    [Range(0.5f, 4f)] public float baseLightIntensity  = 1.4f;
    [Range(0f, 0.5f)] public float flickerAmplitude    = 0.28f;
    public float flickerSpeed = 4.5f;

    [Header("Combat Reaction")]
    [Tooltip("If true, fire intensifies when enemies are within combatRadius.")]
    public bool reactToCombat = true;
    public float combatRadius = 12f;
    [Range(1f, 3f)] public float combatIntensityMultiplier = 1.6f;
    public float combatFadeSpeed = 2f;

    // ── State ─────────────────────────────────────────────────────────────────
    private float _currentIntensityMultiplier = 1f;
    private float _targetIntensityMultiplier  = 1f;
    private float _flickerOffset;

    private void Awake()
    {
        // Auto-find child components if not assigned.
        if (fireParticles  == null) fireParticles  = GetComponentInChildren<ParticleSystem>();
        if (pointLight     == null) pointLight     = GetComponentInChildren<Light>();

        _flickerOffset = Random.Range(0f, 100f); // stagger flicker phase per torch
    }

    private void OnEnable()
    {
        if (fireParticles  != null && !fireParticles.isPlaying)  fireParticles.Play();
        if (emberParticles != null && !emberParticles.isPlaying) emberParticles.Play();
    }

    private void Update()
    {
        UpdateCombatReaction();
        UpdateFlicker();
    }

    // ── Flicker ───────────────────────────────────────────────────────────────

    private void UpdateFlicker()
    {
        if (pointLight == null) return;

        float noise = Mathf.PerlinNoise(
            Time.time * flickerSpeed + _flickerOffset, 0f);
        float flicker = Mathf.Lerp(
            baseLightIntensity - flickerAmplitude,
            baseLightIntensity + flickerAmplitude,
            noise);

        pointLight.intensity = flicker * _currentIntensityMultiplier;
    }

    // ── Combat Reaction ───────────────────────────────────────────────────────

    private void UpdateCombatReaction()
    {
        if (!reactToCombat) return;

        bool combatNearby = IsCombatNearby();
        _targetIntensityMultiplier = combatNearby
            ? combatIntensityMultiplier
            : 1f;

        _currentIntensityMultiplier = Mathf.MoveTowards(
            _currentIntensityMultiplier,
            _targetIntensityMultiplier,
            combatFadeSpeed * Time.deltaTime);

        // Scale ember emission with combat intensity.
        if (emberParticles != null)
        {
            var em = emberParticles.emission;
            em.rateOverTime = 8f * _currentIntensityMultiplier;
        }
    }

    private bool IsCombatNearby()
    {
        // Consider "combat nearby" if any enemy is alive within combatRadius.
        var cols = Physics.OverlapSphere(transform.position, combatRadius);
        foreach (var col in cols)
        {
            if (col.CompareTag("Enemy") || col.CompareTag("EnemyProjectile"))
                return true;
        }
        return false;
    }

    // ── Editor helpers ────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (!reactToCombat) return;
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, combatRadius);
    }
}
```

---

## 2. Lana Studio prefab wiring

1. Browse `Assets/Lana Studio/Casual RPG VFX/` for fire / torch prefabs.
2. For every torch, brazier, or lantern prefab in the village scene:
   a. Add `TorchFireController` to the root.
   b. Drag the existing fire `ParticleSystem` child into **Fire Particles**.
   c. Drag the embers/sparks child into **Ember Particles** (if present).
   d. Drag the `Light` child into **Point Light**.
   e. Set `Base Light Intensity` ≈ 1.2–1.8 depending on size.
3. If no ember sub-system exists, add one:
   - Duplicate the fire PS, reduce size and emission rate to ~8/s.
   - Set velocity upward, short lifetime, golden colour.

---

## 3. Performance note

`Physics.OverlapSphere` in `Update` on every torch is expensive if there are
many torches. If the village has >8 torches, switch to an event-based approach:
- `WaveManager` broadcasts `OnCombatStarted` / `OnCombatEnded`.
- `TorchFireController` subscribes and sets a local `_combatActive` flag.
- Remove the per-Update `IsCombatNearby()` call entirely.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Environment/TorchFireController.cs` | **Create** |
| All torch/brazier/lantern prefabs in scene | **Edit** — add `TorchFireController`, wire PS + Light |

---

## Acceptance Criteria

- [ ] All torches in the village flicker realistically (no uniform constant light)
- [ ] Each torch has a slightly different flicker phase (no synchronised pulsing)
- [ ] Embers rise from braziers
- [ ] Light intensity visibly increases when enemies are within 12 m (if `reactToCombat` true)
- [ ] Embers speed up slightly during combat
- [ ] Torches still look good when `combatRadius` gizmo is visible in Scene view
- [ ] No `NullReferenceException` when Light or Ember PS is not assigned
