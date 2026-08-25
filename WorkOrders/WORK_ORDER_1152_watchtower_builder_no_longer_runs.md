**Status:** READY - the code is FIXED (`f295971b6`, all three levels, idempotency proven) but ⛔ **the re-baked L1 prefab has NOT been pushed to R2**, so the fix is on no device. ⚠ Pushing is WORK REMAINING, not verification - which is why this is not Fixed. *(Bucket corrected 2026-08-24; I left this Fixed by hand this morning and the lint was right to reject it.)* *(Status audit 2026-08-24: status CONFIRMED READY, unchanged. Repaired only a broken command in the body - it rendered as `tools` + newline + `2-ship.ps1`; it now reads `tools\r2-ship.ps1`.)*
>  PRIOR: **Status:** FIXED 2026-08-23 (f295971b6) — builder runs on all three levels, idempotency proven by re-run, taper-asserted. ⚠ R2 push owed: the L1 prefab was re-baked. AWAITING OWNER CLOSE.

# WORK ORDER 1152 — WoodenWatchtowerBuilder no longer runs, and it fails on a level that looks fine

**Minted:** 2026-08-22 (CLI, banner bumped 1152 -> 1154 alongside WO-1153 in the SAME edit)
**Lane:** Art pipeline / editor tooling. **Class:** THE TOOL ITSELF IS BROKEN.
**Found by:** the WO-1055 archer-tower lane, 2026-08-22.

## THE FINDING

`DeNelle.Editor.WoodenWatchtowerBuilder.Build` **aborts on L1**:

```
[WoodenWatchtowerBuilder] FAILED: L1: the prefab has no renderer-bearing child to carry
the upright correction - its structure is not the wrapper+model shape this builder authors
    at WoodenWatchtowerBuilder.FindModelChild (...WoodenWatchtowerBuilder.cs:1202)
WOODEN_WATCHTOWER_BUILD_FAIL
```

⚠ **It fails on L1 — a level that renders CORRECTLY in game.** So this is not the L3 defect; the
builder has rotted independently of any tower being wrong. It also logs
`MaterialLocation.External is obsolete` against the FBX on the way in (Unity 6 dropped that mode).

## WHY THIS MATTERS MORE THAN ONE TOWER

This builder is the **only tool that regenerates the wooden-watchtower wrapper prefabs**, and those
wrappers are the ORIENTATION AUTHORITY — proven on 2026-08-22 by rendering both layers separately:

```
Tower_Wooden_Watchtower_L3__model.png    0.59 x 1.00 x 0.58   UPRIGHT
Tower_Wooden_Watchtower_L3__prefab.png   0.59 x 0.58 x 1.00   LYING DOWN
```

Same asset, two layers, opposite results. That is why three asset-layer attempts (re-running the
baker, `bakeAxisConversion`, catalog eulers) each moved the number by exactly zero, and why WO-1055
had to be fixed by writing to the prefab child directly (`ArcherTowerL3Pitch`).

**So the repo currently has no working way to regenerate those wrappers.** The next art change to
this family has no pipeline.

## SCOPE

1. Diagnose why `FindModelChild` (`:1202`) finds nothing. The prefab IS a wrapper: it holds a nested
   `PrefabInstance` with transform overrides in `m_Modifications`. Establish whether the renderers
   are genuinely absent at that moment (the obsolete `materialLocation` breaking the import is a
   candidate) or whether the search no longer matches the current shape.
2. Repair the builder, or replace it with a tool that regenerates the wrappers from the FBXs.
3. Re-bake all three levels and prove each stands, **by render, not by bounds**.

## ⛔ CONSTRAINTS

- ⚠ **AABB CANNOT PROVE ORIENTATION.** `+90` and `-90` are bounds-identical, so height, footprint
  and every numeric gate in this repo read the same for an upright and an upside-down model. Use the
  **taper test** (`JewelerPitchSolver.TaperRatio`): mesh spread in the top 20% of the bounds versus
  the bottom 20% — a building tapers, so upright reads well below 1 and upside-down well above.
- ⚠ The basis-vector test is ALSO unreliable on these meshes: at the jeweler's CORRECT pitch,
  `meshUp(forward)` reads `-1.00`. Two signals lied on 2026-08-22; only geometry told the truth.
- Existing tools to use rather than duplicate: `StructureContentReimport` (an importer-setting change
  does NOT reliably reimport in batchmode — a correct fix will read as DISPROVEN if you skip it),
  `StructureNativePoseProbe`, `StructurePoseCapture`, `JewelerPitchSolver`.
- Do NOT hand-edit `.unity` scenes. Do NOT bake with the editor open.
- Content is CDN-served and content-hashed: a re-bake needs its OWN R2 push (`tools\r2-ship.ps1`).

## ACCEPTANCE

- [ ] The builder runs to `WOODEN_WATCHTOWER_BUILD_OK` on all three levels
- [ ] Each level renders UPRIGHT in a capture, and the prefab and model layers AGREE
- [ ] `[structure-orientation]` is green for the watchtower family

## ⚠ 2026-08-24 - FIXED is defensible, but this is NOT deployed

Code and content landed and were measured, so `FIXED` is the honest bucket. ⛔ **But the re-baked L1
prefab has NOT been pushed to R2, so the fix is not on any device and cannot be felt-verified.**

⛔ **And a missing push fails SILENTLY** (§16): the build installs, launches and plays, showing
placeholder art with **no error on screen**. ⭐ **Bundle names are content-hashed, so this re-bake
needs ITS OWN push** - a previous push cannot cover it, and the bucket looking full proves nothing.
Run `tools\r2-ship.ps1` and judge by `R2_PUSH_OK` / `R2_PARITY_OK` on a **fresh** log, never the exit
code.

⚠ **Do not close this on the repo state.** The repo is right; the device is not.
