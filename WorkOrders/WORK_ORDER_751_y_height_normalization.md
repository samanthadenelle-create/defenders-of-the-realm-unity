# WORK ORDER 751 — Y-height normalization: default height + sparse overrides + audit tool

**Status:** SPEC — READY TO IMPLEMENT (owner rulings 2026-07-19). Blocked only on the 2 target numbers.
**Classification:** system + tool (extends the existing DEF-208 fit-to-height). Player-felt (structure sizing).
**PO:** Sam. Memory: [[normalize-items-by-y-height]]. Principle: ARCHITECTURE_PRINCIPLES §4 (scale from bounds).

## The problem
At `orientation.scale = 1.0` every structure renders at its raw modeled size -> inconsistent, some look
"smaller" (owner, felt-test on Seeker). `StructureFactory.Create` ALREADY fits-to-height when
`entry.repo.visualHeight > 0` (DEF-208, StructureFactory.cs ~:75-83); when it's missing it falls back to
legacy fit-to-largest-dimension - the inconsistency source.

## Owner design (2026-07-19) — one default, sparse overrides
- **All structures have a relationship to a single DEFAULT height.** A structure with **no value / null
  `visualHeight` DEFAULTS to `DefaultVisualHeight`** (normalized to the standard height) — NOT the legacy
  fit-to-largest.
- **Override only for things that should be larger** (towers). A tower sets an explicit `visualHeight`
  greater than the default; everything else stays valueless and inherits the default.

## Work
1. **`StructureFactory.cs`** — add `DefaultVisualHeight` (const/config). Change the fallback: when
   `entry.repo == null || entry.repo.visualHeight <= 0`, **fit-to-height using `DefaultVisualHeight`**
   (replace the legacy fit-to-largest branch). Keep the existing `visualHeight > 0` override path as-is.
   Preserve `manual=true` orientation. `[Flow:Structure]` trace the chosen height + source (default vs override).
2. **Overrides in `structures-catalog.json`** (dual-copy) — set an explicit `repo.visualHeight` ONLY on
   the larger structures (towers: arcane-tower/spire, archer tower, wizard tower, etc.). Remove/leave-null
   `visualHeight` on standard buildings so they inherit the default. (Do not touch the wall/gate castle system unless owner says.)
3. **AUDIT TOOL** — editor menu `Defenders/Build/Audit Structure Heights` (new, or extend
   `CatalogOrientationBaker`): for every structure, load the prefab, measure combined-renderer Y-extent,
   and REPORT a table: id | measured Y-extent | effective target height (default vs override) | computed
   scale | flag if wildly off. Read-only report first (owner tunes overrides from it); optional `-write`
   to bake overrides. Emit a `STRUCTURE_HEIGHT_AUDIT` marker for headless.

## Target numbers (owner to confirm — proposed defaults, felt-tunable)
- **`DefaultVisualHeight` (standard building):** **4 m** (~2x the 1.8 m Knight).
- **Tower override:** **7 m**. Small stations (jeweler/apothecary bench) may want a ~2.5 m override or the default.

## Acceptance
- Every no-value structure renders at the default height; towers render at their override; sizes look
  consistent (owner felt-verify on Seeker). The audit tool lists the height relationship for all structures.
- Gate: `COMPILE_GATE_OK` + DataRegression `REGRESSION_OK` (no new red). Dual-copy synced.

## Do NOT
- Don't set a per-entry visualHeight on everything (defeats the default relationship) — only the larger overrides.
- ASCII-only; dual-copy the catalog; don't hand-edit .unity scenes.
