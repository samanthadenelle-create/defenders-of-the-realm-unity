**Status:** FIXED 2026-08-23 (1c04cb38b) — Gate joins the wall carve-out; measured 5.99 m fitted X vs authored 2.8. ⚠ LATENT: gate_stone is palette-locked, so nothing player-facing to feel yet. AWAITING OWNER CLOSE.

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

## ⭐ THE MEASUREMENT — TAKEN 2026-08-23. THE GATE DOES OVER-CLAIM, BY A FULL EXTRA CELL.

Scope item 1 said measure first and close the ticket if the row is already correct. It is not.

`StructurePoseCapture` on `Gate_Medieval_Medium.prefab` (`Builds/gate-pose-capture`):

```
[PoseCap] Gate_Medieval_Medium__prefab.png  size=(15.75 x 10.52 x 5.22)
```

`gate_stone` authors `heightMul` 1.0 and **no** `maxFootprint` (`RepoProps.maxFootprint` defaults to
`0f` = off), so it fits to `StructureFactory.YHeightVariable = 4 m`:

| | native | x 4.00/10.52 | vs `cellSize = 3f` (`PlacementGrid.cs:37`) |
|---|---|---|---|
| X | 15.75 | **5.99 m** | **2 cells** |
| Z | 5.22 | 1.98 m | 1 cell |
| authored `footprint` | | **2.8 m** | **1 cell** |

**The claim is 2x1 where the authoring says 1x1.** That is the same over-claim walls had before
WO-972, and it is exactly the "gate-beside-wall may still reject" case the ticket predicted.

⚠ `PoseCapture` prints `LYING DOWN` for this row. **That is NOT an orientation defect** — the flag is
`size.y >= max(x,z)`, and a gate is legitimately wider than it is tall. Do not "fix" it.

⚠ **This is LATENT, not live.** `gate_stone` is hard-locked out of the build palette
(`build-categories.json:28-33`, Defense `lockedIds`), confirmed on device at
`Logs/device/2026-08-20-equip.log` (`palette-excluded: 4 locked id(s) filtered [...gate_stone]`). No
player can place a gate today and no save contains one — so this is a landmine armed for the day the
gate unlocks, not a bug anyone is hitting. Fix it, but do not rank it as player-facing.

---

## SCOPE

1. **Measure `gate_stone`'s mesh footprint first** and confirm whether it over-claims against its
   authored 2.8 m. **If it does not, close this ticket with the measurement** — do not "fix" a row
   that is already correct.
2. It does. Extend the carve-out to Gate.

   ⛔ **DO NOT TAKE THIS TICKET'S OWN "PREFER THE GENERAL RULE" ADVICE AS WRITTEN** (corrected
   2026-08-23). It reads *"generalise it so that any row carrying an authored `placement.footprint`
   uses it"* — but **all 28 catalog rows author `placement.footprint`**, so that is a repo-wide claim
   remap of every structure in the game, not a carve-out. WO-972's safety argument
   (`StructureFactory.cs:944-950`) holds only for a **shrinking** claim: *"a shrinking claim can never
   invalidate a saved layout."* Any row whose authored number EXCEEDS its measured mesh would GROW its
   claim and can break an existing saved town on replay. If the general rule is still wanted, it must
   be clamped `min(authored, measured)` — never bare. The two-token Gate addition is the safe change.
3. ⚠ **CORRECTED 2026-08-23 — there is only ONE site, not two.** This item said to apply the fix to
   both `BuildModeController.cs:1566` and `BaseLayoutLoader.cs:326`. Both already call the **same**
   `StructureFactory.MeasureClaimFootprintXZ`, so the single early return at **`StructureFactory.cs:968`**
   (`if (entry == null || entry.type != CatalogType.Wall) return measured;`) covers both paths. Adding
   `Gate` to that one type test is the whole change. Do not go looking for a second seam.
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
