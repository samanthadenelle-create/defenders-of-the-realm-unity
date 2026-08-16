# RESULT — WO-1100 portal threshold aura NULL material (MagentaProbe M2)

**Date:** 2026-08-16  **Seat:** CLI (commit `bb9844a97`)
**Status:** IMPLEMENTED - theory DISPROVED; one OWNER RULING left open (see below)

## Finding: the WO's premise was wrong

The 12 M2 FAIL captures (seq 2404-2415) were **MagentaGuard FALSE-POSITIVES**: the probed
renderers are **authored-DISABLED container renderers** — 339 such renderers exist across the
packs, deliberately shipped with no material because they never render. The portal aura's
material was never missing; nothing was visually broken.

## What changed

1. **Normalizer** landed in `VFXManager.Hovl.cs`: authored-disabled null-slot container
   renderers are normalized so MagentaGuard no longer flags them as M2 — the guard's signal
   stays clean for GENUINE defects.
2. **New `[vfx-null-slot]` regression suite**: scans for enabled renderers with null material
   slots so the next real escape fails the gate instead of the owner's morning (the WO §3.3
   gate-widening intent, delivered as a suite).
3. **5 GENUINE enabled null-slot ParticlePack prefabs found** by that scan:
   `PP_EarthShatter`, `PP_GoopSpray` (x3 variants), `PP_LightnigStormCloud`.
   **OWNER RULING OPEN: retag-or-repair** — per the no-creative-substitution rule these are
   the owner's call, not a CLI pick. They are surfaced, not silently patched.
4. Separate but portal-related: the portal later gained the **owner-picked dark-star circle**
   (owner tag "use this rotated for the portals", commit `264bbf7fb`).

## Files

- `Assets/_Modules/VFX/VFXManager.Hovl.cs` (normalizer)
- `[vfx-null-slot]` regression suite (new, registered)

## Verification

- Gate green + committed; the MagentaProbe M2 line is absent for portal spawns with the
  normalizer in place; the new suite is the permanent tripwire.

## PO action

Rule on the 5 genuine null-slot ParticlePack prefabs: retag (pick a replacement effect) or
repair (restore/assign their materials). Nothing else pends on this WO.
