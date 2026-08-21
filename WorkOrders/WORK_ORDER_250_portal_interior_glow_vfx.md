**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 250 — Portal Interior Glow VFX
**Status: READY TO IMPLEMENT**
**WO:** 250 | **Lane:** CODE (parallel safe)
**Closes:** DEF-100

---
## Problem

Portal arch interior is a flat dead prop — no VFX, no sense of depth or destination. Player has no visual cue that it's enterable.

---
## Implementation

**File:** `Assets/_Modules/Village/DungeonPortal.cs` (extend existing component)

Add two VFX layers using existing project assets only:

**Layer 1 — Idle loop (always on):**
```csharp
// On Start(): spawn a looping particle system inside the arch
var idleVfx = VFXManager.Play(VFXType.Portal_Idle, transform.position + Vector3.forward * 0.1f);
// If Portal_Idle doesn't exist: use VFXType.SpellBurst with emission rate 0.5, loop = true
// Scale: 0.6f — fills arch interior without overflowing
```

**Layer 2 — Proximity intensify:**
```csharp
// In Update() — when hero enters 3m trigger:
float dist = Vector3.Distance(_hero.position, transform.position);
if (dist < 3f)
{
    float t = 1f - (dist / 3f);   // 0 at 3m, 1 at 0m
    _idleVfx.emission.rateOverTimeMultiplier = Mathf.Lerp(0.5f, 3f, t);
}
```

**Add SfxId:** `Portal_Ambient` — low ambient hum, looped, 3D spatial. Fire on `Start()` via `CoreServices.Audio?.PlaySfx(SfxId.Portal_Ambient)`.

---
## Acceptance criteria
- [ ] Portal arch interior shows a looping particle or additive-glow effect at idle
- [ ] Effect emission increases ≥1.5× when player enters 3m radius
- [ ] No frame-rate drop >5fps on mobile WebGL with effect active
- [ ] Uses only existing VFX assets from `VFXManager` — no new external imports
- [ ] No UXML / UIDocument
- [ ] Null-conditional on all `CoreServices.Audio` calls
- [ ] Brace balance check passed

## What NOT to touch
- `Village.unity` — do not hand-edit
- `DungeonPortal` entry/exit logic — VFX layer only

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
