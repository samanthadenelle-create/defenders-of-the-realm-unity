<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 183 — Stairs Orientation (open onto the wall)

**Status:** READY TO IMPLEMENT
**Lane:** A (Village Scene — SERIAL, `VillageSceneBuilder.cs`)
**Source:** playtest 2026-05-31 (owner screenshot)
**Priority:** P1 (rampart access broken / reads as floating geometry)

## Problem
The rampart stairs are oriented wrong — they jut out at an angle, disconnected from the wall,
floating over open ground (see playtest screenshot). They should **turn to open onto / ascend
against the wall**, landing on the rampart walkway.

## Acceptance
- Stairs rotated so the run ascends **against the curtain wall** and the top landing meets the
  rampart walkway (no gap, no float).
- **Stairs run the FULL wall height** — top step reaches the parapet (playtest r2: at least one stair
  was too short and stopped partway up the wall).
- Base of stairs sits flush on the ground; top connects to a walkable parapet (ties to WO-181).
- Hero can walk up the stairs and onto the wall top; collision + navmesh continuous.
- **Consistent at EVERY stair instance** — playtest found a mix of floating, wrong-rotation, and
  too-short stairs. Fix the placement/generation so all instances are correct, not one-offs.

## Do NOT touch
- Wall tier/material logic, gate work (Batch A handles those).

## ROOT CAUSE (architect 2026-05-31 — why it keeps regressing)
Bug is in `Assets/Editor/VillageSceneBuilder.Fortify.cs`, the `Ramp` lambda in `BuildRamparts` (~L205-238),
at the rotation line (~L230):
```
st.transform.rotation = Quaternion.LookRotation(horiz, Vector3.up);  // aligns prefab +Z to climb dir
```
- **`Stairs_Medieval_Stone` does NOT ascend along local +Z** — its steps rise along a different model axis. So
  `LookRotation(horiz, up)` spins the staircase sideways → steps face outward, wedge detached from the wall. THE actual defect.
- **WO-166 (`071e478`/`cb7e0eb`) only edited the four endpoint coordinates + rebaked navmesh — it never touched
  L230.** That's why it "fixed" the position but the spin stayed wrong, 3× running. The invisible nav plank
  (symmetric box) hides its own mis-rotation; only the asymmetric visual exposes it.
- **Too short to reach parapet:** `NormalizeProp(st, 4f)` scales the longest edge to 4m, but the slope to
  topY=5 is ~10.3m → stair stops partway up.

## EXACT FIX
1. Add a model-forward correction: `st.transform.rotation = Quaternion.LookRotation(horiz, Vector3.up) * STAIR_MODEL_FWD_FIX;`
   where `STAIR_MODEL_FWD_FIX` is the Euler mapping the prefab's authored forward onto +Z (verify in-editor; almost
   certainly `Euler(0,180,0)` or `Euler(0,-90,0)`).
2. Scale the visual to the slope length (~`sqrt(rampRun² + topY²)` ≈ 10.3m along its run axis), not fixed 4m; `SnapFeetToParent` so base=y0, top step meets the walkway at topY.
3. Apply once in the `Ramp` lambda so all 4 instances inherit it.
Files: `VillageSceneBuilder.Fortify.cs` (Ramp L205-238), `Helpers.cs` (NormalizeProp L240, SnapFeetToParent L264), prefab `Stairs_Medieval_Stone.prefab` (verify authored forward).

## Gate
Brace check; green build; commit `feat: implement WO-183 — stairs orientation`; folds into the next village bake (do not bake standalone). Screenshot for UI validation.
