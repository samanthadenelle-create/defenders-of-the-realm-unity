> ⚠ **NUMBER COLLISION — this document does not own WO-334; `WORK_ORDER_334_tower_placement_rotate_menu.md` does.**
> Referred to hereafter as **WO-334-B (asset orient/inspect tool)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

# WORK ORDER 334 — Reusable Asset Orient/Inspect tool (preview + save-recipe + report-back + color-error tag)

> Renumbered 332→**334** (owner 2026-06-07) — 332 belongs to DTT aim sensitivity. This is the full
> preview/orient pane built from the concept (docs/design/preview-pane-concept.jpg): 3-axis sliders + snap.

**Status: SPEC (owner 2026-06-07 — "yes, build it").** **Lane:** 10 (Build/Tooling). **Seed:** the
fixed `BuildPreviewModal` (WO-314) + the "perfect offset" reuse note in WO-288. **Thesis:** this IS the
"AI orientation tool" product idea + the systematic asset pipeline (orient → save recipe → verify →
bake), made into a reusable in-engine tool. Force-multiplier for every asset bug.

## Goal
Generalize the build-preview modal into a **reusable inspector callable on ANY asset** (prefab / mesh /
weapon / character / building) that lets a human dial it in visually, **persists the adjustments as a
per-id recipe**, and **reports them back to the agent** to bake into code — and **auto-flags color/material
errors**.

## Visual target (owner concept 2026-06-07 — docs/design/preview-pane-concept.jpg)
"This is how it should look." Full **3-axis** rotation — **X Pitch / Y Yaw / Z Roll** sliders (color-coded
red/green/blue) + per-axis nudge buttons + live value readouts, a **Snap** setting (e.g. 45°), the item name,
**Confirm Placement / Cancel / Reset Rotation**, ornate rune frame. **IGNORE the "SKR cost"** shown in the
concept — build costs use game resources (wood/iron/crystals), not the premium token; the orient pane shows
no cost line. The per-id recipe must
store the **full euler (pitch/yaw/roll)**, not just yaw — required for gear grips (bow/sword need pitch+roll).
The current WO-314 fix is the functional yaw-only foundation; this is the target it grows into.

## Concrete use cases (owner 2026-06-07 — "value can't be overstated")
ONE orient tool serves both structures AND gear, each persisting its own per-id offset recipe:
- **Tower placement** (build mode) — rotate before commit (the current BuildPreviewModal use).
- **Weapons — bow & sword** — the per-weapon GRIP/in-hand orientation (the bow-sideways bug class).
- **Armor** — fit/orientation on the body.
Foundation = fixing the preview pane (WO-314: render the object + reliable close). Build that solid first.

## Capabilities
1. **Preview anywhere** — `AssetInspector.Show(assetId or prefabPath)`; isolated preview rig (reuse the
   WO-314 PREVIEW_LAYER + offscreen root + masked cam/lights). Works in editor AND a dev runtime overlay.
2. **Adjust** — rotate (yaw/pitch/roll), position offset, scale; live readout (mirror the yaw readout).
3. **Save recipe** — persist the correction **per asset id** as JSON (generalize `RotationCorrectionRegistry`
   → an `OrientationRecipe { id, eulerOffset, posOffset, scale }` registry). Applied at spawn by the
   factories (StructureFactory / VisualFactory / GearVisualApplier) so the asset "always lands right."
4. **Report back to the agent** — write the saved recipes to a known data file (e.g.
   `docs/orient/orientation-recipes.json` or `Assets/Resources/Data/orientation-recipes.json`) that Claude
   reads to bake the values into code/factory defaults. Closes the loop: human dials → tool saves → agent applies.
5. **🎯 Color-error tagging (the enhancement)** — on preview, detect + TAG when an asset renders an ERROR color:
   - **Untextured/default** (flat white/grey — e.g. unbound-material FBX → the white-trees bug).
   - **Magenta** (missing/incompatible shader — non-URP material).
   - **Wrong default tint** (e.g. the green fallback when a basecolor path is null — the companion-green bug).
   Heuristics: sample the rendered material/_BaseMap (null map → untextured; shader name not URP → magenta-risk;
   dominant flat color near known fallbacks). Write flagged ids + the error type into the report file so the
   agent can fix the texture/material binding programmatically.

## Why it matters
Every asset bug this session (green companion WO-310, white trees WO-323, the bow/weapon grip, the green-pill
mis-scale) is a recipe/material issue a human spots visually. This tool makes that loop: **flag → recipe →
agent bakes** — instead of "playtest → describe → agent digs." It's also the Asset-Store product candidate.

## Build approach (phased)
- **Phase 1:** generalize BuildPreviewModal → AssetInspector.Show(id) + the OrientationRecipe registry +
  factory apply + JSON persistence (the orient/save half).
- **Phase 2:** the report-back file + an agent-readable format.
- **Phase 3:** color-error detection + tagging into the report.

## Notes
- Code-built UI (no UXML). Reuses WO-314's isolation rig + RotationCorrectionRegistry pattern.
- Relates to: WO-288 (weapon grip offset), WO-310/323 (the color-error class it would auto-catch).
- Local WO (renumbered 334).
- **RENDER FIX (diagnosed 2026-06-07 — THE white-box cause):** the preview camera was never explicitly
  drawn; URP skips an off-screen RT Base camera in its auto-loop. Drive it manually —
  `_previewCam.enabled = false` + `_previewCam.Render()` in a `LateUpdate` (after the yaw/euler is applied,
  so drag-rotate is live). This is the render foundation the whole tool stands on; build on it.
- **Detailed spec authored by the Claude UI session** (owner 2026-06-07) — build to THAT spec (UI writes
  specs, CLI builds, per CLAUDE.md §2). This file is the seed/notes; the UI spec is authoritative.
