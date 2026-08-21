<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-29
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-29) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 590 — Fill the Castle-Seam Dip with Water + Fish (fix floating-castle look)

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Owner directive:** 2026-06-29 felt-test. Outside the castle, OuterWorld landscape bends UNDER the
MainCastle scene landscape, leaving a DIP — the castle reads as FLOATING. Owner OK with the bend by
design; wants WATER in the dip so it reads as a moat/lake waterfront. Owner provided a Grok spec
(animated water plane + URP scrolling normals + a FishSchool). She noted we already have animated
water + fish assets.

## What we already have (REUSE — do not greenfield)
- `Assets/_Modules/Village/World/CastleMoatBuilder.cs` — runtime-injected (RuntimeInitializeOnLoad +
  sceneLoaded), flag-gated `FeatureFlags.CastleMoat` (default ON), builds a translucent teal water
  RING (r=46m, width 3m, `WaterY=-0.4`) + 4 drawbridges, and attaches `MoatWaterShimmer` (DEF-195 —
  the scrolling ripple normal = the "animated water"). Idempotent, WebGL-safe, mobile-cheap, FlowTrace'd.
- `Assets/_Modules/Village/MoatWaterShimmer.cs` — the animated water component.
- `ExteriorTerrainBuilder.ApplyWater` — alt water tint path.
- Fish: `Assets/polyperfect/.../Animals_M/SM_Fish.fbx` + `SM_Fish_Swarm.fbx` (gitignored pack — present
  in owner's editor, ABSENT on clean clone → must degrade gracefully per §4).

## Goal
The castle should sit on/ beside water, not float over a void.

## Scope
1. **Measure the dip (SME, instrument-first §12).** Find the seam geometry: where OuterWorld terrain
   bends under MainCastle, the dip's Y (bottom) and its XZ extent (how far the visible gap rings the
   castle island). Sources: CastleMoatBuilder (r=46 perimeter), the OuterWorld boundary/terrain
   injector, ExteriorTerrainBuilder, and any castle↔outerworld seam offset. Do NOT guess Y/extent —
   read it from the builders or a headless FloorDiag-style dump.
2. **Water FILL (extend CastleMoatBuilder, don't fork).** Add a broad water surface that fills the dip
   ring between the castle island edge and the OuterWorld terrain (a "lake fill" beyond the existing
   thin moat ring), at the measured dip Y, so no void shows under the castle. Reuse the existing
   transparent URP/Lit water material + `MoatWaterShimmer`. Keep it flag-gated, idempotent, collider-
   stripped (visual only), shared-material, ASCII-only, FlowTrace'd. Tunable constants like the moat.
3. **Fish school (graceful).** Add a small `FishSchool` MonoBehaviour (wander + stay-in-bounds, per
   Grok's spec; optional gentle bob + per-fish speed/scale variation) spawning N fish inside the water
   bounds, just under `WaterY`. Load the fish model from a committed path; if absent (clean clone),
   `Debug.LogWarning` and skip (NO hard error, §4). To make fish survive a clean build, copy a single
   low-poly fish mesh into `Assets/Resources/` (e.g. `Resources/Env/Fish.prefab` or load SM_Fish);
   if that's not feasible without the gitignored pack, gate fish behind the asset's presence and note
   the gap in a FlowTrace line (no silent truncation).

## Acceptance criteria
- In MainCastle_Hall, looking out from the castle, the dip reads as water (no floating-over-void).
- Water animates (shimmer) and is de-glossed teal (no glassy pane).
- Fish swim within the water when the model is available; clean build logs a graceful skip, never errors.
- Flag-gated (`ff.castlemoat` 0 hides all of it). Idempotent across scene reloads. No scene file edits.
- Gate: COMPILE_GATE_OK + REGRESSION_OK.

## What NOT to touch
- No `.unity` scene hand-edits (runtime injector pattern only, §3).
- No navmesh re-bake / castle footprint change (that's the moat design note's editor-lane work).
- No new gitignored-pack hard dependency at runtime (graceful-optional only, §4).
- Don't touch the inventory files (parallel lane).

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `CastleMoatBuilder.cs:275,420; FishSchool.cs` — moat band + fish. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
