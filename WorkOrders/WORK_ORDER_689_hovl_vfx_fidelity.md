**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

> **SOURCE: Grok execution package 2026-07-12** (owner-relayed, built from the docs/SME dossier fleet). Slotted into the WO numbering by CLI; reconcile against docs/SME/WO677_PHASE0_APPLICABILITY.md (the code-verified assessment).

# 🛠️ Work Order: Hovl Studio VFX Fidelity Fix (Backlog Item #3)

**Priority:** P0 (Visual quality)  
**Effort:** Medium  
**Impact:** Very High — makes Hovl look like the demos again

---

## Goal
Close the four concrete visual gaps that make our Hovl VFX look significantly worse than the vendor demos.

## The Four Gaps (from assessment)

1. **Bloom is off**  
   Demo scenes use Bloom intensity 5. Our default volume has Bloom off (only arena has local bloom 1.4). Overworld looks glow-less.

2. **Our tint flattens the art**  
   `ApplyStartColor` flood-fills every particle system with one flat color, destroying the authored bright-core / soft-halo layering. Hovl’s own recolor script only shifts hue and preserves saturation/value/alpha.

3. **Trails are cut mid-air**  
   Our `handle.Stop()` hard-clears the projectile on impact. Vendor mover lets trails finish their lifetime.

4. **Impacts spawn unrotated**  
   Impacts use identity rotation instead of being oriented to the hit surface.

---

## Tasks for Claude

### 1. Bloom
- Add a global Volume Profile (or update the existing one) with Bloom enabled.
- Recommended starting values (match vendor demos):  
  - Intensity: 4.5–5.5  
  - Threshold: 0.9–1.1  
  - Soft Knee: 0.5  
  - Diffusion: 7  
- Make sure it only affects the overworld / main camera (or provide a clean way to toggle per scene).

### 2. Fix Color Application
- Stop using the current `ApplyStartColor` that flattens everything.
- Implement (or switch to) a hue-only recolor method that preserves the original saturation, value, and alpha of Hovl particles (exactly like Hovl’s own recolor script).
- Document how to tint a VFX correctly going forward.

### 3. Trails
- Change projectile impact logic so that `Stop()` does **not** immediately kill trail renderers.
- Let trails finish their lifetime after impact (or give them a short fade-out).
- Prefer using Hovl’s own mover patterns where possible.

### 4. Impact Orientation
- When spawning impact VFX, set the rotation to match the hit surface normal (or the direction of the projectile at impact).
- Provide a clean utility method: `SpawnImpact(vfxKey, position, normal)`.

### 5. Validation
- Create a simple debug scene or add a test button that spawns the most common Hovl projectiles + impacts so we can A/B compare against the vendor demos.
- Log a clear “HOVL_FIDELITY_FIXED” message once all four gaps are closed.

---

## Deliverables
- Updated Volume / Bloom setup
- Correct color application method
- Trail lifetime fix
- Oriented impact spawning
- Short before/after notes + any new utility methods

**Do not** change any gameplay logic. This is pure visual fidelity.

Keep everything clean and well-commented.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
