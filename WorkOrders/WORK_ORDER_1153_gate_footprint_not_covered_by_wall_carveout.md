**Status:** READY TO IMPLEMENT

# WORK ORDER 1153 — Gates are not covered by the wall footprint carve-out

**Minted:** 2026-08-22 (CLI, banner bumped 1152 -> 1154 alongside WO-1152 in the SAME edit)
**Lane:** Build mode / placement. **Class:** A FIX THAT STOPPED ONE ROW SHORT.
**Found by:** the WO-972 verification pass, 2026-08-22.

## THE FINDING

WO-972 fixed wall-beside-wall placement by having the claim path use the AUTHORED footprint instead
of the measured mesh. But `StructureFactory.MeasureClaimFootprintXZ` special-cases
**`entry.type == CatalogType.Wall` ONLY**.

**`gate_stone` is `"type": "Gate"`** (`structures-catalog.json`, `footprint: 2.8`). So a gate still
claims its MEASURED MESH, and if that mesh exceeds the 3.00 m cell on either axis it over-claims
exactly the way walls used to — meaning **gate-beside-wall may still reject**.

⚠ WO-972 never tested a gate. `WORK_ORDER_1020` explicitly lists *"Stone wall + gate adjacency
verified too"* and that box is UNCHECKED.

## SCOPE

1. **Measure `gate_stone`'s mesh footprint first** and confirm whether it over-claims against its
   authored 2.8 m. **If it does not, close this ticket with the measurement** — do not "fix" a row
   that is already correct.
2. If it does, extend the authored-footprint carve-out to Gate, or generalise it so that any row
   carrying an authored `placement.footprint` uses it. Prefer the general rule: a per-type list is
   how this stopped one row short in the first place.
3. Apply to BOTH claim paths, exactly as WO-972 did — placement (`BuildModeController.cs:1566`) and
   replay (`BaseLayoutLoader.cs:326`). A fix on one path only produces a town that cannot be rebuilt
   from its own save.
4. Extend `WallAdjacencyRegression` to cover gate-beside-wall so this cannot regress.

## ⛔ CONSTRAINTS

- `RepoProps.maxFootprint` is a MESH-FIT ceiling consumed via `SkinOptions.MaxFootprint` — it is
  **NOT** on the claim path. Do not confuse the two.
- Do NOT re-hardcode a level ceiling; `RepoProps.MaxStructureLevel` is the single authority.
- The existing instrumentation is sufficient and must not be stripped: the wall claim line
  (`StructureFactory.cs:896-900`), `[Flow:Grid] Occupy` with footprint (`PlacementGrid.cs:195`), and
  both reject paths with their worded refusals (`BuildModeController.cs:1643`, `:1679`).
- Owner is RED/GREEN COLOURBLIND — a refusal must stay a WORD, never a red tint alone.

## ACCEPTANCE

- [ ] A gate places directly beside a wall with no `gate=CellGrid` or `gate=WorldOverlap` reject
- [ ] The same holds on REPLAY from a saved layout, not just on fresh placement
- [ ] A regression covers it, and is shown to FAIL before the fix
- [ ] Two gates on the SAME cell still reject, with the worded refusal intact
